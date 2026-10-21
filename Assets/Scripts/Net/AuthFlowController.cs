using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.Net
{
    /// <summary>The stages of the mandatory login flow.</summary>
    public enum AuthState
    {
        /// <summary>Unity Services are still booting.</summary>
        Initializing,

        /// <summary>Checking for a session cached on this device.</summary>
        CheckingCachedSession,

        /// <summary>No usable session; the sign-in screen is showing.</summary>
        SignedOut,

        /// <summary>Signed in; fetching the Cloud Save profile to see if setup is needed.</summary>
        LoadingProfile,

        /// <summary>Signed in but the profile is incomplete; the setup screens are showing.</summary>
        NeedsProfileSetup,

        /// <summary>Signed in with a complete profile; protected scenes are reachable.</summary>
        Ready,

        /// <summary>An unrecoverable step failed. <see cref="AuthFlowController.Message"/> explains.</summary>
        Error,
    }

    /// <summary>
    /// Runs the mandatory login flow and owns every scene transition it causes.
    /// <para>
    /// The game always boots into the Auth scene. From there this controller decides between three
    /// outcomes: a cached session with a complete profile goes straight to MainMenu, a cached session
    /// without one stops at profile setup, and no session stops at sign-in. UI controllers only call
    /// into this class (<see cref="OnPlayerSignedIn"/>, <see cref="OnProfileSetupComplete"/>,
    /// <see cref="OnPlayerSignedOut"/>) — they never load a scene themselves, so there is exactly one
    /// place to look when wondering why a transition happened.
    /// </para>
    /// <para>
    /// Scene names are plain strings rather than <see cref="Overlink.UI.SceneIndex"/> constants because
    /// the UI assembly depends on this one and the reference would close a cycle.
    /// </para>
    /// </summary>
    public sealed class AuthFlowController : MonoBehaviour
    {
        public static AuthFlowController Instance { get; private set; }

        public AuthState CurrentState { get; private set; } = AuthState.Initializing;

        /// <summary>Player-facing text for the current state, used for the Error state and logging.</summary>
        public string Message { get; private set; } = "";

        public event Action<AuthState> StateChanged;

        const string AuthSceneName = "Auth";
        const string MainMenuSceneName = "MainMenu";

        bool isProcessing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateBeforeScene()
        {
            EnsureInstance();
        }

        /// <summary>
        /// Creates the controller if the boot hook has not run yet. The Auth screen calls this because
        /// it is the first UI to touch the flow and must not depend on script execution order.
        /// </summary>
        public static AuthFlowController EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AuthFlowController");
            DontDestroyOnLoad(go);
            return go.AddComponent<AuthFlowController>();
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
            _ = StartAuthenticationFlowAsync();
        }

        /// <summary>
        /// Entry point, run once at boot. Safe to call again after an <see cref="AuthState.Error"/>.
        /// </summary>
        public async Task StartAuthenticationFlowAsync()
        {
            if (isProcessing) return;
            isProcessing = true;

            try
            {
                ChangeState(AuthState.Initializing, "Initializing…");

                await AccountManager.Instance.EnsureInitializedAsync();

                ChangeState(AuthState.CheckingCachedSession, "Checking for a saved session…");

                if (AccountManager.Instance.IsSignedIn)
                {
                    Debug.Log("[AuthFlow] Cached session found.");
                    await HandleAuthenticatedPlayerAsync();
                }
                else
                {
                    Debug.Log("[AuthFlow] No cached session, showing sign-in.");
                    ChangeState(AuthState.SignedOut, "");
                    ShowAuthScene();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthFlow] Failed to start authentication flow: {e}");
                ChangeState(AuthState.Error, AuthResult.FromException(e).UserMessage);
                ShowAuthScene();
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>
        /// Called by the Auth UI after a successful sign-in. Loads the profile and either finishes
        /// setup or moves on to MainMenu.
        /// </summary>
        public async void OnPlayerSignedIn()
        {
            if (isProcessing) return;
            isProcessing = true;

            try
            {
                await HandleAuthenticatedPlayerAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthFlow] Error after sign-in: {e}");
                ChangeState(AuthState.Error, AuthResult.FromException(e).UserMessage);
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>Called by the Auth UI once the profile has been saved.</summary>
        public void OnProfileSetupComplete()
        {
            Debug.Log("[AuthFlow] Profile setup complete, loading main menu.");
            ChangeState(AuthState.Ready, "");
            LoadMainMenu();
        }

        /// <summary>Called by the Auth UI when the player abandons setup and signs out again.</summary>
        public void OnPlayerSignedOut()
        {
            PlayerProfileService.Instance.ClearProfile();
            ChangeState(AuthState.SignedOut, "");
            ShowAuthScene();
        }

        /// <summary>
        /// True when the player may enter a protected scene: signed in with a complete profile.
        /// <see cref="Overlink.UI.ProtectedSceneGuard"/> consults this on scene load.
        /// </summary>
        public bool IsAccessGranted => CurrentState == AuthState.Ready && AccountManager.Instance.IsSignedIn;

        void ShowAuthScene()
        {
            if (SceneManager.GetActiveScene().name != AuthSceneName)
            {
                SceneManager.LoadScene(AuthSceneName);
            }
        }

        void LoadMainMenu()
        {
            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                SceneManager.LoadScene(MainMenuSceneName);
            }
        }

        /// <summary>
        /// Loads the Cloud Save profile and routes the player to whichever of MainMenu or profile
        /// setup their state warrants.
        /// </summary>
        async Task HandleAuthenticatedPlayerAsync()
        {
            ChangeState(AuthState.LoadingProfile, "Loading your profile…");

            PlayerProfile profile = null;
            try
            {
                profile = await PlayerProfileService.Instance.LoadProfileAsync();
            }
            catch (Exception e)
            {
                // A profile we cannot read is treated as absent: the player re-runs setup rather
                // than being locked out by a transient Cloud Save failure.
                Debug.LogWarning($"[AuthFlow] Profile load failed, falling back to setup: {e}");
            }

            if (profile == null || !profile.IsComplete())
            {
                Debug.Log("[AuthFlow] Profile incomplete, showing setup.");
                ChangeState(AuthState.NeedsProfileSetup, "");
                ShowAuthScene();
            }
            else
            {
                Debug.Log("[AuthFlow] Profile complete, loading main menu.");
                AccountManager.Instance.ApplyIdentity(profile.username, profile.tag);
                ChangeState(AuthState.Ready, "");
                LoadMainMenu();
            }
        }

        void ChangeState(AuthState newState, string message)
        {
            Message = message ?? "";
            if (CurrentState == newState) return;

            Debug.Log($"[AuthFlow] State: {CurrentState} → {newState}{(string.IsNullOrEmpty(Message) ? "" : $" ({Message})")}");
            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
