using System;
using System.Text;
using NUnit.Framework;
using Overlink.Net;

namespace Overlink.Net.Tests
{
    /// <summary>
    /// <see cref="GoogleTokenProvider.VerifyNonce"/>, the last gate an ID token passes before the game
    /// trusts it with a Unity authentication.
    /// <para>
    /// This check used to be two inline <c>string.Equals</c> calls, one inside each platform's
    /// <c>#if</c>. Both were unreachable from EditMode: the WebGL one lives behind
    /// <c>UNITY_WEBGL &amp;&amp; !UNITY_EDITOR</c>, so the first time it ever ran in anger was a
    /// deployed build, and the failure it guards against arrives as one indistinguishable sentence.
    /// Hoisting it into a shared method is what makes these cases possible; the two platform paths now
    /// cannot disagree about a check one of them has been tested against.
    /// </para>
    /// </summary>
    public class NonceVerificationTests
    {
        const string Nonce = "0123456789abcdef0123456789abcdef";

        static string Base64Url(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>An unsigned ID-token-shaped string whose payload carries the given nonce claim.</summary>
        static string Token(string payloadJson) =>
            Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}") + "." + Base64Url(payloadJson) + ".signature";

        static string TokenWithNonce(string nonce) =>
            Token("{\"iss\":\"https://accounts.google.com\",\"aud\":\"client-id\",\"sub\":\"42\"," +
                  "\"nonce\":\"" + nonce + "\"}");

        static GoogleTokenProvider.GoogleDeclineException Catch(string token, string nonce)
        {
            try
            {
                GoogleTokenProvider.VerifyNonce(token, nonce);
                return null;
            }
            catch (GoogleTokenProvider.GoogleDeclineException e)
            {
                return e;
            }
        }

        [Test]
        public void AMatchingNonce_ReturnsTheSameTokenUntouched()
        {
            string token = TokenWithNonce(Nonce);

            Assert.AreSame(token, GoogleTokenProvider.VerifyNonce(token, Nonce),
                "the token must reach Unity's SDK byte for byte");
        }

        /// <summary>
        /// Padding is the quiet half of base64url: a payload of any length has to decode, and the
        /// decoder restores the <c>=</c> characters this helper strips. Varying the nonce varies the
        /// payload length, which walks every remainder modulo four rather than trusting one example.
        /// </summary>
        [Test]
        public void EveryPayloadLength_Decodes()
        {
            for (int length = 1; length <= 24; length++)
            {
                string nonce = Nonce.Substring(0, length);
                string token = TokenWithNonce(nonce);
                Assert.AreEqual(token, GoogleTokenProvider.VerifyNonce(token, nonce),
                    "rejected a valid token carrying a " + length + "-character nonce");
            }
        }

        [Test]
        public void ADifferentNonce_IsAProviderFailureAndSaysWrongNonce()
        {
            GoogleTokenProvider.GoogleDeclineException e =
                Catch(TokenWithNonce(Nonce), "fedcba9876543210fedcba9876543210");

            Assert.IsNotNull(e, "a mismatched nonce was accepted");
            Assert.AreEqual(GoogleTokenProvider.GoogleDecline.Failed, e.Decline);
            StringAssert.Contains("wrong nonce", e.Message);
        }

        /// <summary>
        /// The distinction these two sentences exist to make: GIS ignoring the nonce it was initialised
        /// with is a bridge or client-configuration bug, while a token from some other request is a
        /// mix-up. Collapsing them is what cost a build cycle to read the difference.
        /// </summary>
        [Test]
        public void ANonceClaimThatIsAbsent_SaysSoRatherThanBlamingTheValue()
        {
            GoogleTokenProvider.GoogleDeclineException e =
                Catch(Token("{\"iss\":\"https://accounts.google.com\",\"sub\":\"42\"}"), Nonce);

            Assert.IsNotNull(e, "a token with no nonce claim was accepted");
            Assert.AreEqual(GoogleTokenProvider.GoogleDecline.Failed, e.Decline);
            StringAssert.Contains("no nonce", e.Message);
            StringAssert.DoesNotContain("wrong nonce", e.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-jwt")]
        [TestCase("only.one")]
        [TestCase("a.b.c.d")]
        public void SomethingThatIsNotAToken_TakesTheAbsentClaimBranch(string token)
        {
            GoogleTokenProvider.GoogleDeclineException e = Catch(token, Nonce);

            Assert.IsNotNull(e, "accepted something that is not a JWT");
            StringAssert.Contains("no nonce", e.Message);
        }

        [Test]
        public void AnEmptyExpectedNonce_StillRejectsATokenCarryingOne()
        {
            GoogleTokenProvider.GoogleDeclineException e = Catch(TokenWithNonce(Nonce), "");

            Assert.IsNotNull(e, "an unset nonce matched everything");
            Assert.AreEqual(GoogleTokenProvider.GoogleDecline.Failed, e.Decline);
        }

        /// <summary>
        /// The trap this exists to close: a token with no nonce claim and an unset expected nonce are two
        /// empty strings, and an equality test alone would wave that through as a valid credential.
        /// </summary>
        [Test]
        public void NothingMatchingNothing_IsNotAMatch()
        {
            GoogleTokenProvider.GoogleDeclineException e = Catch(TokenWithNonce(null), null);

            Assert.IsNotNull(e, "an absent nonce and an absent expectation matched each other");
            Assert.AreEqual(GoogleTokenProvider.GoogleDecline.Failed, e.Decline);
        }

        /// <summary>
        /// The raw token is a bearer credential. It goes to no player, no <c>AuthResult</c> message and
        /// therefore no cloud log, however useful it would have been while debugging this flow.
        /// </summary>
        [Test]
        public void TheRejectionNeverCarriesTheTokenItRefused()
        {
            string token = TokenWithNonce("stale-from-another-request");
            GoogleTokenProvider.GoogleDeclineException e = Catch(token, Nonce);

            Assert.IsNotNull(e);
            AuthResult result = AuthResult.FromException(e);
            StringAssert.DoesNotContain(token, result.UserMessage);
            StringAssert.DoesNotContain(token, result.TechnicalMessage);
            StringAssert.DoesNotContain(token, e.Message);
        }

        /// <summary>
        /// A nonce rejection is never a cancellation. Telling a player they changed their mind when
        /// Google answered wrongly is the same lie the first bridge told about One Tap.
        /// </summary>
        [Test]
        public void ARejectedNonce_IsNotReportedAsThePlayerChangingTheirMind()
        {
            AuthResult result = AuthResult.FromException(Catch(TokenWithNonce("other"), Nonce));

            Assert.IsFalse(result.Success);
            Assert.AreNotEqual(AuthErrorCode.UserCancelled, result.ErrorCode);
        }
    }
}
