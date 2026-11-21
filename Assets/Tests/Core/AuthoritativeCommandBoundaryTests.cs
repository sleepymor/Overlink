using System;
using NUnit.Framework;

namespace Overlink.Core.Tests
{
    public sealed class AuthoritativeCommandBoundaryTests
    {
        [Test]
        public void RejectsUnknownPeerAndSpoofedSeatWithoutEndingMatch()
        {
            var state = new MatchState(1);
            var boundary = new AuthoritativeCommandBoundary(new MatchEngine(state), 10, 20);
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(99, 0, new ConcedeCommand(PlayerIndex.Player)));
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(10, 0, new ConcedeCommand(PlayerIndex.Opponent)));
            Assert.IsFalse(state.IsMatchOver());
            Assert.AreEqual(0, boundary.Revision);
        }

        [Test]
        public void RejectsStaleCommandsAndAcceptsAuthenticatedConcession()
        {
            var state = new MatchState(1);
            var boundary = new AuthoritativeCommandBoundary(new MatchEngine(state), 10, 20);
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(20, 1, new ConcedeCommand(PlayerIndex.Opponent)));
            Assert.IsFalse(state.IsMatchOver());
            Assert.AreEqual(CommandResult.Success, boundary.Execute(20, 0, new ConcedeCommand(PlayerIndex.Opponent)));
            Assert.AreEqual(1, boundary.Revision);
            Assert.AreEqual(MatchResult.PlayerWin, state.Result);
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(20, 0, new ConcedeCommand(PlayerIndex.Opponent)));
        }

        [Test]
        public void IllegalActionDoesNotAdvanceRevisionAndSeatsMustBeDistinct()
        {
            var engine = new MatchEngine(new MatchState(1));
            Assert.Throws<ArgumentException>(() => new AuthoritativeCommandBoundary(engine, 10, 10));
            var boundary = new AuthoritativeCommandBoundary(engine, 10, 20);
            Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(10, 0, new DrawCardCommand(PlayerIndex.Player)));
            Assert.AreEqual(0, boundary.Revision);
        }
    }
}
