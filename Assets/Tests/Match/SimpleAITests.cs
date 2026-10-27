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

            // The premise of the assertion below was only half right when it was written. A Core at 30 does
            // survive three rounds of 2-damage swings, but this test's deck is all 1-cost 2/2 Units, so the AI
            // fields five of them in its first Main phase - and that is four Nodes held, which satisfies GDD
            // 2.1.8 Node Domination at its next Upkeep. The match ending there is a legitimate win, not an
            // illegal move, and this test is about moves.
            //
            // So what has to hold is stated positively instead: control came back every round (asserted in the
            // loop above, and the failure that matters for legality), and if the match did end it ends for a
            // named victory condition rather than by a sweep nobody can explain. Stage 2 exempted turn 1 from
            // the domination check, which does not help this test - the AI's second Upkeep is turn 2 or 3 and
            // it still holds whatever Nodes survived.
            if (state.IsMatchOver())
            {
                Assert.IsNotEmpty(state.ResultReason,
                    $"A match that ends mid-test must end for a stated victory condition, not an unexplained " +
                    $"one (result {state.Result}, turn {state.TurnNumber}).");
            }
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

        [Test]
        public void SimpleAI_ChooseLineup_FillsAllFiveActivesAndBuysNoReroll()
        {
            var state = new MatchState(5);
            var engine = new MatchEngine(state);
            state.InitializeSetup(CreateTestDeck(30), CreateTestDeck(30));

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);

            Assert.AreEqual(MatchState.LineupOfferSize, ai.ChooseLineup());

            Board board = state.Players[1].Board;
            Assert.IsNotNull(board.CoreActive[0].Card, "the Core is fielded");
            for (int node = 0; node < Board.NodeCount; node++)
            {
                Assert.IsNotNull(board.NodeActive[node][0].Card, $"Node {node + 1} is fielded");
            }

            Assert.IsTrue(state.Players[1].LineupConfirmed);
            Assert.AreEqual(0, state.Players[1].LineupRerolls,
                "the AI does not pay for information it has no way to price");
            Assert.AreEqual(0, state.Players[1].LineupOffer.Count, "and nothing was left unfielded in the offer");
        }

        [Test]
        public void SimpleAI_PutsTheBestBodyInTheCoreAndTheNextTwoOnTheMultiplierNodes()
        {
            // Node 1 and Node 2 grant the Core Active +1 ATK and +1 HP, so the second- and third-best bodies
            // are the ones making the best body stronger. A lineup that filled the Nodes in deck order would
            // leave that value on the table, and this is the only test that can see it happen.
            var deck = new List<CardDefinition>
            {
                Body("weak", 1, 0),
                Body("light", 1, 1),
                Body("medium", 2, 1),
                Body("heavy", 2, 2),
                Body("biggest", 3, 2)
            };

            var state = new MatchState(7);
            var engine = new MatchEngine(state);
            state.InitializeSetup(deck, new List<CardDefinition>(deck));

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 11);
            ai.ChooseLineup();

            Board board = state.Players[1].Board;
            Assert.AreEqual("biggest", board.CoreActive[0].Card.Definition.Name, "strongest body holds the Core");
            Assert.AreEqual("heavy", board.NodeActive[0][0].Card.Definition.Name, "Node 1 feeds it ATK");
            Assert.AreEqual("medium", board.NodeActive[1][0].Card.Definition.Name, "Node 2 feeds it HP");
            Assert.AreEqual("light", board.NodeActive[2][0].Card.Definition.Name);
            Assert.AreEqual("weak", board.NodeActive[3][0].Card.Definition.Name);
        }

        [Test]
        public void SimpleAI_ThroughTheSetupPath_OpensALiveMatchAndMakesLegalMoves()
        {
            // The whole opening, end to end: setup, two lineups, the coin, and a full AI turn on top of a board
            // that already holds five units per side. This is the path the shipped game is about to start using,
            // and before Stage 2's turn-1 domination exemption it would have ended at the first Upkeep.
            var state = new MatchState(9);
            var engine = new MatchEngine(state);
            state.InitializeSetup(CreateTestDeck(30), CreateTestDeck(30));

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.ChooseLineup();

            // The human seat confirms through the same command the UI will use.
            List<CardInstance> offer = state.Players[0].LineupOffer;
            var ids = new int[ConfirmLineupCommand.RequiredLength];
            for (int i = 0; i < ids.Length && i < offer.Count; i++)
            {
                ids[i] = offer[i].InstanceId;
            }

            Assert.AreEqual(CommandResult.Success,
                engine.ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, ids)));

            Assert.IsFalse(state.SetupInProgress, "both lineups in, the match is live");
            Assert.AreEqual(MatchResult.None, state.Result, "and the opening did not award it to the coin winner");
            Assert.AreEqual(5, state.Players[0].Hand.Count);
            Assert.AreEqual(5, state.Players[1].Hand.Count);

            // Whichever seat the coin put first, the AI has to actually be the one up before TakeTurn can be
            // judged: it short-circuits off-turn by design, and a test that assumed an order would pass by
            // watching it do nothing.
            if (state.FirstPlayer == PlayerIndex.Player)
            {
                Assert.AreEqual(CommandResult.Success,
                    engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player)));
            }

            Assert.AreEqual(PlayerIndex.Opponent, state.ActivePlayer, "the AI is up to take a turn");

            // What the AI's turn has to do is move the position. It is not asserted that control comes back,
            // because a full opening lineup makes that false for a legitimate reason: five Ready bodies swing
            // on turn 1, an unblocked hit through a Core Active transfers to the Core, and a Core holds 3
            // durability - so a seat that develops nothing simply dies. Demanding the hand-back would have
            // tested the opponent's weakness rather than the setup path, so the end-of-turn shape is stated as
            // "something happened, or the match ended for a named condition".
            int aiHandBefore = state.Players[1].Hand.Count;
            int myUnitsBefore = state.Players[0].Board.GetAllUnits(PlayerIndex.Player).Count;
            int myCoreBefore = state.Players[0].CoreDurability;

            ai.TakeTurn();

            Assert.IsTrue(
                state.Players[1].Hand.Count < aiHandBefore
                    || state.Players[0].CoreDurability < myCoreBefore
                    || state.Players[0].Board.GetAllUnits(PlayerIndex.Player).Count < myUnitsBefore,
                "the AI deployed, damaged the Core, or killed a body - its turn was not a no-op");
            Assert.IsTrue(state.ActivePlayer == PlayerIndex.Player || state.IsMatchOver(),
                "and either control came back to the human seat or the AI's turn ended the match outright");
            Assert.IsTrue(!state.IsMatchOver() || !string.IsNullOrEmpty(state.ResultReason),
                "and an ended match names the condition that ended it, not an unexplained sweep " +
                $"(result {state.Result}, turn {state.TurnNumber})");
        }

        [Test]
        public void SimpleAI_PressesAnAbilityInsteadOfTakingATradeItLoses()
        {
            var state = new MatchState(13);
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));
            HandOverToOpponent(state, engine, TurnPhase.Combat);

            // A 1/1 with [Activate] facing a 5/5: attacking is suicide for nothing, so the Combat step is spent
            // on the ability. NP is zeroed so the GainNP effect is the only thing that can move it, and the AI
            // holds exactly one body - a second Ready Active would take its own swing at the 5/5 and the test
            // would be measuring a trade rather than the decision not to.
            CardDefinition able = CreateTestCardDefinition("test_ability", "Breaker");
            able.Keywords = new[] { Keyword.Activate };
            able.Effects = new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 3) };
            CardInstance mine = Place(state, PlayerIndex.Opponent, able, SlotType.CoreActive, 1, 1);

            CardInstance enemy = Place(state, PlayerIndex.Player, CreateTestCardDefinition("test_wall", "Wall"),
                SlotType.CoreActive, 5, 5);

            state.Players[1].CurrentNP = 0;
            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.FinishTurn();

            Assert.AreEqual(3, state.Players[1].CurrentNP, "the ability fired and its effect resolved");
            Assert.AreEqual(UnitState.Exhausted, mine.State, "and it cost the unit its Combat step");
            Assert.AreEqual(5, enemy.CurrentHp, "the 1/1 never swung into the 5/5");
        }

        [Test]
        public void SimpleAI_StrikesAnOpenCoreRatherThanPressingAnAbility()
        {
            var state = new MatchState(17);
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));
            HandOverToOpponent(state, engine, TurnPhase.Combat);

            CardDefinition able = CreateTestCardDefinition("test_ability", "Breaker");
            able.Keywords = new[] { Keyword.Activate };
            able.Effects = new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 3) };
            Place(state, PlayerIndex.Opponent, able, SlotType.CoreActive, 1, 1);

            state.Players[1].CurrentNP = 0;
            int durabilityBefore = state.Players[0].CoreDurability;

            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.FinishTurn();

            Assert.Less(state.Players[0].CoreDurability, durabilityBefore,
                "an undefended Core is the win condition, and no ability outranks it");
            Assert.AreEqual(0, state.Players[1].CurrentNP, "so the ability stayed unpressed");
        }

        [Test]
        public void SimpleAI_TakesATradeItWinsRatherThanPressingAnAbility()
        {
            var state = new MatchState(19);
            var engine = new MatchEngine(state);
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));
            HandOverToOpponent(state, engine, TurnPhase.Combat);

            CardDefinition able = CreateTestCardDefinition("test_ability", "Breaker");
            able.Keywords = new[] { Keyword.Activate };
            able.Effects = new[] { new EffectDescriptor(EffectType.GainNP, TargetKind.Self, 3) };
            Place(state, PlayerIndex.Opponent, able, SlotType.CoreActive, 3, 3);
            Place(state, PlayerIndex.Player, CreateTestCardDefinition("test_soft", "Soft"),
                SlotType.CoreActive, 1, 1);

            state.Players[1].CurrentNP = 0;
            var ai = new SimpleAI(engine, state, PlayerIndex.Opponent, 42);
            ai.FinishTurn();

            Assert.AreEqual(0, state.Players[0].Board.GetAllUnits(PlayerIndex.Player).Count,
                "the favourable trade was taken");
            Assert.AreEqual(0, state.Players[1].CurrentNP, "and the ability lost to a kill it survives");
        }

        /// <summary>A body with a distinct stat total, so a ranking assertion has something to rank.</summary>
        private CardDefinition Body(string name, int atk, int hp)
        {
            CardDefinition definition = CreateTestCardDefinition($"test_{name}", name);
            definition.Atk = atk;
            definition.Hp = hp;
            definition.Cost = 1;
            return definition;
        }

        /// <summary>
        /// Puts a live Ready unit in a socket. The AI tests need boards shaped exactly one way, and playing a
        /// card into position would drag NP, the phase ladder and the summoning rule into each of them.
        /// </summary>
        private CardInstance Place(MatchState state, PlayerIndex who, CardDefinition definition,
            SlotType slotType, int atk, int hp, int index = 0, int node = -1)
        {
            definition.Atk = atk;
            definition.Hp = hp;
            var card = new CardInstance(definition, who);
            card.State = UnitState.Ready;
            card.HasSummoningDelay = false;
            state.GetPlayer(who).Board.GetSlot(slotType, index, node).Card = card;
            return card;
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
