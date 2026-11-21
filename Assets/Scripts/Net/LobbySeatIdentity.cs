using System;
using Unity.Services.Lobbies.Models;

namespace Overlink.Net
{
    /// <summary>A public key selected from a Lobby service response, not a Relay payload.</summary>
    public sealed class LobbySeatIdentity
    {
        public const string ModulusKey = "SeatProofModulus";
        public const string ExponentKey = "SeatProofExponent";
        public string LobbyId { get; }
        public string PlayerId { get; }
        public string Modulus { get; }
        public string Exponent { get; }

        LobbySeatIdentity(string lobbyId, string playerId, string modulus, string exponent)
        {
            LobbyId = lobbyId;
            PlayerId = playerId;
            Modulus = modulus;
            Exponent = exponent;
        }

        /// <summary>Rejects stale membership, host migration, malformed keys and ambiguous seats.</summary>
        public static bool TryRead(Lobby lobby, string expectedLobbyId, string expectedHostId,
            string expectedOpponentId, out LobbySeatIdentity identity)
            => TryReadSeat(lobby, expectedLobbyId, expectedHostId, expectedOpponentId, false, out identity);

        /// <summary>Selects the host's key from fresh Lobby data after checking both seats and host identity.</summary>
        public static bool TryReadHost(Lobby lobby, string expectedLobbyId, string expectedHostId,
            string expectedGuestId, out LobbySeatIdentity identity)
            => TryReadSeat(lobby, expectedLobbyId, expectedHostId, expectedGuestId, true, out identity);

        static bool TryReadSeat(Lobby lobby, string expectedLobbyId, string expectedHostId,
            string expectedOpponentId, bool selectHost, out LobbySeatIdentity identity)
        {
            identity = null;
            if (lobby == null || string.IsNullOrEmpty(expectedLobbyId) ||
                string.IsNullOrEmpty(expectedHostId) || string.IsNullOrEmpty(expectedOpponentId) ||
                expectedHostId == expectedOpponentId || lobby.Id != expectedLobbyId ||
                lobby.HostId != expectedHostId || lobby.Players == null || lobby.Players.Count != 2)
                return false;

            Player host = null, opponent = null;
            foreach (Player player in lobby.Players)
            {
                if (player == null) return false;
                if (player.Id == expectedHostId && host == null) host = player;
                else if (player.Id == expectedOpponentId && opponent == null) opponent = player;
                else return false;
            }
            Player selected = selectHost ? host : opponent;
            if (host == null || opponent == null || selected?.Data == null ||
                !selected.Data.TryGetValue(ModulusKey, out PlayerDataObject n) ||
                !selected.Data.TryGetValue(ExponentKey, out PlayerDataObject e) ||
                n == null || e == null || n.Visibility != PlayerDataObject.VisibilityOptions.Member ||
                e.Visibility != PlayerDataObject.VisibilityOptions.Member ||
                !ValidKey(n.Value, e.Value)) return false;
            identity = new LobbySeatIdentity(lobby.Id, selected.Id, n.Value, e.Value);
            return true;
        }

        static bool ValidKey(string modulus, string exponent)
        {
            if (string.IsNullOrEmpty(modulus) || modulus.Length != 344 ||
                string.IsNullOrEmpty(exponent) || exponent.Length > 12) return false;
            try
            {
                byte[] n = Convert.FromBase64String(modulus);
                byte[] e = Convert.FromBase64String(exponent);
                return n.Length == 256 && (n[0] & 0x80) != 0 && (n[255] & 1) == 1 &&
                    e.Length == 3 && e[0] == 1 && e[1] == 0 && e[2] == 1;
            }
            catch (FormatException) { return false; }
        }
    }
}
