using System;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace Overlink.Net
{
    /// <summary>
    /// The outcome of an authentication operation: whether it worked, and — when it did not — a
    /// message safe to show the player alongside the raw one worth putting in a log.
    /// <para>
    /// Authentication failures used to end up verbatim in a status label, which leaked SDK wording
    /// to players and gave support nothing structured to grep for. Every fallible path in
    /// <see cref="AccountManager"/> returns one of these instead of throwing across the UI boundary.
    /// </para>
    /// </summary>
    public sealed class AuthResult
    {
        public bool Success { get; private set; }
        public AuthErrorCode ErrorCode { get; private set; }

        /// <summary>Player-facing. Always set.</summary>
        public string UserMessage { get; private set; }

        /// <summary>Raw SDK text for logs and support. Empty on success.</summary>
        public string TechnicalMessage { get; private set; }

        public static AuthResult Ok()
        {
            return new AuthResult
            {
                Success = true,
                ErrorCode = AuthErrorCode.None,
                UserMessage = "",
                TechnicalMessage = ""
            };
        }

        public static AuthResult Fail(AuthErrorCode code, string userMessage, string technicalMessage = "")
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = code,
                UserMessage = userMessage,
                TechnicalMessage = technicalMessage
            };
        }

        /// <summary>Local validation failure that never reached the network.</summary>
        public static AuthResult InvalidInput(string message)
        {
            return Fail(AuthErrorCode.InvalidInput, message, message);
        }

        /// <summary>
        /// Translates an exception thrown by the Unity Authentication SDK.
        /// <para>
        /// The numeric code is preferred because it is stable; the message-substring pass below it
        /// exists for the username/password endpoints, which surface OAuth-style text ("invalid credentials",
        /// "username already exists") without a code of their own.
        /// </para>
        /// </summary>
        public static AuthResult FromException(Exception ex)
        {
            if (ex == null) return Fail(AuthErrorCode.Unknown, "An unexpected error occurred.", "");

            if (ex is RequestFailedException rfe)
            {
                var mapped = FromErrorCode(rfe.ErrorCode, ex.Message);
                if (mapped != null) return mapped;
            }

            // Operation cancelled by the player closing the browser tab / dismissing the prompt.
            if (ex is OperationCanceledException)
            {
                return Fail(AuthErrorCode.UserCancelled, "Sign-in was cancelled.", ex.Message);
            }

            // Google's own outcomes, checked before the substring pass below on purpose: several of its
            // sentences legitimately contain the words "password" or "timeout", and matching those
            // against the message would tell the player they picked a weak password or lost connectivity.
            if (ex is GoogleTokenProvider.GoogleDeclineException decline)
            {
                switch (decline.Decline)
                {
                    case GoogleTokenProvider.GoogleDecline.Unavailable:
                        // Google refused this host outright, or its UI could not be drawn here. Neither
                        // is something the player can fix, so lead with the path that definitely works.
                        // The word "password" is deliberate and load-bearing: GoogleDeclineMappingTests
                        // pins that the structured branch beats the substring pass below it.
                        return Fail(AuthErrorCode.ConfigError,
                            "Google sign-in isn't available on this host. Continue with a username and " +
                            "password, or try Google again later from the game's own page.", ex.Message);
                    case GoogleTokenProvider.GoogleDecline.Failed:
                        return Fail(AuthErrorCode.InvalidCredentials,
                            "Google sign-in didn't return a credential. Please try again.", ex.Message);
                    case GoogleTokenProvider.GoogleDecline.TimedOut:
                        return Fail(AuthErrorCode.UserCancelled,
                            "Google sign-in didn't finish in time. Please try again.", ex.Message);
                    default:
                        return Fail(AuthErrorCode.UserCancelled, "Google sign-in was cancelled.", ex.Message);
                }
            }

            string message = (ex.Message ?? "").ToLowerInvariant();

            if (message.Contains("invalid credentials") || message.Contains("wrong password")
                || message.Contains("invalid username") || message.Contains("invalid grant"))
            {
                return Fail(AuthErrorCode.InvalidCredentials, "Invalid username or password.", ex.Message);
            }
            if (message.Contains("already exists") || message.Contains("in use") || message.Contains("taken"))
            {
                return Fail(AuthErrorCode.AccountAlreadyExists, "That username is already taken.", ex.Message);
            }
            if (message.Contains("already linked"))
            {
                return Fail(AuthErrorCode.AccountAlreadyLinked,
                    "That Google account is already connected to a different account.", ex.Message);
            }
            if (message.Contains("cancel"))
            {
                return Fail(AuthErrorCode.UserCancelled, "Sign-in was cancelled.", ex.Message);
            }
            if (message.Contains("password"))
            {
                return Fail(AuthErrorCode.WeakPassword,
                    "That password does not meet the requirements.", ex.Message);
            }
            if (ex is System.Net.Sockets.SocketException
                || ex is System.Net.WebException
                || message.Contains("network") || message.Contains("connection")
                || message.Contains("timeout") || message.Contains("unreachable"))
            {
                return Fail(AuthErrorCode.NetworkError,
                    "Could not reach the service. Check your connection and try again.", ex.Message);
            }
            if (ex is InvalidOperationException)
            {
                // AccountManager raises these for its own guards (busy match, missing client id),
                // and their text is already written for the player.
                return Fail(AuthErrorCode.InvalidState, ex.Message, ex.Message);
            }

            return Fail(AuthErrorCode.Unknown, "Sign-in failed. Please try again.", ex.ToString());
        }

        /// <summary>
        /// Maps the SDK's numeric error codes to a result. Codes 10000–10010 are
        /// <c>AuthenticationErrorCodes</c>; 10102–10110 belong to the player-accounts (OAuth) surface.
        /// <para>
        /// An if-chain rather than a switch because those codes are <c>static readonly</c> fields, not
        /// compile-time constants. Returns null when the code says nothing useful and the caller should
        /// fall back to reading the message.
        /// </para>
        /// </summary>
        static AuthResult FromErrorCode(int code, string rawMessage)
        {
            if (code == AuthenticationErrorCodes.ClientInvalidUserState
                || code == AuthenticationErrorCodes.ClientNoActiveSession)
            {
                return Fail(AuthErrorCode.InvalidState,
                    "Your session expired. Please sign in again.", rawMessage);
            }
            if (code == AuthenticationErrorCodes.InvalidParameters)
            {
                return Fail(AuthErrorCode.InvalidInput,
                    "Check your username and password and try again.", rawMessage);
            }
            if (code == AuthenticationErrorCodes.AccountAlreadyLinked
                || code == AuthenticationErrorCodes.AccountLinkLimitExceeded)
            {
                return Fail(AuthErrorCode.AccountAlreadyLinked,
                    code == AuthenticationErrorCodes.AccountAlreadyLinked
                        ? "That Google account is already connected to a different account."
                        : "Too many accounts are connected to that provider.",
                    rawMessage);
            }
            if (code == AuthenticationErrorCodes.InvalidSessionToken)
            {
                return Fail(AuthErrorCode.SessionExpired,
                    "Your session is no longer valid. Please sign in again.", rawMessage);
            }
            if (code == AuthenticationErrorCodes.InvalidProvider)
            {
                return Fail(AuthErrorCode.ServiceUnavailable,
                    "This sign-in method is not available right now.", rawMessage);
            }
            if (code == AuthenticationErrorCodes.BannedUser)
            {
                return Fail(AuthErrorCode.AccountBanned,
                    "This account has been suspended. Contact support.", rawMessage);
            }
            if (code == AuthenticationErrorCodes.EnvironmentMismatch)
            {
                return Fail(AuthErrorCode.ServiceUnavailable,
                    "This build cannot use the saved session. Please sign in again.", rawMessage);
            }
            if (code == 10102 || code == 10103) // MissingClientId / InvalidClient
            {
                return Fail(AuthErrorCode.ConfigError,
                    "Sign-in is not configured correctly for this build.", rawMessage);
            }
            if (code == 10106) // InvalidGrant — an expired or replayed Google code / id token
            {
                return Fail(AuthErrorCode.InvalidCredentials,
                    "That Google sign-in is no longer valid. Please try again.", rawMessage);
            }
            if (code == 429)
            {
                return Fail(AuthErrorCode.RateLimited,
                    "Too many attempts. Please wait a moment and try again.", rawMessage);
            }
            if (code >= 400 && code < 500)
            {
                return Fail(AuthErrorCode.InvalidCredentials, "Invalid username or password.", rawMessage);
            }
            if (code >= 500)
            {
                return Fail(AuthErrorCode.ServiceUnavailable,
                    "The service is having trouble right now. Please try again later.", rawMessage);
            }

            return null;
        }
    }

    /// <summary>Why an authentication operation failed.</summary>
    public enum AuthErrorCode
    {
        /// <summary>No error.</summary>
        None,

        /// <summary>Credentials did not match any account.</summary>
        InvalidCredentials,

        /// <summary>The requested username is registered already.</summary>
        AccountAlreadyExists,

        /// <summary>The external provider is connected to another account.</summary>
        AccountAlreadyLinked,

        /// <summary>The player backed out of the provider prompt.</summary>
        UserCancelled,

        /// <summary>The request never reached the service.</summary>
        NetworkError,

        /// <summary>The service rejected a well-formed request for a temporary reason.</summary>
        ServiceUnavailable,

        /// <summary>Cloud Save could not read the profile.</summary>
        ProfileLoadFailed,

        /// <summary>Cloud Save could not write the profile.</summary>
        ProfileSaveFailed,

        /// <summary>Another player already owns this Username#Tag.</summary>
        IdentityTaken,

        /// <summary>Input failed local validation.</summary>
        InvalidInput,

        /// <summary>The session is no longer usable.</summary>
        InvalidState,

        /// <summary>The account is suspended.</summary>
        AccountBanned,

        /// <summary>The project or build is missing provider configuration.</summary>
        ConfigError,

        /// <summary>Too many attempts in quick succession.</summary>
        RateLimited,

        /// <summary>Password fails the service's strength rules.</summary>
        WeakPassword,

        /// <summary>Saved session is no longer valid.</summary>
        SessionExpired,

        /// <summary>Nothing above matched.</summary>
        Unknown,
    }
}
