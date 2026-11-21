using System;
using Overlink.Core;

namespace Overlink.Net
{
    /// <summary>
    /// Host-side seat and command gate. Construct only from a fresh authenticated Lobby
    /// response: the lobby ID, opponent ID, and opponent public key must never come from
    /// a Relay message. NGO must provide sender IDs from its receive callback.
    /// </summary>
    public sealed class AuthenticatedMatchSeats
    {
        readonly MatchEngine engine;
        readonly ulong hostPeer;
        readonly string lobbyId, opponentId, modulus, exponent;
        ulong pendingPeer;
        byte[] pendingChallenge;
        ulong? opponentPeer;
        AuthoritativeCommandBoundary boundary;
        bool closed;

        public bool IsAuthenticated => opponentPeer.HasValue;
        public long Revision => boundary?.Revision ?? 0;

        public AuthenticatedMatchSeats(MatchEngine engine, ulong hostPeer, string lobbyId,
            string opponentId, string opponentModulus, string opponentExponent)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(opponentId) ||
                string.IsNullOrEmpty(opponentModulus) || string.IsNullOrEmpty(opponentExponent))
                throw new ArgumentException("A lobby, opponent and verified public key are required.");
            this.hostPeer = hostPeer;
            this.lobbyId = lobbyId;
            this.opponentId = opponentId;
            modulus = opponentModulus;
            exponent = opponentExponent;
        }

        /// <summary>Only one challenge is outstanding; issuing a new one invalidates the old one.</summary>
        public byte[] IssueChallenge(ulong senderPeer)
        {
            if (senderPeer == hostPeer || IsAuthenticated || closed) return null;
            pendingPeer = senderPeer;
            pendingChallenge = LobbySeatProof.CreateChallenge();
            return (byte[])pendingChallenge.Clone();
        }

        public bool Authenticate(ulong senderPeer, byte[] signature)
        {
            if (IsAuthenticated || pendingChallenge == null || senderPeer != pendingPeer) return false;
            byte[] challenge = pendingChallenge;
            pendingChallenge = null; // A failed proof consumes the nonce as well.
            if (!LobbySeatProof.Verify(modulus, exponent, lobbyId, opponentId, senderPeer, challenge, signature))
                return false;
            opponentPeer = senderPeer;
            boundary = new AuthoritativeCommandBoundary(engine, hostPeer, senderPeer);
            return true;
        }

        public CommandResult Execute(ulong senderPeer, long revision, GameCommand command)
        {
            return !IsAuthenticated || boundary == null ? CommandResult.InvalidCommand : boundary.Execute(senderPeer, revision, command);
        }

        /// <summary>
        /// Invalidates command execution on disconnect. A new gate is required for reconnection;
        /// it must restore the authoritative revision as well as the authenticated seat.
        /// </summary>
        public void Disconnect(ulong senderPeer)
        {
            if (pendingChallenge != null && senderPeer == pendingPeer) pendingChallenge = null;
            if (opponentPeer == senderPeer)
            {
                opponentPeer = null;
                closed = true;
            }
        }
    }
}
