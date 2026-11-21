using System;
using Overlink.Core;

namespace Overlink.Net
{
    /// <summary>Host-side one-shot proof for one NGO sender. Identity must come from fresh Lobby data.</summary>
    public sealed class RelaySeatHandshake
    {
        readonly LobbySeatIdentity identity;
        readonly ulong hostPeer;
        readonly ulong guestPeer;
        byte[] challenge;
        bool accepted;
        bool boundaryCreated;

        public RelaySeatHandshake(LobbySeatIdentity identity, ulong hostPeer, ulong guestPeer)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (hostPeer == guestPeer) throw new ArgumentException("Peers must be distinct.");
            this.hostPeer = hostPeer;
            this.guestPeer = guestPeer;
            challenge = LobbySeatProof.CreateChallenge();
        }

        public byte[] Challenge => challenge == null ? null : (byte[])challenge.Clone();
        public bool IsAuthenticated => accepted;
        public bool IsAuthenticatedPeer(ulong peer) => accepted && peer == guestPeer;

        public bool Accept(ulong senderPeer, byte[] signature)
        {
            if (senderPeer != guestPeer || challenge == null || accepted) return false;
            byte[] nonce = challenge;
            challenge = null;
            accepted = LobbySeatProof.Verify(identity.Modulus, identity.Exponent,
                identity.LobbyId, identity.PlayerId, senderPeer, nonce, signature);
            return accepted;
        }

        /// <summary>Only create this once, for the actual authoritative match engine.</summary>
        public AuthoritativeCommandBoundary CreateCommandBoundary(MatchEngine engine)
        {
            if (!accepted || boundaryCreated) throw new InvalidOperationException("Relay seat is not authenticated or was already bound.");
            var boundary = new AuthoritativeCommandBoundary(engine, hostPeer, guestPeer);
            boundaryCreated = true;
            return boundary;
        }
    }
}
