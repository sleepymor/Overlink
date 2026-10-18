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
        /// </summary>
        public void TakeTurn()
        {
            if (state.ActivePlayer != player || state.IsMatchOver())
            {
                return;
            }

            PlayerState playerState = state.GetPlayer(player);

            // A single call must complete the turn, not advance one phase: passing Cleanup flips
            // ActivePlayer, and if we only handled one phase per call the opponent could never reach Main.
            int guard = 0;
            while (state.ActivePlayer == player && !state.IsMatchOver() && guard++ < 64)
            {
                switch (state.CurrentPhase)
                {
                    case TurnPhase.Upkeep:
                    case TurnPhase.Draw:
                    case TurnPhase.End:
                    case TurnPhase.Cleanup:
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    case TurnPhase.Main:
                        TryPlayCards(playerState);
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    case TurnPhase.Combat:
                        TryAttack(playerState);
                        engine.ExecuteCommand(new PassPhaseCommand(player));
                        break;

                    default:
                        return;
                }
            }
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
            List<CardInstance> myUnits = playerState.Board.GetActiveUnits(player);

            foreach (CardInstance attacker in myUnits)
            {
                if (attacker.Definition.Type != CardType.Unit || !attacker.CanAct())
                {
                    continue;
                }

                // Re-read the enemy board each attack: an earlier trade in this same combat may have
                // destroyed the unit we were about to target.
                List<CardInstance> enemyUnits = opponent.Board.GetActiveUnits(opponent.Index)
                    .Where(u => u.IsAlive())
                    .ToList();

                CommandResult result;
                if (enemyUnits.Count > 0)
                {
                    CardInstance target = enemyUnits[random.Next(enemyUnits.Count)];
                    result = engine.ExecuteCommand(new DeclareAttackCommand(player, attacker.InstanceId, target.InstanceId));
                    if (result == CommandResult.Success)
                    {
                        Debug.Log($"[AI] {attacker.Definition.Name} attacks {target.Definition.Name}");
                    }
                }
                else
                {
                    result = engine.ExecuteCommand(new DeclareAttackCommand(player, attacker.InstanceId));
                    if (result == CommandResult.Success)
                    {
                        Debug.Log($"[AI] {attacker.Definition.Name} attacks opponent's Core");
                    }
                }
            }
        }
    }
}
