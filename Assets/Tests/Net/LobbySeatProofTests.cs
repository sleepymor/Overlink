using NUnit.Framework;

namespace Overlink.Net.Tests
{
    public sealed class LobbySeatProofTests
    {
        [Test]
        public void ProofIsBoundToLobbyPlayerPeerAndChallenge()
        {
            using (var key = new LobbySeatProof())
            {
                byte[] challenge = LobbySeatProof.CreateChallenge();
                byte[] signature = key.Sign("lobby", "guest", 42, challenge);
                Assert.AreEqual(256, System.Convert.FromBase64String(key.PublicModulus).Length, "RSA modulus size");
                Assert.AreEqual(256, signature.Length, "RSA signature size");
                Assert.IsTrue(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "lobby", "guest", 42, challenge, signature));
                Assert.IsFalse(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "other", "guest", 42, challenge, signature));
                Assert.IsFalse(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "lobby", "other", 42, challenge, signature));
                Assert.IsFalse(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "lobby", "guest", 43, challenge, signature));
                challenge[0] ^= 1;
                Assert.IsFalse(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "lobby", "guest", 42, challenge, signature));
            }
        }

        [Test]
        public void RejectsOtherKeysAndMalformedInputs()
        {
            using (var key = new LobbySeatProof())
            using (var other = new LobbySeatProof())
            {
                byte[] challenge = LobbySeatProof.CreateChallenge();
                byte[] signature = key.Sign("lobby", "guest", 42, challenge);
                Assert.IsFalse(LobbySeatProof.Verify(other.PublicModulus, other.PublicExponent, "lobby", "guest", 42, challenge, signature));
                Assert.IsFalse(LobbySeatProof.Verify("invalid", key.PublicExponent, "lobby", "guest", 42, challenge, signature));
                Assert.IsFalse(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "lobby", "guest", 42, new byte[1], signature));
                Assert.IsFalse(LobbySeatProof.Verify(key.PublicModulus, key.PublicExponent, "lobby", "guest", 42, challenge, new byte[1]));
            }
        }
    }
}
