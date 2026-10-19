using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Overlink.Core.Tests
{
    /// <summary>
    /// <see cref="CombatLegality"/> is the set of moves a pointer-driven board is allowed to light up. It is
    /// derived state, so the interesting failure is not a crash but a quiet disagreement with
    /// <see cref="MatchEngine"/>: a socket that glows for a move the engine refuses, or a move the player has
    /// that never lights. Most of this fixture therefore cross-checks the queries against the engine rather
    /// than against themselves.
    /// <para>
    /// Board geometry worth knowing while reading: one Core Active and four Node Actives per player
    /// (<see cref="Board.CoreActiveSlots"/>/<see cref="Board.NodeActiveSlots"/>), so a test that wants two of
    /// its own attackers on the table must use a Node slot.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class CombatLegalityTests
    {
        // ---- Who may attack ----------------------------------------------------------

        [Test]
        public void CanAttack_ReadyActiveInOwnCombat_IsTrue()
        {
            MatchState state = OurCombat();
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);

            Assert.IsTrue(CombatLegality.CanAttack(state, PlayerIndex.Player, me));
        }

        [Test]
        public void CanAttack_RejectsEveryWayACardFailsToBeAnAttacker()
        {
            MatchState state = OurCombat();

            CardInstance fresh = CoreUnit(state, PlayerIndex.Player, "fresh", 3, 5);
            fresh.HasSummoningDelay = true;
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, fresh),
                "Summoning Delay holds a unit out of its first Combat step.");

            CardInstance tired = NodeUnit(state, PlayerIndex.Player, 0, "tired", 3, 5);
            tired.Exhaust();
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, tired),
                "An exhausted unit has already acted.");

            CardInstance benched = BenchUnit(state, PlayerIndex.Player, 0, "benched", 3, 5);
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, benched),
                "A Bench unit is out of the fight until it is promoted.");

            CardInstance relay = NodeUnit(state, PlayerIndex.Player, 1, "relay", 0, 0);
            relay.Definition.Type = CardType.Relay;
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, relay),
                "A Relay has no ATK (GDD 4.15.1) and can never declare an attack.");

            CardInstance theirs = CoreUnit(state, PlayerIndex.Opponent, "enemy", 3, 5);
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, theirs),
                "Asking about an enemy card through the player's lens is not a move the player has.");

            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, null));
            Assert.IsFalse(CombatLegality.CanAttack(null, PlayerIndex.Player, theirs));
        }

        [Test]
        public void CanAttack_OutsideOurOwnCombatStep_IsFalse()
        {
            MatchState state = NewMatch();
            var engine = new MatchEngine(state);
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);

            Assert.AreEqual(TurnPhase.Upkeep, state.CurrentPhase);
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, me),
                "Still Upkeep: nobody is attacking yet.");

            WalkTo(TurnPhase.Main, state, engine);
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, me),
                "Main is where units are deployed, not where they swing.");

            WalkTo(TurnPhase.Combat, state, engine);
            Assert.IsTrue(CombatLegality.CanAttack(state, PlayerIndex.Player, me));

            // Hand the turn over: the same Ready unit is now defending rather than attacking.
            engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));
            WalkTo(TurnPhase.Combat, state, engine);
            Assert.AreEqual(PlayerIndex.Opponent, state.ActivePlayer);
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, me),
                "It is the opponent's Combat step, so the player may not attack - but it may block.");
            Assert.IsTrue(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, me));
        }

        [Test]
        public void CanAttack_AfterTheMatchIsOver_IsFalse()
        {
            MatchState state = OurCombat();
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);
            state.Concede(PlayerIndex.Player);

            Assert.IsTrue(state.IsMatchOver());
            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, me),
                "A finished match offers no moves, however Ready the unit looks.");
        }

        // ---- What an attacker may hit -------------------------------------------------

        [Test]
        public void AttackTargets_ListsEveryLivingEnemyActive_AndLeadsWithTheMirror()
        {
            MatchState state = OurCombat();
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);
            CardInstance mirror = CoreUnit(state, PlayerIndex.Opponent, "mirror", 2, 5);
            CardInstance onNode = NodeUnit(state, PlayerIndex.Opponent, 2, "node2", 2, 5);
            CardInstance dead = NodeUnit(state, PlayerIndex.Opponent, 1, "corpse", 2, 5);
            dead.State = UnitState.Destroyed;
            CardInstance benched = BenchUnit(state, PlayerIndex.Opponent, 0, "benched", 4, 4);

            List<CardInstance> targets = CombatLegality.AttackTargets(state, PlayerIndex.Player, me);

            CollectionAssert.Contains(targets, mirror);
            CollectionAssert.Contains(targets, onNode);
            CollectionAssert.DoesNotContain(targets, dead, "A destroyed card cannot be hit.");
            CollectionAssert.DoesNotContain(targets, benched,
                "A Bench card is in no battlefield, so the click layer must never light it - even though the " +
                "engine's FindCardInstance would resolve it.");
            Assert.AreEqual(mirror, targets[0],
                "The enemy in our own battlefield leads, because it is the one that can block us.");
        }

        [Test]
        public void AttackTargets_MirrorIsByBattlefieldNotByIndex()
        {
            // A Node attacker's mirror is the enemy's Active in that same Node, not the enemy Core Active.
            MatchState state = OurCombat();
            CardInstance me = NodeUnit(state, PlayerIndex.Player, 2, "attacker", 3, 5);
            CardInstance enemyCore = CoreUnit(state, PlayerIndex.Opponent, "enemyCore", 2, 5);
            CardInstance enemyNode2 = NodeUnit(state, PlayerIndex.Opponent, 2, "enemyNode2", 2, 5);
            NodeUnit(state, PlayerIndex.Opponent, 0, "enemyNode0", 2, 5);

            List<CardInstance> targets = CombatLegality.AttackTargets(state, PlayerIndex.Player, me);

            Assert.AreEqual(enemyNode2, targets[0], "Node 3's mirror is Node 3, not the Core.");
            CollectionAssert.Contains(targets, enemyCore, "Any battlefield may be attacked from any other.");
        }

        [Test]
        public void AttackTargets_IncludesAnEnemyRelayBecauseTheEngineDoes()
        {
            // MatchEngine filters the attacker's type and not the target's, so a Relay holding a Node is a
            // legitimate thing to knock out of it. The query has to agree with the engine even where the GDD
            // is silent, or the socket stays dark for a move the player actually has.
            MatchState state = OurCombat();
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);
            CardInstance relay = NodeUnit(state, PlayerIndex.Opponent, 0, "relay", 0, 0);
            relay.Definition.Type = CardType.Relay;

            CollectionAssert.Contains(CombatLegality.AttackTargets(state, PlayerIndex.Player, me), relay);
        }

        [Test]
        public void AttackTargets_WhenNothingMaySwing_IsEmpty()
        {
            MatchState state = OurCombat();
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);
            me.Exhaust();
            CoreUnit(state, PlayerIndex.Opponent, "enemy", 2, 5);

            Assert.AreEqual(0, CombatLegality.AttackTargets(state, PlayerIndex.Player, me).Count,
                "A unit that cannot act has no targets, rather than a full list the engine will refuse.");
        }

        [Test]
        public void AttackTargets_EveryOfferedTargetIsAcceptedByTheEngine()
        {
            // An offered set is only worth having if the engine accepts every member of it. Each candidate is
            // tried on a freshly built copy of the same board, because declaring an attack exhausts its
            // attacker: a shared board would judge the second attempt against a state the first one changed.
            MatchState probe = Load();
            CardInstance probeAttacker = Find(probe, PlayerIndex.Player, "attacker");
            string[] offered = CombatLegality.AttackTargets(probe, PlayerIndex.Player, probeAttacker)
                .Select(t => t.Definition.Name).ToArray();

            Assert.AreEqual(3, offered.Length, "The probe board should offer all three enemy Actives.");

            foreach (string name in offered)
            {
                MatchState attempt = Load();
                CardInstance attacker = Find(attempt, PlayerIndex.Player, "attacker");
                CardInstance chosen = Find(attempt, PlayerIndex.Opponent, name);

                CommandResult result = new MatchEngine(attempt).ExecuteCommand(
                    new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, chosen.InstanceId));

                Assert.AreEqual(CommandResult.Success, result,
                    $"{name} was offered as a target but the engine refused the attack.");
            }

            // The board every probe and every attempt is built from, kept here so the two cannot drift apart.
            MatchState Load()
            {
                MatchState s = OurCombat();
                CoreUnit(s, PlayerIndex.Player, "attacker", 3, 5);
                CoreUnit(s, PlayerIndex.Opponent, "mirror", 2, 5);
                NodeUnit(s, PlayerIndex.Opponent, 1, "node1", 2, 5);
                NodeUnit(s, PlayerIndex.Opponent, 3, "node3", 2, 5);
                return s;
            }
        }

        [Test]
        public void CanAttackCore_TracksTheAttackerCheck()
        {
            MatchState state = OurCombat();
            CardInstance me = CoreUnit(state, PlayerIndex.Player, "attacker", 3, 5);
            CoreUnit(state, PlayerIndex.Opponent, "enemy", 2, 5);

            Assert.IsTrue(CombatLegality.CanAttackCore(state, PlayerIndex.Player, me),
                "A direct Core hit is a legal choice even with an enemy alive (GDD 2.1.6 Step 4), so the " +
                "option is offered whenever the unit may swing.");

            me.Exhaust();
            Assert.IsFalse(CombatLegality.CanAttackCore(state, PlayerIndex.Player, me));
        }

        // ---- Who may block -------------------------------------------------------------

        [Test]
        public void CanBlock_IsTheDefendersMirrorImage()
        {
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);

            CardInstance mine = CoreUnit(state, PlayerIndex.Player, "mine", 2, 5);
            Assert.IsTrue(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, mine));

            CardInstance theirs = NodeUnit(state, PlayerIndex.Opponent, 0, "theirs", 2, 5);
            Assert.IsFalse(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, theirs),
                "The attacker's own units are not the defender's to commit.");

            CardInstance tired = NodeUnit(state, PlayerIndex.Player, 0, "tired", 2, 5);
            tired.Exhaust();
            Assert.IsFalse(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, tired));

            CardInstance cannot = NodeUnit(state, PlayerIndex.Player, 1, "cannot", 2, 5);
            cannot.Definition.Keywords = new[] { Keyword.CannotBlock };
            Assert.IsFalse(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, cannot),
                "[Cannot Block] is a keyword on the card, not a phase rule (GDD 2.1.5).");

            CardInstance benched = BenchUnit(state, PlayerIndex.Player, 0, "benched", 2, 5);
            Assert.IsFalse(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, benched));

            Assert.IsFalse(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, null));
            Assert.IsFalse(CombatLegality.CanBlock(state, null, PlayerIndex.Player, mine));
        }

        [Test]
        public void CanBlock_SummoningDelayDoesNotHoldABlockerBack()
        {
            // GDD 2.1.5 limits a fresh deployment to "cannot attack or switch"; the engine's CanBlock says the
            // same, and a query describing the engine must not be stricter than it.
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);
            CardInstance fresh = CoreUnit(state, PlayerIndex.Player, "fresh", 2, 5);
            fresh.HasSummoningDelay = true;

            Assert.IsFalse(CombatLegality.CanAttack(state, PlayerIndex.Player, fresh));
            Assert.IsTrue(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, fresh),
                "A unit deployed this turn may still be assigned as a blocker.");
        }

        [Test]
        public void CanBlock_OutsideTheOpponentsCombatStep_IsFalse()
        {
            MatchState state = OurCombat();
            var engine = new MatchEngine(state);
            CardInstance mine = CoreUnit(state, PlayerIndex.Player, "mine", 2, 5);

            Assert.IsFalse(CombatLegality.CanBlock(state, engine, PlayerIndex.Player, mine),
                "It is our own Combat step, so there is nothing for us to block.");
        }

        // ---- What a blocker may answer --------------------------------------------------

        [Test]
        public void BlockableAttackers_IsRestrictedToTheBlockersOwnBattlefield()
        {
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);

            CardInstance coreBlocker = CoreUnit(state, PlayerIndex.Player, "coreBlocker", 2, 5);
            CardInstance nodeBlocker = NodeUnit(state, PlayerIndex.Player, 2, "nodeBlocker", 2, 5);

            CardInstance enemyCore = CoreUnit(state, PlayerIndex.Opponent, "enemyCore", 3, 5);
            CardInstance enemyNode2 = NodeUnit(state, PlayerIndex.Opponent, 2, "enemyNode2", 3, 5);
            CardInstance enemyNode0 = NodeUnit(state, PlayerIndex.Opponent, 0, "enemyNode0", 3, 5);

            List<CardInstance> fromCore = CombatLegality.BlockableAttackers(
                state, engine, PlayerIndex.Player, coreBlocker);
            CollectionAssert.Contains(fromCore, enemyCore);
            CollectionAssert.DoesNotContain(fromCore, enemyNode2,
                "A Core Active cannot step into a Node's fight (GDD 2.1.6 Step 2).");
            CollectionAssert.DoesNotContain(fromCore, enemyNode0);

            List<CardInstance> fromNode = CombatLegality.BlockableAttackers(
                state, engine, PlayerIndex.Player, nodeBlocker);
            CollectionAssert.Contains(fromNode, enemyNode2, "Node 3 answers Node 3.");
            CollectionAssert.DoesNotContain(fromNode, enemyCore);
            CollectionAssert.DoesNotContain(fromNode, enemyNode0, "Node 3 does not answer Node 1.");
        }

        [Test]
        public void BlockableAttackers_SkipsAnAttackerThatCannotSwing()
        {
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);
            CardInstance blocker = CoreUnit(state, PlayerIndex.Player, "blocker", 2, 5);
            CardInstance tired = CoreUnit(state, PlayerIndex.Opponent, "tired", 3, 5);
            tired.Exhaust();

            Assert.AreEqual(0, CombatLegality.BlockableAttackers(state, engine, PlayerIndex.Player, blocker).Count,
                "An exhausted enemy has no attack to answer, so its socket must not light as a block target.");
        }

        [Test]
        public void BlockableAttackers_SkipsAnEnemyRelayBecauseARelayNeverAttacks()
        {
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);
            CardInstance blocker = NodeUnit(state, PlayerIndex.Player, 1, "blocker", 2, 5);
            CardInstance relay = NodeUnit(state, PlayerIndex.Opponent, 1, "relay", 0, 0);
            relay.Definition.Type = CardType.Relay;

            Assert.AreEqual(0, CombatLegality.BlockableAttackers(state, engine, PlayerIndex.Player, blocker).Count,
                "Answering a Relay would waste the block on a card that cannot swing.");
        }

        [Test]
        public void BlockableAttackers_SkipsAnAttackerThatAlreadyHasABlocker()
        {
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);
            CardInstance blocker = NodeUnit(state, PlayerIndex.Player, 0, "blocker", 2, 5);
            CardInstance attacker = NodeUnit(state, PlayerIndex.Opponent, 0, "attacker", 3, 5);

            Assert.AreEqual(1, CombatLegality.BlockableAttackers(state, engine, PlayerIndex.Player, blocker).Count);

            engine.ExecuteCommand(new DeclareBlockCommand(PlayerIndex.Player, blocker.InstanceId, attacker.InstanceId));

            Assert.AreEqual(0, CombatLegality.BlockableAttackers(state, engine, PlayerIndex.Player, blocker).Count,
                "One blocker per attacker: a met attacker is not offered again, and this blocker is spent.");
        }

        [Test]
        public void BlockableAttackers_EveryOfferedBlockIsAcceptedByTheEngine()
        {
            // Same shape as the attack cross-check: ask the query on one board, then hand every offered block to
            // a freshly built copy of it, because recording a block mutates the pending-block map.
            MatchState probe = Load();
            CardInstance probeBlocker = Find(probe, PlayerIndex.Player, "blocker");
            var probeEngine = new MatchEngine(probe);

            string[] offered = CombatLegality
                .BlockableAttackers(probe, probeEngine, PlayerIndex.Player, probeBlocker)
                .Select(a => a.Definition.Name).ToArray();

            Assert.AreEqual(1, offered.Length);

            foreach (string name in offered)
            {
                MatchState attempt = Load();
                CardInstance blocker = Find(attempt, PlayerIndex.Player, "blocker");
                CardInstance attacker = Find(attempt, PlayerIndex.Opponent, name);

                Assert.AreEqual(CommandResult.Success, new MatchEngine(attempt).ExecuteCommand(
                    new DeclareBlockCommand(PlayerIndex.Player, blocker.InstanceId, attacker.InstanceId)),
                    $"blocker was offered a block on {name} that the engine would not record.");
            }

            MatchState Load()
            {
                MatchState s = TheirCombat();
                CoreUnit(s, PlayerIndex.Player, "blocker", 2, 5);
                CoreUnit(s, PlayerIndex.Opponent, "enemyCore", 3, 5);
                return s;
            }
        }

        [Test]
        public void MisplacedBlock_IsOfferedByNowhereYetStillRecordedByTheEngine()
        {
            // The reason BlockableAttackers repeats the placement rule instead of trusting the engine's
            // declaration check: ExecuteDeclareBlock happily records a block from the wrong battlefield, and
            // ConsumePendingBlocker then ignores it. Lighting that socket would promise the player a save
            // their unit never makes.
            MatchState state = TheirCombat();
            var engine = new MatchEngine(state);

            CardInstance nodeBlocker = NodeUnit(state, PlayerIndex.Player, 2, "nodeBlocker", 2, 5);
            CardInstance victim = CoreUnit(state, PlayerIndex.Player, "victim", 2, 5);
            CardInstance enemyCore = CoreUnit(state, PlayerIndex.Opponent, "enemyCore", 3, 5);

            Assert.AreEqual(0, CombatLegality.BlockableAttackers(state, engine, PlayerIndex.Player, nodeBlocker).Count,
                "The query must not offer a Core attack to a Node 3 blocker.");

            // The engine still accepts it, which is exactly the trap the query exists to avoid.
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Player, nodeBlocker.InstanceId, enemyCore.InstanceId)));

            int victimBefore = victim.CurrentHp;
            engine.ExecuteCommand(new DeclareAttackCommand(
                PlayerIndex.Opponent, enemyCore.InstanceId, victim.InstanceId));

            Assert.AreEqual(victimBefore - 3, victim.CurrentHp,
                "The misplaced block is ignored, so the attack lands on its target - a lit socket here would " +
                "have been a lie.");
            Assert.AreEqual(5, nodeBlocker.CurrentHp, "The blocker never entered the fight.");
        }

        // ---- Battlefield identity ---------------------------------------------------------

        [Test]
        public void SharesBattlefield_MatchesTheEnginesReading()
        {
            var state = new MatchState();
            Board board = state.Players[0].Board;

            Assert.IsTrue(CombatLegality.SharesBattlefield(board.CoreActive[0], board.CoreActive[0]));
            Assert.IsTrue(CombatLegality.SharesBattlefield(board.NodeActive[1][0], board.NodeActive[1][0]));
            Assert.IsFalse(CombatLegality.SharesBattlefield(board.NodeActive[1][0], board.NodeActive[2][0]),
                "Two different Nodes are two different battles.");
            Assert.IsFalse(CombatLegality.SharesBattlefield(board.CoreActive[0], board.NodeActive[1][0]));
            Assert.IsFalse(CombatLegality.SharesBattlefield(board.CoreActive[0], board.CoreBench[0]),
                "A Bench slot shares no battlefield with anything, least of all its own Active.");
            Assert.IsFalse(CombatLegality.SharesBattlefield(null, board.CoreActive[0]));
            Assert.IsFalse(CombatLegality.SharesBattlefield(board.CoreActive[0], null));
        }

        // ---- fixtures -----------------------------------------------------------------------

        /// <summary>A live match in the player's Combat phase, with nothing on either board.</summary>
        private static MatchState OurCombat()
        {
            MatchState state = NewMatch();
            WalkTo(TurnPhase.Combat, state, new MatchEngine(state));
            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer);
            Assert.AreEqual(TurnPhase.Combat, state.CurrentPhase);
            return state;
        }

        /// <summary>The same, handed over: the opponent is swinging and the player may block.</summary>
        private static MatchState TheirCombat()
        {
            MatchState state = OurCombat();
            var engine = new MatchEngine(state);
            engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));
            WalkTo(TurnPhase.Combat, state, engine);
            Assert.AreEqual(PlayerIndex.Opponent, state.ActivePlayer);
            Assert.AreEqual(TurnPhase.Combat, state.CurrentPhase);
            return state;
        }

        private static MatchState NewMatch()
        {
            var state = new MatchState();
            state.InitializeMatch(Deck(), Deck());
            return state;
        }

        private static void WalkTo(TurnPhase target, MatchState state, MatchEngine engine)
        {
            int guard = 0;
            while (state.CurrentPhase != target && !state.IsMatchOver() && guard++ < 8)
            {
                engine.ExecuteCommand(new PassPhaseCommand(state.ActivePlayer));
            }

            Assert.AreEqual(target, state.CurrentPhase, $"Could not reach {target}.");
        }

        private static List<CardInstance> AllActives(MatchState state, PlayerIndex owner)
        {
            return state.GetPlayer(owner).Board.GetActiveUnits(owner);
        }

        /// <summary>
        /// Pulls one Active unit out of a board by name. <c>Single</c> rather than <c>First</c> on purpose: the
        /// callers rebuild the same board twice - once to ask the query, once to hand the engine a command - and
        /// a board that silently grew or lost a unit should fail here rather than pick an arbitrary survivor.
        /// </summary>
        private static CardInstance Find(MatchState state, PlayerIndex owner, string name)
        {
            return AllActives(state, owner).Single(c => c.Definition.Name == name);
        }

        private static CardInstance CoreUnit(MatchState state, PlayerIndex owner, string name, int atk, int hp)
        {
            CardInstance unit = Instance(UnitDef(name, atk, hp), owner);
            state.GetPlayer(owner).Board.CoreActive[0].Card = unit;
            return unit;
        }

        private static CardInstance NodeUnit(MatchState state, PlayerIndex owner, int node, string name, int atk, int hp)
        {
            CardInstance unit = Instance(UnitDef(name, atk, hp), owner);
            state.GetPlayer(owner).Board.NodeActive[node][0].Card = unit;
            return unit;
        }

        private static CardInstance BenchUnit(MatchState state, PlayerIndex owner, int index, string name, int atk, int hp)
        {
            CardInstance unit = Instance(UnitDef(name, atk, hp), owner);
            state.GetPlayer(owner).Board.CoreBench[index].Card = unit;
            return unit;
        }

        private static CardInstance Instance(CardDefinition def, PlayerIndex owner)
        {
            return new CardInstance(def, owner) { State = UnitState.Ready };
        }

        private static List<CardDefinition> Deck()
        {
            var list = new List<CardDefinition>();
            for (int i = 0; i < 20; i++)
            {
                list.Add(UnitDef("deck_" + i, 1, 1));
            }

            return list;
        }

        private static CardDefinition UnitDef(string name, int atk, int hp)
        {
            return new CardDefinition
            {
                Id = "card_fsti_" + Math.Abs(name.GetHashCode() % 900 + 100),
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 1,
                Atk = atk,
                Hp = hp
            };
        }
    }
}
