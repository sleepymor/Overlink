using System;
using System.Threading.Tasks;
using Overlink.Net;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// The whole of the game's login front end: sign-in, registration, the age gate and the
    /// Username#Tag picker, in one screen.
    /// <para>
    /// This controller owns no policy. It collects input, asks <see cref="AccountManager"/> or
    /// <see cref="PlayerProfileService"/> to do the work, and hands the successful result to
    /// <see cref="AuthFlowController"/>, which is the only thing allowed to change scenes. That split
    /// is why there is no scene loading anywhere in this file.
    /// </para>
    /// <para>
    /// Every reference is optional. If none are assigned the screen builds itself at runtime from
    /// <c>AuthScreenController.Build.cs</c> (see <see cref="BuildRuntimeUi"/>), so the Auth scene works
    /// with nothing but an empty GameObject carrying this component, and a designer can later replace
    /// the generated look by wiring the fields in the Inspector without touching any code.
    /// </para>
    /// </summary>
    public sealed partial class AuthScreenController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject loadingPanel;
        [SerializeField] GameObject signInPanel;
        [SerializeField] GameObject setupPanel;
        [SerializeField] GameObject ageStep;
        [SerializeField] GameObject identityStep;

        [Header("Sign-in")]
        [SerializeField] TextMeshProUGUI loadingText;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] TextMeshProUGUI signInError;
        [SerializeField] TMP_InputField usernameInput;
        [SerializeField] TMP_InputField passwordInput;
        [SerializeField] TMP_InputField confirmPasswordInput;
        [SerializeField] Button googleButton;
        [SerializeField] Button submitButton;
        [SerializeField] Button toggleModeButton;
        [SerializeField] Button forgotPasswordButton;

        [Header("Age gate")]
        [SerializeField] TMP_InputField ageInput;
        [SerializeField] TextMeshProUGUI ageError;
        [SerializeField] Button ageContinueButton;

        [Header("Identity")]
        [SerializeField] TMP_InputField identityUsernameInput;
        [SerializeField] TMP_InputField identityTagInput;
        [SerializeField] TextMeshProUGUI identityPreview;
        [SerializeField] TextMeshProUGUI identityError;
        [SerializeField] Button randomTagButton;
        [SerializeField] Button saveIdentityButton;
        [SerializeField] Button signOutButton;

        [Header("Rules")]
        [Tooltip("Players must confirm at least this age to continue. Enforced server-side too once Cloud Code exists.")]
        [SerializeField, Min(0)] int minimumAge = 13;

        bool registerMode;
        bool busy;
        AuthFlowController flow;

        void Awake()
        {
            // The Auth scene is the game's first scene, so this component is often the very first
            // thing to touch the managers. Ask for them explicitly rather than hoping the
            // [RuntimeInitializeOnLoadMethod] hook already ran.
            AccountManager.EnsureInstance();
            AuthFlowController.EnsureInstance();
            PlayerProfileService.EnsureInstance();

            if (!IsWired()) BuildRuntimeUi();
        }

        void Start()
        {
            flow = AuthFlowController.Instance;
            if (flow != null) flow.StateChanged += OnAuthStateChanged;
            if (AccountManager.Instance != null) AccountManager.Instance.StatusChanged += OnAccountStatus;

            WireButtons();
            ApplyState(flow != null ? flow.CurrentState : AuthState.Initializing);
            SetBusy(false);
        }

        void OnDestroy()
        {
            if (flow != null) flow.StateChanged -= OnAuthStateChanged;
            if (AccountManager.Instance != null) AccountManager.Instance.StatusChanged -= OnAccountStatus;
        }

        void OnAuthStateChanged(AuthState state) => ApplyState(state);

        void OnAccountStatus(string status)
        {
            if (statusText != null && signInPanel != null && signInPanel.activeSelf)
                statusText.text = status;
        }

        /// <summary>Routes one auth-flow state to the panel that represents it.</summary>
        void ApplyState(AuthState state)
        {
            bool loading = state == AuthState.Initializing
                           || state == AuthState.CheckingCachedSession
                           || state == AuthState.LoadingProfile;

            SetPanel(loadingPanel, loading);
            SetPanel(signInPanel, state == AuthState.SignedOut || state == AuthState.Error);
            SetPanel(setupPanel, state == AuthState.NeedsProfileSetup);

            if (loading && loadingText != null)
            {
                loadingText.text = string.IsNullOrEmpty(flow?.Message) ? "Starting…" : flow.Message;
            }

            if (state == AuthState.Error && signInError != null)
            {
                signInError.text = flow != null && !string.IsNullOrEmpty(flow.Message)
                    ? flow.Message
                    : "Something went wrong. Please try again.";
                signInError.gameObject.SetActive(true);
            }

            if (state == AuthState.NeedsProfileSetup) EnterSetupFlow();
        }

        void SetPanel(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        // ---------------------------------------------------------------- sign in

        public async void OnGoogleClicked()
        {
            if (busy || AccountManager.Instance == null) return;
            SetBusy(true);
            ClearError(signInError);

            AuthResult result = await AccountManager.Instance.SignInWithGoogleAsync();
            SetBusy(false);

            if (result.Success) flow?.OnPlayerSignedIn();
            else ShowError(signInError, result.UserMessage);
        }

        public async void OnSubmitClicked()
        {
            if (busy || AccountManager.Instance == null) return;

            string username = CurrentUsername();
            string password = CurrentPassword();

            if (registerMode)
            {
                // Unity's credential rules are stricter than a "passwords match" check, so the local
                // pass is deliberately shallow and the service's answer is the one the player reads.
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    ShowError(signInError, "Enter a username and a password.");
                    return;
                }
                if (password != ConfirmPassword())
                {
                    ShowError(signInError, "Passwords do not match.");
                    return;
                }
            }
            else if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError(signInError, "Enter your username and password.");
                return;
            }

            SetBusy(true);
            ClearError(signInError);

            AuthResult result = registerMode
                ? await AccountManager.Instance.SignUpWithUsernamePasswordAsync(username, password)
                : await AccountManager.Instance.SignInWithUsernamePasswordAsync(username, password);
            SetBusy(false);

            if (result.Success) flow?.OnPlayerSignedIn();
            else ShowError(signInError, result.UserMessage);
        }

        /// <summary>Flips the same form between "sign in" and "create account".</summary>
        public void OnToggleModeClicked()
        {
            registerMode = !registerMode;
            ClearError(signInError);

            if (confirmPasswordInput != null)
                confirmPasswordInput.gameObject.SetActive(registerMode);
            if (submitButton != null)
                submitButton.GetComponentInChildren<TextMeshProUGUI>().text =
                    registerMode ? "Create Account" : "Sign In";
            if (toggleModeButton != null)
                toggleModeButton.GetComponentInChildren<TextMeshProUGUI>().text =
                    registerMode ? "I already have an account" : "Create an account";
            if (titleText != null)
                titleText.text = registerMode ? "Create your account" : "Sign in";
            if (forgotPasswordButton != null)
                forgotPasswordButton.gameObject.SetActive(!registerMode);
        }

        // ---------------------------------------------------------------- setup

        /// <summary>
        /// Opens the setup steps at the right place: a brand-new account starts at the age gate, and a
        /// partially set-up one resumes straight at the identity step instead of asking twice.
        /// </summary>
        void EnterSetupFlow()
        {
            PlayerProfile draft = PlayerProfileService.Instance?.BeginSetup();
            bool ageDone = draft != null && draft.ageConfirmed;

            SetPanel(ageStep, !ageDone);
            SetPanel(identityStep, ageDone);
            RefreshIdentityPreview();
        }

        public void OnAgeContinueClicked()
        {
            ClearError(ageError);

            string raw = ageInput != null ? ageInput.text : "";
            if (!int.TryParse((raw ?? "").Trim(), out int age) || age <= 0 || age > 150)
            {
                ShowError(ageError, "Enter your age as a number.");
                return;
            }

            if (age < minimumAge)
            {
                ShowError(ageError, $"You must be at least {minimumAge} to play.");
                return;
            }

            PlayerProfile draft = PlayerProfileService.Instance?.DraftProfile;
            if (draft != null) draft.ageConfirmed = true;

            SetPanel(ageStep, false);
            SetPanel(identityStep, true);
            RefreshIdentityPreview();
        }

        public void OnRandomTagClicked()
        {
            if (identityTagInput != null)
                identityTagInput.text = PlayerIdentity.GenerateRandomTag();
            RefreshIdentityPreview();
        }

        public void OnIdentityFieldChanged(string _) => RefreshIdentityPreview();

        void RefreshIdentityPreview()
        {
            if (identityPreview == null) return;

            string username = identityUsernameInput != null ? identityUsernameInput.text.Trim() : "";
            string tag = (identityTagInput != null ? identityTagInput.text : "").ToUpperInvariant().Trim();

            identityPreview.text = string.IsNullOrEmpty(username) || string.IsNullOrEmpty(tag)
                ? "Name#Tag"
                : $"{username}#{tag}";
        }

        /// <summary>
        /// Validates the chosen identity locally, then commits the profile in one Cloud Save write.
        /// <para>
        /// Uniqueness is deliberately not checked here. Claiming a pair has to be atomic across every
        /// client, which only a Cloud Code endpoint can do; a client-side guess would be racy anyway.
        /// Two players can therefore hold the same Username#Tag until that endpoint exists.
        /// </para>
        /// </summary>
        public async void OnSaveIdentityClicked()
        {
            if (busy || PlayerProfileService.Instance == null) return;

            string username = identityUsernameInput != null ? identityUsernameInput.text.Trim() : "";
            string tag = (identityTagInput != null ? identityTagInput.text : "").ToUpperInvariant().Trim();

            ClearError(identityError);

            if (!PlayerIdentity.ValidateUsername(username, out string usernameError))
            {
                ShowError(identityError, usernameError);
                return;
            }
            if (!PlayerIdentity.ValidateTag(tag, out string tagError))
            {
                ShowError(identityError, tagError);
                return;
            }

            SetBusy(true);

            WarnIfUniquenessUnchecked();
            AuthResult result = await PlayerProfileService.Instance.CommitSetupAsync(username, tag);
            SetBusy(false);

            if (result.Success) flow?.OnProfileSetupComplete();
            else ShowError(identityError, result.UserMessage);
        }

        /// <summary>Leaves setup and drops the session, so the player can sign in as somebody else.</summary>
        public void OnSignOutClicked()
        {
            if (busy) return;
            if (AccountManager.Instance != null && AccountManager.Instance.SignOut())
                flow?.OnPlayerSignedOut();
        }

        public void OnForgotPasswordClicked()
        {
            ShowError(signInError, "Password resets are not available yet. Create a new account instead.");
        }

        // ---------------------------------------------------------------- plumbing

        string CurrentUsername() => usernameInput != null ? usernameInput.text.Trim() : "";

        string CurrentPassword() => passwordInput != null ? passwordInput.text : "";

        string ConfirmPassword() => confirmPasswordInput != null ? confirmPasswordInput.text : "";

        void ShowError(TextMeshProUGUI field, string message)
        {
            if (field == null) return;
            field.text = message;
            field.gameObject.SetActive(true);
        }

        void ClearError(TextMeshProUGUI field)
        {
            if (field == null) return;
            field.text = "";
            field.gameObject.SetActive(false);
        }

        /// <summary>
        /// Locks the form while an operation is in flight. Google sign-in leaves the app for a browser
        /// round trip, so without this a player can fire a second request while the first waits on
        /// the loopback callback and get a confusing "already in progress" failure.
        /// </summary>
        void SetBusy(bool value)
        {
            busy = value;
            SetInteractable(googleButton, !value);
            SetInteractable(submitButton, !value);
            SetInteractable(toggleModeButton, !value);
            SetInteractable(ageContinueButton, !value);
            SetInteractable(randomTagButton, !value);
            SetInteractable(saveIdentityButton, !value);
            SetInteractable(signOutButton, !value);

            if (usernameInput != null) usernameInput.interactable = !value;
            if (passwordInput != null) passwordInput.interactable = !value;
            if (confirmPasswordInput != null) confirmPasswordInput.interactable = !value;
            if (ageInput != null) ageInput.interactable = !value;
            if (identityUsernameInput != null) identityUsernameInput.interactable = !value;
            if (identityTagInput != null) identityTagInput.interactable = !value;
        }

        static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        void WireButtons()
        {
            onClick(googleButton, OnGoogleClicked);
            onClick(submitButton, OnSubmitClicked);
            onClick(toggleModeButton, OnToggleModeClicked);
            onClick(ageContinueButton, OnAgeContinueClicked);
            onClick(randomTagButton, OnRandomTagClicked);
            onClick(saveIdentityButton, OnSaveIdentityClicked);
            onClick(signOutButton, OnSignOutClicked);
            onClick(forgotPasswordButton, OnForgotPasswordClicked);

            onSubmit(passwordInput, OnSubmitClicked);
            onSubmit(confirmPasswordInput, OnSubmitClicked);
            onSubmit(ageInput, OnAgeContinueClicked);
            onSubmit(identityTagInput, OnSaveIdentityClicked);

            onEdit(identityUsernameInput, OnIdentityFieldChanged);
            onEdit(identityTagInput, OnIdentityFieldChanged);
        }

        // The UnityEvent AddListener overloads want UnityAction / UnityAction<T>, which a plain Action
        // delegate does not convert to, so each helper wraps rather than passes through.

        static void onClick(Button button, Action action)
        {
            if (button != null) button.onClick.AddListener(Throwing(action));
        }

        static void onSubmit(TMP_InputField field, Action action)
        {
            if (field != null) field.onSubmit.AddListener(_ => Throwing(action)());
        }

        static void onEdit(TMP_InputField field, Action<string> action)
        {
            if (field != null) field.onValueChanged.AddListener(value => action(value));
        }

        /// <summary>
        /// Wraps a handler so an unexpected exception surfaces in the log instead of silently killing
        /// the button's click event, which is otherwise indistinguishable from "the UI does nothing".
        /// </summary>
        static UnityAction Throwing(Action action) => () =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        };

        /// <summary>
        /// Logs once per play session that two players may claim the same Username#Tag, so the gap is
        /// visible in the editor console instead of being silently accepted.
        /// </summary>
        static void WarnIfUniquenessUnchecked()
        {
            if (uniquenessWarningShown) return;
            uniquenessWarningShown = true;
            Debug.Log("[Auth] Username#Tag uniqueness is not enforced — needs a Cloud Code reservation endpoint.");
        }

        static bool uniquenessWarningShown;
    }
}
