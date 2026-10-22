using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

namespace Overlink.Net
{
    /// <summary>
    /// Reads and writes the player's profile in Unity Cloud Save.
    /// <para>
    /// This is the only authoritative store for the <c>Username#Tag</c> identity and the age
    /// confirmation. Neither may live in PlayerPrefs: the age gate is a compliance requirement, so it
    /// has to follow the account to whatever device the player signs in on, and the identity is how
    /// other players find them.
    /// </para>
    /// <para>
    /// Setup is staged rather than incremental. <see cref="BeginSetup"/> opens an in-memory draft, the
    /// age and identity screens edit it, and <see cref="CommitSetupAsync"/> writes it in a single
    /// <c>SaveAsync</c>. One write means there is no window where a profile exists with an age but no
    /// name — a half-saved profile would leave the player stuck between the two screens forever.
    /// </para>
    /// </summary>
    public sealed class PlayerProfileService : MonoBehaviour
    {
        public static PlayerProfileService Instance { get; private set; }

        public const string KeyUsername = "profile_username";
        public const string KeyTag = "profile_tag";
        public const string KeyAgeConfirmed = "profile_age_confirmed";
        public const string KeyEmail = "profile_email";
        public const string KeyEmailVerified = "profile_email_verified";
        public const string KeyCompleted = "profile_completed";
        public const string KeyVersion = "profile_version";
        public const string KeyCreatedAt = "profile_created_at";

        static readonly HashSet<string> AllKeys = new HashSet<string>
        {
            KeyUsername, KeyTag, KeyAgeConfirmed, KeyEmail, KeyEmailVerified,
            KeyCompleted, KeyVersion, KeyCreatedAt
        };

        /// <summary>The profile loaded from the cloud, or null when signed out / never set up.</summary>
        public PlayerProfile CurrentProfile { get; private set; }

        /// <summary>The draft being filled in during first-time setup. Null outside setup.</summary>
        public PlayerProfile DraftProfile { get; private set; }

        public bool IsReady { get; private set; }
        public bool IsLoading { get; private set; }
        public string Status { get; private set; } = "Waiting for authentication.";

        public event Action<PlayerProfile> ProfileLoaded;
        public event Action<string> StatusChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateBeforeScene()
        {
            EnsureInstance();
        }

        /// <summary>Creates the service if the boot hook has not run yet.</summary>
        public static PlayerProfileService EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PlayerProfileService");
            DontDestroyOnLoad(go);
            return go.AddComponent<PlayerProfileService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (AccountManager.Instance != null)
                AccountManager.Instance.AccountChanged += OnAccountChanged;
        }

        void OnDestroy()
        {
            if (AccountManager.Instance != null)
                AccountManager.Instance.AccountChanged -= OnAccountChanged;
        }

        void OnAccountChanged(AccountKind kind)
        {
            // Covers sign-out, the discarded guest session, and switching accounts: in every case
            // the previous player's data must stop being readable from the new one's screens.
            if (kind != AccountKind.UsernamePassword && kind != AccountKind.Google)
                ClearProfile();
        }

        public void ClearProfile()
        {
            CurrentProfile = null;
            DraftProfile = null;
            IsReady = false;
            SetStatus("Profile cleared.");
        }

        /// <summary>
        /// Loads the signed-in player's profile from Cloud Save.
        /// Returns null when the account has no profile yet, which is what first-time setup keys off.
        /// Throws when the read itself failed, so callers can distinguish "new player" from "service down".
        /// </summary>
        public async Task<PlayerProfile> LoadProfileAsync()
        {
            if (AccountManager.Instance == null || !AccountManager.Instance.IsSignedIn)
                throw new InvalidOperationException("Cannot load a profile while signed out.");

            IsLoading = true;
            SetStatus("Loading profile…");
            try
            {
                IDictionary<string, Item> data =
                    await CloudSaveService.Instance.Data.Player.LoadAsync(AllKeys);

                if (data == null || data.Count == 0)
                {
                    CurrentProfile = null;
                    IsReady = true;
                    SetStatus("No profile yet — first-time setup.");
                    return null;
                }

                var profile = new PlayerProfile
                {
                    username = ReadString(data, KeyUsername),
                    tag = ReadString(data, KeyTag),
                    ageConfirmed = ReadBool(data, KeyAgeConfirmed),
                    email = ReadString(data, KeyEmail),
                    emailVerified = ReadBool(data, KeyEmailVerified),
                    profileCompleted = ReadBool(data, KeyCompleted),
                    profileVersion = ReadInt(data, KeyVersion, PlayerProfile.CurrentVersion),
                    createdAt = ReadLong(data, KeyCreatedAt, 0)
                };

                CurrentProfile = profile;
                IsReady = true;
                SetStatus(profile.IsComplete()
                    ? $"Profile loaded: {profile.username}#{profile.tag}"
                    : "Profile is incomplete.");
                ProfileLoaded?.Invoke(profile);
                return profile;
            }
            catch (CloudSaveException e)
            {
                IsReady = false;
                SetStatus($"Profile load error: {e.Message}");
                throw new AuthUnavailableException(AuthErrorCode.ProfileLoadFailed,
                    "Could not load your profile.", e.Message, e);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Starts (or resumes) first-time setup. The draft seeds from any partial profile already in
        /// the cloud so a player who quit mid-setup on a previous run does not lose the age step.
        /// </summary>
        public PlayerProfile BeginSetup()
        {
            DraftProfile = CurrentProfile != null
                ? CurrentProfile.Clone()
                : new PlayerProfile();
            return DraftProfile;
        }

        /// <summary>
        /// Writes the setup draft to Cloud Save and promotes it to the live profile.
        /// The caller is responsible for validating the identity first — see
        /// <see cref="PlayerIdentity.ValidateUsername"/> and <see cref="PlayerIdentity.ValidateTag"/>.
        /// The address is validated here rather than by the caller because it is the one field with a
        /// policy of its own and more than one screen may eventually submit it.
        /// </summary>
        public async Task<AuthResult> CommitSetupAsync(string username, string tag, string email)
        {
            if (AccountManager.Instance == null || !AccountManager.Instance.IsSignedIn)
                return AuthResult.Fail(AuthErrorCode.InvalidState, "You are no longer signed in.");

            if (!PlayerIdentity.ValidateUsername(username, out string usernameError))
                return AuthResult.InvalidInput(usernameError);

            tag = (tag ?? "").ToUpperInvariant();
            if (!PlayerIdentity.ValidateTag(tag, out string tagError))
                return AuthResult.InvalidInput(tagError);

            if (!EmailPolicy.Validate(email, out string emailError))
                return AuthResult.InvalidInput(emailError);

            PlayerProfile draft = DraftProfile ?? new PlayerProfile();
            draft.username = username;
            draft.tag = tag;
            draft.ageConfirmed = true;
            draft.profileCompleted = true;
            draft.profileVersion = PlayerProfile.CurrentVersion;

            string normalized = EmailPolicy.Normalize(email);
            // Verification belongs to the address, not to the player. Editing an address revokes the
            // proof that came with the old one, which matters the moment anything sets this to true:
            // without the reset, a verified-but-unused flag would survive a change of address and
            // silently claim proof that was never given for the new one.
            if (draft.email != normalized) draft.emailVerified = false;
            draft.email = normalized;

            if (draft.createdAt == 0)
                draft.createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            SetStatus("Saving profile…");
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(ToSaveData(draft));
            }
            catch (CloudSaveException e)
            {
                Debug.LogError($"[PlayerProfileService] Save failed: {e}");
                SetStatus("Could not save your profile.");
                return AuthResult.Fail(AuthErrorCode.ProfileSaveFailed,
                    "We could not save your profile. Check your connection and try again.", e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerProfileService] Save failed: {e}");
                return AuthResult.FromException(e);
            }

            CurrentProfile = draft;
            DraftProfile = null;
            IsReady = true;
            AccountManager.Instance.ApplyIdentity(draft.username, draft.tag);
            SetStatus($"Profile saved: {draft.username}#{draft.tag}");
            ProfileLoaded?.Invoke(draft);
            return AuthResult.Ok();
        }

        /// <summary>
        /// Persists a change to the live profile (used by settings screens later). Setup uses
        /// <see cref="CommitSetupAsync"/> instead so the write stays single-shot.
        /// </summary>
        public async Task<AuthResult> SaveProfileAsync(PlayerProfile profile)
        {
            if (profile == null) return AuthResult.InvalidInput("No profile to save.");
            if (AccountManager.Instance == null || !AccountManager.Instance.IsSignedIn)
                return AuthResult.Fail(AuthErrorCode.InvalidState, "You are no longer signed in.");

            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(ToSaveData(profile));
                CurrentProfile = profile;
                AccountManager.Instance.ApplyIdentity(profile.username, profile.tag);
                SetStatus("Profile saved.");
                return AuthResult.Ok();
            }
            catch (CloudSaveException e)
            {
                return AuthResult.Fail(AuthErrorCode.ProfileSaveFailed,
                    "We could not save your profile.", e.Message);
            }
        }

        static Dictionary<string, object> ToSaveData(PlayerProfile profile) => new Dictionary<string, object>
        {
            { KeyUsername, profile.username ?? "" },
            { KeyTag, profile.tag ?? "" },
            { KeyAgeConfirmed, profile.ageConfirmed },
            { KeyEmail, profile.email ?? "" },
            { KeyEmailVerified, profile.emailVerified },
            { KeyCompleted, profile.profileCompleted },
            { KeyVersion, profile.profileVersion },
            { KeyCreatedAt, profile.createdAt },
        };

        // Cloud Save round-trips numbers through JSON, so an integer written as a long can come back
        // typed as a double. Each reader falls back through the numeric types before giving up rather
        // than throwing and taking the whole profile down with it.

        static string ReadString(IDictionary<string, Item> data, string key)
        {
            if (!data.TryGetValue(key, out var field) || field?.Value == null) return "";
            try { return field.Value.GetAs<string>() ?? ""; }
            catch { return ""; }
        }

        static bool ReadBool(IDictionary<string, Item> data, string key)
        {
            if (!data.TryGetValue(key, out var field) || field?.Value == null) return false;
            try { return field.Value.GetAs<bool>(); }
            catch { return false; }
        }

        static int ReadInt(IDictionary<string, Item> data, string key, int fallback)
        {
            long value = ReadLong(data, key, fallback);
            return value is >= int.MinValue and <= int.MaxValue ? (int)value : fallback;
        }

        static long ReadLong(IDictionary<string, Item> data, string key, long fallback)
        {
            if (!data.TryGetValue(key, out var field) || field?.Value == null) return fallback;
            try { return field.Value.GetAs<long>(); }
            catch
            {
                try { return (long)field.Value.GetAs<double>(); }
                catch { return fallback; }
            }
        }

        void SetStatus(string message)
        {
            Status = message;
            Debug.Log($"[PlayerProfileService] {message}");
            StatusChanged?.Invoke(message);
        }
    }

    /// <summary>
    /// Raised when Cloud Save could not serve a profile at all, as opposed to reporting that none
    /// exists yet. <see cref="AuthFlowController"/> turns it into a retryable error state.
    /// </summary>
    public sealed class AuthUnavailableException : Exception
    {
        public AuthErrorCode Code { get; }
        public string TechnicalMessage { get; }

        public AuthUnavailableException(AuthErrorCode code, string message, string technical, Exception inner)
            : base(message, inner)
        {
            Code = code;
            TechnicalMessage = technical;
        }
    }
}
