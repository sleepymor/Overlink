using System;
using System.Linq;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Net.Tests
{
    public sealed class MatchCommandCodecTests
    {
        [Test]
        public void RoundTripsEveryCommandUsingReceiverAssignedSeat()
        {
            var p = PlayerIndex.Player;
            GameCommand[] commands = {
                new DrawCardCommand(p), new PassPhaseCommand(p), new EndTurnCommand(p),
                new ConcedeCommand(p), new RerollLineupCommand(p),
                new PlayCardCommand(p, 12, SlotType.NodeActive, 0, 2),
                new SwitchPositionCommand(p, 13, SlotType.CoreBench, 1, -1),
                new DeclareAttackCommand(p, 14, -1), new DeclareBlockCommand(p, 15, 16),
                new ActivateAbilityCommand(p, 17), new RetreatCommand(p, 18, 19),
                new MulliganCommand(p, new[] { 1, 2, 3 }),
                new ConfirmLineupCommand(p, new[] { 1, 2, 0, 4, 5 })
            };
            foreach (GameCommand original in commands)
            {
                byte[] bytes = MatchCommandCodec.Encode(42, original);
                Assert.IsTrue(MatchCommandCodec.TryDecode(bytes, PlayerIndex.Opponent, out long revision, out GameCommand decoded), original.Type.ToString());
                Assert.AreEqual(42, revision);
                Assert.AreEqual(PlayerIndex.Opponent, decoded.Player);
                Assert.AreEqual(original.Type, decoded.Type);
                CollectionAssert.AreEqual(bytes, MatchCommandCodec.Encode(revision, decoded));
            }
        }

        [Test]
        public void RejectsMalformedTruncatedAndOversizedPayloads()
        {
            byte[] valid = MatchCommandCodec.Encode(0, new PlayCardCommand(PlayerIndex.Player, 1, SlotType.CoreActive, 0));
            for (int i = 0; i < valid.Length; i++)
                Assert.IsFalse(MatchCommandCodec.TryDecode(valid.Take(i).ToArray(), PlayerIndex.Opponent, out _, out _), "length " + i);
            Assert.IsFalse(MatchCommandCodec.TryDecode(valid.Concat(new byte[] { 0 }).ToArray(), PlayerIndex.Opponent, out _, out _));
            Assert.IsFalse(MatchCommandCodec.TryDecode(new byte[MatchCommandCodec.MaxPayloadBytes + 1], PlayerIndex.Opponent, out _, out _));
            Assert.IsFalse(MatchCommandCodec.TryDecode(valid, (PlayerIndex)99, out _, out _));
            byte[] badVersion = (byte[])valid.Clone(); badVersion[0] = 99;
            byte[] badType = (byte[])valid.Clone(); badType[9] = 255;
            byte[] badSlot = (byte[])valid.Clone(); badSlot[14] = 255;
            byte[] negativeRevision = (byte[])valid.Clone(); negativeRevision[8] = 128;
            foreach (byte[] bad in new[] { badVersion, badType, badSlot, negativeRevision })
                Assert.IsFalse(MatchCommandCodec.TryDecode(bad, PlayerIndex.Opponent, out _, out _));
            Assert.Throws<ArgumentException>(() => MatchCommandCodec.Encode(0, new MulliganCommand(PlayerIndex.Player, new int[6])));
        }

        [Test]
        public void DecodedCommandStillRequiresAuthenticatedPeerAndCurrentRevision()
        {
            var state = new MatchState(1);
            var boundary = new AuthoritativeCommandBoundary(new MatchEngine(state), 0, 8);
            byte[] bytes = MatchCommandCodec.Encode(0, new ConcedeCommand(PlayerIndex.Player));
            Assert.IsTrue(MatchCommandCodec.TryDecode(bytes, PlayerIndex.Opponent, out long revision, out GameCommand command));
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(9, revision, command));
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(8, revision + 1, command));
            Assert.AreEqual(CommandResult.Success, boundary.Execute(8, revision, command));
            Assert.AreEqual(MatchResult.PlayerWin, state.Result);
        }
    }
}
