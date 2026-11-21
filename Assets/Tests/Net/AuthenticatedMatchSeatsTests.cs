using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Net.Tests
{
    public sealed class AuthenticatedMatchSeatsTests
    {
        [Test]
        public void CommandsRequireAValidProofForTheActualRelaySender()
        {
            using (var guest = new LobbySeatProof())
            {
                var state = new MatchState(1);
                var seats = new AuthenticatedMatchSeats(new MatchEngine(state), 0, "lobby", "guest", guest.PublicModulus, guest.PublicExponent);
                Assert.AreEqual(CommandResult.InvalidCommand, seats.Execute(7, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                byte[] challenge = seats.IssueChallenge(7);
                byte[] wrongPeer = guest.Sign("lobby", "guest", 8, challenge);
                Assert.IsFalse(seats.Authenticate(7, wrongPeer));
                byte[] correct = guest.Sign("lobby", "guest", 7, challenge);
                Assert.IsFalse(seats.Authenticate(7, correct), "A failed proof consumes its challenge");
                challenge = seats.IssueChallenge(7);
                correct = guest.Sign("lobby", "guest", 7, challenge);
                Assert.IsFalse(seats.Authenticate(8, correct));
                Assert.IsTrue(seats.Authenticate(7, correct));
                Assert.AreEqual(CommandResult.InvalidCommand, seats.Execute(8, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.AreEqual(CommandResult.InvalidCommand, seats.Execute(7, 0, new ConcedeCommand(PlayerIndex.Player)));
                Assert.AreEqual(CommandResult.Success, seats.Execute(7, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.AreEqual(MatchResult.PlayerWin, state.Result);
                Assert.AreEqual(1, seats.Revision);
            }
        }

        [Test]
        public void DisconnectImmediatelyRevokesTheSeat()
        {
            using (var guest = new LobbySeatProof())
            {
                var state = new MatchState(1);
                var seats = new AuthenticatedMatchSeats(new MatchEngine(state), 0, "lobby", "guest", guest.PublicModulus, guest.PublicExponent);
                byte[] challenge = seats.IssueChallenge(7);
                Assert.IsTrue(seats.Authenticate(7, guest.Sign("lobby", "guest", 7, challenge)));
                seats.Disconnect(7);
                Assert.IsFalse(seats.IsAuthenticated);
                Assert.IsNull(seats.IssueChallenge(7), "Disconnected gate cannot reset its revision by reauthentication");
                Assert.AreEqual(CommandResult.InvalidCommand, seats.Execute(7, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.IsFalse(state.IsMatchOver());
            }
        }
    }
}
