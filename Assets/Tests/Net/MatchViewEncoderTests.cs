using System;
using System.Text;
using System.Linq;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Net.Tests
{
    public sealed class MatchViewEncoderTests
    {
        static CardInstance Card(string id, PlayerIndex seat) =>
            new CardInstance(new CardDefinition { Id = id, Type = CardType.Unit, Hp = 5 }, seat);

        [Test]
        public void EncodesOnlyGuestPrivateAndPublicCards()
        {
            var state = new MatchState(1);
            state.Players[0].Hand.Add(Card("secret_hand", PlayerIndex.Player));
            state.Players[0].Deck.Add(Card("secret_deck", PlayerIndex.Player));
            state.Players[0].Discard.Add(Card("public_discard", PlayerIndex.Player));
            state.Players[0].LineupOffer.Add(Card("secret_offer", PlayerIndex.Player));
            state.Players[1].Hand.Add(Card("guest_hand", PlayerIndex.Opponent));
            state.Players[0].Board.CoreActive[0].Card = Card("public_unit", PlayerIndex.Player);
            var view = new MatchViewSnapshot(state, PlayerIndex.Opponent, 12);
            byte[] data = MatchViewEncoder.EncodeGuest(view);
            string text = Encoding.UTF8.GetString(data);
            StringAssert.DoesNotContain("secret_hand", text);
            StringAssert.DoesNotContain("secret_deck", text);
            StringAssert.DoesNotContain("secret_offer", text);
            StringAssert.Contains("guest_hand", text);
            StringAssert.Contains("public_unit", text);
            StringAssert.Contains("public_discard", text);
            Assert.LessOrEqual(data.Length, MatchViewEncoder.MaxPayloadBytes);
            CollectionAssert.AreEqual(data, MatchViewEncoder.EncodeGuest(view));
        }

        [Test]
        public void RejectsHostViewAndOversizedStrings()
        {
            var state = new MatchState(1);
            Assert.Throws<ArgumentException>(() => MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(state, PlayerIndex.Player, 0)));
            state.Players[1].Hand.Add(Card(new string('x', 129), PlayerIndex.Opponent));
            Assert.Throws<ArgumentException>(() => MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(state, PlayerIndex.Opponent, 0)));
        }

        [Test]
        public void DecodeRoundTripAndRevisionOrdering()
        {
            var state = new MatchState(1);
            state.Players[0].Hand.Add(Card("host_hidden", PlayerIndex.Player));
            state.Players[0].Discard.Add(Card("host_discard", PlayerIndex.Player));
            state.Players[1].Discard.Add(Card("guest_discard", PlayerIndex.Opponent));
            state.Players[1].Hand.Add(Card("guest_visible", PlayerIndex.Opponent));
            state.Players[0].Board.NodeBench[2][1].Card = Card("board_visible", PlayerIndex.Player);
            byte[] data = MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(state, PlayerIndex.Opponent, 4));
            Assert.IsTrue(MatchViewDecoder.TryDecode(data, out var view));
            Assert.AreEqual(4, view.Revision);
            Assert.IsEmpty(view.Players[0].Hand);
            Assert.AreEqual("guest_visible", view.Players[1].Hand[0].DefinitionId);
            Assert.AreEqual("board_visible", view.Players[0].Board[0].Card.DefinitionId);
            Assert.AreEqual("host_discard", view.Players[0].Discard[0].DefinitionId);
            Assert.AreEqual("guest_discard", view.Players[1].Discard[0].DefinitionId);
            CollectionAssert.AreEqual(data, MatchViewEncoder.EncodeGuest(view));
            var receiver = new MatchViewReceiver(PlayerIndex.Opponent);
            Assert.IsTrue(receiver.TryApply(view));
            Assert.IsFalse(receiver.TryApply(view));
            byte[] newer = MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(state, PlayerIndex.Opponent, 7));
            Assert.IsTrue(MatchViewDecoder.TryDecode(newer, out var next));
            Assert.IsTrue(receiver.TryApply(next));
            Assert.IsFalse(receiver.TryApply(view));
        }

        [Test]
        public void DecoderRejectsTruncationInvalidHeadersAndExtraBytes()
        {
            byte[] valid = MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(new MatchState(1), PlayerIndex.Opponent, 0));
            for (int i = 0; i < valid.Length; i++)
                Assert.IsFalse(MatchViewDecoder.TryDecode(valid.Take(i).ToArray(), out _), "length " + i);
            Assert.IsFalse(MatchViewDecoder.TryDecode(valid.Concat(new byte[] { 0 }).ToArray(), out _));
            Assert.IsFalse(MatchViewDecoder.TryDecode(new byte[MatchViewDecoder.MaxPayloadBytes + 1], out _));
            foreach (int offset in new[] { 0, 9, 10, 11, 17 })
            {
                byte[] bad = (byte[])valid.Clone(); bad[offset] = 255;
                Assert.IsFalse(MatchViewDecoder.TryDecode(bad, out _), "offset " + offset);
            }
            byte[] negativeRevision = (byte[])valid.Clone(); negativeRevision[8] = 128;
            Assert.IsFalse(MatchViewDecoder.TryDecode(negativeRevision, out _));
        }
    }
}
