using System.Collections.Generic;
using System.Linq;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Minimal deterministic AI opponent for M4.
    /// Makes legal moves through the MatchEngine validator and never mutates state directly.
    /// </summary>
    public sealed class SimpleAI
    {
        private readonly MatchEngine engine;
        private readonly MatchState state;
        private readonly PlayerIndex player;
        private readonly System.Random random;

        public SimpleAI(MatchEngine engine, MatchState state, PlayerIndex player, int seed = 0)
        {
            this.engine = engine;
            this.state = state;
            this.player = player;
            this.random = new System.Random(seed);
        }

        /// <summary>
        /// Runs this player's whole turn: skips the entry phases, plays what it can afford, attacks with
        /// every ready unit, then ends the turn. Safe to call once per frame from the coordinator because
        /// it returns immediately when it is not this player's turn.
        /// <para>
        /// Split into <see cref="TakeTurnUntilCombat"/> and <see cref="FinishTurn"/> so a caller that wants to
        /// hand the defender a block window can stop in the middle. Composing the two here keeps every
        /// existing caller - and every test - on exactly the behaviour they always had.
        /// </para>
        /// </summary>
        public void TakeTurn()
        {
            if (!TakeTurnUntilCombat())
            {
                return;
            }

            FinishTurn();
        }

        /// <summary>
        /// Advances this player through Upkeep, Draw and Main and stops on the threshold of Combat, leaving
        /// the phase untouched. Returns false when it is not this player's turn (or the match is over), in
        /// which case nothing ran.
        /// <para>
        /// The pause point matters: combat resolution in this engine is immediate, so an opponent that walks
        /// into Combat and attacks in the same frame leaves the defender no moment in which to commit a
        /// blocker. Stopping here is what gives the coordinator somewhere to hold the door open.
        /// </para>
        /// </summary>
        public bool TakeTurnUntilCombat()
        {
            if (state.ActivePlayer != player || state.IsMatchOver())
            {
                return false;
            }

            PlayerState playerState = state.GetPlayer(player);

            int guard = 0;
            while (state.ActivePlayer == player && !state.IsMatchOver()
                   && state.CurrentPhase != TurnPhase.Combat && guard++ < 32)
            {
                switch (state.CurrentPhase)
                {
                    case TurnPhase.Upkeep:
                    case TurnPhase.Draw:
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    case TurnPhase.Main:
                        TryPlayCards(playerState);
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    default:
                        // End and Cleanup on the way in means the turn was handed over mid-walk; passing
                        // them keeps this half-turn as total as the old single loop was, and the guard stops
                        // it from spinning if a phase ever refuses to advance.
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;
                }
            }

            return state.ActivePlayer == player && state.CurrentPhase == TurnPhase.Combat;
        }

        /// <summary>
        /// The back half of the turn: attack with everything ready, then pass out of Combat and run End and
        /// Cleanup, which hands initiative to the opponent. Safe to call when it is not our turn; it does
        /// nothing, exactly as <see cref="TakeTurn"/> would.
        /// </summary>
        public void FinishTurn()
        {
            if (state.ActivePlayer != player || state.IsMatchOver())
            {
                return;
            }

            PlayerState playerState = state.GetPlayer(player);

            int guard = 0;
            while (state.ActivePlayer == player && !state.IsMatchOver() && guard++ < 32)
            {
                switch (state.CurrentPhase)
                {
                    case TurnPhase.Combat:
                        TryAttack(playerState);
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    case TurnPhase.Upkeep:
                    case TurnPhase.Draw:
                    case TurnPhase.Main:
                        // Reachable only if a combat effect bounced the turn back a phase; advancing rather
                        // than looping forever is the whole point of the guard, so treat it as the AI playing
                        // what it can and moving on.
                        if (state.CurrentPhase == TurnPhase.Main)
                        {
                            TryPlayCards(playerState);
                        }
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    case TurnPhase.End:
                    case TurnPhase.Cleanup:
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    default:
                        return;
                }
            }
        }

        /// <summary>
        /// Fields this player's opening lineup and closes its setup: the strongest body stands in the Core
        /// Active, the rest fill Node 1-4 in order, nothing is left open and nothing is rerolled.
        /// <para>
        /// Filling every socket is the whole point of the opening under the contest rule. A Node pays its bonus
        /// only to the side that holds it while the opponent does not, so a full lineup is 4-4 and worth nothing
        /// to either player - which is exactly neutral, and the only way to be sure the opponent cannot count
        /// three Nodes uncontested on their first Upkeep. Leaving one open hands it to them for free.
        /// </para>
        /// <para>
        /// Node order is not arbitrary either. Node 1 and Node 2 are the two that grant Core multipliers, so the
        /// second- and third-best bodies are the ones whose survival makes the best body hit harder. The bonus
        /// does not resolve on turn 1 - both sides hold those Nodes - so the ranking is buying the <em>durability
        /// of the multiplier source</em>, to be cashed once a trade clears the opponent's holder off it.
        /// </para>
        /// <para>
        /// The AI never buys a reroll, which is a decision rather than an omission: a reroll is priced in NP,
        /// a currency the AI has no model for valuing against information it cannot weigh. It keeps the five
        /// it was shown. <see cref="MatchEngine"/> still enforces the escalation, so a future opponent that
        /// <em>can</em> price it has the rule waiting rather than needing one invented.
        /// </para>
        /// </summary>
        /// <returns>How many units were fielded, which is fewer than five when the offer came up short.</returns>
        public int ChooseLineup()
        {
            PlayerState mine = state.GetPlayer(player);
            if (mine.LineupConfirmed || mine.LineupOffer.Count == 0)
            {
                return 0;
            }

            List<CardInstance> offer = mine.LineupOffer
                .OrderByDescending(c => c.Definition.Hp + c.Definition.Atk)
                .ThenByDescending(c => c.Definition.Cost)
                .ToList();

            int[] activeInstanceIds = new int[ConfirmLineupCommand.RequiredLength];
            for (int i = 0; i < activeInstanceIds.Length && i < offer.Count; i++)
            {
                activeInstanceIds[i] = offer[i].InstanceId;
            }

            CommandResult result = engine.ExecuteCommand(new ConfirmLineupCommand(player, activeInstanceIds));
            if (result != CommandResult.Success)
            {
                Debug.LogWarning($"[AI] Opening lineup was rejected ({result})");
                return 0;
            }

            return System.Math.Min(offer.Count, activeInstanceIds.Length);
        }

        /// <summary>
        /// Defends against a combat the opponent is currently running. Called by the coordinator during the
        /// player's Combat phase; does nothing on our own turn or outside combat.
        /// </summary>
        public void DeclareBlocks()
        {
            if (state.IsMatchOver() || state.ActivePlayer == player || state.CurrentPhase != TurnPhase.Combat)
            {
                return;
            }

            PlayerState defender = state.GetPlayer(player);
            PlayerState attackerOwner = state.GetOpponent(player);

            List<CardInstance> attackers = attackerOwner.Board.GetActiveUnits(attackerOwner.Index)
                .Where(u => u.Definition.Type == CardType.Unit && u.CanAct())
                .OrderByDescending(u => u.CurrentAtk)
                .ToList();

            List<CardInstance> blockers = defender.Board.GetActiveUnits(player)
                .Where(u => engine.CanBlock(u))
                .ToList();

            foreach (CardInstance attacker in attackers)
            {
                if (!attacker.IsAlive() || engine.GetPendingBlocker(attacker.InstanceId) != 0)
                {
                    continue;
                }

                // Keep the blocker alive if possible; among those prefer the one that also kills the
                // attacker, then the weakest so the biggest body stays free.
                CardInstance chosen = blockers
                    .Where(b => b.CurrentHp > attacker.CurrentAtk)
                    .OrderBy(b => b.CurrentAtk >= attacker.CurrentHp ? 0 : 1)
                    .ThenBy(b => b.CurrentAtk)
                    .FirstOrDefault();

                if (chosen == null)
                {
                    continue;
                }

                CommandResult result = engine.ExecuteCommand(
                    new DeclareBlockCommand(player, chosen.InstanceId, attacker.InstanceId));

                if (result == CommandResult.Success)
                {
                    blockers.Remove(chosen);
                    Debug.Log($"[AI] Declares {chosen.Definition.Name} to block {attacker.Definition.Name}");
                }
            }
        }

        private void TryPlayCards(PlayerState playerState)
        {
            // Spend the biggest cards first; re-check affordability each iteration because earlier plays
            // have already consumed NP.
            List<CardInstance> affordableCards = playerState.Hand
                .Where(c => playerState.CanAfford(c.Definition.Cost))
                .OrderByDescending(c => c.Definition.Cost)
                .ToList();

            foreach (CardInstance card in affordableCards)
            {
                if (!playerState.CanAfford(card.Definition.Cost) || !playerState.Hand.Contains(card))
                {
                    continue;
                }

                CommandResult result;
                string where;

                switch (card.Definition.Type)
                {
                    case CardType.Ability:
                        // Abilities occupy no slot; the engine ignores the slot fields for them.
                        result = engine.ExecuteCommand(new PlayCardCommand(player, card.InstanceId, SlotType.CoreActive, 0));
                        where = "as Ability";
                        break;

                    case CardType.Protocol:
                        if (playerState.Board.Protocol != null && playerState.Board.Protocol.IsAlive())
                        {
                            continue;
                        }

                        result = engine.ExecuteCommand(new PlayCardCommand(player, card.InstanceId, SlotType.CoreActive, 0));
                        where = "as Protocol";
                        break;

                    default:
                        // Units take a Core slot; Relays and Units can also take Node slots. The board
                        // decides which, so Relay deployment is not a special case here.
                        if (!playerState.Board.TryFindFreeSlot(card.Definition.Type, out BoardSlot slot))
                        {
                            continue;
                        }

                        result = engine.ExecuteCommand(
                            new PlayCardCommand(player, card.InstanceId, slot.Type, slot.Index, slot.NodeIndex));
                        where = $"to {slot.Type}[{slot.Index}]{(slot.NodeIndex >= 0 ? $" node {slot.NodeIndex + 1}" : string.Empty)}";
                        break;
                }

                if (result == CommandResult.Success)
                {
                    Debug.Log($"[AI] Played {card.Definition.Name} {where}");
                }
            }
        }

        private void TryAttack(PlayerState playerState)
        {
            PlayerState opponent = state.GetOpponent(player);

            // Iterate by re-reading the board rather than walking a snapshot: an activation can destroy
            // something, a trade can kill the actor, and the ready set is what has to stay truthful as we go.
            while (true)
            {
                CardInstance actor = playerState.Board.GetActiveUnits(player)
                    .FirstOrDefault(u => u != null && u.IsAlive() && u.Definition.Type == CardType.Unit && u.CanAct());
                if (actor == null)
                {
                    return;
                }

                List<CardInstance> enemyUnits = opponent.Board.GetActiveUnits(opponent.Index)
                    .Where(u => u != null && u.IsAlive())
                    .ToList();

                // An open enemy Core outranks everything: it is the win condition, and no ability the AI
                // could press instead of hitting it moves the match forward.
                if (enemyUnits.Count == 0)
                {
                    if (engine.ExecuteCommand(new DeclareAttackCommand(player, actor.InstanceId))
                        != CommandResult.Success)
                    {
                        return;
                    }

                    Debug.Log($"[AI] {actor.Definition.Name} attacks opponent's Core");
                    continue;
                }

                CardInstance favourable = enemyUnits.FirstOrDefault(t => WouldKillAndSurvive(actor, t));
                if (favourable != null)
                {
                    engine.ExecuteCommand(new DeclareAttackCommand(player, actor.InstanceId, favourable.InstanceId));
                    Debug.Log($"[AI] {actor.Definition.Name} attacks {favourable.Definition.Name}");
                    continue;
                }

                // No good trade on offer. A unit holding [Activate] spends its Combat step on the ability
                // instead of swinging into a loss — that is what the keyword is for, and it is why an
                // opponent that only ever attacks never appears to use the cards it drew.
                if (CombatLegality.CanActivate(state, player, actor))
                {
                    if (engine.ExecuteCommand(new ActivateAbilityCommand(player, actor.InstanceId))
                        != CommandResult.Success)
                    {
                        return;
                    }

                    Debug.Log($"[AI] {actor.Definition.Name} activates its skill");
                    continue;
                }

                CardInstance target = enemyUnits[random.Next(enemyUnits.Count)];
                if (engine.ExecuteCommand(new DeclareAttackCommand(player, actor.InstanceId, target.InstanceId))
                    != CommandResult.Success)
                {
                    return;
                }

                Debug.Log($"[AI] {actor.Definition.Name} attacks {target.Definition.Name}");
            }
        }

        /// <summary>
        /// True when <paramref name="attacker"/> can take <paramref name="defender"/> out and still be standing
        /// after the exchange.
        /// <para>
        /// It is an estimate of the <em>trade</em>, not a replay: it reads ATK/HP/Shield and the two keywords
        /// that change them ([Piercing] bypasses Shield, first strike decides who answers at all), and it
        /// ignores Lifesteal, shields granted mid-combat and every effect that is not a stat on the card. The
        /// engine stays the authority on whether the attack is legal and on what actually happens; this only
        /// decides which legal attack the AI prefers.
        /// </para>
        /// <para>
        /// First strike comes from <see cref="MatchEngine.StrikesFirst"/> rather than from the keyword, because
        /// Node 3 grants it to a Core Active as well and an estimate that read only the card face would call a
        /// lethal exchange safe.
        /// </para>
        /// </summary>
        private bool WouldKillAndSurvive(CardInstance attacker, CardInstance defender)
        {
            bool attackerStrikesFirst = engine.StrikesFirst(attacker) && !engine.StrikesFirst(defender);
            bool defenderStrikesFirst = engine.StrikesFirst(defender) && !engine.StrikesFirst(attacker);

            bool kills = attacker.CurrentAtk >= EffectiveHp(defender, attacker);
            if (attackerStrikesFirst)
            {
                // The defender dies before it has an answer, so only the kill matters.
                return kills;
            }

            bool survives = defender.CurrentAtk < EffectiveHp(attacker, defender);

            // Either the defender strikes first and this is only worth taking if we outlive it, or the blows
            // land together and both halves of the trade have to come out ahead.
            return kills && survives;
        }

        /// <summary>
        /// How much damage it takes to kill <paramref name="target"/> against <paramref name="source"/>, which
        /// is what <see cref="CardInstance.TakeDamage"/> does with Shield — absorb first, overflow to HP —
        /// except under [Piercing], where Shield is skipped entirely.
        /// </summary>
        static int EffectiveHp(CardInstance target, CardInstance source)
        {
            return target.CurrentHp
                + (source.HasKeyword(Keyword.Piercing) ? 0 : target.CurrentShield);
        }
    }
}
