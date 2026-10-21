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
        /// <summary>Schema version this build writes. Bump it when the shape of the profile changes.</summary>
        public const int CurrentVersion = 1;

        public string username = "";
        public string tag = "";
        public bool ageConfirmed;
        public bool profileCompleted;
        public int profileVersion = CurrentVersion;

        /// <summary>Unix seconds at which the profile was first created; 0 until it is saved.</summary>
        public long createdAt;

        public PlayerIdentity Identity => new PlayerIdentity(username, tag);

        /// <summary>True when the player may enter protected scenes.</summary>
        public bool IsComplete()
        {
            return !string.IsNullOrEmpty(username)
                && !string.IsNullOrEmpty(tag)
                && ageConfirmed
                && profileCompleted;
        }

        public PlayerProfile Clone() => new PlayerProfile
        {
            username = username,
            tag = tag,
            ageConfirmed = ageConfirmed,
            profileCompleted = profileCompleted,
            profileVersion = profileVersion,
            createdAt = createdAt
        };
    }
}
