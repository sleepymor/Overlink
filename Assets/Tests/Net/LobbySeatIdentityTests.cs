using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using Unity.Services.Lobbies.Models;

namespace Overlink.Net.Tests
{
    public sealed class LobbySeatIdentityTests
    {
        static Lobby MakeLobby(LobbySeatProof proof)
        {
            var data = new Dictionary<string, PlayerDataObject>
            {
                { LobbySeatIdentity.ModulusKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, proof.PublicModulus) },
                { LobbySeatIdentity.ExponentKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, proof.PublicExponent) }
            };
            return new Lobby(id: "lobby", hostId: "host", players: new List<Player>
            {
                new Player("host"), new Player("guest", data: data)
            });
        }

        [Test]
        public void AcceptsOnlyExpectedOpponentKeyFromHostLobby()
        {
            using (var proof = new LobbySeatProof())
            {
                Lobby lobby = MakeLobby(proof);
                Assert.IsTrue(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out var identity));
                Assert.AreEqual(proof.PublicModulus, identity.Modulus);
                Assert.AreEqual("guest", identity.PlayerId);
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "other", "host", "guest", out _));
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "lobby", "guest", "host", out _));
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "other", out _));
                lobby.Players.Add(new Player("intruder"));
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out _));
            }
        }

        [Test]
        public void RejectsUnpublishedAndMalformedKeys()
        {
            using (var proof = new LobbySeatProof())
            {
                Lobby lobby = MakeLobby(proof);
                var data = lobby.Players[1].Data;
                data[LobbySeatIdentity.ModulusKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Private, proof.PublicModulus);
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out _));
                data[LobbySeatIdentity.ModulusKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "invalid");
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out _));
                data[LobbySeatIdentity.ModulusKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, proof.PublicModulus);
                data[LobbySeatIdentity.ExponentKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "AQ==");
                Assert.IsFalse(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out _));
            }
        }

        [Test]
        public void RetrievedOpponentKeyCanAuthenticateAndAuthorizeOnlyItsRelaySeat()
        {
            using (var proof = new LobbySeatProof())
            {
                Lobby lobby = MakeLobby(proof);
                Assert.IsTrue(LobbySeatIdentity.TryRead(lobby, "lobby", "host", "guest", out var identity));
                var state = new MatchState(7);
                var seats = new AuthenticatedMatchSeats(new MatchEngine(state), 0,
                    identity.LobbyId, identity.PlayerId, identity.Modulus, identity.Exponent);
                byte[] challenge = seats.IssueChallenge(9);
                Assert.IsTrue(seats.Authenticate(9, proof.Sign("lobby", "guest", 9, challenge)));
                Assert.AreEqual(CommandResult.InvalidCommand, seats.Execute(10, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.AreEqual(CommandResult.Success, seats.Execute(9, 0, new ConcedeCommand(PlayerIndex.Opponent)));
                Assert.AreEqual(MatchResult.PlayerWin, state.Result);
            }
        }
    }
}
