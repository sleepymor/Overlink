using NUnit.Framework;
using Overlink.Net;

namespace Overlink.Net.Tests
{
    /// <summary>
    /// The client-side copy of Unity's password rules, and the sentence a player gets when they miss one.
    /// <para>
    /// These tests exist because the rule itself is transcribed from the SDK's documentation rather than
    /// exposed by any API: nothing at runtime can be asked "what makes a password acceptable here?", so
    /// the only thing keeping <see cref="PasswordPolicy"/> honest is a case per clause. A boundary that
    /// drifts from Unity's is a policy that lies to the player in one direction (rejecting a password the
    /// service would take) or nags in the other (accepting one it will refuse) — and the second is the
    /// reason this class was written in the first place, so it must not become the first.
    /// </para>
    /// </summary>
    public class PasswordPolicyTests
    {
        const string Valid = "Passw0rd!";

        [Test]
        public void APasswordMeetingEveryRule_PassesSilently()
        {
            Assert.IsTrue(PasswordPolicy.Validate(Valid, out string error));
            Assert.IsNull(error);
        }

        [TestCase("short1!")]                                  // 7 chars, everything else present
        [TestCase("aaaaaaaa1!")]                               // no uppercase
        [TestCase("AAAAAAAA1!")]                               // no lowercase
        [TestCase("Password!")]                                // no digit
        [TestCase("Passw0rd")]                                 // no symbol
        public void EachMissingRule_IsNamedInTheError(string password)
        {
            Assert.IsFalse(PasswordPolicy.Validate(password, out string error));
            Assert.IsNotEmpty(error);
            StringAssert.StartsWith("Your password needs ", error);
        }

        [TestCase(PasswordPolicy.MinLength)]
        [TestCase(PasswordPolicy.MaxLength)]
        public void TheBoundariesThemselves_AreAccepted(int length)
        {
            // "Aa1!" satisfies all four content rules in four characters, so the rest is padding that
            // cannot break any of them; the point is that 8 and 30 pass, not that some spelling does.
            string password = "Aa1!" + new string('b', length - 4);
            Assert.AreEqual(length, password.Length, "the fixture stopped matching the policy");
            Assert.IsTrue(PasswordPolicy.Validate(password, out string error), error);
        }

        [Test]
        public void TooShort_SaysHowMuchIsMissing()
        {
            PasswordPolicy.Validate("Ab1!", out string error);
            StringAssert.Contains($"{PasswordPolicy.MinLength} characters", error);
            StringAssert.Contains("(it has 4)", error);
        }

        [Test]
        public void TooLong_DoesNotAskForMoreLength()
        {
            // Everything but the length is in order here, so the length is the only thing to say.
            string password = "Aa1!" + new string('b', PasswordPolicy.MaxLength - 4 + 1);
            PasswordPolicy.Validate(password, out string error);
            StringAssert.Contains("no more than " + PasswordPolicy.MaxLength, error);
            StringAssert.DoesNotContain("uppercase", error);
            StringAssert.DoesNotContain("symbol", error);
        }

        [Test]
        public void Spaces_AreRejectedRatherThanCountedAsASymbol()
        {
            Assert.IsFalse(PasswordPolicy.Validate("correct horse battery staple 1A!", out string error));
            StringAssert.Contains("spaces", error);
        }

        [TestCase(null)]
        [TestCase("")]
        public void AnEmptyPassword_IsNotDescribedByItsMissingRules(string password)
        {
            Assert.IsFalse(PasswordPolicy.Validate(password, out string error));
            Assert.AreEqual("Choose a password.", error);
        }

        /// <summary>
        /// One reply, every miss: fixing a password a rule at a time means a form the player has to
        /// submit four times, so the error lists everything wrong at once, in prose. A password cannot
        /// miss all five rules — an empty one is caught first, and any character belongs to some class —
        /// so this misses four and says so without naming the fifth.
        /// </summary>
        [Test]
        public void SeveralMissingRules_AreListedRatherThanShortCircuited()
        {
            PasswordPolicy.Validate("abcdef", out string error);   // too short, no upper, no digit, no symbol
            StringAssert.Contains("8 characters", error);
            StringAssert.Contains("uppercase", error);
            StringAssert.Contains("number", error);
            StringAssert.Contains("symbol", error);
            StringAssert.Contains(" and ", error);
            StringAssert.DoesNotContain("lowercase", error);
        }

        /// <summary>
        /// The form's standing hint and the error for a miss come from the same policy, and the hint has
        /// to be enough on its own — a player who reads it before typing should never need the error.
        /// </summary>
        [Test]
        public void TheRequirementLine_CoversEveryRuleTheValidatorChecks()
        {
            string line = PasswordPolicy.Requirements;
            StringAssert.Contains(PasswordPolicy.MinLength + "–" + PasswordPolicy.MaxLength, line);
            StringAssert.Contains("uppercase", line);
            StringAssert.Contains("lowercase", line);
            StringAssert.Contains("number", line);
            StringAssert.Contains("symbol", line);
        }

        /// <summary>
        /// The backstop, in the shape the service actually sends it: 10002 is the code for a malformed
        /// credential of either kind, so the detail text is the only clue, and a player who is told to
        /// "check your username and password" after picking a weak password has been told nothing.
        /// </summary>
        [Test]
        public void AServiceSidePasswordRejection_QuotesTheRequirements()
        {
            AuthResult result = AuthResult.FromException(
                new Unity.Services.Core.RequestFailedException(
                    Unity.Services.Authentication.AuthenticationErrorCodes.InvalidParameters,
                    "password does not meet the requirements"));

            Assert.AreEqual(AuthErrorCode.WeakPassword, result.ErrorCode);
            StringAssert.Contains(PasswordPolicy.Requirements, result.UserMessage);
        }

        /// <summary>
        /// The other half of the same code must keep its own sentence, or the discriminator above turns
        /// a bad username into advice about symbols.
        /// </summary>
        [Test]
        public void AServiceSideUsernameRejection_DoesNotTalkAboutPasswords()
        {
            AuthResult result = AuthResult.FromException(
                new Unity.Services.Core.RequestFailedException(
                    Unity.Services.Authentication.AuthenticationErrorCodes.InvalidParameters,
                    "username contains invalid characters"));

            Assert.AreEqual(AuthErrorCode.InvalidInput, result.ErrorCode);
            StringAssert.Contains("username and password", result.UserMessage);
            StringAssert.DoesNotContain(PasswordPolicy.Requirements, result.UserMessage);
        }
    }
}
