using NUnit.Framework;
using System.Collections.Generic;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public sealed class MatchEngineTests
    {
        [Test]
        public void MatchEngine_ExecutePlayCard_PlacesUnitOnBoard()
        {
            var state = new MatchState();
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            var card = new CardInstance(definition, PlayerIndex.Player);
            state.Players[0].Hand.Add(card);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreActive, 0);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.IsTrue(state.Players[0].Board.CoreActive[0].IsOccupied());
            Assert.AreEqual(card, state.Players[0].Board.CoreActive[0].Card);
            Assert.AreEqual(0, state.Players[0].Hand.Count);
        }

        [Test]
        public void MatchEngine_ExecutePlayCard_FailsWhenInsufficientNP()
        {
            var state = new MatchState();
            var definition = CreateTestCardDefinition("test_001", "Expensive Card", CardType.Unit);
            definition.Cost = 100;
            var card = new CardInstance(definition, PlayerIndex.Player);
            state.Players[0].Hand.Add(card);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreActive, 0);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.InsufficientNP, result);
            Assert.AreEqual(1, state.Players[0].Hand.Count);
        }

        [Test]
        public void MatchEngine_ExecutePlayCard_FailsWhenWrongPhase()
        {
            var state = new MatchState();
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            var card = new CardInstance(definition, PlayerIndex.Player);
            state.Players[0].Hand.Add(card);
            state.CurrentPhase = TurnPhase.Draw;

            var engine = new MatchEngine(state);
            var command = new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreActive, 0);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.InvalidPhase, result);
        }

        [Test]
        public void MatchEngine_ExecutePlayCard_FailsWhenSlotOccupied()
        {
            var state = new MatchState();
            var def1 = CreateTestCardDefinition("test_001", "Card 1", CardType.Unit);
            var def2 = CreateTestCardDefinition("test_002", "Card 2", CardType.Unit);
            var card1 = new CardInstance(def1, PlayerIndex.Player);
            var card2 = new CardInstance(def2, PlayerIndex.Player);
            
            state.Players[0].Board.CoreActive[0].Card = card1;
            state.Players[0].Hand.Add(card2);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new PlayCardCommand(PlayerIndex.Player, card2.InstanceId, SlotType.CoreActive, 0);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.SlotOccupied, result);
        }

        [Test]
        public void MatchEngine_ExecutePlayCard_PlacesProtocolCorrectly()
        {
            var state = new MatchState();
            var definition = CreateTestCardDefinition("test_001", "Test Protocol", CardType.Protocol);
            var card = new CardInstance(definition, PlayerIndex.Player);
            state.Players[0].Hand.Add(card);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new PlayCardCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreActive, 0);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(card, state.Players[0].Board.Protocol);
            Assert.AreEqual(0, state.Players[0].Hand.Count);
        }

        [Test]
        public void MatchEngine_ExecuteDeclareAttack_DamagesDefender()
        {
            var state = new MatchState();
            var attackerDef = CreateTestCardDefinition("test_001", "Attacker", CardType.Unit);
            attackerDef.Atk = 3;
            // Base stats are 1/1, so raising only Atk leaves the attacker on 1 HP. Combat is simultaneous
            // (GDD 2.1.2), so the defender's 1 retaliation would kill it and the test would never reach
            // the Exhausted assertion it exists to make. Give it the same durability as the defender.
            attackerDef.Hp = 5;
            var defenderDef = CreateTestCardDefinition("test_002", "Defender", CardType.Unit);
            defenderDef.Hp = 5;

            var attacker = new CardInstance(attackerDef, PlayerIndex.Player);
            attacker.State = UnitState.Ready;
            var defender = new CardInstance(defenderDef, PlayerIndex.Opponent);
            defender.State = UnitState.Ready;

            state.Players[0].Board.CoreActive[0].Card = attacker;
            state.Players[1].Board.CoreActive[0].Card = defender;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            var command = new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, defender.InstanceId);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(2, defender.CurrentHp);
            Assert.AreEqual(UnitState.Exhausted, attacker.State);
        }

        [Test]
        public void MatchEngine_ExecuteDeclareAttack_DamagesCore()
        {
            var state = new MatchState();
            var attackerDef = CreateTestCardDefinition("test_001", "Attacker", CardType.Unit);
            attackerDef.Atk = 5;
            var attacker = new CardInstance(attackerDef, PlayerIndex.Player);
            attacker.State = UnitState.Ready;

            state.Players[0].Board.CoreActive[0].Card = attacker;
            state.CurrentPhase = TurnPhase.Combat;
            int initialDurability = state.Players[1].CoreDurability;

            var engine = new MatchEngine(state);
            var command = new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            // GDD 2.1.6 Step 4: an unblocked direct Core attack always costs exactly 1 durability,
            // whatever the attacker's ATK.
            Assert.AreEqual(initialDurability - 1, state.Players[1].CoreDurability);
        }

        [Test]
        public void MatchEngine_ExecuteDeclareAttack_FailsWhenUnitCannotAct()
        {
            var state = new MatchState();
            var attackerDef = CreateTestCardDefinition("test_001", "Attacker", CardType.Unit);
            attackerDef.Keywords = new[] { Keyword.SummoningDelay };
            var attacker = new CardInstance(attackerDef, PlayerIndex.Player);

            state.Players[0].Board.CoreActive[0].Card = attacker;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            var command = new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.UnitCannotAct, result);
        }

        [Test]
        public void MatchEngine_ExecuteSwitchPosition_MovesCard()
        {
            var state = new MatchState();
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            var card = new CardInstance(definition, PlayerIndex.Player);

            state.Players[0].Board.CoreActive[0].Card = card;
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new SwitchPositionCommand(PlayerIndex.Player, card.InstanceId, SlotType.CoreBench, 0);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.IsFalse(state.Players[0].Board.CoreActive[0].IsOccupied());
            Assert.IsTrue(state.Players[0].Board.CoreBench[0].IsOccupied());
            Assert.AreEqual(card, state.Players[0].Board.CoreBench[0].Card);
        }

        [Test]
        public void MatchEngine_ExecutePassPhase_AdvancesPhase()
        {
            var state = new MatchState();
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new PassPhaseCommand(PlayerIndex.Player);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(TurnPhase.Combat, state.CurrentPhase);
        }

        [Test]
        public void MatchEngine_ExecuteEndTurn_SwitchesPlayer()
        {
            var state = new MatchState();
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            var command = new EndTurnCommand(PlayerIndex.Player);
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(PlayerIndex.Opponent, state.ActivePlayer);
        }

        [Test]
        public void MatchEngine_ExecuteMulligan_ReshufflesAndRedraws()
        {
            var state = new MatchState();
            var playerDeck = CreateTestDeck(30);
            var opponentDeck = CreateTestDeck(30);
            state.InitializeMatch(playerDeck, opponentDeck);

            var cardToMulligan = state.Players[0].Hand[0];
            int mulliganId = cardToMulligan.InstanceId;

            var engine = new MatchEngine(state);
            var command = new MulliganCommand(PlayerIndex.Player, new[] { mulliganId });
            var result = engine.ExecuteCommand(command);

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(MatchState.StartingHandSize, state.Players[0].Hand.Count);
            Assert.IsFalse(state.Players[0].Hand.Contains(cardToMulligan));
        }

        [Test]
        public void MatchEngine_Combat_FirstStrike_DamagesFirst()
        {
            var state = new MatchState();
            var attackerDef = CreateTestCardDefinition("test_001", "Attacker", CardType.Unit);
            attackerDef.Atk = 5;
            attackerDef.Hp = 2;
            attackerDef.Keywords = new[] { Keyword.FirstStrike };

            var defenderDef = CreateTestCardDefinition("test_002", "Defender", CardType.Unit);
            defenderDef.Atk = 3;
            defenderDef.Hp = 3;

            var attacker = new CardInstance(attackerDef, PlayerIndex.Player);
            attacker.State = UnitState.Ready;
            var defender = new CardInstance(defenderDef, PlayerIndex.Opponent);
            defender.State = UnitState.Ready;

            state.Players[0].Board.CoreActive[0].Card = attacker;
            state.Players[1].Board.CoreActive[0].Card = defender;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            var command = new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, defender.InstanceId);
            engine.ExecuteCommand(command);

            Assert.IsFalse(defender.IsAlive());
            Assert.AreEqual(2, attacker.CurrentHp);
        }

        [Test]
        public void MatchEngine_Combat_Piercing_IgnoresShield()
        {
            var state = new MatchState();
            var attackerDef = CreateTestCardDefinition("test_001", "Attacker", CardType.Unit);
            attackerDef.Atk = 3;
            attackerDef.Keywords = new[] { Keyword.Piercing };

            var defenderDef = CreateTestCardDefinition("test_002", "Defender", CardType.Unit);
            defenderDef.Hp = 5;
            defenderDef.Shield = 2;

            var attacker = new CardInstance(attackerDef, PlayerIndex.Player);
            attacker.State = UnitState.Ready;
            var defender = new CardInstance(defenderDef, PlayerIndex.Opponent);
            defender.State = UnitState.Ready;

            state.Players[0].Board.CoreActive[0].Card = attacker;
            state.Players[1].Board.CoreActive[0].Card = defender;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            var command = new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, defender.InstanceId);
            engine.ExecuteCommand(command);

            Assert.AreEqual(2, defender.CurrentShield);
            Assert.AreEqual(2, defender.CurrentHp);
        }

        [Test]
        public void MatchEngine_ExecuteConcede_ByPlayer_OpponentWins()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);

            var result = engine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Player));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(MatchResult.OpponentWin, state.Result);
            Assert.IsTrue(state.IsMatchOver());
            Assert.AreEqual("YOU CONCEDED", state.ResultReason);
        }

        [Test]
        public void MatchEngine_ExecuteConcede_ByNonActivePlayer_PlayerWins()
        {
            var state = new MatchState();
            var engine = new MatchEngine(state);

            // Player is the active player by default, so the opponent is conceding out of turn. That is
            // the point: the active-player gate has to let Concede through alongside DeclareBlock.
            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer);
            var result = engine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Opponent));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(MatchResult.PlayerWin, state.Result);
            Assert.AreEqual("OPPONENT CONCEDED", state.ResultReason);
        }

        [Test]
        public void MatchEngine_ExecuteConcede_LeavesCoreDurabilityUntouched()
        {
            // A concession is not a Core defeat. Durability must stay at its starting value so the banner
            // reason, not the counters, is what distinguishes the two loss paths.
            var state = new MatchState();
            var engine = new MatchEngine(state);

            engine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Player));

            Assert.AreEqual(PlayerState.StartingCoreDurability, state.Players[0].CoreDurability);
            Assert.AreEqual(PlayerState.StartingCoreDurability, state.Players[1].CoreDurability);
        }

        [Test]
        public void MatchEngine_ExecuteConcede_RejectedOnceMatchIsOver()
        {
            var state = new MatchState();
            state.Concede(PlayerIndex.Player);
            var engine = new MatchEngine(state);

            var result = engine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Opponent));

            Assert.AreEqual(CommandResult.InvalidPhase, result);
            Assert.AreEqual(MatchResult.OpponentWin, state.Result);
        }

        [Test]
        public void MatchState_Concede_AfterResultIsSet_DoesNotOverwriteResult()
        {
            var state = new MatchState();
            state.Concede(PlayerIndex.Player);
            string firstReason = state.ResultReason;

            state.Concede(PlayerIndex.Opponent);

            Assert.AreEqual(MatchResult.OpponentWin, state.Result);
            Assert.AreEqual(firstReason, state.ResultReason);
        }

        [Test]
        public void MatchState_CheckVictoryConditions_AfterConcession_DoesNotReviveMatch()
        {
            // Once conceded, a later Core-destruction sweep must not replace the concession result — the
            // match is already over and the banner reason has to stay stable.
            var state = new MatchState();
            state.Concede(PlayerIndex.Player);

            state.Players[1].CoreDurability = 0;
            state.CheckVictoryConditions();

            Assert.AreEqual(MatchResult.OpponentWin, state.Result);
            Assert.AreEqual("YOU CONCEDED", state.ResultReason);
        }

        private CardDefinition CreateTestCardDefinition(string id, string name, CardType type = CardType.Unit)
        {
            var def = new CardDefinition
            {
                Id = id,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = type,
                Cost = 1,
                Atk = 1,
                Hp = 1,
                Shield = 0
            };
            return def;
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
