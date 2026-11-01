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
            Assert.AreEqual(20, player.CoreDurability);
            player.TakeDamage(5);
            player.RepairCore(100);
            Assert.AreEqual(20, player.CoreDurability, "Repair must not exceed the new Core maximum.");
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
        public void Board_HoldsNode_ReturnsTrueWhenActiveSlotOccupied()
        {
            var board = new Board();
            var definition = CreateTestCardDefinition("test_001", "Test Unit", CardType.Unit);
            var card = new CardInstance(definition, PlayerIndex.Player);
            board.NodeActive[0][0].Card = card;

            bool held = board.HoldsNode(0, PlayerIndex.Player);

            Assert.IsTrue(held);
        }

        [Test]
        public void Board_HoldsNode_ReturnsFalseWhenEmpty()
        {
            var board = new Board();

            bool held = board.HoldsNode(0, PlayerIndex.Player);

            Assert.IsFalse(held);
        }

        [Test]
        public void ControlsNode_BothSidesHoldIt_NeitherSideControlsIt()
        {
            // GDD 2.1.7 read as uncontested control, and the case the opening lineup creates by construction:
            // both sides field a Node Active for all four Nodes, so at 4-4 the Nodes pay nothing to anybody.
            // Under the looser reading every Node counted for both players, which put three of four within reach
            // the instant setup closed.
            var state = new MatchState();
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            FieldNode(state, PlayerIndex.Player, 0);
            FieldNode(state, PlayerIndex.Opponent, 0);

            Assert.IsFalse(state.ControlsNode(PlayerIndex.Player, 0),
                "holding a Node the opponent also holds is not controlling it");
            Assert.IsFalse(state.ControlsNode(PlayerIndex.Opponent, 0),
                "and it is not controlling it for them either");
            Assert.AreEqual(0, state.CountControlledNodes(PlayerIndex.Player));
            Assert.AreEqual(0, state.CountControlledNodes(PlayerIndex.Opponent));
        }

        [Test]
        public void ControlsNode_EmptySocketIsHeldByNobody()
        {
            var state = new MatchState();
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            Assert.IsFalse(state.ControlsNode(PlayerIndex.Player, 2));
            Assert.IsFalse(state.ControlsNode(PlayerIndex.Opponent, 2));
        }

        [Test]
        public void ControlsNode_TheSurvivorOfATradeTakesTheNode()
        {
            // The whole economy of a Node, in one test: fielding a body is the prerequisite, and the bonus arrives
            // when the opponent's body stops existing. If this read the other way the opening lineup would have
            // made every Node free for whoever happened to fill it first.
            var state = new MatchState();
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            FieldNode(state, PlayerIndex.Player, 1);
            FieldNode(state, PlayerIndex.Opponent, 1);
            Assert.IsFalse(state.ControlsNode(PlayerIndex.Player, 1), "contested while both hold it");

            state.GetPlayer(PlayerIndex.Opponent).Board.NodeActive[1][0].Card = null;

            Assert.IsTrue(state.ControlsNode(PlayerIndex.Player, 1),
                "and the side that kept its body on the Node holds it once the other does not");
            Assert.AreEqual(1, state.CountControlledNodes(PlayerIndex.Player));
            Assert.AreEqual(0, state.CountControlledNodes(PlayerIndex.Opponent));
        }

        [Test]
        public void ControlsNode_OneKillDoesNotOpenANodeTheBenchCanStillPromoteInto()
        {
            // The consequence of the contest rule meeting the promotion rule, and the one a player is most likely
            // to guess wrong: a destroyed Active is refilled from that same Node's Bench inside
            // ResolveDestructionAndPromotion, so by the time anyone asks who controls it, the socket is occupied
            // again. Clearing an opponent's Node therefore means emptying their Bench for that Node as well -
            // which makes the two sockets an ordering question rather than interchangeable targets.
            var state = new MatchState();
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            FieldNode(state, PlayerIndex.Player, 0);
            CardInstance theirs = FieldNode(state, PlayerIndex.Opponent, 0);
            var reserve = new CardInstance(CreateTestCardDefinition("reserve", "Node Reserve", CardType.Unit),
                PlayerIndex.Opponent) { State = UnitState.Ready };
            state.GetPlayer(PlayerIndex.Opponent).Board.NodeBench[0][0].Card = reserve;

            var attacker = new CardInstance(CreateTestCardDefinition("attacker", "Attacker", CardType.Unit),
                PlayerIndex.Player) { State = UnitState.Ready };
            state.GetPlayer(PlayerIndex.Player).Board.CoreActive[0].Card = attacker;

            state.CurrentPhase = TurnPhase.Combat;
            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, theirs.InstanceId)));

            Assert.IsFalse(theirs.IsAlive(), "the holder is dead");
            Assert.AreSame(reserve, state.GetPlayer(PlayerIndex.Opponent).Board.NodeActive[0][0].Card,
                "and its Bench replacement is standing on the Node");
            Assert.IsFalse(state.ControlsNode(PlayerIndex.Player, 0),
                "so the Node is still contested and the kill bought nothing");
            Assert.IsFalse(state.ControlsNode(PlayerIndex.Opponent, 0), "for either of us");
        }

        [Test]
        public void CoreMultipliers_DoNotResolveWhileTheNodeIsContested()
        {
            // Node 1 grants +1 ATK and Node 2 grants +1 HP to the Core Active, and both are folded into the
            // stats the player reads on the card face - so a contested Node has to leave those numbers alone, and
            // clearing the opponent's holder has to move them. This is the bookkeeping RefreshNodeMultipliers
            // exists for, tested from both ends of the trade rather than only the buffed one.
            var state = new MatchState();
            state.InitializeMatch(CreateTestDeck(30), CreateTestDeck(30));

            var definition = CreateTestCardDefinition("core_probe", "Core Body", CardType.Unit);
            definition.Atk = 2;
            definition.Hp = 2;
            var core = new CardInstance(definition, PlayerIndex.Player);
            state.GetPlayer(PlayerIndex.Player).Board.CoreActive[0].Card = core;

            for (int node = 0; node < 2; node++)
            {
                FieldNode(state, PlayerIndex.Player, node);
                FieldNode(state, PlayerIndex.Opponent, node);
            }

            state.RefreshNodeMultipliers();
            Assert.AreEqual(2, core.CurrentAtk, "a contested Node 1 grants no ATK");
            Assert.AreEqual(2, core.CurrentHp, "and a contested Node 2 grants no HP");

            // The opponent's holders are cleared one Node at a time, so the two bonuses are shown to be
            // independent rather than a single "any Node" flag.
            state.GetPlayer(PlayerIndex.Opponent).Board.NodeActive[0][0].Card = null;
            state.RefreshNodeMultipliers();
            Assert.AreEqual(3, core.CurrentAtk, "Node 1 opens and the Core gains its ATK");
            Assert.AreEqual(2, core.CurrentHp, "while Node 2 is still contested and grants nothing");

            state.GetPlayer(PlayerIndex.Opponent).Board.NodeActive[1][0].Card = null;
            state.RefreshNodeMultipliers();
            Assert.AreEqual(3, core.CurrentHp, "then the same for its HP");
            Assert.AreEqual(1, core.NodeAtkBonus);
            Assert.AreEqual(1, core.NodeHpBonus);

            // Losing a Node you were cashing has to take the bonus straight back off the card.
            state.GetPlayer(PlayerIndex.Player).Board.NodeActive[0][0].Card = null;
            state.RefreshNodeMultipliers();
            Assert.AreEqual(2, core.CurrentAtk, "vacating Node 1 gives the ATK back");
            Assert.AreEqual(0, core.NodeAtkBonus, "and leaves no phantom bonus behind");
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

        /// <summary>
        /// Puts a live body in one side's Active socket for <paramref name="nodeIndex"/> and returns it. Written
        /// by hand rather than played through: these tests are about which side a Node pays, and dragging NP costs
        /// and the summoning rules into them would obscure exactly that.
        /// </summary>
        private CardInstance FieldNode(MatchState state, PlayerIndex who, int nodeIndex)
        {
            var definition = CreateTestCardDefinition($"node_{who}_{nodeIndex}", $"Node {nodeIndex + 1} body", CardType.Unit);
            var card = new CardInstance(definition, who);
            state.GetPlayer(who).Board.NodeActive[nodeIndex][0].Card = card;
            return card;
        }
    }
}
