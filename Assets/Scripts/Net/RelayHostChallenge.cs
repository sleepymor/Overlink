using System;

namespace Overlink.Net
{
    /// <summary>
    /// Guest-side one-shot host proof. The identity must be fetched from fresh Lobby data;
    /// this object does not establish mutual authentication or send network messages.
    /// </summary>
    public sealed class RelayHostChallenge
    {
        readonly LobbySeatIdentity host;
        readonly ulong serverPeer;
        byte[] challenge;
        public bool IsAuthenticated { get; private set; }

        public RelayHostChallenge(LobbySeatIdentity host, ulong serverPeer)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.serverPeer = serverPeer;
            challenge = LobbySeatProof.CreateChallenge();
        }

        public byte[] Challenge => challenge == null ? null : (byte[])challenge.Clone();

        public bool Accept(ulong sender, byte[] signature)
        {
            if (sender != serverPeer || challenge == null || IsAuthenticated) return false;
            byte[] nonce = challenge;
            challenge = null;
            IsAuthenticated = LobbySeatProof.Verify(host.Modulus, host.Exponent, host.LobbyId,
                host.PlayerId, serverPeer, nonce, signature);
            return IsAuthenticated;
        }
    }
}
