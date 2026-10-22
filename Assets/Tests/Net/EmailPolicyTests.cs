using NUnit.Framework;
using Overlink.Net;

namespace Overlink.Net.Tests
{
    /// <summary>
    /// The address rules a player is held to before an address reaches Cloud Save.
    /// <para>
    /// <see cref="EmailPolicy"/> is not transcribed from any API — Unity has no email field to be wrong
    /// about — so unlike <see cref="PasswordPolicyTests"/> these cases are not guarding a copy. What
    /// they guard is the two-sided cost of getting the filter wrong: too strict and a real player with a
    /// real address is told it is invalid, which is a support ticket and possibly a lost account; too
    /// lenient and we store an address we then claim to be able to reach them at, which is worse than
    /// not asking. Both directions therefore have cases here, and the accepted list deliberately includes
    /// the awkward-but-real forms (plus-tagging, digits, long TLDs, inner hyphens).
    /// </para>
    /// </summary>
    public class EmailPolicyTests
    {
        const string Valid = "player@example.com";

        [TestCase("player@example.com")]
        [TestCase("a.b@example.com")]
        [TestCase("a+b.filter@example.com")]                     // plus-tagging is legal and widely used
        [TestCase("player+receipts@gmail.com")]
        [TestCase("name@sub.domain.example.com")]
        [TestCase("9@example.com")]                              // a single-digit local part is legal
        [TestCase("a_b-c.d*e@example.com")]                      // atext punctuation
        [TestCase("name@my-domain.co.uk")]                       // inner hyphen, two-part TLD
        [TestCase("name@example.travel")]                        // long TLD
        [TestCase("NAME@EXAMPLE.COM")]                           // case is a spelling, not a rule
        public void RealWorldAddresses_AreAccepted(string email)
        {
            Assert.IsTrue(EmailPolicy.Validate(email, out string error),
                $"rejected a usable address: {email} → {error}");
            Assert.IsNull(error);
        }

        [Test]
        public void AnEmptyAddress_IsRejectedWithARequest()
        {
            Assert.IsFalse(EmailPolicy.Validate("", out string error));
            StringAssert.Contains("Enter your email", error);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NothingAtAll_NeverPasses(string email)
        {
            Assert.IsFalse(EmailPolicy.Validate(email, out string error));
            Assert.IsNotEmpty(error);
        }

        [TestCase("playerexample.com", "no @")]
        [TestCase("a@b@c.com", "two @")]
        [TestCase("@example.com", "no local part")]
        [TestCase("player@", "no domain")]
        [TestCase("pla yer@example.com", "interior space")]
        [TestCase(".player@example.com", "leading dot")]
        [TestCase("player.@example.com", "trailing dot")]
        [TestCase("pl..ayer@example.com", "double dot")]
        [TestCase("play(er)@example.com", "illegal punctuation")]
        [TestCase("player@localhost", "domain with no dot")]
        [TestCase("player@-example.com", "leading hyphen in domain")]
        [TestCase("player@example-.com", "trailing hyphen in domain")]
        [TestCase("player@example.123", "numeric TLD")]
        [TestCase("player@example.c", "one-letter TLD")]
        [TestCase("jos\u00e9@example.com", "non-ASCII local part")]
        [TestCase("player@exämple.com", "non-ASCII domain")]
        public void MistakesPlayersActuallyMake_AreRejected(string email, string what)
        {
            Assert.IsFalse(EmailPolicy.Validate(email, out string error), what);
            Assert.IsNotEmpty(error, what);
        }

        [Test]
        public void TheLengthLimitsAreTheOnesRFC5321States()
        {
            // 254 = 256-octet reverse path minus the angle brackets; 64 and 63 are the local-part and
            // label caps. If these constants drift, the comments in EmailPolicy cite the sections.
            Assert.AreEqual(254, EmailPolicy.MaxLength);
            Assert.AreEqual(64, EmailPolicy.MaxLocalLength);
            Assert.AreEqual(63, EmailPolicy.MaxDomainLabelLength);
        }

        [Test]
        public void AnAddressOneCharOverTheTotalLimit_IsRejected()
        {
            string email = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63)
                           + "." + new string('d', 63) + ".com";
            Assert.Greater(email.Length, EmailPolicy.MaxLength);
            Assert.IsFalse(EmailPolicy.Validate(email, out string error));
            StringAssert.Contains("longer than", error);
        }

        [Test]
        public void AnAddressExactlyAtTheTotalLimit_IsAccepted()
        {
            // The boundary itself has to pass: rejecting a 254-char address that every mail server
            // accepts is the "too strict" side of the cost, and it is the kind of off-by-one that a
            // single over-the-boundary test would not catch. 64 + 1 + 63 + 1 + 63 + 1 + 61 = 254.
            string email = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63)
                           + "." + new string('d', 61);
            Assert.AreEqual(EmailPolicy.MaxLength, email.Length);
            Assert.IsTrue(EmailPolicy.Validate(email, out string error), error);
        }

        [Test]
        public void ALocalPartOneCharOverItsLimit_IsRejectedByName()
        {
            string email = new string('a', EmailPolicy.MaxLocalLength + 1) + "@example.com";
            Assert.IsFalse(EmailPolicy.Validate(email, out string error));
            StringAssert.Contains("before the @", error);
        }

        [Test]
        public void ADomainLabelOverItsLimit_IsRejectedByName()
        {
            string email = "a@" + new string('b', EmailPolicy.MaxDomainLabelLength + 1) + ".com";
            Assert.IsFalse(EmailPolicy.Validate(email, out string error));
            StringAssert.Contains("domain", error);
        }

        [Test]
        public void SurroundingWhitespaceIsTrimmedRatherThanReported()
        {
            // Autofill and copy-paste put a space at the end of this field constantly. It is not a
            // mistake the player can see, so reporting it would produce an error nobody can act on.
            // Interior space is a different matter and is rejected above.
            Assert.IsTrue(EmailPolicy.Validate("  player@example.com  ", out string error), error);
        }

        [Test]
        public void NormalizationLowercasesAndTrimsForStorage()
        {
            Assert.AreEqual("player@example.com", EmailPolicy.Normalize("  PLAYer@Example.COM "));
        }

        [Test]
        public void Normalize_IsNullSafeAndAgreesWithValidate()
        {
            Assert.AreEqual("", EmailPolicy.Normalize(null));
            Assert.AreEqual("", EmailPolicy.Normalize("   "));

            // Whatever is accepted must still be accepted after the transformation we store it in,
            // otherwise the saved value and the validated value are different strings by design.
            EmailPolicy.Validate("NAME@EXAMPLE.com", out _);
            string normalized = EmailPolicy.Normalize("NAME@EXAMPLE.com");
            Assert.IsTrue(EmailPolicy.Validate(normalized, out string error), error);
        }

        [Test]
        public void EveryErrorSentenceIsPlayerFacing()
        {
            // A policy that explains itself in RFC vocabulary is a policy nobody can follow. This is the
            // same bar the password errors hold themselves to, and it is cheap to hold.
            string[] bad =
            {
                "x", "@", "a@", "@b.com", "a b@c.com", "a..b@c.com", "a@b", "a@b.c", "a@-b.com",
                "a@b.1", "\u00e9@b.com", "a@b.c\u00f6m", "a(b@c.com", new string('a', 65) + "@b.com"
            };

            foreach (string email in bad)
            {
                Assert.IsFalse(EmailPolicy.Validate(email, out string error), email);
                Assert.IsNotEmpty(error, email);
                Assert.That(error, Does.Not.Contain("RFC"), email);
                Assert.That(error, Does.Not.Contain("exception"), email);
                Assert.That(error, Does.Not.Contain("null"), email);
            }
        }

        [Test]
        public void AQuotedLocalPartIsDeliberatelyUnsupported()
        {
            // `"player name"@example.com` is legal under RFC 5322 and is essentially never issued by a
            // consumer mail provider. Pinned as a test so that if it ever changes, it changes on purpose
            // rather than by someone relaxing the local-part filter.
            Assert.IsFalse(EmailPolicy.Validate("\"player name\"@example.com", out string error));
            Assert.IsNotEmpty(error);
        }
    }
}
