using System.Collections.Generic;
using NUnit.Framework;
using Unity.Services.Lobbies.Models;

namespace Overlink.Net.Tests
{
    public sealed class RelayHostChallengeTests
    {
        static Lobby LobbyWithHost(LobbySeatProof hostKey)
        {
            return new Lobby(id: "lobby", hostId: "host", players: new List<Player> {
                new Player("host", data: new Dictionary<string, PlayerDataObject> {
                    { LobbySeatIdentity.ModulusKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, hostKey.PublicModulus) },
                    { LobbySeatIdentity.ExponentKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, hostKey.PublicExponent) } }),
                new Player("guest") });
        }

        [Test]
        public void HostProofBindsFreshLobbyKeyPeerAndChallenge()
        {
            using (var key = new LobbySeatProof())
            {
                var lobby = LobbyWithHost(key);
                Assert.IsTrue(LobbySeatIdentity.TryReadHost(lobby, "lobby", "host", "guest", out var host));
                Assert.IsFalse(LobbySeatIdentity.TryReadHost(lobby, "lobby", "guest", "host", out _));
                var proof = new RelayHostChallenge(host, 0);
                byte[] challenge = proof.Challenge;
                byte[] signature = key.Sign("lobby", "host", 0, challenge);
                Assert.IsFalse(proof.Accept(9, signature));
                Assert.IsTrue(proof.Accept(0, signature));
                Assert.IsTrue(proof.IsAuthenticated);
                Assert.IsFalse(proof.Accept(0, signature));
                var fresh = new RelayHostChallenge(host, 0);
                Assert.IsFalse(fresh.Accept(0, signature));
                Assert.IsFalse(fresh.Accept(0, key.Sign("lobby", "host", 0, fresh.Challenge ?? challenge)));
            }
        }

        [Test]
        public void HostCannotImpersonateLobbyGuestKeyOrOtherLobby()
        {
            using (var hostKey = new LobbySeatProof())
            using (var attacker = new LobbySeatProof())
            {
                var lobby = LobbyWithHost(hostKey);
                Assert.IsFalse(LobbySeatIdentity.TryReadHost(lobby, "other", "host", "guest", out _));
                Assert.IsTrue(LobbySeatIdentity.TryReadHost(lobby, "lobby", "host", "guest", out var identity));
                var challenge = new RelayHostChallenge(identity, 0);
                Assert.IsFalse(challenge.Accept(0, attacker.Sign("lobby", "host", 0, challenge.Challenge)));
                Assert.IsFalse(challenge.IsAuthenticated);
            }
        }
    }
}
