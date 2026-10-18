using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Overlink.Net
{
    public enum AccountKind
    {
        SignedOut,
        Guest,
        Google,
    }

    /// <summary>
    /// Owns the player's Unity Authentication session. Guest accounts are anonymous and can
    /// be upgraded in place by linking Google, preserving the Unity player ID and cloud data.
    /// </summary>
    public sealed class AccountManager : MonoBehaviour
    {
        public static AccountManager Instance { get; private set; }

        public AccountKind Kind { get; private set; } = AccountKind.SignedOut;
        public string PlayerId => AuthenticationService.Instance != null
            ? AuthenticationService.Instance.PlayerId : "";
        public bool IsSignedIn => AuthenticationService.Instance != null
                                && AuthenticationService.Instance.IsSignedIn;
        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "Not signed in.";
        public string GoogleClientId => settings != null ? settings.clientId : "";

        public event Action<AccountKind> AccountChanged;
        public event Action<string> StatusChanged;

        [SerializeField] GoogleAuthSettings settings;

        Task initializationTask;
        Task operationTask;

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

            // The account controls are created at runtime so the existing demo scene does not
            // need fragile serialized references or a platform-specific UI prefab.
            if (GetComponent<AccountMenuUI>() == null) gameObject.AddComponent<AccountMenuUI>();
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
                initializationTask = null;
                throw;
            }
        }

        async Task RestoreCachedSessionAsync()
        {
            var auth = AuthenticationService.Instance;
            if (auth == null) return;
            if (!auth.IsSignedIn && auth.SessionTokenExists)
                await auth.SignInAnonymouslyAsync();
            if (!auth.IsSignedIn) return;

            Kind = AccountKind.Guest;
            try
            {
                await auth.GetPlayerInfoAsync();
                if (!string.IsNullOrEmpty(auth.PlayerInfo?.GetGoogleId())) Kind = AccountKind.Google;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Account] Could not inspect cached account type: {e.Message}");
            }
            SetStatus(Kind == AccountKind.Google ? "Signed in with Google." : "Guest session restored.");
            NotifyAccountChanged();
        }

        /// <summary>Signs in anonymously and keeps the account usable without Google.</summary>
        public Task PlayAsGuestAsync()
        {
            return RunOperationAsync(async () =>
            {
                await EnsureInitializedAsync();
                var auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync();
                Kind = AccountKind.Guest;
                SetStatus("Playing as guest.");
                NotifyAccountChanged();
            });
        }

        /// <summary>
        /// Gets a browser token and signs in, or links it to the current anonymous account.
        /// Linking is attempted first so guest progress is not silently abandoned.
        /// </summary>
        public Task SignInWithGoogleAsync()
        {
            return RunOperationAsync(async () =>
            {
                await EnsureInitializedAsync();
                if (NetBootstrap.Instance != null && NetBootstrap.Instance.IsListening)
                    throw new InvalidOperationException("Leave the current match before changing accounts.");
                if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
                    throw new InvalidOperationException("Leave the current lobby before changing accounts.");
                if (string.IsNullOrWhiteSpace(GoogleClientId))
                    throw new InvalidOperationException("Configure Resources/GoogleAuthSettings with a Google client ID first.");

                SetStatus("Opening Google sign-in…");
                string idToken = await GoogleTokenProvider.RequestIdTokenAsync(GoogleClientId);
                var auth = AuthenticationService.Instance;

                if (auth.IsSignedIn)
                {
                    if (auth.PlayerInfo == null) await auth.GetPlayerInfoAsync();
                    if (string.IsNullOrEmpty(auth.PlayerInfo?.GetGoogleId()))
                        await auth.LinkWithGoogleAsync(idToken);
                }
                else
                {
                    await auth.SignInWithGoogleAsync(idToken);
                }

                Kind = AccountKind.Google;
                SetStatus("Signed in with Google.");
                NotifyAccountChanged();
            });
        }

        public bool SignOut()
        {
            if (IsBusy) return false;
            if (NetBootstrap.Instance != null && NetBootstrap.Instance.IsListening)
            {
                SetStatus("Leave the current match before signing out.");
                return false;
            }
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
            {
                SetStatus("Leave the current lobby before signing out.");
                return false;
            }

            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);
            Kind = AccountKind.SignedOut;
            SetStatus("Signed out.");
            NotifyAccountChanged();
            return true;
        }

        /// <summary>Used by LobbyManager before creating or joining service objects.</summary>
        public async Task<bool> EnsureSignedInAsync()
        {
            await EnsureInitializedAsync();
            if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
                return true;
            SetStatus("Choose Continue with Google or Play as Guest first.");
            return false;
        }

        async Task RunOperationBody(Func<Task> operation)
        {
            IsBusy = true;
            try
            {
                await operation();
            }
            catch (Exception e)
            {
                SetStatus($"Account action failed: {e.Message}");
                Debug.LogWarning($"[Account] {e}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        Task RunOperationAsync(Func<Task> operation)
        {
            if (operationTask != null && !operationTask.IsCompleted) return operationTask;
            operationTask = RunOperationBody(operation);
            return operationTask;
        }

        void NotifyAccountChanged() => AccountChanged?.Invoke(Kind);

        void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
            Debug.Log($"[Account] {status}");
        }
    }
}
