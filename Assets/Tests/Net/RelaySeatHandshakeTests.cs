using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using Unity.Services.Lobbies.Models;

namespace Overlink.Net.Tests
{
    public sealed class RelaySeatHandshakeTests
    {
        static LobbySeatIdentity Identity(LobbySeatProof proof)
        {
            var lobby = new Lobby(id: "lobby", hostId: "host", players: new List<Player>
            {
                new Player("host"), new Player("guest", data: new Dictionary<string, PlayerDataObject>
                {
                    { LobbySeatIdentity.ModulusKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, proof.PublicModulus) },
                    { LobbySeatIdentity.ExponentKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, proof.PublicExponent) }
                })
            });
            Assert.IsTrue(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out var identity));
            return identity;
        }

        [Test]
        public void AcceptedProofBindsOnlyActualSenderToCommandBoundary()
        {
            using (var proof = new LobbySeatProof())
            {
                var handshake = new RelaySeatHandshake(Identity(proof), 0, 8);
                var state = new MatchState(1);
                Assert.IsFalse(handshake.IsAuthenticatedPeer(8));
                Assert.Throws<InvalidOperationException>(() => handshake.CreateCommandBoundary(new MatchEngine(state)));
                byte[] signature = proof.Sign("lobby", "guest", 8, handshake.Challenge);
                Assert.IsFalse(handshake.Accept(9, signature));
                Assert.IsTrue(handshake.Accept(8, signature));
                Assert.IsTrue(handshake.IsAuthenticatedPeer(8));
                Assert.IsFalse(handshake.IsAuthenticatedPeer(9));
                Assert.IsFalse(handshake.IsAuthenticatedPeer(0));
                Assert.IsFalse(handshake.Accept(8, signature));
                var boundary = handshake.CreateCommandBoundary(new MatchEngine(state));
                Assert.Throws<InvalidOperationException>(() => handshake.CreateCommandBoundary(new MatchEngine(state)));
                Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(9, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.AreEqual(CommandResult.InvalidCommand, boundary.Execute(8, 0, new ConcedeCommand(PlayerIndex.Player)));
                Assert.AreEqual(CommandResult.Success, boundary.Execute(8, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.AreEqual(MatchResult.PlayerWin, state.Result);
            }
        }

        [Test]
        public void BadProofConsumesChallengeAndCannotBeReplayed()
        {
            using (var proof = new LobbySeatProof())
            using (var attacker = new LobbySeatProof())
            {
                var handshake = new RelaySeatHandshake(Identity(proof), 0, 8);
                byte[] challenge = handshake.Challenge;
                Assert.IsFalse(handshake.Accept(8, attacker.Sign("lobby", "guest", 8, challenge)));
                Assert.IsFalse(handshake.Accept(8, proof.Sign("lobby", "guest", 8, challenge)));
                Assert.IsFalse(handshake.IsAuthenticated);
            }
        }
    }
}
