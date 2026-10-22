using System;

namespace Overlink.Net
{
    /// <summary>
    /// The player's account-scoped profile, persisted in Cloud Save by
    /// <see cref="PlayerProfileService"/>.
    /// <para>
    /// Only what the login flow needs today: an identity, proof the age gate was passed, and a version
    /// stamp. The version is written from the first save onward so a later migration can tell a profile
    /// created before a field existed from one that simply left it empty — stats belong to that later
    /// step and are deliberately not modelled here yet.
    /// </para>
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        /// <summary>
        /// Schema version this build writes. Bumped to 2 when the email fields were added, so a profile
        /// created before that is distinguishable from one whose player simply left the field empty —
        /// the distinction a later migration or a support lookup needs.
        /// </summary>
        public const int CurrentVersion = 2;

        public string username = "";
        public string tag = "";
        public bool ageConfirmed;
        public bool profileCompleted;
        public int profileVersion = CurrentVersion;

        /// <summary>
        /// The player's contact address, lower-cased by <see cref="EmailPolicy.Normalize"/>.
        /// <para>
        /// Self-reported and <b>unverified</b>: Unity Authentication has no email field on the credential
        /// record and no way to send a message, so this is a promise the player made, not proof. Nothing
        /// security-sensitive may key off it — see the verification note in <c>docs/AUTH_FLOW.md</c>.
        /// </para>
        /// </summary>
        public string email = "";

        /// <summary>
        /// False for every address collected by this build. Reserved as the landing spot for a future
        /// verification step (email OTP, or the verified address that comes back in a Unity ID
        /// <c>IdToken</c>) so that step does not need another schema change.
        /// </summary>
        public bool emailVerified;

        /// <summary>Unix seconds at which the profile was first created; 0 until it is saved.</summary>
        public long createdAt;

        public PlayerIdentity Identity => new PlayerIdentity(username, tag);

        /// <summary>
        /// True when the player may enter protected scenes.
        /// <para>
        /// An address is part of that bar because the planned microtransactions need a way to reach the
        /// buyer, and an account we cannot contact is not an account that can spend money. The
        /// consequence is deliberate and was taken knowingly: a profile saved before this field existed
        /// is now incomplete, so an existing player is bounced back to the setup step once. Their name
        /// and age are already filled in and stay as they were, so the interruption is one field.
        /// </para>
        /// </summary>
        public bool IsComplete()
        {
            return !string.IsNullOrEmpty(username)
                && !string.IsNullOrEmpty(tag)
                && ageConfirmed
                && profileCompleted
                && !string.IsNullOrEmpty(email);
        }

        public PlayerProfile Clone() => new PlayerProfile
        {
            username = username,
            tag = tag,
            ageConfirmed = ageConfirmed,
            profileCompleted = profileCompleted,
            profileVersion = profileVersion,
            email = email,
            emailVerified = emailVerified,
            createdAt = createdAt
        };
    }
}
