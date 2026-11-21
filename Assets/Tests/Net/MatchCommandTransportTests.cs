using NUnit.Framework;
using Overlink.Core;
using System.Text;

namespace Overlink.Net.Tests
{
    public sealed class MatchCommandTransportTests
    {
        [Test]
        public void ValidRequestReturnsResultAndRejectsReplay()
        {
            var state = new MatchState(1);
            var boundary = new AuthoritativeCommandBoundary(new MatchEngine(state), 0, 8);
            byte[] request = MatchCommandTransport.CreateRequest(5, 0, new ConcedeCommand(PlayerIndex.Player));
            Assert.IsTrue(MatchCommandTransport.TryExecute(request, 8, boundary, out byte[] response, out GameCommand accepted));
            Assert.AreEqual(PlayerIndex.Opponent, accepted.Player);
            Assert.IsTrue(MatchCommandTransport.TryReadResponse(response, out long id, out long revision, out CommandResult result));
            Assert.AreEqual(5, id);
            Assert.AreEqual(1, revision);
            Assert.AreEqual(CommandResult.Success, result);
            Assert.IsTrue(MatchCommandTransport.TryExecute(request, 8, boundary, out response, out accepted));
            Assert.IsNull(accepted);
            Assert.IsTrue(MatchCommandTransport.TryReadResponse(response, out id, out revision, out result));
            Assert.AreEqual(CommandResult.InvalidCommand, result);
            Assert.AreEqual(1, revision);
        }

        [Test]
        public void InvalidSenderAndStaleRevisionDoNotMutateState()
        {
            var state = new MatchState(1);
            var boundary = new AuthoritativeCommandBoundary(new MatchEngine(state), 0, 8);
            byte[] request = MatchCommandTransport.CreateRequest(1, 0, new ConcedeCommand(PlayerIndex.Player));
            Assert.IsTrue(MatchCommandTransport.TryExecute(request, 9, boundary, out byte[] response, out GameCommand accepted));
            Assert.IsNull(accepted);
            Assert.IsTrue(MatchCommandTransport.TryReadResponse(response, out _, out long revision, out CommandResult result));
            Assert.AreEqual(CommandResult.InvalidCommand, result);
            Assert.AreEqual(0, revision);
            request = MatchCommandTransport.CreateRequest(2, 1, new ConcedeCommand(PlayerIndex.Player));
            Assert.IsTrue(MatchCommandTransport.TryExecute(request, 8, boundary, out response, out accepted));
            Assert.IsNull(accepted);
            Assert.IsFalse(state.IsMatchOver());
        }

        [Test]
        public void MalformedEnvelopesNeverExecute()
        {
            var state = new MatchState(1);
            var boundary = new AuthoritativeCommandBoundary(new MatchEngine(state), 0, 8);
            byte[] request = MatchCommandTransport.CreateRequest(1, 0, new ConcedeCommand(PlayerIndex.Player));
            request[8] = 99;
            Assert.IsFalse(MatchCommandTransport.TryExecute(request, 8, boundary, out _, out _));
            Assert.IsFalse(MatchCommandTransport.TryExecute(new byte[MatchCommandTransport.MaxRequestBytes + 1], 8, boundary, out _, out _));
            Assert.IsFalse(MatchCommandTransport.TryReadResponse(new byte[MatchCommandTransport.ResponseBytes], out _, out _, out _));
            Assert.AreEqual(0, boundary.Revision);
            Assert.IsFalse(state.IsMatchOver());
        }

        [Test]
        public void BattleDeckEnvelopeRejectsAmbiguityAndInvalidCounts()
        {
            var deck = new DeckData();
            for (int i = 1; i <= 10; i++) deck.Cards[$"card_fsti_{i:000}"] = 3;
            string json = DeckData.ToJson(deck);
            Assert.IsTrue(NetBootstrap.TryReadBattleDeck(Encoding.UTF8.GetBytes(json), out DeckData parsed));
            Assert.AreEqual(10, parsed.Cards.Count);
            Assert.IsFalse(NetBootstrap.TryReadBattleDeck(Encoding.UTF8.GetBytes(json.Replace("\"version\":1,", "\"version\":1,\"version\":1,")), out _));
            Assert.IsFalse(NetBootstrap.TryReadBattleDeck(Encoding.UTF8.GetBytes(json.Replace("\"faction\":\"fsti\"", "\"faction\":\"unknown\"")), out _));
            Assert.IsFalse(NetBootstrap.TryReadBattleDeck(new byte[] { 0xff }, out _));
            Assert.IsFalse(NetBootstrap.TryReadBattleDeck(new byte[4097], out _));
            deck.Cards["card_fsti_001"] = 4;
            Assert.IsFalse(NetBootstrap.TryReadBattleDeck(Encoding.UTF8.GetBytes(DeckData.ToJson(deck)), out _));
            deck.Cards["card_fsti_001"] = 1;
            Assert.IsFalse(NetBootstrap.TryReadBattleDeck(Encoding.UTF8.GetBytes(DeckData.ToJson(deck)), out _));
        }
    }
}
