using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Overlink.Net
{
    /// <summary>
    /// Proof of possession for a Relay connection. The public key must be read from the
    /// expected player's authenticated Lobby player data, not supplied by the connection.
    /// The host must issue a fresh unpredictable challenge and consume it only once.
    /// This primitive does not fetch Lobby data or bind a network seat by itself.
    /// </summary>
    public sealed class LobbySeatProof : IDisposable
    {
        const int KeyBytes = 256;
        public const int ChallengeBytes = 32;
        readonly RSA key;

        public LobbySeatProof()
        {
            // Mono's default RSA.Create provider can ignore a later KeySize assignment.
            key = new RSACryptoServiceProvider(KeyBytes * 8) { PersistKeyInCsp = false };
        }

        public string PublicModulus => Convert.ToBase64String(key.ExportParameters(false).Modulus);
        public string PublicExponent => Convert.ToBase64String(key.ExportParameters(false).Exponent);

        public static byte[] CreateChallenge()
        {
            var bytes = new byte[ChallengeBytes];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return bytes;
        }

        public byte[] Sign(string lobbyId, string playerId, ulong relayClientId, byte[] challenge)
        {
            return key.SignData(Payload(lobbyId, playerId, relayClientId, challenge),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        public static bool Verify(string modulus, string exponent, string lobbyId, string playerId,
            ulong relayClientId, byte[] challenge, byte[] signature)
        {
            try
            {
                if (string.IsNullOrEmpty(modulus) || modulus.Length > 344 ||
                    string.IsNullOrEmpty(exponent) || exponent.Length > 12 ||
                    signature == null || signature.Length != KeyBytes) return false;
                byte[] n = Convert.FromBase64String(modulus);
                byte[] e = Convert.FromBase64String(exponent);
                if (n.Length != KeyBytes || e.Length == 0 || e.Length > 8) return false;
                byte[] payload = Payload(lobbyId, playerId, relayClientId, challenge);
                using (RSA verifier = RSA.Create())
                {
                    verifier.ImportParameters(new RSAParameters { Modulus = n, Exponent = e });
                    return verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException ||
                                          error is CryptographicException || error is OverflowException)
            {
                return false;
            }
        }

        static byte[] Payload(string lobbyId, string playerId, ulong peer, byte[] challenge)
        {
            if (string.IsNullOrEmpty(lobbyId) || lobbyId.Length > 128 ||
                string.IsNullOrEmpty(playerId) || playerId.Length > 128 ||
                challenge == null || challenge.Length != ChallengeBytes)
                throw new ArgumentException("Invalid lobby seat proof inputs.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write("Overlink.LobbySeatProof.v1");
                writer.Write(lobbyId);
                writer.Write(playerId);
                writer.Write(peer);
                writer.Write(challenge);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public void Dispose() => key.Dispose();
    }
}
