using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    /// <summary>
    /// Resolves <see cref="EffectDescriptor"/> instances against a <see cref="MatchState"/>.
    /// <para>
    /// This covers the full 16-value <see cref="EffectType"/> vocabulary from GDD 4.15.2. Every effect is
    /// resolved as an instant effect: <see cref="EffectDescriptor.Duration"/> on a Buff/Debuff is recorded
    /// in the event log and its stat delta is applied permanently, because a per-turn duration tracker is
    /// outside M3/M4 scope. Cards that need a duration must re-apply the buff each turn.
    /// </para>
    /// <para>
    /// Trigger rules implemented here:
    /// <list type="bullet">
    /// <item>Ability — effects resolve on play, then the card is discarded.</item>
    /// <item>Unit/Relay with <see cref="Keyword.Deploy"/> — effects resolve on deployment.</item>
    /// <item>Protocol — effects resolve on play; Upkeep-triggered protocols are not yet evaluated each turn.</item>
    /// <item>Non-Deploy Unit — its effects are not auto-triggered; they wait for an explicit trigger keyword.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class EffectInterpreter
    {
        private readonly MatchState state;

        /// <summary>
        /// Optional lookup used by <see cref="EffectType.Summon"/> to turn a ReferenceId into a definition.
        /// When null, or when the id is unknown, the summon is skipped and logged instead of throwing.
        /// </summary>
        public Func<string, CardDefinition> CardResolver { get; set; }

        public EffectInterpreter(MatchState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Resolves every effect on a card in source order.
        /// </summary>
        /// <param name="source">The card that is resolving. May be null for state-driven effects.</param>
        /// <param name="controller">The player the effect is resolving for.</param>
        /// <param name="context">Short label prefixed to each log line, e.g. "Ability Pulse Strike".</param>
        public void Resolve(CardInstance source, PlayerIndex controller, IReadOnlyList<EffectDescriptor> effects, string context)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                ResolveOne(source, controller, effects[i], context);
            }

            // Damage, Destroy, Exhaust and Summon can empty an Active slot; settle the board once at the
            // end of the batch so promotion order stays stable across multi-effect cards.
            state.ResolveDestructionAndPromotion();
        }

        /// <summary>Resolves a single effect.</summary>
        public void ResolveOne(CardInstance source, PlayerIndex controller, EffectDescriptor effect, string context)
        {
            PlayerState own = state.GetPlayer(controller);
            string prefix = string.IsNullOrEmpty(context) ? "[Effect]" : context;

            switch (effect.Type)
            {
                case EffectType.DealDamage:
                case EffectType.DealAoEDamage:
                    foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
                    {
                        int shieldBefore = target.CurrentShield;
                        target.TakeDamage(effect.Amount);
                        state.LogEvent($"{prefix}: {target.Definition.Name} takes {effect.Amount} damage");
                        // GDD 2.1.7: damage to a shield-less Core Active transfers to its controller's Core.
                        state.ApplyCoreDamageTransfer(target, effect.Amount, shieldBefore);
                    }
                    break;

                case EffectType.Heal:
                    foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
                    {
                        target.Heal(effect.Amount);
                        state.LogEvent($"{prefix}: {target.Definition.Name} heals {effect.Amount} HP");
                    }
                    break;

                case EffectType.Draw:
                    DrawCards(own, effect.Amount, prefix);
                    break;

                case EffectType.GainNP:
                    own.GainNP(effect.Amount);
                    state.LogEvent($"{prefix}: {controller} gains {effect.Amount} NP");
                    break;

                case EffectType.Buff:
                    ApplyStatChange(source, controller, effect, +1, prefix);
                    break;

                case EffectType.Debuff:
                    ApplyStatChange(source, controller, effect, -1, prefix);
                    break;

                case EffectType.GrantShield:
                    foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
                    {
                        target.AddShield(effect.Amount);
                        state.LogEvent($"{prefix}: {target.Definition.Name} gains {effect.Amount} Shield");
                    }
                    break;

                case EffectType.Destroy:
                    foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
                    {
                        target.Destroy();
                        state.LogEvent($"{prefix}: {target.Definition.Name} is destroyed");
                    }
                    break;

                case EffectType.Summon:
                    ResolveSummon(own, controller, effect, prefix);
                    break;

                case EffectType.Exhaust:
                    foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
                    {
                        target.Exhaust();
                        state.LogEvent($"{prefix}: {target.Definition.Name} is exhausted");
                    }
                    break;

                case EffectType.AddKeyword:
                    // EffectDescriptor carries no keyword payload, so an AddKeyword effect cannot name the
                    // keyword it grants. Documented in EffectDescriptor.ReferenceId and CardManagerWindow;
                    // deliberately logged rather than silently ignored.
                    state.LogEvent($"{prefix}: AddKeyword is not resolvable (no keyword payload in the schema)");
                    break;

                case EffectType.SwitchPosition:
                case EffectType.Move:
                    ResolveMove(source, controller, effect, prefix);
                    break;

                case EffectType.PreventBlock:
                    foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
                    {
                        target.CannotBeBlocked = true;
                        state.LogEvent($"{prefix}: {target.Definition.Name} cannot be blocked");
                    }
                    break;

                case EffectType.Repair:
                    // The validator only permits Repair against Self/Commander, so this is always Core
                    // durability. The 3-point cap comes from CardValidator.MaxRepairAmount.
                    int repair = Math.Min(effect.Amount, CardValidator.MaxRepairAmount);
                    own.RepairCore(repair);
                    state.LogEvent($"{prefix}: {controller} repairs {repair} Core durability");
                    break;

                default:
                    state.LogEvent($"{prefix}: unhandled EffectType {effect.Type}");
                    break;
            }
        }

        private void ApplyStatChange(CardInstance source, PlayerIndex controller, EffectDescriptor effect, int sign, string prefix)
        {
            int atkDelta = sign * effect.Amount;
            int hpDelta = sign * effect.Amount2;

            foreach (CardInstance target in ResolveUnitTargets(source, controller, effect.Target))
            {
                if (atkDelta != 0)
                {
                    target.ModifyAtk(atkDelta);
                    state.LogEvent($"{prefix}: {target.Definition.Name} ATK {atkDelta:+0;-0}");
                }

                if (hpDelta != 0)
                {
                    target.ModifyHp(hpDelta);
                    state.LogEvent($"{prefix}: {target.Definition.Name} HP {hpDelta:+0;-0}");
                }

                if (effect.Duration != EffectDescriptor.InstantDuration)
                {
                    state.LogEvent($"{prefix}: {target.Definition.Name} modifier recorded for duration {effect.Duration}");
                }
            }
        }

        private void ResolveSummon(PlayerState own, PlayerIndex controller, EffectDescriptor effect, string prefix)
        {
            if (string.IsNullOrEmpty(effect.ReferenceId))
            {
                state.LogEvent($"{prefix}: Summon skipped, ReferenceId is empty");
                return;
            }

            CardDefinition definition = CardResolver != null ? CardResolver(effect.ReferenceId) : null;
            if (definition == null)
            {
                state.LogEvent($"{prefix}: Summon skipped, no definition for '{effect.ReferenceId}'");
                return;
            }

            if (definition.Type != CardType.Unit && definition.Type != CardType.Relay)
            {
                state.LogEvent($"{prefix}: Summon skipped, '{definition.Name}' is not a Unit or Relay");
                return;
            }

            if (!own.Board.TryFindFreeSlot(definition.Type, out BoardSlot slot))
            {
                state.LogEvent($"{prefix}: Summon skipped, no free slot for {definition.Name}");
                return;
            }

            var summoned = new CardInstance(definition, controller);
            slot.Card = summoned;
            state.LogEvent($"{prefix}: summons {definition.Name} into {slot.Type}");
        }

        private void ResolveMove(CardInstance source, PlayerIndex controller, EffectDescriptor effect, string prefix)
        {
            PlayerState own = state.GetPlayer(controller);
            List<CardInstance> targets = ResolveUnitTargets(source, controller, effect.Target);
            if (targets.Count == 0)
            {
                state.LogEvent($"{prefix}: Move skipped, no unit target");
                return;
            }

            CardInstance target = targets[0];
            BoardSlot current = own.Board.FindSlot(target);
            if (current == null)
            {
                state.LogEvent($"{prefix}: Move skipped, {target.Definition.Name} is not on the board");
                return;
            }

            // Move is defined as "relocate to the other half of the layout": Bench → Active when there is
            // room, otherwise Active → Bench. There is no free-form destination in the descriptor.
            if (current.Type == SlotType.CoreBench || current.Type == SlotType.NodeBench)
            {
                BoardSlot destination = FindFreeSlotOn(own.Board, target.Definition.Type, active: true);
                if (destination == null)
                {
                    state.LogEvent($"{prefix}: Move skipped, no free Active slot");
                    return;
                }

                current.Card = null;
                destination.Card = target;
                state.LogEvent($"{prefix}: {target.Definition.Name} moves {current.Type} -> {destination.Type}");
            }
            else
            {
                BoardSlot destination = FindFreeSlotOn(own.Board, target.Definition.Type, active: false);
                if (destination == null)
                {
                    state.LogEvent($"{prefix}: Move skipped, no free Bench slot");
                    return;
                }

                current.Card = null;
                destination.Card = target;
                state.LogEvent($"{prefix}: {target.Definition.Name} moves {current.Type} -> {destination.Type}");
            }
        }

        /// <summary>
        /// First free legal slot on the given board: Active slots when <paramref name="active"/> is true,
        /// Bench slots otherwise. Move never relocates a card onto the opponent's half of the layout.
        /// </summary>
        private static BoardSlot FindFreeSlotOn(Board board, CardType cardType, bool active)
        {
            for (int i = 0; i < Board.CoreActiveSlots; i++)
            {
                BoardSlot slot = active ? board.CoreActive[i] : board.CoreBench[i];
                if (!slot.IsOccupied() && slot.CanHost(cardType))
                {
                    return slot;
                }
            }

            if (active && cardType == CardType.Relay)
            {
                // Relays are node cards; Core Active can never host them, so they need Node Active here.
                for (int node = 0; node < Board.NodeCount; node++)
                {
                    for (int i = 0; i < Board.NodeActiveSlots; i++)
                    {
                        BoardSlot slot = board.NodeActive[node][i];
                        if (!slot.IsOccupied() && slot.CanHost(cardType))
                        {
                            return slot;
                        }
                    }
                }

                return null;
            }

            for (int node = 0; node < Board.NodeCount; node++)
            {
                int slotCount = active ? Board.NodeActiveSlots : Board.NodeBenchSlots;
                for (int i = 0; i < slotCount; i++)
                {
                    BoardSlot slot = active ? board.NodeActive[node][i] : board.NodeBench[node][i];
                    if (!slot.IsOccupied() && slot.CanHost(cardType))
                    {
                        return slot;
                    }
                }
            }

            return null;
        }

        private void DrawCards(PlayerState player, int amount, string prefix)
        {
            int drawn = 0;
            for (int i = 0; i < amount; i++)
            {
                if (player.DrawCard() != null)
                {
                    drawn++;
                }
            }

            state.LogEvent($"{prefix}: {player.Index} draws {drawn} card(s)");
        }

        /// <summary>
        /// Maps a <see cref="TargetKind"/> onto the units it can legally hit.
        /// Player targets (Commander / EnemyCommander) resolve to an empty list: the validator already
        /// rejects DealDamage/Heal/Destroy against them, and Repair is handled separately as Core repair.
        /// </summary>
        public List<CardInstance> ResolveUnitTargets(CardInstance source, PlayerIndex controller, TargetKind target)
        {
            PlayerState own = state.GetPlayer(controller);
            PlayerState foe = state.GetOpponent(controller);
            var targets = new List<CardInstance>();

            switch (target)
            {
                case TargetKind.Self:
                    if (source != null && source.IsAlive())
                    {
                        targets.Add(source);
                    }
                    break;

                case TargetKind.AlliedActive:
                case TargetKind.AllAlliedActive:
                    targets.AddRange(own.Board.GetCoreActiveUnits(controller));
                    break;

                case TargetKind.EnemyActive:
                case TargetKind.AllEnemyActive:
                    targets.AddRange(foe.Board.GetCoreActiveUnits(foe.Index));
                    break;

                case TargetKind.Node:
                    targets.AddRange(own.Board.GetNodeUnits(controller));
                    break;

                case TargetKind.EnemyNode:
                    targets.AddRange(foe.Board.GetNodeUnits(foe.Index));
                    break;

                case TargetKind.Deck:
                case TargetKind.Discard:
                case TargetKind.Hand:
                case TargetKind.Commander:
                case TargetKind.EnemyCommander:
                default:
                    // Zone and player targets carry no unit payload. Draw/Repair/other zone effects are
                    // resolved by their own branches above.
                    break;
            }

            return targets;
        }
    }
}
