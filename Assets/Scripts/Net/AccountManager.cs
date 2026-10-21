using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Overlink.Net
{
    /// <summary>How the current Unity Authentication session was established.</summary>
    public enum AccountKind
    {
        /// <summary>No session.</summary>
        SignedOut,

        /// <summary>Signed in through Google OAuth.</summary>
        Google,

        /// <summary>Signed in with Unity username &amp; password credentials.</summary>
        UsernamePassword,

        /// <summary>
        /// A legacy anonymous session left over from the removed guest flow. Never granted access
        /// to the game; <see cref="AccountManager.RestoreCachedSessionAsync"/> signs it out on sight.
        /// </summary>
        Guest,
    }

    /// <summary>
    /// Owns the player's Unity Authentication session and nothing else.
    /// <para>
    /// Login is mandatory: this class never creates a session by itself. It only restores whatever
    /// the Unity Authentication SDK cached on disk, and it rejects anonymous (guest) sessions on
    /// sight because the game has no guest tier.
    /// </para>
    /// <para>
    /// The player's <c>Username#Tag</c> identity deliberately does <b>not</b> live in Unity's player-name
    /// field. <c>GetPlayerNameAsync</c> auto-generates a name when none exists, so it cannot tell "unset"
    /// from "set", and <c>UpdatePlayerNameAsync</c> rejects whitespace, which the username grammar allows.
    /// Cloud Save owns the identity instead — see <see cref="PlayerProfileService"/> — and mirrors it here
    /// through <see cref="ApplyIdentity"/> purely so other systems can read it without another round trip.
    /// </para>
    /// </summary>
    public sealed class AccountManager : MonoBehaviour
    {
        public static AccountManager Instance { get; private set; }

        public AccountKind Kind { get; private set; } = AccountKind.SignedOut;

        public string PlayerId => AuthenticationService.Instance != null
            ? AuthenticationService.Instance.PlayerId : "";

        /// <summary>Display username from the Cloud Save profile. Empty until the profile loads.</summary>
        public string PlayerName { get; private set; } = "";

        /// <summary>Riot-style tag from the Cloud Save profile. Empty until the profile loads.</summary>
        public string PlayerTag { get; private set; } = "";

        public PlayerIdentity PlayerIdentity => new PlayerIdentity(PlayerName, PlayerTag);

        public bool IsSignedIn => AuthenticationService.Instance != null
                                && AuthenticationService.Instance.IsSignedIn;

        /// <summary>
        /// True when a session token from an earlier run is sitting in PlayerPrefs. Distinct from
        /// <see cref="IsSignedIn"/>: the token is loaded at initialization, but no session is live until
        /// <see cref="RestoreCachedSessionAsync"/> resumes it.
        /// </summary>
        public bool HasCachedSession => AuthenticationService.Instance != null
                                     && AuthenticationService.Instance.SessionTokenExists;

        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "Not signed in.";
        public string GoogleClientId => settings != null ? settings.clientId : "";

        public event Action<AccountKind> AccountChanged;
        public event Action<string> StatusChanged;
        public event Action<PlayerIdentity> PlayerIdentityChanged;

        [SerializeField] GoogleAuthSettings settings;

        Task initializationTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateBeforeScene()
        {
            EnsureInstance();
        }

        public static AccountManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AccountManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<AccountManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (settings == null) settings = Resources.Load<GoogleAuthSettings>("GoogleAuthSettings");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Task EnsureInitializedAsync()
        {
            if (initializationTask == null) initializationTask = InitializeAsync();
            return initializationTask;
        }

        async Task InitializeAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                await RestoreCachedSessionAsync();
            }
            catch
            {
                // Let a later retry re-run initialization instead of caching the failure forever.
                initializationTask = null;
                throw;
            }
        }

        /// <summary>
        /// Adopts a session the Unity Authentication SDK left in PlayerPrefs from a previous run.
        /// <para>
        /// The SDK does not do this by itself. <c>UnityServices.InitializeAsync()</c> loads the cached
        /// session token into <see cref="Unity.Services.Authentication.IAuthenticationService.SessionToken"/>
        /// but leaves <c>IsSignedIn</c> false — the only internal caller of that resume path is the
        /// refresh scheduler, which refuses to run until a session is already signed in. So the game has
        /// to ask for the resume explicitly, and the only public call that performs it is
        /// <c>SignInAnonymouslyAsync</c>: when a cached token exists it takes a
        /// "continuing existing session with cached token" branch and never touches the anonymous
        /// provider. Passing <c>CreateAccount = false</c> makes it fail rather than mint an anonymous
        /// account in the case where no token is there to resume, which is what keeps this compatible
        /// with the no-guest-accounts rule.
        /// </para>
        /// <para>
        /// The resumed account is then inspected, and anything that is not a real login — above all the
        /// anonymous sessions the removed guest flow used to leave behind — is discarded on sight.
        /// </para>
        /// </summary>
        async Task RestoreCachedSessionAsync()
        {
            var auth = AuthenticationService.Instance;
            if (auth == null)
            {
                Kind = AccountKind.SignedOut;
                return;
            }

            if (!auth.IsSignedIn && auth.SessionTokenExists)
            {
                try
                {
                    await auth.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
                }
                catch (Exception e)
                {
                    // A token the service no longer recognises. Drop it so the next start does not
                    // repeat the failure, and let the player sign in properly.
                    Debug.LogWarning($"[Account] Cached session could not be resumed: {e.Message}");
                    auth.SignOut(true);
                    Kind = AccountKind.SignedOut;
                    SetStatus("Please sign in again.");
                    NotifyAccountChanged();
                    return;
                }
            }

            if (!auth.IsSignedIn)
            {
                Kind = AccountKind.SignedOut;
                return;
            }

            try
            {
                await auth.GetPlayerInfoAsync();
            }
            catch (Exception e)
            {
                // Without player info we cannot prove this is a real login, and "prove it" is the
                // whole point of the mandatory-login rule, so the session is discarded.
                Debug.LogWarning($"[Account] Could not inspect resumed account: {e.Message}");
                auth.SignOut(true);
                Kind = AccountKind.SignedOut;
                SetStatus("Please sign in again.");
                NotifyAccountChanged();
                return;
            }

            Kind = Classify(auth.PlayerInfo);

            if (Kind == AccountKind.Guest)
            {
                Debug.Log("[Account] Discarding legacy anonymous guest session.");
                auth.SignOut(true);
                Kind = AccountKind.SignedOut;
                SetStatus("Guest accounts are no longer supported. Please sign in.");
                NotifyAccountChanged();
                return;
            }

            SetStatus(Kind == AccountKind.Google
                ? "Signed in with Google."
                : "Signed in with Username & Password.");
            NotifyAccountChanged();
        }

        /// <summary>
        /// Maps the account's provider links onto <see cref="AccountKind"/>.
        /// <para>
        /// Order matters. A Google link wins because that is the credential the player is most likely
        /// to be using; failing that, a non-null <c>PlayerInfo.Username</c> is a positive statement from
        /// the service that this is a username/password account, and it is preferred over inspecting the
        /// identity list. Only when neither is present does this fall back to reading the provider type
        /// ids, which mirror the SDK's endpoint stems (<c>anonymous</c>, <c>usernamepassword</c>).
        /// An account that matches nothing is reported as <see cref="AccountKind.Guest"/>, because an
        /// unrecognisable session is exactly the case the guest rule exists to catch.
        /// </para>
        /// </summary>
        static AccountKind Classify(Unity.Services.Authentication.PlayerInfo info)
        {
            if (info == null) return AccountKind.Guest;
            if (!string.IsNullOrEmpty(info.GetGoogleId())) return AccountKind.Google;
            if (!string.IsNullOrEmpty(info.Username)) return AccountKind.UsernamePassword;

            var identities = info.Identities;
            if (identities != null)
            {
                foreach (var identity in identities)
                {
                    string type = identity?.TypeId ?? "";
                    if (type.Length > 0
                        && type.IndexOf("anonymous", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return AccountKind.UsernamePassword;
                    }
                }
            }

            return AccountKind.Guest;
        }

        /// <summary>
        /// Gets a Google ID token through the OAuth loopback flow and signs in with it.
        /// <para>
        /// Any session already in place is signed out first rather than linked into. Linking was the
        /// original behaviour and it is why signing in with a Google account that had ever been linked
        /// before failed with "already linked to Google": the link is permanent from Unity's side, so
        /// the only sane operation on the Auth screen is "replace the current session".
        /// </para>
        /// </summary>
        public Task<AuthResult> SignInWithGoogleAsync()
        {
            return RunOperationWithResultAsync(async () =>
            {
                await EnsureInitializedAsync();
                GuardSessionChanges("changing accounts");
                if (string.IsNullOrWhiteSpace(GoogleClientId))
                    throw new InvalidOperationException(
                        "Configure Resources/GoogleAuthSettings with a Google OAuth Web Client ID first.");

                SetStatus("Opening Google sign-in…");

                string idToken = await GoogleTokenProvider.RequestIdTokenAsync(GoogleClientId);
                var auth = AuthenticationService.Instance;

                if (auth.IsSignedIn) auth.SignOut(true);

                await auth.SignInWithGoogleAsync(idToken);

                Kind = AccountKind.Google;
                ClearIdentity();
                SetStatus("Signed in with Google.");
                NotifyAccountChanged();
                return AuthResult.Ok();
            });
        }

        /// <summary>
        /// Creates a new Unity account with username &amp; password credentials.
        /// Unity's own credential rules apply on top of the UI's checks; violations come back
        /// translated through <see cref="AuthResult.FromException"/>.
        /// </summary>
        public Task<AuthResult> SignUpWithUsernamePasswordAsync(string username, string password)
        {
            return RunOperationWithResultAsync(async () =>
            {
                await EnsureInitializedAsync();
                GuardSessionChanges("creating an account");

                SetStatus("Creating account…");
                var auth = AuthenticationService.Instance;
                if (auth.IsSignedIn) auth.SignOut(true);

                await auth.SignUpWithUsernamePasswordAsync(username, password);

                Kind = AccountKind.UsernamePassword;
                ClearIdentity();
                SetStatus("Account created.");
                NotifyAccountChanged();
                return AuthResult.Ok();
            });
        }

        /// <summary>Signs an existing Unity account in with username &amp; password credentials.</summary>
        public Task<AuthResult> SignInWithUsernamePasswordAsync(string username, string password)
        {
            return RunOperationWithResultAsync(async () =>
            {
                await EnsureInitializedAsync();
                GuardSessionChanges("signing in");

                SetStatus("Signing in…");
                var auth = AuthenticationService.Instance;
                if (auth.IsSignedIn) auth.SignOut(true);

                await auth.SignInWithUsernamePasswordAsync(username, password);

                Kind = AccountKind.UsernamePassword;
                ClearIdentity();
                SetStatus("Signed in.");
                NotifyAccountChanged();
                return AuthResult.Ok();
            });
        }

        /// <summary>
        /// Changes the current account's password. Unity signs the player out everywhere else
        /// as a side effect, so the local session is ended too and the Auth screen takes over.
        /// </summary>
        public Task<AuthResult> UpdatePasswordAsync(string currentPassword, string newPassword)
        {
            return RunOperationWithResultAsync(async () =>
            {
                await EnsureInitializedAsync();
                if (!IsSignedIn)
                    throw new InvalidOperationException("You must be signed in to change your password.");

                SetStatus("Updating password…");
                await AuthenticationService.Instance.UpdatePasswordAsync(currentPassword, newPassword);

                SetStatus("Password updated. Please sign in again.");
                SignOut();
                return AuthResult.Ok();
            });
        }

        /// <summary>
        /// Ends the local session. Returns false (and explains why) when the player is still in a
        /// match or a lobby, because tearing the session down underneath a live connection leaves
        /// the transport half-authorised.
        /// </summary>
        public bool SignOut()
        {
            if (IsBusy)
            {
                SetStatus("Please wait for the current operation to finish.");
                return false;
            }

            string blocker = SessionChangeBlocker();
            if (blocker != null)
            {
                SetStatus(blocker);
                return false;
            }

            var auth = AuthenticationService.Instance;
            if (auth != null && auth.IsSignedIn) auth.SignOut(true);

            Kind = AccountKind.SignedOut;
            ClearIdentity();
            SetStatus("Signed out.");
            NotifyAccountChanged();
            return true;
        }

        /// <summary>Used by the Net layer before creating or joining service objects.</summary>
        public async Task<bool> EnsureSignedInAsync()
        {
            await EnsureInitializedAsync();
            if (IsSignedIn && Kind != AccountKind.Guest) return true;
            SetStatus("Sign in first.");
            return false;
        }

        /// <summary>
        /// Mirrors the Cloud Save profile's identity into this manager so listeners (lobby, presence,
        /// HUD) can read a player id without another network round trip. Not a store: Cloud Save owns
        /// the only authoritative copy.
        /// </summary>
        public void ApplyIdentity(string username, string tag)
        {
            PlayerName = username ?? "";
            PlayerTag = tag ?? "";
            PlayerIdentityChanged?.Invoke(new PlayerIdentity(PlayerName, PlayerTag));
        }

        void ClearIdentity() => ApplyIdentity("", "");

        /// <summary>Null when the session may be changed, otherwise the player-facing reason it may not.</summary>
        static string SessionChangeBlocker()
        {
            if (NetBootstrap.Instance != null && NetBootstrap.Instance.IsListening)
                return "Leave the current match before changing accounts.";
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
                return "Leave the current lobby before changing accounts.";
            return null;
        }

        void GuardSessionChanges(string verb)
        {
            string blocker = SessionChangeBlocker();
            if (blocker != null) throw new InvalidOperationException(blocker);
        }

        async Task<AuthResult> RunOperationWithResultAsync(Func<Task<AuthResult>> operation)
        {
            IsBusy = true;
            try
            {
                return await operation();
            }
            catch (Exception e)
            {
                var result = AuthResult.FromException(e);
                SetStatus(result.UserMessage);
                Debug.LogWarning($"[Account] {result.TechnicalMessage}");
                return result;
            }
            finally
            {
                IsBusy = false;
            }
        }

        void NotifyAccountChanged() => AccountChanged?.Invoke(Kind);

        void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
            Debug.Log($"[Account] {status}");
        }

        // ---- Device-local player data ----

        /// <summary>
        /// Saves a player-scoped string on this device only. Settings and local preferences belong
        /// here; anything the player would expect to follow them across devices belongs in Cloud Save.
        /// </summary>
        public void SaveLocalData(string key, string value)
        {
            PlayerPrefs.SetString($"Player_{PlayerId}_{key}", value);
            PlayerPrefs.Save();
        }

        /// <summary>Loads a device-local string written by <see cref="SaveLocalData"/>.</summary>
        public string LoadLocalData(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString($"Player_{PlayerId}_{key}", defaultValue);
        }

        /// <summary>Saves a player-scoped integer on this device only.</summary>
        public void SaveLocalInt(string key, int value)
        {
            PlayerPrefs.SetInt($"Player_{PlayerId}_{key}", value);
            PlayerPrefs.Save();
        }

        /// <summary>Loads a device-local integer written by <see cref="SaveLocalInt"/>.</summary>
        public int LoadLocalInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt($"Player_{PlayerId}_{key}", defaultValue);
        }
    }
}
