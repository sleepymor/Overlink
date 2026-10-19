using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using Overlink.Match;

namespace Overlink.Match.Tests
{
    [TestFixture]
    public sealed class SimpleAITests
    {
        [Test]
        public void SimpleAI_PlaysAffordableCards()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            // Hand control to the opponent through the real phase machine rather than assigning
            // CurrentPhase: TakeTurn() short-circuits unless ActivePlayer is the AI, which is exactly the
            // defect this test used to miss.
            HandOverToOpponent(state, engine, TurnPhase.Main);

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);

            int initialHandCount = state.Players[1].Hand.Count;
            int unitsBefore = state.Players[1].Board.GetAllUnits(PlayerIndex.Opponent).Count;

            ai.TakeTurn();

            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer,
                "The AI must complete its whole turn and hand control back.");
            Assert.Greater(state.Players[1].Board.GetAllUnits(PlayerIndex.Opponent).Count, unitsBefore,
                "The AI should have deployed at least one affordable card.");
            Assert.Less(state.Players[1].Hand.Count, initialHandCount,
                "Playing cards must reduce the AI hand.");
        }

        [Test]
        public void SimpleAI_AttacksWithReadyUnits()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            CardDefinition attackerDef = CreateTestCardDefinition("ai_unit", "AI Unit");
            attackerDef.Atk = 3;
            var unit = new CardInstance(attackerDef, PlayerIndex.Opponent);
            unit.State = UnitState.Ready;
            state.Players[1].Board.CoreActive[0].Card = unit;

            HandOverToOpponent(state, engine, TurnPhase.Combat);

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            int initialCoreDurability = state.Players[0].CoreDurability;

            ai.TakeTurn();

            Assert.IsTrue(
                unit.State == UnitState.Exhausted || state.Players[0].CoreDurability < initialCoreDurability,
                "The ready unit should have attacked.");
        }

        // ---- The turn split -------------------------------------------------------------
        // TakeTurn() used to be one loop, which meant the opponent's whole turn landed in a single frame. That
        // is fine for the AI and fatal for the defender: blocking is the one rule the player gets to use on
        // the opponent's turn, and with no pause in the middle of Combat there is nowhere to ask them. So the
        // turn is now two calls, and the seam between them is the block window.

        [Test]
        public void TakeTurnUntilCombat_StopsOnTheThresholdAndClaimsItReachedIt()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            CardDefinition attackerDef = CreateTestCardDefinition("ai_unit", "AI Unit");
            attackerDef.Atk = 3;
            var unit = new CardInstance(attackerDef, PlayerIndex.Opponent);
            unit.State = UnitState.Ready;
            state.Players[1].Board.CoreActive[0].Card = unit;

            HandOverToOpponent(state, engine, TurnPhase.Main);
            int coreBefore = state.Players[0].CoreDurability;

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);

            Assert.IsTrue(ai.TakeTurnUntilCombat(),
                "The AI played its Main step and walked into Combat, so the seam reports success.");
            Assert.AreEqual(TurnPhase.Combat, state.CurrentPhase, "and it stops *at* Combat, not past it.");
            Assert.AreEqual(PlayerIndex.Opponent, state.ActivePlayer);
            Assert.AreEqual(coreBefore, state.Players[0].CoreDurability,
                "Nothing has swung yet - that is the entire point of stopping here.");
            Assert.AreEqual(UnitState.Ready, unit.State, "The attacker is still Ready for the block window.");

            ai.FinishTurn();

            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer,
                "The second half hands control back, exactly as the whole turn always did.");
            Assert.IsTrue(unit.State == UnitState.Exhausted || state.Players[0].CoreDurability < coreBefore,
                "and the attack that was withheld has now happened.");
        }

        [Test]
        public void TakeTurnUntilCombat_AlreadyInCombat_StopsImmediatelyWithoutAttacking()
        {
            // The block window opens on a turn that was handed over mid-stride. If the first half attacked on
            // the way past Combat, the window would be offering blocks against an attack already resolved.
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            CardDefinition attackerDef = CreateTestCardDefinition("ai_unit", "AI Unit");
            attackerDef.Atk = 3;
            var unit = new CardInstance(attackerDef, PlayerIndex.Opponent);
            unit.State = UnitState.Ready;
            state.Players[1].Board.CoreActive[0].Card = unit;

            HandOverToOpponent(state, engine, TurnPhase.Combat);
            int coreBefore = state.Players[0].CoreDurability;

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);

            Assert.IsTrue(ai.TakeTurnUntilCombat(), "Already at Combat counts as having reached it.");
            Assert.AreEqual(coreBefore, state.Players[0].CoreDurability, "without attacking on the way in.");
            Assert.AreEqual(UnitState.Ready, unit.State);
        }

        [Test]
        public void TakeTurnUntilCombat_NotOurTurn_RunsNothingAndSaysSo()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            int handBefore = state.Players[1].Hand.Count;

            Assert.IsFalse(ai.TakeTurnUntilCombat(),
                "It is the player's turn, so the opponent may not even draw.");
            Assert.AreEqual(handBefore, state.Players[1].Hand.Count);
            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer);

            // FinishTurn is the same contract: called out of turn it does nothing rather than passing a phase
            // belonging to somebody else.
            Assert.DoesNotThrow(ai.FinishTurn);
            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer);
        }

        [Test]
        public void TakeTurn_ComposesTheTwoHalvesIdenticallyToTheOldSingleLoop()
        {
            // The regression guard for the split itself: composing the halves must be indistinguishable from
            // what TakeTurn always did, so every pre-existing caller keeps the behaviour it was written for.
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            CardDefinition attackerDef = CreateTestCardDefinition("ai_unit", "AI Unit");
            attackerDef.Atk = 3;
            var unit = new CardInstance(attackerDef, PlayerIndex.Opponent);
            unit.State = UnitState.Ready;
            state.Players[1].Board.CoreActive[0].Card = unit;

            HandOverToOpponent(state, engine, TurnPhase.Main);
            int coreBefore = state.Players[0].CoreDurability;

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.TakeTurn();

            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer, "Control returns to the player.");
            Assert.IsTrue(unit.State == UnitState.Exhausted || state.Players[0].CoreDurability < coreBefore,
                "The whole turn ran, front half and back.");
        }

        [Test]
        public void SimpleAI_DeclaresBlocksWhileDefending()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            CardDefinition attackerDef = CreateTestCardDefinition("player_unit", "Player Unit");
            attackerDef.Atk = 2;
            var attacker = new CardInstance(attackerDef, PlayerIndex.Player);
            attacker.State = UnitState.Ready;
            state.Players[0].Board.CoreActive[0].Card = attacker;

            CardDefinition blockerDef = CreateTestCardDefinition("ai_wall", "AI Wall");
            blockerDef.Atk = 1;
            blockerDef.Hp = 5;
            var blocker = new CardInstance(blockerDef, PlayerIndex.Opponent);
            blocker.State = UnitState.Ready;
            state.Players[1].Board.CoreActive[0].Card = blocker;

            // Walk the player into their Combat phase without ending the turn. From Upkeep the sequence is
            // Upkeep -> Draw -> Main -> Combat, so three steps land on Combat; a fourth would spill into
            // End and hand the turn over.
            int guard = 0;
            while (state.CurrentPhase != TurnPhase.Combat
                   && state.ActivePlayer == PlayerIndex.Player
                   && guard++ < 6
                   && !state.IsMatchOver())
            {
                engine.ExecuteCommand(new PassPhaseCommand(PlayerIndex.Player));
            }

            Assert.AreEqual(TurnPhase.Combat, state.CurrentPhase);

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.DeclareBlocks();

            Assert.AreEqual(blocker.InstanceId, engine.GetPendingBlocker(attacker.InstanceId),
                "The AI should commit a surviving blocker to the player's ready attacker.");
        }

        [Test]
        public void GameCoordinator_CreatesValidMatchState()
        {
            var state = new MatchState();
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            Assert.IsNotNull(state);
            Assert.AreEqual(MatchState.StartingHandSize, state.Players[0].Hand.Count);
            Assert.AreEqual(MatchState.StartingHandSize, state.Players[1].Hand.Count);
            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer);
            Assert.AreEqual(TurnPhase.Upkeep, state.CurrentPhase);
        }

        [Test]
        public void SimpleAI_MakesLegalMoves()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            // The player never defends in this test, and GDD Cores only hold 3 durability, so give the
            // player a deep Core to keep the test about move legality rather than an early loss. Three
            // rounds only: with 2/2 test units the AI would otherwise grind a normal Core to zero and the
            // "match ended too early" assertion would fire on a legitimate loss.
            state.Players[0].CoreDurability = 30;
            for (int turn = 0; turn < 3; turn++)
            {
                while (state.ActivePlayer == PlayerIndex.Player && !state.IsMatchOver())
                {
                    engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));
                }

                if (state.IsMatchOver())
                {
                    break;
                }

                var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42 + turn);
                ai.TakeTurn();

                Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer,
                    $"Round {turn}: the AI turn must finish and return control.");
            }

            Assert.IsFalse(state.IsMatchOver(), "Match ended too early");
        }

        [Test]
        public void SimpleAI_PlaysProtocols()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            // Isolate the protocol: give the AI only that card and enough NP so the cheaper-card-first
            // ordering cannot starve it.
            CardDefinition protocol = CreateTestCardDefinition("test_protocol", "Test Protocol", CardType.Protocol);
            protocol.Cost = 1;
            protocol.Atk = 0;
            protocol.Hp = 0;
            var protocolInstance = new CardInstance(protocol, PlayerIndex.Opponent);

            state.Players[1].Hand.Clear();
            state.Players[1].Hand.Add(protocolInstance);
            state.Players[1].CurrentNP = 10;

            HandOverToOpponent(state, engine, TurnPhase.Main);

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.TakeTurn();

            Assert.IsNotNull(state.Players[1].Board.Protocol, "The AI should have played the Protocol.");
        }

        [Test]
        public void SimpleAI_PlaysRelaysToNodes()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            CardDefinition relay = CreateTestCardDefinition("test_relay", "Test Relay", CardType.Relay);
            relay.Cost = 1;
            relay.Atk = 0;
            relay.Hp = 0;
            var relayInstance = new CardInstance(relay, PlayerIndex.Opponent);

            state.Players[1].Hand.Clear();
            state.Players[1].Hand.Add(relayInstance);
            state.Players[1].CurrentNP = 10;

            HandOverToOpponent(state, engine, TurnPhase.Main);

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.TakeTurn();

            // Relays cannot sit on Core, so the AI must have used a Node slot and taken that Node.
            Board board = state.Players[1].Board;
            bool relayOnNode = false;
            for (int node = 0; node < Board.NodeCount; node++)
            {
                for (int i = 0; i < Board.NodeActiveSlots; i++)
                {
                    if (board.NodeActive[node][i].Card == relayInstance) relayOnNode = true;
                }

                for (int i = 0; i < Board.NodeBenchSlots; i++)
                {
                    if (board.NodeBench[node][i].Card == relayInstance) relayOnNode = true;
                }
            }

            Assert.IsTrue(relayOnNode, "The Relay should have been deployed to a Node slot.");
            Assert.AreEqual(1, board.CountControlledNodes(PlayerIndex.Opponent),
                "A Relay in a Node Active slot must control that Node.");
        }

        /// <summary>
        /// Ends the player's turn and passes the opponent through the entry phases until
        /// <paramref name="target"/> is reached, without ending the opponent's turn.
        /// </summary>
        private static void HandOverToOpponent(MatchState state, MatchEngine engine, TurnPhase target)
        {
            engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));

            while (state.ActivePlayer == PlayerIndex.Opponent
                   && state.CurrentPhase != target
                   && !state.IsMatchOver())
            {
                // EndTurn hands over at the opponent's Upkeep; each PassPhase walks one step
                // (Upkeep -> Draw -> Main -> Combat -> End -> Cleanup).
                if (state.CurrentPhase == TurnPhase.Cleanup)
                {
                    break;
                }

                engine.ExecuteCommand(new PassPhaseCommand(PlayerIndex.Opponent));
            }
        }

        private CardDefinition CreateTestCardDefinition(string id, string name, CardType type = CardType.Unit)
        {
            return new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = type,
                Cost = 2,
                Atk = type == CardType.Unit ? 2 : 0,
                Hp = type == CardType.Unit ? 2 : 0,
                Shield = 0,
                Keywords = System.Array.Empty<Keyword>(),
                Effects = System.Array.Empty<EffectDescriptor>()
            };
        }

        private List<CardDefinition> CreateTestDeck(int size)
        {
            var deck = new List<CardDefinition>();
            for (int i = 0; i < size; i++)
            {
                deck.Add(CreateTestCardDefinition($"test_{i:000}", $"Card {i}"));
            }
            return deck;
        }
    }
}
