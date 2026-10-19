using System.Collections.Generic;

namespace Overlink.Core
{
    /// <summary>
    /// The combat questions a front end has to ask <em>before</em> a move exists: which of my units may act,
    /// what it may hit, and which enemy attacks one of my units could answer.
    /// <para>
    /// <see cref="MatchEngine"/> can only answer these by being offered a concrete command, and a command that
    /// is refused returns an opaque <see cref="CommandResult"/> with no enumeration of what <em>would</em> have
    /// worked. A pointer-driven board cannot light a socket by trying every command each frame, so the
    /// permissible set is computed here instead and the engine stays the sole authority on executing it.
    /// </para>
    /// <para>
    /// Everything in this file is derived state: it reads <see cref="MatchState"/> and never writes it, and it
    /// deliberately mirrors the checks <see cref="MatchEngine"/> applies rather than inventing its own. Where
    /// the two must agree, the engine is cited so a future rule change is visible on both sides. Being headless,
    /// it is testable without a scene, a camera or a frame - which is the practical reason it is a separate file
    /// rather than a handful of methods on the match presenter.
    /// </para>
    /// </summary>
    public static class CombatLegality
    {
        /// <summary>
        /// True when <paramref name="who"/> may nominate <paramref name="card"/> as an attacker right now: it is
        /// theirs, it is a Unit, it stands in an Active slot, it is Ready, and it is their own Combat phase.
        /// <para>
        /// The engine's equivalent gate is the head of
        /// <see cref="MatchEngine"/>'s declare-attack path: <c>CurrentPhase == Combat</c>,
        /// <c>FindActiveCardInstance</c> for the attacker, <c>CanAct()</c>, and the
        /// <c>Type != CardType.Unit</c> refusal. Only a Unit attacks - a Relay in a Node Active slot occupies a
        /// combat socket but has no ATK (GDD 4.15.1).
        /// </para>
        /// </summary>
        public static bool CanAttack(MatchState state, PlayerIndex who, CardInstance card)
        {
            if (state == null || card == null || state.IsMatchOver())
            {
                return false;
            }

            if (state.ActivePlayer != who || state.CurrentPhase != TurnPhase.Combat)
            {
                return false;
            }

            if (card.Owner != who || card.Definition.Type != CardType.Unit || !card.CanAct())
            {
                return false;
            }

            // Only an Active attacks: a Bench card is out of the fight until it is promoted, which is the
            // exclusion Board.GetActiveUnits documents.
            BoardSlot slot = state.GetPlayer(who).Board.FindSlot(card);
            return slot != null && IsActiveSlot(slot);
        }

        /// <summary>
        /// Every enemy card <paramref name="attacker"/> may legally hit: all living enemy cards in an Active
        /// slot, in any of the five battlefields.
        /// <para>
        /// An attacker is never restricted to its own battlefield - only a <em>blocker</em> is (GDD 2.1.6
        /// Step 2) - which is why the enemy in the attacker's own battlefield sorts to the front rather than
        /// being the only candidate. That one can actually be interposed against the attack, so a player who
        /// ignores targeting order and clicks the first lit socket gets the answer the keyboard path always
        /// gave.
        /// </para>
        /// <para>
        /// Benched enemies are absent even though <c>MatchEngine.FindCardInstance</c> resolves a Bench card as
        /// a target: <see cref="Board.GetActiveUnits"/> is the combat set, and offering a move the GDD does not
        /// contain would make the UI a lie. The engine-side permissiveness is a known hole, flagged and left
        /// alone; this is the boundary that keeps it invisible to the player.
        /// </para>
        /// <para>
        /// Relays in Active slots are included, matching the engine, which filters the <em>attacker</em>'s type
        /// and not the target's. A Relay is a legitimate thing to knock out of a Node.
        /// </para>
        /// </summary>
        public static List<CardInstance> AttackTargets(MatchState state, PlayerIndex who, CardInstance attacker)
        {
            var targets = new List<CardInstance>();
            if (!CanAttack(state, who, attacker))
            {
                return targets;
            }

            PlayerState opponent = state.GetOpponent(who);
            BoardSlot mine = state.GetPlayer(who).Board.FindSlot(attacker);
            SlotType myType = mine.Type;
            int myNode = mine.NodeIndex;

            foreach (CardInstance candidate in opponent.Board.GetActiveUnits(opponent.Index))
            {
                if (candidate != null && candidate.IsAlive())
                {
                    targets.Add(candidate);
                }
            }

            targets.Sort((a, b) => Relevance(a).CompareTo(Relevance(b)));
            return targets;

            int Relevance(CardInstance candidate)
            {
                BoardSlot slot = opponent.Board.FindSlot(candidate);
                bool sameBattlefield = slot != null && slot.Type == myType && slot.NodeIndex == myNode;
                return sameBattlefield ? 0 : 1;
            }
        }

        /// <summary>
        /// True when the direct Core strike is worth offering. The engine accepts a Core attack whether or not
        /// the opponent has blockers - an unblocked attack is a legal <em>choice</em>, not a fallback, and only
        /// a pending Core Active can intercept it - so this governs whether the option is presented, not
        /// whether it works.
        /// </summary>
        public static bool CanAttackCore(MatchState state, PlayerIndex who, CardInstance attacker)
        {
            return CanAttack(state, who, attacker);
        }

        /// <summary>
        /// True when <paramref name="who"/> may commit <paramref name="card"/> as a blocker right now. Blocking
        /// is the defender's move, so this is the mirror image of <see cref="CanAttack"/>: it is the
        /// <em>opponent's</em> Combat phase, the card is ours, it is Active, and the engine's own
        /// <see cref="MatchEngine.CanBlock"/> accepts it.
        /// <para>
        /// Delegating the unit-shaped check to <c>CanBlock</c> is deliberate: that is where [Cannot Block] and
        /// the Summoning-Delay exemption live, and a click layer with its own copy would drift.
        /// </para>
        /// </summary>
        public static bool CanBlock(MatchState state, MatchEngine engine, PlayerIndex who, CardInstance card)
        {
            if (state == null || engine == null || card == null || state.IsMatchOver())
            {
                return false;
            }

            if (state.ActivePlayer == who || state.CurrentPhase != TurnPhase.Combat)
            {
                return false;
            }

            if (card.Owner != who || !engine.CanBlock(card))
            {
                return false;
            }

            BoardSlot slot = state.GetPlayer(who).Board.FindSlot(card);
            return slot != null && IsActiveSlot(slot);
        }

        /// <summary>
        /// The enemy attacks <paramref name="blocker"/> could legally answer. A block is keyed to a specific
        /// attacker in this engine - <c>DeclareBlockCommand</c> records one pending blocker per attacker, and
        /// <c>ConsumePendingBlocker</c> applies it when that attacker swings - so the useful question is not
        /// "can this unit block" but "what can this unit block <em>right now</em>".
        /// <para>
        /// The placement rule is enforced where it bites: a Core attack may only be met by a Core Active, and a
        /// unit attack only by an Active sharing the target's battlefield. That is the same test
        /// <c>ConsumePendingBlocker</c> applies on consumption; repeating it here is what stops a socket from
        /// lighting up for a block the engine is about to quietly ignore.
        /// </para>
        /// </summary>
        public static List<CardInstance> BlockableAttackers(MatchState state, MatchEngine engine,
            PlayerIndex who, CardInstance blocker)
        {
            var list = new List<CardInstance>();
            if (!CanBlock(state, engine, who, blocker))
            {
                return list;
            }

            PlayerState mine = state.GetPlayer(who);
            PlayerState attackerOwner = state.GetOpponent(who);
            BoardSlot mySlot = mine.Board.FindSlot(blocker);

            foreach (CardInstance enemy in attackerOwner.Board.GetActiveUnits(attackerOwner.Index))
            {
                if (enemy == null || !enemy.IsAlive() || enemy.Definition.Type != CardType.Unit || !enemy.CanAct())
                {
                    continue;
                }

                // One blocker per attacker: somebody already standing in front of this one is not a target.
                if (engine.GetPendingBlocker(enemy.InstanceId) != 0)
                {
                    continue;
                }

                BoardSlot theirs = attackerOwner.Board.FindSlot(enemy);
                if (theirs == null || !SharesBattlefield(mySlot, theirs))
                {
                    continue;
                }

                list.Add(enemy);
            }

            return list;
        }

        /// <summary>
        /// True when two slots are the same battlefield: both Core Actives, or Actives of the same Node.
        /// Bench slots never qualify, because a benched card is not in a battlefield at all.
        /// </summary>
        public static bool SharesBattlefield(BoardSlot a, BoardSlot b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Type == SlotType.CoreActive && b.Type == SlotType.CoreActive)
            {
                return true;
            }

            return a.Type == SlotType.NodeActive && b.Type == SlotType.NodeActive && a.NodeIndex == b.NodeIndex;
        }

        static bool IsActiveSlot(BoardSlot slot)
        {
            return slot.Type == SlotType.CoreActive || slot.Type == SlotType.NodeActive;
        }
    }
}
