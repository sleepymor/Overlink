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

        /// <summary>Signed in with an email that is not yet verified; the code step is showing.</summary>
        NeedsEmailCode,

        /// <summary>Signed in and verified, but the first-run tutorial has not been finished or skipped.</summary>
        NeedsTutorial,

        /// <summary>Signed in with a complete profile; protected scenes are reachable.</summary>
        Ready,

        /// <summary>An unrecoverable step failed. <see cref="AuthFlowController.Message"/> explains.</summary>
        Error,

        /// <summary>Compatibility check failed; a retry is required before continuing.</summary>
        VersionCheckFailed,

        /// <summary>The installed client is below the server's minimum supported version.</summary>
        UpdateRequired,
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
        public string UpdateUrl { get; private set; } = "";
        bool versionVerified;

        public event Action<AuthState> StateChanged;

        const string AuthSceneName = "Auth";
        const string MainMenuSceneName = "MainMenu";
        const string TutorialSceneName = "Tutorial";

        /// <summary>
        /// Standalone presentation scenes. They are opened straight from the Editor to review card
        /// art, so they must never start the authentication flow or the boot hook would bounce them
        /// back to the Main Menu.
        /// <para>
        /// Matched by prefix rather than exact name because <c>BuildCardPresentationPreviewScene</c>
        /// generates a family of these (one per card type, one per faction collection), and an exact
        /// list silently redirects every scene nobody remembered to add. Keep a prefix here if a new
        /// preview family is named outside these three.
        /// </para>
        /// </summary>
        static readonly string[] CardPreviewScenePrefixes =
        {
            "CardPresentation",
            "CardCollectionPreview",
            "CardFrameMatrixPreview",
        };

        static bool IsCardPreviewScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < CardPreviewScenePrefixes.Length; i++)
            {
                if (sceneName.StartsWith(CardPreviewScenePrefixes[i], System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        bool isProcessing;

        // Set by a password sign-in or account creation, and held until a code is verified. It is not
        // set by a cached session, so a relaunch does not ask again. It survives profile setup on purpose:
        // a new account has to finish setup, which collects the email, before the code can be sent.
        bool emailCodeRequired;

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
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsCardPreviewScene(scene.name)) return;
            Debug.Log("[AuthFlow] Card presentation preview loaded; authentication routing disabled.");
            ChangeState(AuthState.Ready, "Card presentation preview bypass");
        }

        void Start()
        {
            if (IsCardPreviewScene(SceneManager.GetActiveScene().name))
            {
                Debug.Log("[AuthFlow] Card presentation preview detected; authentication flow bypassed.");
                ChangeState(AuthState.Ready, "Card presentation preview bypass");
                return;
            }

            _ = StartAuthenticationFlowAsync();
        }

        /// <summary>
        /// Entry point, run once at boot. Safe to call again after an <see cref="AuthState.Error"/>.
        /// </summary>
        public async Task StartAuthenticationFlowAsync()
        {
            if (!Application.isPlaying) return;
            if (isProcessing) return;
            isProcessing = true;

            try
            {
                ChangeState(AuthState.Initializing, "Initializing…");

                await AccountManager.Instance.EnsureInitializedAsync();
                if (!Application.isPlaying) return;

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
                if (!Application.isPlaying) return;
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
            if (!Application.isPlaying) return;
            if (isProcessing) return;
            isProcessing = true;

            // Only a Unity-account password sign-in asks for a code. Google's own email_verified claim is
            // checked on the server, so it does not.
            emailCodeRequired = AccountManager.Instance != null
                && AccountManager.Instance.Kind == AccountKind.UsernamePassword;

            try
            {
                string googleIdToken = AccountManager.Instance != null
                    ? AccountManager.Instance.TakePendingGoogleIdToken()
                    : null;
                if (!string.IsNullOrEmpty(googleIdToken)) await VerifyGoogleEmailAsync(googleIdToken);

                await HandleAuthenticatedPlayerAsync();
            }
            catch (Exception e)
            {
                if (!Application.isPlaying) return;
                Debug.LogError($"[AuthFlow] Error after sign-in: {e}");
                ChangeState(AuthState.Error, AuthResult.FromException(e).UserMessage);
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>Called by the Auth UI once the profile has been saved.</summary>
        public async void OnProfileSetupComplete()
        {
            if (!versionVerified)
            {
                _ = StartAuthenticationFlowAsync();
                return;
            }

            string profileEmail = PlayerProfileService.Instance?.CurrentProfile?.email ?? "";
            if (emailCodeRequired || !await IsEmailVerifiedAsync(profileEmail))
            {
                Debug.Log("[AuthFlow] Profile setup complete, email code required.");
                ChangeState(AuthState.NeedsEmailCode, "");
                ShowAuthScene();
                return;
            }

            PlayerProfile profile = PlayerProfileService.Instance?.CurrentProfile;
            if (profile == null || !profile.IsComplete())
            {
                _ = StartAuthenticationFlowAsync();
                return;
            }

            await RouteToReadyAsync(profile);
        }

        /// <summary>
        /// Called by the identity step once the address is saved. The address is proven here, before the
        /// starter faction is picked. Returns false when the address needs no code.
        /// </summary>
        public async Task<bool> OnEmailAddressSavedAsync(string email)
        {
            if (!Application.isPlaying) return false;
            if (!emailCodeRequired && await IsEmailVerifiedAsync(email)) return false;

            ChangeState(AuthState.NeedsEmailCode, "");
            ShowAuthScene();
            return true;
        }

        /// <summary>
        /// Called by the code step once the server has accepted the code. Clears the sign-in requirement
        /// and re-runs the routing, so the player continues to the next step (or MainMenu) from there.
        /// </summary>
        public async void OnEmailVerified()
        {
            if (!Application.isPlaying) return;
            if (isProcessing) return;
            isProcessing = true;
            emailCodeRequired = false;

            try
            {
                await HandleAuthenticatedPlayerAsync();
            }
            catch (Exception e)
            {
                if (!Application.isPlaying) return;
                Debug.LogError($"[AuthFlow] Error after email verification: {e}");
                ChangeState(AuthState.Error, AuthResult.FromException(e).UserMessage);
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>
        /// Called by the tutorial once <c>tutorialCompleted</c> is saved. Re-runs the routing from the
        /// saved profile, so the player continues to MainMenu, or to any step still outstanding.
        /// </summary>
        public async void OnTutorialCompleted()
        {
            if (!Application.isPlaying) return;
            if (isProcessing) return;
            isProcessing = true;

            try
            {
                // The email gate was already passed before the tutorial was shown, and a replay runs from a
                // Ready session, so only the tutorial flag needs re-checking here.
                PlayerProfile profile = PlayerProfileService.Instance?.CurrentProfile;
                if (profile == null || !profile.IsComplete())
                {
                    await HandleAuthenticatedPlayerAsync();
                    return;
                }

                await RouteToReadyAsync(profile);
            }
            catch (Exception e)
            {
                if (!Application.isPlaying) return;
                Debug.LogError($"[AuthFlow] Error after tutorial: {e}");
                ChangeState(AuthState.Error, AuthResult.FromException(e).UserMessage);
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>Called by the Auth UI when the player abandons setup and signs out again.</summary>
        public void OnPlayerSignedOut()
        {
            emailCodeRequired = false;
            versionVerified = false;
            UpdateUrl = "";
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
            if (IsCardPreviewScene(SceneManager.GetActiveScene().name)) return;
            if (SceneManager.GetActiveScene().name != AuthSceneName)
            {
                SceneManager.LoadScene(AuthSceneName);
            }
        }

        void LoadMainMenu()
        {
            // Keep-rule: only the Auth scene itself routes onward to MainMenu. The flow controller is
            // a persistent service — resuming a signed-in session finishes its profile request
            // wherever the player (or the Editor) happens to be, and routing that continuation to
            // MainMenu would kick a directly opened protected scene (Settings, Collection,
            // Gameplay, the card previews) out from under verification. Normal boot starts in Auth
            // and still reaches MainMenu unchanged; so do sign-in and profile-setup completion,
            // which the Auth UI only ever calls from the Auth scene. Every other active scene is
            // left alone, which subsumes the old Gameplay and card-preview special cases.
            // The first-run tutorial is also part of the routing, so finishing it in its own scene
            // continues to MainMenu the same way.
            string active = SceneManager.GetActiveScene().name;
            if (active != AuthSceneName && active != TutorialSceneName) return;

            SceneManager.LoadScene(MainMenuSceneName);
        }

        /// <summary>
        /// Loads the Cloud Save profile and routes the player to whichever of MainMenu or profile
        /// setup their state warrants.
        /// </summary>
        async Task HandleAuthenticatedPlayerAsync()
        {
            if (!Application.isPlaying) return;
            versionVerified = false;
            ChangeState(AuthState.Initializing, "Checking game version…");
            ShowAuthScene();
            ClientVersionResponse version;
            try
            {
                var cloud = CloudPlayerDataService.Instance ?? CloudPlayerDataService.EnsureInstance();
                version = await cloud.CheckClientVersionAsync(Application.version);
                if (version == null || string.IsNullOrWhiteSpace(version.minimumVersion))
                    throw new InvalidOperationException("Version service returned an invalid response.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthFlow] Version check failed: {e}");
                ChangeState(AuthState.VersionCheckFailed, "Couldn't check the game version. Check your connection and try again.");
                return;
            }
            if (!Application.isPlaying) return;
            UpdateUrl = version.updateUrl ?? "";
            if (!version.supported)
            {
                ChangeState(AuthState.UpdateRequired, $"Please update Overlink to v{version.minimumVersion} or newer to continue.");
                return;
            }
            versionVerified = true;
            ChangeState(AuthState.LoadingProfile, "Loading your profile…");

            PlayerProfile profile = null;
            try
            {
                profile = await PlayerProfileService.Instance.LoadProfileAsync();
                if (!Application.isPlaying) return;
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
                if (emailCodeRequired || !await IsEmailVerifiedAsync(profile.email))
                {
                    // Fails closed: if the check cannot run, the player is asked for a code rather than
                    // let in. The code step is the only place that can clear this state.
                    Debug.Log("[AuthFlow] Email not verified, showing code step.");
                    ChangeState(AuthState.NeedsEmailCode, "");
                    ShowAuthScene();
                    return;
                }

                CloudPlayerState progression = await EnsureCloudProgressionAsync();
                if (!Application.isPlaying) return;
                if (progression == null || !progression.starterGranted)
                {
                    // A profile can be complete while the one-time starter grant is still pending
                    // (for example if the app closed between the two server writes). Keep the player
                    // in setup until a faction choice has been accepted by Cloud Code.
                    Debug.Log("[AuthFlow] Starter faction is not set, showing setup.");
                    ChangeState(AuthState.NeedsProfileSetup, "");
                    ShowAuthScene();
                    return;
                }
                AccountManager account = AccountManager.Instance ?? AccountManager.EnsureInstance();
                account.ApplyIdentity(profile.username, profile.tag);
                await RouteToReadyAsync(profile);
            }
        }

        /// <summary>
        /// The single place that decides the last step before the main menu. Both the cached-session path
        /// and the setup path reach it, so the email and tutorial gates cannot drift apart.
        /// </summary>
        async Task RouteToReadyAsync(PlayerProfile profile)
        {
            if (!profile.tutorialCompleted)
            {
                Debug.Log("[AuthFlow] Tutorial not completed, showing tutorial.");
                ChangeState(AuthState.NeedsTutorial, "");
                ShowTutorialScene();
                return;
            }

            Debug.Log("[AuthFlow] Profile complete, loading main menu.");
            ChangeState(AuthState.Ready, "");
            LoadMainMenu();
        }

        void ShowTutorialScene()
        {
            if (IsCardPreviewScene(SceneManager.GetActiveScene().name)) return;
            if (SceneManager.GetActiveScene().name != TutorialSceneName)
            {
                SceneManager.LoadScene(TutorialSceneName);
            }
        }

        // Failure is not fatal here. Without a verified record the routing check asks for a code, so the
        // player is never let in on the strength of a claim that did not verify.
        async Task VerifyGoogleEmailAsync(string idToken)
        {
            try
            {
                CloudPlayerDataService service = CloudPlayerDataService.Instance
                    ?? CloudPlayerDataService.EnsureInstance();
                await service.VerifyGoogleEmailAsync(idToken);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthFlow] Google email verification failed: {e.Message}");
            }
        }

        // True only when the server holds a verified record for this exact address. A change of address
        // since verification therefore sends the player back to the code step.
        async Task<bool> IsEmailVerifiedAsync(string profileEmail)
        {
            try
            {
                CloudPlayerDataService service = CloudPlayerDataService.Instance
                    ?? CloudPlayerDataService.EnsureInstance();
                EmailVerificationState state = await service.GetEmailVerifiedAsync();
                return state != null && state.verified
                    && string.Equals(state.email, profileEmail, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthFlow] Email verification check failed: {e.Message}");
                return false;
            }
        }

        async Task<CloudPlayerState> EnsureCloudProgressionAsync()
        {
            CloudPlayerDataService service = CloudPlayerDataService.Instance
                ?? CloudPlayerDataService.EnsureInstance();
            return await service.LoadAsync();
        }

        void ChangeState(AuthState newState, string message)
        {
            Message = message ?? "";
            if (CurrentState == newState)
            {
                StateChanged?.Invoke(newState);
                return;
            }

            Debug.Log($"[AuthFlow] State: {CurrentState} → {newState}{(string.IsNullOrEmpty(Message) ? "" : $" ({Message})")}");
            CurrentState = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
