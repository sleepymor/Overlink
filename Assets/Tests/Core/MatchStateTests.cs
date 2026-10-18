using NUnit.Framework;
using System.Collections.Generic;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public sealed class MatchStateTests
    {
        [Test]
        public void MatchState_Initialization_SetsCorrectDefaults()
        {
            var state = new MatchState();

            Assert.AreEqual(PlayerIndex.Player, state.ActivePlayer);
            Assert.AreEqual(TurnPhase.Upkeep, state.CurrentPhase);
            Assert.AreEqual(1, state.TurnNumber);
            Assert.AreEqual(MatchResult.None, state.Result);
            Assert.IsFalse(state.IsMatchOver());
            Assert.IsNotNull(state.Players[0]);
            Assert.IsNotNull(state.Players[1]);
        }

        [Test]
        public void PlayerState_Initialization_SetsStartingValues()
        {
            var player = new PlayerState(PlayerIndex.Player);

            Assert.AreEqual(PlayerState.StartingNP, player.CurrentNP);
            Assert.AreEqual(PlayerState.StartingCoreDurability, player.CoreDurability);
            Assert.IsNotNull(player.Hand);
            Assert.IsNotNull(player.Deck);
            Assert.IsNotNull(player.Discard);
            Assert.IsNotNull(player.Board);
        }

        [Test]
        public void PlayerState_SpendNP_DeductsCorrectly()
        {
            var player = new PlayerState(PlayerIndex.Player);
            int initial = player.CurrentNP;

            bool success = player.SpendNP(2);

            Assert.IsTrue(success);
            Assert.AreEqual(initial - 2, player.CurrentNP);
        }

        [Test]
        public void PlayerState_SpendNP_FailsWhenInsufficient()
        {
            var player = new PlayerState(PlayerIndex.Player);

            bool success = player.SpendNP(100);

            Assert.IsFalse(success);
        }

        [Test]
        public void PlayerState_GainNP_AddsUpToTheCap()
        {
            var player = new PlayerState(PlayerIndex.Player);
            int initial = player.CurrentNP;

            player.GainNP(2, PlayerState.NpCap(0));

            Assert.AreEqual(initial + 2, player.CurrentNP);
        }

        [Test]
        public void PlayerState_GainNP_CapsAtTenPlusNodes()
        {
            var player = new PlayerState(PlayerIndex.Player);
            player.CurrentNP = 9;

            player.GainNP(5, PlayerState.NpCap(0));

            Assert.AreEqual(PlayerState.MaxNP, player.CurrentNP);
        }

        [Test]
        public void PlayerState_NpCap_ScalesWithControlledNodes()
        {
            Assert.AreEqual(PlayerState.MaxNP, PlayerState.NpCap(0));
            Assert.AreEqual(PlayerState.MaxNP + 3, PlayerState.NpCap(3));
        }

        [Test]
        public void PlayerState_TakeDamage_ReducesCoreDurability()
        {
            var player = new PlayerState(PlayerIndex.Player);
            int initial = player.CoreDurability;

            player.TakeDamage(2);

            Assert.AreEqual(initial - 2, player.CoreDurability);
        }

        [Test]
        public void PlayerState_IsDefeated_ReturnsTrueWhenCoreZero()
        {
            var player = new PlayerState(PlayerIndex.Player);

            player.TakeDamage(PlayerState.StartingCoreDurability);

            Assert.IsTrue(player.IsDefeated());
        }

        [Test]
        public void PlayerState_DrawCard_MovesCardFromDeckToHand()
        {
            var player = new PlayerState(PlayerIndex.Player);
            var definition = CreateTestCardDefinition("test_001", "Test Card");
            var card = new CardInstance(definition, PlayerIndex.Player);
            player.Deck.Add(card);

            CardInstance drawn = player.DrawCard();

            Assert.AreEqual(card, drawn);
            Assert.AreEqual(0, player.Deck.Count);
            Assert.AreEqual(1, player.Hand.Count);
            Assert.Contains(card, player.Hand);
        }

        [Test]
        public void PlayerState_DrawCard_ReshufflesWhenDeckEmpty()
        {
            var player = new PlayerState(PlayerIndex.Player);
            var definition = CreateTestCardDefinition("test_001", "Test Card");
            var card = new CardInstance(definition, PlayerIndex.Player);
            player.Discard.Add(card);

            CardInstance drawn = player.DrawCard();

            Assert.AreEqual(card, drawn);
            Assert.AreEqual(0, player.Discard.Count);
            Assert.AreEqual(1, player.Hand.Count);
        }

        [Test]
        public void MatchState_InitializeMatch_LoadsDecksAndDrawsHands()
        {
            var state = new MatchState();
            var playerDeck = CreateTestDeck(30);
            var opponentDeck = CreateTestDeck(30);

            state.InitializeMatch(playerDeck, opponentDeck);

            Assert.AreEqual(MatchState.StartingHandSize, state.Players[0].Hand.Count);
            Assert.AreEqual(MatchState.StartingHandSize, state.Players[1].Hand.Count);
            Assert.AreEqual(30 - MatchState.StartingHandSize, state.Players[0].Deck.Count);
            Assert.AreEqual(30 - MatchState.StartingHandSize, state.Players[1].Deck.Count);
        }

        [Test]
        public void MatchState_CheckVictoryConditions_DetectsPlayerWin()
        {
            var state = new MatchState();
            state.Players[1].TakeDamage(PlayerState.StartingCoreDurability);

            state.CheckVictoryConditions();

            Assert.AreEqual(MatchResult.PlayerWin, state.Result);
            Assert.IsTrue(state.IsMatchOver());
        }

        [Test]
        public void MatchState_CheckVictoryConditions_DetectsOpponentWin()
        {
            var state = new MatchState();
            state.Players[0].TakeDamage(PlayerState.StartingCoreDurability);

            state.CheckVictoryConditions();

            Assert.AreEqual(MatchResult.OpponentWin, state.Result);
            Assert.IsTrue(state.IsMatchOver());
        }

        [Test]
        public void MatchState_CheckVictoryConditions_SimultaneousCores_ActivePlayerWins()
        {
            var state = new MatchState();
            state.Players[0].TakeDamage(PlayerState.StartingCoreDurability);
            state.Players[1].TakeDamage(PlayerState.StartingCoreDurability);

            state.CheckVictoryConditions();

            // GDD 2.1.8: if both Cores are destroyed in the same batch the active player wins.
            Assert.AreEqual(MatchResult.PlayerWin, state.Result);
        }

        [Test]
        public void Board_IsNodeControlled_ReturnsTrueWhenActiveSlotOccupied()
        {
            var board = new Board();
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            var card = new CardInstance(definition, PlayerIndex.Player);
            board.NodeActive[0][0].Card = card;

            bool controlled = board.IsNodeControlled(0, PlayerIndex.Player);

            Assert.IsTrue(controlled);
        }

        [Test]
        public void Board_IsNodeControlled_ReturnsFalseWhenEmpty()
        {
            var board = new Board();

            bool controlled = board.IsNodeControlled(0, PlayerIndex.Player);

            Assert.IsFalse(controlled);
        }

        [Test]
        public void Board_CountControlledNodes_ReturnsCorrectCount()
        {
            var board = new Board();
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            var card1 = new CardInstance(definition, PlayerIndex.Player);
            var card2 = new CardInstance(definition, PlayerIndex.Player);

            board.NodeActive[0][0].Card = card1;
            board.NodeActive[2][0].Card = card2;

            int count = board.CountControlledNodes(PlayerIndex.Player);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void CardInstance_TakeDamage_ReducesHPAfterShield()
        {
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            definition.Hp = 5;
            definition.Shield = 2;
            var card = new CardInstance(definition, PlayerIndex.Player);

            card.TakeDamage(4);

            Assert.AreEqual(0, card.CurrentShield);
            Assert.AreEqual(3, card.CurrentHp);
            Assert.IsTrue(card.IsAlive());
        }

        [Test]
        public void CardInstance_TakeDamage_DestroysWhenHPReachesZero()
        {
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            definition.Hp = 3;
            var card = new CardInstance(definition, PlayerIndex.Player);

            card.TakeDamage(5);

            Assert.AreEqual(0, card.CurrentHp);
            Assert.AreEqual(UnitState.Destroyed, card.State);
            Assert.IsFalse(card.IsAlive());
        }

        [Test]
        public void CardInstance_Heal_RestoresHP()
        {
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            definition.Hp = 5;
            var card = new CardInstance(definition, PlayerIndex.Player);
            card.TakeDamage(3);

            card.Heal(2);

            Assert.AreEqual(4, card.CurrentHp);
        }

        [Test]
        public void CardInstance_Heal_CapsAtMaxHP()
        {
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            definition.Hp = 5;
            var card = new CardInstance(definition, PlayerIndex.Player);
            card.TakeDamage(2);

            card.Heal(10);

            Assert.AreEqual(5, card.CurrentHp);
        }

        [Test]
        public void CardInstance_CanAct_ReturnsFalseWithSummoningDelay()
        {
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            definition.Keywords = new[] { Keyword.SummoningDelay };
            var card = new CardInstance(definition, PlayerIndex.Player);

            Assert.IsFalse(card.CanAct());
        }

        [Test]
        public void CardInstance_SetReady_RemovesSummoningDelay()
        {
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            definition.Keywords = new[] { Keyword.SummoningDelay };
            var card = new CardInstance(definition, PlayerIndex.Player);

            card.SetReady();

            Assert.IsFalse(card.HasSummoningDelay);
            Assert.AreEqual(UnitState.Ready, card.State);
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
