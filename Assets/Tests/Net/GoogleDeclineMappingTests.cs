using NUnit.Framework;
using Overlink.Net;

namespace Overlink.Net.Tests
{
    /// <summary>
    /// The Google-specific branch of <see cref="AuthResult.FromException"/>.
    /// <para>
    /// Everything else in the auth layer is tested through the SDK's own error codes. These cases exist
    /// because the provider's failures never reach an SDK code at all — the browser bridge is the only
    /// thing between the player and a message — and because <see cref="AuthResult"/> resolves unknown
    /// exceptions by matching English substrings. Two of Google's sentences collide with that pass by
    /// construction, so the ordering of the structured branch is load-bearing rather than stylistic; the
    /// details below are written to contain the exact words that would misdirect the substring matcher.
    /// </para>
    /// </summary>
    public class GoogleDeclineMappingTests
    {
        const string PlayerText = "ignored";

        static AuthResult Map(GoogleTokenProvider.GoogleDecline decline, string detail = PlayerText)
        {
            return AuthResult.FromException(
                new GoogleTokenProvider.GoogleDeclineException(decline, detail));
        }

        [Test]
        public void Cancelled_LandsOnUserCancelled()
        {
            Assert.AreEqual(AuthErrorCode.UserCancelled,
                Map(GoogleTokenProvider.GoogleDecline.Cancelled).ErrorCode);
        }

        [Test]
        public void Failed_LandsOnInvalidCredentials()
        {
            Assert.AreEqual(AuthErrorCode.InvalidCredentials,
                Map(GoogleTokenProvider.GoogleDecline.Failed).ErrorCode);
        }

        /// <summary>
        /// The itch.io case. The player is told to use a username and password, which contains the word
        /// "password"; without the structured branch the substring pass reads that as a password-strength
        /// complaint and answers "That password does not meet the requirements."
        /// </summary>
        [Test]
        public void Unavailable_SurvivesItsOwnReferenceToPasswords()
        {
            var result = Map(GoogleTokenProvider.GoogleDecline.Unavailable,
                "GIS can't render here; the player needs a username and password instead");

            Assert.AreEqual(AuthErrorCode.ConfigError, result.ErrorCode);
            Assert.AreNotEqual(AuthErrorCode.WeakPassword, result.ErrorCode);
            StringAssert.Contains("username and password", result.UserMessage);
        }

        /// <summary>
        /// Same shape as the case above: "timeout" is a substring the network-error branch keys on, and a
        /// stalled provider prompt has nothing to do with connectivity.
        /// </summary>
        [Test]
        public void TimedOut_DoesNotReadAsANetworkFailure()
        {
            var result = Map(GoogleTokenProvider.GoogleDecline.TimedOut,
                "timeout waiting for the browser bridge");

            Assert.AreEqual(AuthErrorCode.UserCancelled, result.ErrorCode);
            Assert.AreNotEqual(AuthErrorCode.NetworkError, result.ErrorCode);
        }

        [Test]
        public void EveryOutcome_ReachesThePlayerWithItsOwnSentence()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (GoogleTokenProvider.GoogleDecline decline in System.Enum.GetValues(
                         typeof(GoogleTokenProvider.GoogleDecline)))
            {
                AuthResult result = Map(decline);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage),
                    decline + " produced no player-facing text");
                Assert.IsFalse(result.Success, decline + " must not report success");
                Assert.IsTrue(seen.Add(result.UserMessage),
                    decline + " shares its sentence with another outcome");
            }
        }

        [Test]
        public void TheBrowserDetailIsKeptForTheLogAndHiddenFromThePlayer()
        {
            const string detail = "One Tap not displayed: invalid_origin [origin: https://html5.itch.zone]";

            AuthResult result = Map(GoogleTokenProvider.GoogleDecline.Unavailable, detail);

            StringAssert.Contains(detail, result.TechnicalMessage);
            StringAssert.DoesNotContain("invalid_origin", result.UserMessage);
            StringAssert.DoesNotContain("[origin:", result.UserMessage);
        }

        [TestCase("unavailable", GoogleTokenProvider.GoogleDecline.Unavailable)]
        [TestCase("failed", GoogleTokenProvider.GoogleDecline.Failed)]
        [TestCase("cancelled", GoogleTokenProvider.GoogleDecline.Cancelled)]
        public void BridgeCodes_TranslateToTheirOutcome(string code, GoogleTokenProvider.GoogleDecline expected)
        {
            Assert.AreEqual(expected, GoogleTokenProvider.DeclineFromCode(code));
        }

        /// <summary>
        /// "fallback" is a handoff mid-flow (One Tap gave way to the Google button), not an outcome, so it
        /// must never be promoted to a failure of its own — a later code always supersedes it.
        /// </summary>
        [Test]
        public void FallbackCode_IsNotAnOutcome()
        {
            Assert.AreEqual(GoogleTokenProvider.GoogleDecline.Cancelled,
                GoogleTokenProvider.DeclineFromCode("fallback"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("something-the-bridge-invented-later")]
        public void AnUnrecognisedCode_FallsBackToTheCheapestSentence(string code)
        {
            Assert.AreEqual(GoogleTokenProvider.GoogleDecline.Cancelled,
                GoogleTokenProvider.DeclineFromCode(code));
        }
    }
}
