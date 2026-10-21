using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlink.Core
{
    // Match engine - command validation and execution
    public sealed class MatchEngine
    {
        public MatchState State { get; }

        /// <summary>
        /// Resolves every card effect that reaches the engine. Owned by the engine so combat, deployment
        /// and Ability play all run through one interpreter instead of ad-hoc damage code.
        /// </summary>
        public EffectInterpreter Effects { get; }

        /// <summary>
        /// Pending block declarations for the current combat, keyed by attacker InstanceId.
        /// A block is a defensive commitment: the non-active player records it and it is consumed the
        /// next time that attacker attacks. Cleared whenever the Combat phase is left.
        /// </summary>
        private readonly Dictionary<int, int> pendingBlocks = new Dictionary<int, int>();

        public MatchEngine(MatchState state, IEnumerable<CardDefinition> cardPool = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Effects = new EffectInterpreter(State);

            if (cardPool != null)
            {
                var pool = new Dictionary<string, CardDefinition>();
                foreach (CardDefinition definition in cardPool)
                {
                    if (definition != null && !string.IsNullOrEmpty(definition.Id))
                    {
                        pool[definition.Id] = definition;
                    }
                }

                if (pool.Count > 0)
                {
                    Effects.CardResolver = id => pool.TryGetValue(id, out CardDefinition found) ? found : null;
                }
            }
        }

        public CommandResult ExecuteCommand(GameCommand command)
        {
            if (command == null)
            {
                return CommandResult.InvalidCommand;
            }

            if (State.IsMatchOver())
            {
                return CommandResult.InvalidPhase;
            }

            // Blocking and conceding are the exceptions to the active-player rule: a block is declared by
            // the defender while the opponent holds initiative, and a concession may be taken on either
            // player's turn because it is a private decision rather than a piece of table action.
            if (command.Type != CommandType.DeclareBlock && command.Type != CommandType.Concede
                && command.Player != State.ActivePlayer)
            {
                return CommandResult.InvalidCommand;
            }

            switch (command)
            {
                case PlayCardCommand playCard:
                    return ExecutePlayCard(playCard);
                case DeclareAttackCommand attack:
                    return ExecuteDeclareAttack(attack);
                case ActivateAbilityCommand ability:
                    return ExecuteActivateAbility(ability);
                case DeclareBlockCommand block:
                    return ExecuteDeclareBlock(block);
                case ConcedeCommand concede:
                    return ExecuteConcede(concede);
                case SwitchPositionCommand switchPos:
                    return ExecuteSwitchPosition(switchPos);
                case PassPhaseCommand _:
                    return ExecutePassPhase();
                case EndTurnCommand _:
                    return ExecuteEndTurn();
                case MulliganCommand mulligan:
                    return ExecuteMulligan(mulligan);
                default:
                    return CommandResult.InvalidCommand;
            }
        }

        /// <summary>
        /// GDD 2.1.8 condition 4: concession. Legal from either player in any phase or turn — there is no
        /// state to validate beyond "the match is not already over", which <see cref="ExecuteCommand"/>
        /// rejects before dispatch, so a conceded match cannot be conceded twice.
        /// </summary>
        private CommandResult ExecuteConcede(ConcedeCommand command)
        {
            State.Concede(command.Player);
            return CommandResult.Success;
        }

        private CommandResult ExecutePlayCard(PlayCardCommand command)
        {
            if (State.CurrentPhase != TurnPhase.Main)
            {
                return CommandResult.InvalidPhase;
            }

            PlayerState player = State.GetPlayer(command.Player);
            CardInstance card = player.Hand.Find(c => c.InstanceId == command.CardInstanceId);

            if (card == null)
            {
                return CommandResult.CardNotInHand;
            }

            if (!player.CanAfford(card.Definition.Cost))
            {
                return CommandResult.InsufficientNP;
            }

            // GDD 4.15.1: an Ability occupies no slot, a Protocol occupies the single player-side
            // Protocol slot, and only Units/Relays take a Core or Node slot. Resolving the slot first
            // would reject both Abilities and Protocols, because CanHost() is false for them.
            BoardSlot targetSlot = null;

            if (card.Definition.Type == CardType.Ability)
            {
                // No slot to validate.
            }
            else if (card.Definition.Type == CardType.Protocol)
            {
                if (player.Board.Protocol != null && player.Board.Protocol.IsAlive())
                {
                    return CommandResult.SlotOccupied;
                }
            }
            else
            {
                targetSlot = GetTargetSlot(player.Board, command.TargetSlotType, command.SlotIndex, command.NodeIndex);
                if (targetSlot == null)
                {
                    return CommandResult.InvalidTarget;
                }

                if (targetSlot.IsOccupied())
                {
                    return CommandResult.SlotOccupied;
                }

                if (!targetSlot.CanHost(card.Definition.Type))
                {
                    return CommandResult.InvalidTarget;
                }
            }

            player.RemoveFromHand(card);
            player.SpendNP(card.Definition.Cost);
            State.LogEvent($"{command.Player} plays {card.Definition.Name} for {card.Definition.Cost} NP");

            if (card.Definition.Type == CardType.Ability)
            {
                player.DiscardCard(card);
                Effects.Resolve(card, command.Player, card.Definition.Effects, $"Ability {card.Definition.Name}");
                State.LogEvent($"Ability {card.Definition.Name} resolves");
            }
            else if (card.Definition.Type == CardType.Protocol)
            {
                player.Board.Protocol = card;
                Effects.Resolve(card, command.Player, card.Definition.Effects, $"Protocol {card.Definition.Name}");
            }
            else
            {
                targetSlot.Card = card;

                // GDD 2.1.11 keyword "Deploy: Effect" - the card's effects fire as it lands. A Unit without
                // Deploy is deliberately not auto-triggered; its effects wait for an explicit trigger.
                if (card.HasKeyword(Keyword.Deploy))
                {
                    Effects.Resolve(card, command.Player, card.Definition.Effects, $"Deploy {card.Definition.Name}");
                }
            }

            State.ResolveDestructionAndPromotion();
            State.CheckVictoryConditions();

            return CommandResult.Success;
        }

        private CommandResult ExecuteDeclareAttack(DeclareAttackCommand command)
        {
            if (State.CurrentPhase != TurnPhase.Combat)
            {
                return CommandResult.InvalidPhase;
            }

            PlayerState player = State.GetPlayer(command.Player);
            PlayerState opponent = State.GetOpponent(command.Player);

            CardInstance attacker = FindActiveCardInstance(player.Board, command.AttackerInstanceId);
            if (attacker == null || !attacker.CanAct())
            {
                return CommandResult.UnitCannotAct;
            }

            // Relays and Protocols are not combatants (GDD 4.15.1: no ATK/HP/Shield), so they can never
            // declare an attack even once they are Ready on the field.
            if (attacker.Definition.Type != CardType.Unit)
            {
                return CommandResult.InvalidTarget;
            }

            // Resolve the declared target (if any) up front so a pre-declared block can be validated
            // against it: only Core Actives block a Core attack, and a unit attack is only blocked by an
            // Active in the same battlefield as the target (GDD 2.1.6 Step 2).
            CardInstance target = null;
            if (command.DefenderInstanceId > 0)
            {
                target = FindCardInstance(opponent.Board, command.DefenderInstanceId);
                if (target == null || !target.IsAlive())
                {
                    return CommandResult.InvalidTarget;
                }
            }

            CardInstance blocker = ConsumePendingBlocker(attacker, target);

            if (blocker != null)
            {
                ResolveCombat(attacker, blocker);
            }
            else if (target != null)
            {
                ResolveCombat(attacker, target);
            }
            else
            {
                // GDD 2.1.6 Step 4: an unblocked direct Core attack reduces durability by exactly 1,
                // regardless of the attacker's ATK.
                opponent.TakeDamage(1);
                State.LogEvent($"{attacker.Definition.Name} hits the {opponent.Index} Core for 1 durability");
            }

            attacker.Exhaust();
            State.ResolveDestructionAndPromotion();
            State.CheckVictoryConditions();
            return CommandResult.Success;
        }

        /// <summary>
        /// Resolves a unit's printed Activate skill during its controller's Combat phase. Activation is a
        /// separate command from attacking: the player chooses one of the two actions and a used unit becomes
        /// Exhausted, so the same unit cannot attack and activate in one combat step.
        /// </summary>
        private CommandResult ExecuteActivateAbility(ActivateAbilityCommand command)
        {
            if (State.CurrentPhase != TurnPhase.Combat)
            {
                return CommandResult.InvalidPhase;
            }

            PlayerState player = State.GetPlayer(command.Player);
            CardInstance card = FindActiveCardInstance(player.Board, command.CardInstanceId);
            if (card == null || !card.CanAct())
            {
                return CommandResult.UnitCannotAct;
            }

            if (card.Definition.Type != CardType.Unit || !card.HasKeyword(Keyword.Activate)
                || card.Definition.Effects == null || card.Definition.Effects.Count == 0)
            {
                return CommandResult.InvalidCommand;
            }

            Effects.Resolve(card, command.Player, card.Definition.Effects, $"Activate {card.Definition.Name}");
            card.Exhaust();
            State.LogEvent($"{card.Definition.Name} activates its skill");
            State.ResolveDestructionAndPromotion();
            State.CheckVictoryConditions();
            return CommandResult.Success;
        }

        /// <summary>
        /// Records a block for the defending player (the player who is not active) during combat.
        /// Attack resolution is immediate in this engine, so a block is a pre-commitment against a
        /// specific enemy unit: the next time that unit attacks, the block consumes itself and combat
        /// resolves against the blocker instead of the declared target or Core.
        /// </summary>
        private CommandResult ExecuteDeclareBlock(DeclareBlockCommand command)
        {
            if (State.CurrentPhase != TurnPhase.Combat)
            {
                return CommandResult.InvalidPhase;
            }

            // Only the defender may block. The active player is the one attacking.
            if (command.Player == State.ActivePlayer)
            {
                return CommandResult.InvalidCommand;
            }

            PlayerState blockerOwner = State.GetPlayer(command.Player);
            PlayerState attackerOwner = State.GetOpponent(command.Player);

            CardInstance blocker = FindActiveCardInstance(blockerOwner.Board, command.BlockerInstanceId);
            if (blocker == null || !blocker.IsAlive())
            {
                return CommandResult.InvalidTarget;
            }

            if (!CanBlock(blocker))
            {
                return CommandResult.CannotBlock;
            }

            CardInstance attacker = FindActiveCardInstance(attackerOwner.Board, command.AttackerInstanceId);
            if (attacker == null || !attacker.IsAlive())
            {
                return CommandResult.InvalidTarget;
            }

            if (pendingBlocks.ContainsKey(attacker.InstanceId) || pendingBlocks.ContainsValue(blocker.InstanceId))
            {
                return CommandResult.InvalidCommand;
            }

            pendingBlocks[attacker.InstanceId] = blocker.InstanceId;
            State.LogEvent($"{command.Player} readies {blocker.Definition.Name} to block {attacker.Definition.Name}");
            return CommandResult.Success;
        }

        /// <summary>
        /// True when the unit is a legal blocker.
        /// <para>
        /// GDD 2.1.6 Step 2 / 2.1.5: blocking is restricted only by the [Cannot Block] keyword and by the
        /// unit being unusable (Exhausted from its own attack, or destroyed). Summoning Delay does NOT
        /// prevent blocking - GDD 2.1.5 limits a fresh deployment to "cannot attack or switch", so a unit
        /// deployed this turn may still be assigned as a blocker.
        /// </para>
        /// </summary>
        public bool CanBlock(CardInstance unit)
        {
            if (unit == null || !unit.IsAlive() || unit.Definition.Type != CardType.Unit)
            {
                return false;
            }

            if (unit.State == UnitState.Exhausted || unit.State == UnitState.Destroyed)
            {
                return false;
            }

            return !HasKeyword(unit, Keyword.CannotBlock);
        }

        /// <summary>Read-only view of the current pending block, or 0 when the attacker is unblocked.</summary>
        public int GetPendingBlocker(int attackerInstanceId)
        {
            return pendingBlocks.TryGetValue(attackerInstanceId, out int blockerId) ? blockerId : 0;
        }

        private CardInstance ConsumePendingBlocker(CardInstance attacker, CardInstance target)
        {
            if (!pendingBlocks.TryGetValue(attacker.InstanceId, out int blockerId))
            {
                return null;
            }

            pendingBlocks.Remove(attacker.InstanceId);

            if (attacker.CannotBeBlocked)
            {
                State.LogEvent($"{attacker.Definition.Name} cannot be blocked; the block is ignored");
                return null;
            }

            PlayerState defenderOwner = State.GetOpponent(attacker.Owner);
            CardInstance blocker = FindActiveCardInstance(defenderOwner.Board, blockerId);
            if (blocker == null || !CanBlock(blocker))
            {
                return null;
            }

            // GDD 2.1.6 Step 2: a direct Core attack may only be blocked by a Core Active, and a unit
            // attack only by an Active in the same battlefield as the target. An otherwise-valid block
            // that is in the wrong place is ignored rather than aborting the attack.
            if (target == null)
            {
                if (!IsCoreActive(defenderOwner.Board, blocker))
                {
                    State.LogEvent($"{blocker.Definition.Name} cannot block a Core attack (not a Core Active)");
                    return null;
                }
            }
            else if (!ShareBattlefield(defenderOwner.Board, blocker, target))
            {
                State.LogEvent($"{blocker.Definition.Name} is not in the same battlefield as the target; the block is ignored");
                return null;
            }

            return blocker;
        }

        private CommandResult ExecuteSwitchPosition(SwitchPositionCommand command)
        {
            if (State.CurrentPhase != TurnPhase.Main)
            {
                return CommandResult.InvalidPhase;
            }

            PlayerState player = State.GetPlayer(command.Player);
            CardInstance card = FindCardInstance(player.Board, command.CardInstanceId);

            if (card == null)
            {
                return CommandResult.InvalidTarget;
            }

            // GDD 2.1.5: a unit under Summoning Delay "cannot attack or switch" until its next turn.
            if (card.HasSummoningDelay)
            {
                return CommandResult.UnitCannotAct;
            }

            BoardSlot currentSlot = player.Board.FindSlot(card);
            if (currentSlot == null)
            {
                return CommandResult.InvalidTarget;
            }

            BoardSlot targetSlot = GetTargetSlot(player.Board, command.TargetSlotType, command.SlotIndex, command.NodeIndex);
            if (targetSlot == null)
            {
                return CommandResult.InvalidTarget;
            }

            if (targetSlot.IsOccupied())
            {
                return CommandResult.SlotOccupied;
            }

            if (!targetSlot.CanHost(card.Definition.Type))
            {
                return CommandResult.InvalidTarget;
            }

            currentSlot.Card = null;
            targetSlot.Card = card;
            State.LogEvent($"{command.Player} switches {card.Definition.Name} position");

            // Leaving or entering the Core Active line changes which units receive the Node 1/2
            // multipliers, so recompute them without resolving destruction/promotion.
            State.RefreshNodeMultipliers();

            return CommandResult.Success;
        }

        private CommandResult ExecutePassPhase()
        {
            State.AdvancePhase();

            // Blocks only live for one combat step; drop them the moment the Combat phase is left.
            if (State.CurrentPhase != TurnPhase.Combat)
            {
                pendingBlocks.Clear();
            }

            return CommandResult.Success;
        }

        private CommandResult ExecuteEndTurn()
        {
            pendingBlocks.Clear();

            // Walk the acting player's remaining phases to End and then hand over. The loop is driven by
            // "am I still active" rather than "is the phase End", because a turn that begins at Draw - which
            // is exactly where a fresh turn starts - must still make progress instead of being a no-op.
            PlayerIndex actor = State.ActivePlayer;
            int guard = 0;

            while (State.ActivePlayer == actor && !State.IsMatchOver() && guard++ < 16)
            {
                State.AdvancePhase();
            }

            return CommandResult.Success;
        }

        private CommandResult ExecuteMulligan(MulliganCommand command)
        {
            // The engine keeps a usable Mulligan command, but GDD 2.1.3 states MVP has no mulligan; it is
            // only reachable if a caller explicitly offers it. It is confined to the opening turn.
            if (State.TurnNumber != 1 ||
                (State.CurrentPhase != TurnPhase.Upkeep && State.CurrentPhase != TurnPhase.Draw))
            {
                return CommandResult.InvalidPhase;
            }

            PlayerState player = State.GetPlayer(command.Player);

            foreach (int instanceId in command.CardInstanceIds)
            {
                CardInstance card = player.Hand.Find(c => c.InstanceId == instanceId);
                if (card != null)
                {
                    player.Hand.Remove(card);
                    player.Deck.Add(card);
                }
            }

            player.ShuffleDeck();

            for (int i = 0; i < command.CardInstanceIds.Length; i++)
            {
                player.DrawCard();
            }

            State.LogEvent($"{command.Player} mulligans {command.CardInstanceIds.Length} card(s)");
            return CommandResult.Success;
        }

        private void ResolveCombat(CardInstance attacker, CardInstance defender)
        {
            bool attackerHasFirstStrike = HasFirstStrike(attacker);
            bool defenderHasFirstStrike = HasFirstStrike(defender);

            State.LogEvent($"{attacker.Definition.Name} attacks {defender.Definition.Name}");

            if (attackerHasFirstStrike && !defenderHasFirstStrike)
            {
                DealCombatDamage(attacker, defender);
                if (defender.IsAlive())
                {
                    DealCombatDamage(defender, attacker);
                }
            }
            else if (defenderHasFirstStrike && !attackerHasFirstStrike)
            {
                DealCombatDamage(defender, attacker);
                if (attacker.IsAlive())
                {
                    DealCombatDamage(attacker, defender);
                }
            }
            else
            {
                DealCombatDamage(attacker, defender);
                DealCombatDamage(defender, attacker);
            }

            // GDD 2.1.7 Node 4 Core multiplier: a controlled Node 4 lets a Core Active draw when it
            // destroys an enemy, even if it also died in the exchange.
            DrawOnKill(attacker, defender);
            DrawOnKill(defender, attacker);
        }

        /// <summary>
        /// GDD 2.1.7 Node 4: when <paramref name="killer"/> (a Core Active whose controller holds Node 4)
        /// destroys <paramref name="victim"/>, its controller draws a card.
        /// </summary>
        private void DrawOnKill(CardInstance killer, CardInstance victim)
        {
            if (killer == null || victim == null || victim.IsAlive())
            {
                return;
            }

            Board board = State.GetPlayer(killer.Owner).Board;
            if (!IsCoreActive(board, killer) || !board.IsNodeControlled(3, killer.Owner))
            {
                return;
            }

            State.GetPlayer(killer.Owner).DrawCard();
            State.LogEvent($"{killer.Definition.Name} destroys {victim.Definition.Name} - draw (Node 4)");
        }

        private void DealCombatDamage(CardInstance source, CardInstance target)
        {
            int damage = source.CurrentAtk;
            bool piercing = HasKeyword(source, Keyword.Piercing);
            int shieldBefore = target.CurrentShield;

            if (piercing)
            {
                target.CurrentHp -= damage;
                if (target.CurrentHp <= 0)
                {
                    target.CurrentHp = 0;
                    target.State = UnitState.Destroyed;
                }
                State.LogEvent($"{source.Definition.Name} deals {damage} piercing damage to {target.Definition.Name}");
            }
            else
            {
                target.TakeDamage(damage);
                State.LogEvent($"{source.Definition.Name} deals {damage} damage to {target.Definition.Name}");
            }

            // GDD 2.1.7: damage to a shield-less Core Active transfers to its controller's Core.
            State.ApplyCoreDamageTransfer(target, damage, shieldBefore);

            if (HasKeyword(source, Keyword.Lifesteal) && damage > 0)
            {
                source.Heal(damage);
                State.LogEvent($"{source.Definition.Name} lifesteals {damage} HP");
            }
        }

        /// <summary>
        /// [First Strike] from the unit's own keywords, plus the Node 3 Core multiplier that grants it to
        /// Core Active Units while Node 3 is controlled (GDD 2.1.7).
        /// </summary>
        private bool HasFirstStrike(CardInstance unit)
        {
            if (HasKeyword(unit, Keyword.FirstStrike))
            {
                return true;
            }

            Board board = State.GetPlayer(unit.Owner).Board;
            return IsCoreActive(board, unit) && board.IsNodeControlled(2, unit.Owner);
        }

        /// <summary>True when the unit currently occupies one of its owner's Core Active slots.</summary>
        private static bool IsCoreActive(Board board, CardInstance unit)
        {
            BoardSlot slot = board.FindSlot(unit);
            return slot != null && slot.Type == SlotType.CoreActive;
        }

        /// <summary>
        /// True when both units occupy the same battlefield: the same Core Active/Bench group, or the
        /// same slot kind at the same Node. Used by GDD 2.1.6 Step 2 to scope blockers to the target.
        /// </summary>
        private static bool ShareBattlefield(Board board, CardInstance a, CardInstance b)
        {
            BoardSlot sa = board.FindSlot(a);
            BoardSlot sb = board.FindSlot(b);
            if (sa == null || sb == null)
            {
                return false;
            }

            return sa.Type == sb.Type && sa.NodeIndex == sb.NodeIndex;
        }

        private BoardSlot GetTargetSlot(Board board, SlotType slotType, int index, int nodeIndex)
        {
            switch (slotType)
            {
                case SlotType.CoreActive:
                    return index >= 0 && index < board.CoreActive.Length ? board.CoreActive[index] : null;
                case SlotType.CoreBench:
                    return index >= 0 && index < board.CoreBench.Length ? board.CoreBench[index] : null;
                case SlotType.NodeActive:
                    if (nodeIndex >= 0 && nodeIndex < Board.NodeCount && index >= 0 && index < board.NodeActive[nodeIndex].Length)
                    {
                        return board.NodeActive[nodeIndex][index];
                    }
                    return null;
                case SlotType.NodeBench:
                    if (nodeIndex >= 0 && nodeIndex < Board.NodeCount && index >= 0 && index < board.NodeBench[nodeIndex].Length)
                    {
                        return board.NodeBench[nodeIndex][index];
                    }
                    return null;
                default:
                    return null;
            }
        }

        private CardInstance FindCardInstance(Board board, int instanceId)
        {
            for (int i = 0; i < board.CoreActive.Length; i++)
            {
                if (board.CoreActive[i].IsOccupied() && board.CoreActive[i].Card.InstanceId == instanceId)
                {
                    return board.CoreActive[i].Card;
                }
            }

            for (int i = 0; i < board.CoreBench.Length; i++)
            {
                if (board.CoreBench[i].IsOccupied() && board.CoreBench[i].Card.InstanceId == instanceId)
                {
                    return board.CoreBench[i].Card;
                }
            }

            for (int node = 0; node < Board.NodeCount; node++)
            {
                for (int i = 0; i < board.NodeActive[node].Length; i++)
                {
                    if (board.NodeActive[node][i].IsOccupied() && board.NodeActive[node][i].Card.InstanceId == instanceId)
                    {
                        return board.NodeActive[node][i].Card;
                    }
                }

                for (int i = 0; i < board.NodeBench[node].Length; i++)
                {
                    if (board.NodeBench[node][i].IsOccupied() && board.NodeBench[node][i].Card.InstanceId == instanceId)
                    {
                        return board.NodeBench[node][i].Card;
                    }
                }
            }

            return null;
        }

        private CardInstance FindActiveCardInstance(Board board, int instanceId)
        {
            for (int i = 0; i < board.CoreActive.Length; i++)
            {
                if (board.CoreActive[i].IsOccupied() && board.CoreActive[i].Card.InstanceId == instanceId)
                {
                    return board.CoreActive[i].Card;
                }
            }

            for (int node = 0; node < Board.NodeCount; node++)
            {
                for (int i = 0; i < board.NodeActive[node].Length; i++)
                {
                    if (board.NodeActive[node][i].IsOccupied() && board.NodeActive[node][i].Card.InstanceId == instanceId)
                    {
                        return board.NodeActive[node][i].Card;
                    }
                }
            }

            return null;
        }

        private bool HasKeyword(CardInstance card, Keyword keyword)
        {
            if (card.Definition.Keywords == null)
            {
                return false;
            }

            return card.Definition.Keywords.Contains(keyword);
        }
    }
}
