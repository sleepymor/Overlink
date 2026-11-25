using System;
using System.Threading.Tasks;
using Overlink.Core;
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
    /// <para>All panels and controls are serialized in <c>Auth.unity</c>. Runtime code only binds
    /// account state and listeners; it never constructs the screen foundation.</para>
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
        [SerializeField] TextMeshProUGUI passwordHint;
        [SerializeField] Button googleButton;
        [SerializeField] TextMeshProUGUI googleDivider;
        [SerializeField] TextMeshProUGUI googleUnavailableNote;
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
        [SerializeField] TMP_InputField identityEmailInput;
        [SerializeField] TextMeshProUGUI identityPreview;
        [SerializeField] TextMeshProUGUI identityError;
        [SerializeField] Button randomTagButton;
        [SerializeField] Button saveIdentityButton;
        [SerializeField] Button signOutButton;

        [Header("Starter faction")]
        [Tooltip("Optional authored panel. When absent, the controller creates a small setup picker at runtime.")]
        [SerializeField] GameObject factionStep;
        [SerializeField] TextMeshProUGUI factionError;

        [Header("Email code")]
        [SerializeField] GameObject emailCodeStep;
        [SerializeField] TextMeshProUGUI emailCodeStatus;
        [SerializeField] TMP_InputField emailCodeInput;
        [SerializeField] TextMeshProUGUI emailCodeError;
        [SerializeField] Button emailCodeSendButton;
        [SerializeField] Button emailCodeVerifyButton;
        [SerializeField] Button emailCodeSignOutButton;

        [Header("Rules")]
        [Tooltip("Players must confirm at least this age to continue. Enforced server-side too once Cloud Code exists.")]
        [SerializeField, Min(0)] int minimumAge = 13;

        bool registerMode;
        bool busy;
        AuthFlowController flow;
        Faction selectedStarterFaction = Faction.Fsti;
        Button[] starterFactionButtons;
        Button versionActionButton;

        void Awake()
        {
            // The Auth scene is the game's first scene, so this component is often the very first
            // thing to touch the managers. Ask for them explicitly rather than hoping the
            // [RuntimeInitializeOnLoadMethod] hook already ran.
            AccountManager.EnsureInstance();
            AuthFlowController.EnsureInstance();
            PlayerProfileService.EnsureInstance();
            CloudPlayerDataService.EnsureInstance();

            EnsureStarterFactionUi();

            if (!IsWired())
            {
                Debug.LogError("[Auth] Serialized Auth UI is incomplete. Author Auth.unity before entering Play mode.");
                return;
            }
        }

        /// <summary>True when every serialized control the controller depends on is assigned.</summary>
        bool IsWired()
        {
            return loadingPanel != null && signInPanel != null && setupPanel != null
                && ageStep != null && identityStep != null && loadingText != null
                && titleText != null && statusText != null && signInError != null
                && usernameInput != null && passwordInput != null && confirmPasswordInput != null
                && submitButton != null && toggleModeButton != null && forgotPasswordButton != null
                && ageInput != null && ageError != null && ageContinueButton != null
                && identityUsernameInput != null && identityTagInput != null && identityEmailInput != null
                && identityPreview != null && identityError != null && randomTagButton != null
                && saveIdentityButton != null && signOutButton != null;
        }

        void Start()
        {
            flow = AuthFlowController.Instance;
            if (flow != null) flow.StateChanged += OnAuthStateChanged;
            if (AccountManager.Instance != null) AccountManager.Instance.StatusChanged += OnAccountStatus;

            WireButtons();
            CreateVersionAction();
            ApplyProviderAvailability();
            ApplyState(flow != null ? flow.CurrentState : AuthState.Initializing);
            SetBusy(false);
        }

        /// <summary>
        /// Swaps the Google affordance for whichever state this build is in.
        /// <para>
        /// Google is a desktop-only path at the moment (see <see cref="AccountManager.SupportsGoogle"/>),
        /// and the web build is the one where showing the button would hurt: a player who clicks a
        /// prominent <b>Continue with Google</b> in an itch.io embed waits out a three-minute deadline
        /// for nothing. But an absent button is its own bad answer, because the player who already has a
        /// Google account cannot tell why it is missing — so the button is not hidden, it is replaced.
        /// One of the two is always on screen, never both and never neither.
        /// </para>
        /// </summary>
        void ApplyProviderAvailability()
        {
            bool google = AccountManager.SupportsGoogle;
            if (googleButton != null) googleButton.gameObject.SetActive(google);
            // The "— or use your Overlink account —" line only means anything with something above it.
            if (googleDivider != null) googleDivider.gameObject.SetActive(google);
            if (googleUnavailableNote != null) googleUnavailableNote.gameObject.SetActive(!google);
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
                           || state == AuthState.LoadingProfile
                           || state == AuthState.VersionCheckFailed
                           || state == AuthState.UpdateRequired;

            SetPanel(loadingPanel, loading);
            SetPanel(signInPanel, state == AuthState.SignedOut || state == AuthState.Error);
            SetPanel(setupPanel, state == AuthState.NeedsProfileSetup);
            SetPanel(emailCodeStep, state == AuthState.NeedsEmailCode);
            if (state == AuthState.NeedsEmailCode) EnterEmailCodeStep();

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

            if (versionActionButton != null)
            {
                bool retry = state == AuthState.VersionCheckFailed;
                bool update = state == AuthState.UpdateRequired && !string.IsNullOrEmpty(flow?.UpdateUrl);
                versionActionButton.gameObject.SetActive(retry || update);
                var label = versionActionButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = retry ? "RETRY" : "UPDATE";
            }

            if (state == AuthState.NeedsProfileSetup) EnterSetupFlow();
        }

        void CreateVersionAction()
        {
            if (loadingPanel == null || submitButton == null) return;
            versionActionButton = Instantiate(submitButton, loadingPanel.transform);
            versionActionButton.name = "Version Action";
            versionActionButton.onClick = new Button.ButtonClickedEvent();
            versionActionButton.onClick.AddListener(() =>
            {
                if (flow == null) return;
                if (flow.CurrentState == AuthState.VersionCheckFailed)
                    _ = flow.StartAuthenticationFlowAsync();
                else if (flow.CurrentState == AuthState.UpdateRequired && !string.IsNullOrEmpty(flow.UpdateUrl))
                    Application.OpenURL(flow.UpdateUrl);
            });
            var rect = versionActionButton.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.35f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            versionActionButton.gameObject.SetActive(false);
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
            // The rules are worth exactly one glance while choosing a password, and are noise when
            // typing one that already exists.
            if (passwordHint != null)
                passwordHint.gameObject.SetActive(registerMode);
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

            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud != null && cloud.IsReady && cloud.State != null && !cloud.State.starterGranted)
            {
                SetPanel(ageStep, false);
                SetPanel(identityStep, false);
                ShowStarterFactionPicker();
                return;
            }

            // Carrying the saved identity back into the form. This was cosmetic while the only way in
            // here was a player who quit mid-setup having typed nothing, and it stopped being cosmetic
            // the moment an address became part of IsComplete(): a profile from before that rule is now
            // bounced into this step with a real name#tag already on the account. Shown an empty form,
            // that player cannot tell "confirm the name you have" from "pick a new one", and a retyped
            // name silently overwrites the identity their friends added them by.
            if (draft != null)
            {
                if (identityUsernameInput != null) identityUsernameInput.text = draft.username ?? "";
                if (identityTagInput != null) identityTagInput.text = draft.tag ?? "";
                if (identityEmailInput != null) identityEmailInput.text = draft.email ?? "";
            }

            SetPanel(ageStep, !ageDone);
            SetPanel(identityStep, ageDone);
            SetPanel(factionStep, false);
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
            SetPanel(factionStep, false);
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
            string email = identityEmailInput != null ? identityEmailInput.text : "";

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
            // Validated here for the player's sake, and again inside CommitSetupAsync for the data's:
            // the message they see is the specific one either way, but the service must never be the
            // only thing standing between a typo and a permanent record.
            if (!EmailPolicy.Validate(email, out string emailError))
            {
                ShowError(identityError, emailError);
                return;
            }

            SetBusy(true);

            WarnIfUniquenessUnchecked();
            AuthResult result = await PlayerProfileService.Instance.CommitSetupAsync(username, tag, email);
            SetBusy(false);

            if (!result.Success)
            {
                SetBusy(false);
                ShowError(identityError, result.UserMessage);
                return;
            }

            // Profile identity and starter grant are separate authoritative records. Do not route to the
            // protected menu until the player has selected a faction and the Cloud Code grant succeeded.
            SetBusy(false);
            string savedEmail = PlayerProfileService.Instance.CurrentProfile?.email ?? email;
            if (flow != null && await flow.OnEmailAddressSavedAsync(savedEmail)) return;
            ShowStarterFactionPicker();
        }

        void EnsureStarterFactionUi()
        {
            if (factionStep == null)
            {
                factionStep = new GameObject("Starter Faction Step", typeof(RectTransform));
                factionStep.transform.SetParent(setupPanel.transform, false);
                RectTransform rect = factionStep.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.08f, .18f);
                rect.anchorMax = new Vector2(.92f, .82f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;

                GameObject titleObject = new GameObject("Title", typeof(RectTransform));
                titleObject.transform.SetParent(factionStep.transform, false);
                TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
                title.text = "CHOOSE YOUR STARTING FACTION";
                title.alignment = TextAlignmentOptions.Center;
                title.fontSize = 22;
                Stretch(title.rectTransform, new Vector2(0, .72f), new Vector2(1, 1));

                GameObject rowObject = new GameObject("Faction Buttons", typeof(RectTransform));
                rowObject.transform.SetParent(factionStep.transform, false);
                RectTransform row = rowObject.GetComponent<RectTransform>();
                Stretch(row, new Vector2(0, .24f), new Vector2(1, .68f));

                factionError = new GameObject("Error", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                factionError.transform.SetParent(factionStep.transform, false);
                factionError.alignment = TextAlignmentOptions.Center;
                factionError.color = new Color(1f, .35f, .35f);
                Stretch(factionError.rectTransform, new Vector2(0, 0), new Vector2(1, .2f));

                starterFactionButtons = new Button[3];
                Faction[] factions = { Faction.Fsti, Faction.Frti, Faction.Fpb };
                for (int i = 0; i < factions.Length; i++)
                {
                    GameObject buttonObject = new GameObject(factions[i].ToString(), typeof(RectTransform), typeof(Image), typeof(Button));
                    buttonObject.transform.SetParent(row, false);
                    RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                    buttonRect.anchorMin = new Vector2(i / 3f, 0);
                    buttonRect.anchorMax = new Vector2((i + 1) / 3f, 1);
                    buttonRect.offsetMin = new Vector2(6, 0);
                    buttonRect.offsetMax = new Vector2(-6, 0);

                    TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                    label.transform.SetParent(buttonObject.transform, false);
                    label.text = factions[i].ToString().ToUpperInvariant();
                    label.alignment = TextAlignmentOptions.Center;
                    Stretch(label.rectTransform, Vector2.zero, Vector2.one);

                    int captured = i;
                    Button button = buttonObject.GetComponent<Button>();
                    button.onClick.AddListener(() => OnStarterFactionClicked(factions[captured]));
                    starterFactionButtons[i] = button;
                }
            }

            factionStep.SetActive(false);
            UpdateStarterFactionButtons();
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void ShowStarterFactionPicker()
        {
            SetPanel(identityStep, false);
            SetPanel(factionStep, true);
            ClearError(factionError);
            UpdateStarterFactionButtons();
        }

        async void OnStarterFactionClicked(Faction faction)
        {
            if (busy) return;
            selectedStarterFaction = faction;
            UpdateStarterFactionButtons();
            SetBusy(true);
            ClearError(factionError);
            try
            {
                CloudPlayerDataService cloud = CloudPlayerDataService.Instance ?? CloudPlayerDataService.EnsureInstance();
                await cloud.EnsureStarterAsync(selectedStarterFaction);
                SetPanel(factionStep, false);
                flow?.OnProfileSetupComplete();
            }
            catch (Exception e)
            {
                ShowError(factionError, "Starter collection could not be granted. Check your connection and retry.");
                Debug.LogException(e);
            }
            finally
            {
                SetBusy(false);
            }
        }

        void UpdateStarterFactionButtons()
        {
            if (starterFactionButtons == null) return;
            Faction[] factions = { Faction.Fsti, Faction.Frti, Faction.Fpb };
            for (int i = 0; i < starterFactionButtons.Length; i++)
            {
                if (starterFactionButtons[i] == null) continue;
                Image image = starterFactionButtons[i].GetComponent<Image>();
                if (image != null)
                    image.color = factions[i] == selectedStarterFaction
                        ? new Color(.16f, .48f, .7f)
                        : new Color(.16f, .22f, .29f);
            }
        }

        /// <summary>Leaves setup and drops the session, so the player can sign in as somebody else.</summary>
        public void OnSignOutClicked()
        {
            if (busy) return;
            if (AccountManager.Instance != null && AccountManager.Instance.SignOut())
                flow?.OnPlayerSignedOut();
        }

        // ---------------------------------------------------------------- email code

        // Nothing is sent on entry. Each code uses one of the account's 5 sends per hour, so the step
        // waits for the player to press Send.
        void EnterEmailCodeStep()
        {
            ClearError(emailCodeError);
            if (emailCodeInput != null) emailCodeInput.text = "";
            if (emailCodeStatus != null)
                emailCodeStatus.text = "Press SEND to receive a 6-digit code at " + MaskEmail(CurrentProfileEmail()) + ".";
        }

        public void OnEmailCodeSendClicked() => _ = SendEmailCodeAsync();

        async Task SendEmailCodeAsync()
        {
            if (busy) return;
            string email = CurrentProfileEmail();
            if (string.IsNullOrEmpty(email))
            {
                ShowError(emailCodeError, "Add an email address to your profile first.");
                return;
            }

            SetBusy(true);
            ClearError(emailCodeError);
            try
            {
                CloudPlayerDataService cloud = CloudPlayerDataService.Instance ?? CloudPlayerDataService.EnsureInstance();
                await cloud.RequestEmailCodeAsync(email);
                if (emailCodeStatus != null)
                    emailCodeStatus.text = "We sent a 6-digit code to " + MaskEmail(email) + ".";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Auth] Email code request failed: {e.Message}");
                ShowError(emailCodeError, EmailCodeMessage(e));
            }
            finally
            {
                SetBusy(false);
            }
        }

        public async void OnEmailCodeVerifyClicked()
        {
            if (busy) return;
            string email = CurrentProfileEmail();
            string code = emailCodeInput != null ? emailCodeInput.text.Trim() : "";
            if (code.Length != 6 || !IsDigits(code))
            {
                ShowError(emailCodeError, "Enter the 6-digit code from your email.");
                return;
            }

            SetBusy(true);
            ClearError(emailCodeError);
            try
            {
                CloudPlayerDataService cloud = CloudPlayerDataService.Instance ?? CloudPlayerDataService.EnsureInstance();
                await cloud.VerifyEmailCodeAsync(email, code);
                SetBusy(false);
                flow?.OnEmailVerified();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Auth] Email code verification failed: {e.Message}");
                ShowError(emailCodeError, EmailCodeMessage(e));
            }
            finally
            {
                SetBusy(false);
            }
        }

        public void OnEmailCodeSignOutClicked() => OnSignOutClicked();

        static string CurrentProfileEmail() => PlayerProfileService.Instance?.CurrentProfile?.email ?? "";

        static string MaskEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 1) return email;
            return email.Substring(0, 2) + "***" + email.Substring(at);
        }

        static bool IsDigits(string value)
        {
            foreach (char c in value)
            {
                if (c < '0' || c > '9') return false;
            }
            return value.Length > 0;
        }

        // Only the code-step messages the module writes on purpose are shown as written. Anything else is
        // a transport or service fault, and gets the generic line so no SDK wording reaches the player.
        static readonly string[] EmailCodeMessagePrefixes =
        {
            "That code",
            "Too many",
            "Enter a valid email",
            "Add an email",
            "Email delivery",
        };

        static string EmailCodeMessage(Exception e)
        {
            string raw = e?.Message ?? "";
            foreach (string prefix in EmailCodeMessagePrefixes)
            {
                if (raw.StartsWith(prefix, StringComparison.Ordinal)) return raw;
            }
            return "Could not complete that step. Check your connection and try again.";
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
            SetInteractable(emailCodeSendButton, !value);
            SetInteractable(emailCodeVerifyButton, !value);
            SetInteractable(emailCodeSignOutButton, !value);

            if (emailCodeInput != null) emailCodeInput.interactable = !value;
            if (usernameInput != null) usernameInput.interactable = !value;
            if (passwordInput != null) passwordInput.interactable = !value;
            if (confirmPasswordInput != null) confirmPasswordInput.interactable = !value;
            if (ageInput != null) ageInput.interactable = !value;
            if (identityUsernameInput != null) identityUsernameInput.interactable = !value;
            if (identityTagInput != null) identityTagInput.interactable = !value;
            if (identityEmailInput != null) identityEmailInput.interactable = !value;
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
            onClick(emailCodeSendButton, OnEmailCodeSendClicked);
            onClick(emailCodeVerifyButton, OnEmailCodeVerifyClicked);
            onClick(emailCodeSignOutButton, OnEmailCodeSignOutClicked);
            onSubmit(emailCodeInput, OnEmailCodeVerifyClicked);

            onSubmit(passwordInput, OnSubmitClicked);
            onSubmit(confirmPasswordInput, OnSubmitClicked);
            onSubmit(ageInput, OnAgeContinueClicked);
            onSubmit(identityTagInput, OnSaveIdentityClicked);
            // The address is the last field in the step, so Enter has to finish it rather than do
            // nothing — and OnSaveIdentityClicked validates username and tag before the address, so a
            // player who hits Enter early gets the first real problem, not a misleading one.
            onSubmit(identityEmailInput, OnSaveIdentityClicked);

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
