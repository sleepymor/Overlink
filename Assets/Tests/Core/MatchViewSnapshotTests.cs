using System;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Tests.Core
{
    public sealed class MatchViewSnapshotTests
    {
        static CardInstance Card(string id, PlayerIndex owner) =>
            new CardInstance(new CardDefinition { Id = id, Type = CardType.Unit, Hp = 5 }, owner);

        [Test]
        public void GuestViewRedactsOpponentPrivateZonesAndCopiesPublicState()
        {
            var state = new MatchState(123);
            var host = state.Players[0];
            var guest = state.Players[1];
            host.Hand.Add(Card("secret-hand", PlayerIndex.Player));
            host.Deck.Add(Card("secret-deck", PlayerIndex.Player));
            host.Discard.Add(Card("public-discard", PlayerIndex.Player));
            host.LineupOffer.Add(Card("secret-offer", PlayerIndex.Player));
            guest.Hand.Add(Card("guest-hand", PlayerIndex.Opponent));
            guest.LineupOffer.Add(Card("guest-offer", PlayerIndex.Opponent));
            var unit = Card("public-unit", PlayerIndex.Player);
            host.Board.NodeBench[2][1].Card = unit;
            var view = new MatchViewSnapshot(state, PlayerIndex.Opponent, 7);

            Assert.AreEqual(7, view.Revision);
            Assert.AreEqual(1, view.Players[0].HandCount);
            Assert.AreEqual(1, view.Players[0].DeckCount);
            Assert.AreEqual(1, view.Players[0].OfferCount);
            Assert.IsEmpty(view.Players[0].Hand);
            Assert.IsEmpty(view.Players[0].LineupOffer);
            Assert.AreEqual("guest-hand", view.Players[1].Hand[0].DefinitionId);
            Assert.AreEqual("guest-offer", view.Players[1].LineupOffer[0].DefinitionId);
            Assert.AreEqual("public-unit", view.Players[0].Board[0].Card.DefinitionId);
            Assert.AreEqual("public-discard", view.Players[0].Discard[0].DefinitionId);
            Assert.AreEqual("public-discard", new MatchViewSnapshot(state, PlayerIndex.Player, 7).Players[0].Discard[0].DefinitionId);
            Assert.AreEqual(SlotType.NodeBench, view.Players[0].Board[0].Slot);
            Assert.AreEqual(2, view.Players[0].Board[0].NodeIndex);
            Assert.AreEqual(1, view.Players[0].Board[0].Index);
            unit.CurrentHp = 1;
            guest.Hand.Clear();
            Assert.AreEqual(5, view.Players[0].Board[0].Card.Health);
            Assert.AreEqual(1, view.Players[1].Hand.Count);
        }

        [Test]
        public void HostViewReversesVisibilityAndRejectsInvalidArguments()
        {
            var state = new MatchState(1);
            state.Players[0].Hand.Add(Card("host", PlayerIndex.Player));
            state.Players[1].Hand.Add(Card("guest", PlayerIndex.Opponent));
            var view = new MatchViewSnapshot(state, PlayerIndex.Player, 0);
            Assert.AreEqual("host", view.Players[0].Hand[0].DefinitionId);
            Assert.IsEmpty(view.Players[1].Hand);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchViewSnapshot(state, (PlayerIndex)2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchViewSnapshot(state, PlayerIndex.Player, -1));
        }

        [Test]
        public void ReceiverOnlyAppliesNewerViewsForItsSeat()
        {
            var state = new MatchState(1);
            var receiver = new MatchViewReceiver(PlayerIndex.Opponent);
            var first = new MatchViewSnapshot(state, PlayerIndex.Opponent, 0);
            var newer = new MatchViewSnapshot(state, PlayerIndex.Opponent, 3);
            Assert.IsFalse(receiver.TryApply(null));
            Assert.IsFalse(receiver.TryApply(new MatchViewSnapshot(state, PlayerIndex.Player, 4)));
            Assert.IsTrue(receiver.TryApply(first));
            Assert.IsFalse(receiver.TryApply(first));
            Assert.IsTrue(receiver.TryApply(newer));
            Assert.IsFalse(receiver.TryApply(first));
            Assert.AreSame(newer, receiver.Current);
        }

        [Test]
        public void GuestViewDoesNotRevealCoinTossDuringLineupSetup()
        {
            var state = new MatchState(5);
            state.InitializeSetup(new System.Collections.Generic.List<CardDefinition> { 
                new CardDefinition { Id = "a", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "b", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "c", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "d", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "e", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "f", Type = CardType.Unit, Cost = 1, Hp = 5 }
            }, new System.Collections.Generic.List<CardDefinition> {
                new CardDefinition { Id = "g", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "h", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "i", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "j", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "k", Type = CardType.Unit, Cost = 1, Hp = 5 },
                new CardDefinition { Id = "l", Type = CardType.Unit, Cost = 1, Hp = 5 }
            });
            state.FlipCoin();

            var view = new MatchViewSnapshot(state, PlayerIndex.Opponent, 1);
            Assert.IsTrue(view.SetupInProgress);
            Assert.IsFalse(view.CoinFlipped);
            Assert.AreEqual(PlayerIndex.Player, view.FirstPlayer);
            Assert.AreEqual(PlayerIndex.Player, view.ActivePlayer);
        }

        [Test]
        public void PresentationStateKeepsPublicBoardAndOwnHandButNotOpponentHand()
        {
            var state = new MatchState(11);
            CardInstance publicCard = Card("public", PlayerIndex.Player);
            publicCard.CurrentHp = 2;
            state.Players[0].Board.CoreActive[0].Card = publicCard;
            state.Players[0].Hand.Add(Card("host-hidden", PlayerIndex.Player));
            state.Players[0].Deck.Add(Card("host-deck-hidden", PlayerIndex.Player));
            state.Players[1].Hand.Add(Card("guest-own", PlayerIndex.Opponent));

            MatchViewSnapshot view = new MatchViewSnapshot(state, PlayerIndex.Opponent, 9);
            MatchState projected = MatchState.FromPresentationView(view,
                id => id == "public" || id == "guest-own"
                    ? new CardDefinition { Id = id, Name = id, Type = CardType.Unit, Hp = 5 }
                    : null);

            Assert.AreEqual(2, projected.Players[1].Board.CoreActive[0].Card.CurrentHp);
            Assert.AreEqual(1, projected.Players[0].Hand.Count);
            Assert.AreEqual("guest-own", projected.Players[0].Hand[0].DefinitionId);
            Assert.IsEmpty(projected.Players[1].Hand);
            Assert.AreEqual(1, projected.Players[1].Deck.Count);
        }

        [TestCase(PlayerIndex.Player, MatchResult.OpponentWin)]
        [TestCase(PlayerIndex.Opponent, MatchResult.PlayerWin)]
        public void GuestPresentationReportsResultFromGuestPerspective(PlayerIndex conceding, MatchResult expected)
        {
            var state = new MatchState(11);
            state.Concede(conceding);
            var view = new MatchViewSnapshot(state, PlayerIndex.Opponent, 1);
            var projected = MatchState.FromPresentationView(view, _ => null);
            Assert.AreEqual(expected, projected.Result);
        }
    }
}
