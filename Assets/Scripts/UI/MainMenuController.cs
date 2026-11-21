// Assets/Scripts/UI/MainMenuController.cs
using Overlink.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Overlink.UI
{
    /// <summary>
    /// The revamped main menu controller, wired to the authored <c>MainMenu.unity</c> scene.
    /// <para>
    /// All UI is authored in the scene (built from the Figma "OVERLINK — Main Menu" design).
    /// Button onClick listeners are wired in code from authored button references, so no UI is
    /// constructed at runtime.
    /// </para>
    /// </summary>
    public sealed partial class MainMenuController : MonoBehaviour
    {
        [Header("Account Display (auto-resolved from scene)")]
        [SerializeField] TextMeshProUGUI playerNameText;
        [SerializeField] Button btnProfile;
        [SerializeField] Button btnSignOut;

        [Header("Profile Panel (auto-resolved from scene)")]
        [SerializeField] GameObject profilePanel;
        [SerializeField] TextMeshProUGUI txtProfileUsername;
        [SerializeField] TextMeshProUGUI txtProfileTag;
        [SerializeField] TextMeshProUGUI txtProfileFullID;
        [SerializeField] Button btnCopyUsername;
        [SerializeField] Button btnCopyTag;
        [SerializeField] Button btnCopyID;
        [SerializeField] Button btnCloseProfile;
        [SerializeField] TextMeshProUGUI copyFeedback;

        // These are authored under the existing profile card. They are resolved by path so older
        // scene copies keep working, while the dialog can still apply one consistent visual treatment.
        TextMeshProUGUI profileTitle;
        Image profileCardImage;
        Outline profileCardOutline;
        LobbyManager lobby;

        void Awake()
        {
            // Resolve authored account elements by path when not assigned in the Inspector.
            ResolveAccountReferences();

            // Wire command buttons by path (authored in the scene, not constructed at runtime). The leaf
            // names carry no slash: Transform.Find splits on '/', so a slash inside a leaf name would make
            // the path unresolvable and the button silently dead.
            WireButton("Canvas/Title and commands/Play commands/Matchmaking command", OnPlayClicked);
            WireButton("Canvas/Title and commands/Play commands/VS AI command", OnVsAIClicked);
            WireButton("Canvas/Title and commands/Play commands/Binder command", OnBinderClicked);
            WireButton("Canvas/Title and commands/Play commands/Gacha command", OnGachaClicked);
            WireButton("Canvas/Title and commands/System commands/Settings command", OnSettingsClicked);
            WireButton("Canvas/Title and commands/System commands/Quit command", OnQuitClicked);
            WireButton("Canvas/Account overlay/Profile action", OnProfileClicked);
            WireButton("Canvas/Account overlay/Sign out action", OnSignOutClicked);

            // Set initial selection.
            var firstBtn = transform.Find("Canvas/Title and commands/Play commands/Matchmaking command")
                ?.GetComponent<Button>();
            if (EventSystem.current != null && firstBtn != null)
                EventSystem.current.SetSelectedGameObject(firstBtn.gameObject);

            WireProfileDialog();
        }

        void Start()
        {
            lobby = EnsureOnlineLobby();
            if (PlayerProfileService.Instance != null)
                PlayerProfileService.Instance.ProfileLoaded += OnProfileLoaded;
            if (AccountManager.Instance != null)
                AccountManager.Instance.PlayerIdentityChanged += OnIdentityChanged;

            UpdatePlayerDisplay();
        }

        void OnDestroy()
        {
            if (PlayerProfileService.Instance != null)
                PlayerProfileService.Instance.ProfileLoaded -= OnProfileLoaded;
            if (AccountManager.Instance != null)
                AccountManager.Instance.PlayerIdentityChanged -= OnIdentityChanged;
        }

        void WireButton(string path, UnityAction action)
        {
            var go = transform.Find(path);
            var button = go != null ? go.GetComponent<Button>() : null;
            if (button != null)
                button.onClick.AddListener(action);
            else
                Debug.LogWarning($"[MainMenu] Button not found at path '{path}'.");
        }

        void ResolveAccountReferences()
        {
            if (playerNameText == null)
            {
                var go = transform.Find("Canvas/Account overlay/Player account/Identity/Player name");
                playerNameText = go != null ? go.GetComponent<TextMeshProUGUI>() : null;
            }
            ConfigurePlayerNameLayout();
            if (btnProfile == null)
            {
                var go = transform.Find("Canvas/Account overlay/Profile action");
                btnProfile = go != null ? go.GetComponent<Button>() : null;
            }
            if (btnSignOut == null)
            {
                var go = transform.Find("Canvas/Account overlay/Sign out action");
                btnSignOut = go != null ? go.GetComponent<Button>() : null;
            }
            Transform profile = transform.Find("Canvas/Profile Panel");
            if (profilePanel == null && profile != null)
                profilePanel = profile.gameObject;

            ResolveProfileDialogReferences();
            StyleProfileDialog();
        }

        /// <summary>
        /// Keep the account identity as one deliberate line. This is also applied at runtime so an
        /// older scene copy cannot reintroduce TMP's narrow-box word wrapping and split a username into
        /// stacked fragments over the account metadata.
        /// </summary>
        void ConfigurePlayerNameLayout()
        {
            if (playerNameText == null) return;

            RectTransform textRect = playerNameText.rectTransform;
            RectTransform identityRect = textRect.parent as RectTransform;
            if (identityRect != null)
            {
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, identityRect.rect.width);
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 31.2f);
            }

            playerNameText.enableWordWrapping = false;
            playerNameText.overflowMode = TextOverflowModes.Ellipsis;
            playerNameText.enableAutoSizing = true;
            playerNameText.fontSizeMin = 10.5f;
            playerNameText.fontSizeMax = 19.2f;
            playerNameText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // ── Command handlers ────────────────────────────────────────────────────

        void OnPlayClicked()
        {
            lobby = EnsureOnlineLobby();
            var template = transform.Find("Canvas/Title and commands/Play commands/Matchmaking command")?.GetComponent<Button>();
            if (template != null) OnlineLobbyPopup.Show(lobby, template);
            else Debug.LogWarning("[MainMenu] Cannot open multiplayer: menu button template is missing.");
        }

        void OnVsAIClicked() => SceneLoader.Load(SceneIndex.Gameplay);

        static LobbyManager EnsureOnlineLobby()
        {
            return LobbyManager.EnsureInstance();
        }

        // The binder is the front door for collection and deck work: it hosts the collection grid and
        // the active-deck view, and from there opens the deck builder. The old standalone
        // "Deckbuilder" entry now points here so the menu does not grow a fifth command.
        void OnBinderClicked() => SceneLoader.Load(SceneIndex.Collection);

        void OnGachaClicked() => SceneLoader.Load(SceneIndex.Gacha);

        void OnSettingsClicked() => SceneLoader.Load(SceneIndex.Settings);

        void OnQuitClicked() => SceneLoader.Quit();

        void OnProfileClicked() => ShowProfilePanel();

        void OnSignOutClicked()
        {
            if (AccountManager.Instance == null) return;
            if (AccountManager.Instance.SignOut())
            {
                PlayerProfileService.Instance?.ClearProfile();
                AuthFlowController.Instance?.OnPlayerSignedOut();
            }
            else
            {
                Debug.LogWarning("[MainMenu] Sign-out refused: " + AccountManager.Instance.Status);
            }
        }

        // ── Account display ─────────────────────────────────────────────────────

        void OnProfileLoaded(PlayerProfile profile) => UpdatePlayerDisplay();

        void OnIdentityChanged(PlayerIdentity identity) => UpdatePlayerDisplay();

        void UpdatePlayerDisplay()
        {
            var profile = PlayerProfileService.Instance?.CurrentProfile;
            // Identity and profile completion are deliberately separate concerns. A profile can have a
            // usable callsign while a newer required field (currently email) still needs setup. The
            // previous IsComplete gate turned that valid identity into three blank fields. Prefer the
            // authoritative Cloud Save identity, then use AccountManager's mirrored identity for the
            // short interval before/after a profile event or when an older scene entered directly.
            PlayerIdentity identity = profile != null && profile.Identity.IsValid
                ? profile.Identity
                : AccountManager.Instance != null
                    ? AccountManager.Instance.PlayerIdentity
                    : default;

            if (playerNameText != null)
                playerNameText.text = identity.IsValid ? identity.DisplayName : "Player";

            if (txtProfileUsername != null) txtProfileUsername.text = identity.IsValid ? identity.Username : "—";
            if (txtProfileTag != null) txtProfileTag.text = identity.IsValid ? identity.Tag : "—";
            if (txtProfileFullID != null)
                txtProfileFullID.text = identity.IsValid ? identity.FullId : "—";

            if (copyFeedback != null)
            {
                copyFeedback.canvasRenderer.SetAlpha(1f);
                copyFeedback.text = identity.IsValid
                    ? "IDENTITY LOADED  /  READY TO SHARE"
                    : "IDENTITY UNAVAILABLE  /  TRY AGAIN";
            }
        }

        void ShowProfilePanel()
        {
            if (profilePanel == null)
            {
                Debug.LogError("[MainMenu] Serialized Profile Panel is missing. Author it in MainMenu.unity; runtime construction is disabled.");
                return;
            }

            UpdatePlayerDisplay();
            SetPanelActive(profilePanel, true);
        }

        /// <summary>Wires the serialized profile dialog controls.</summary>
        void WireProfileDialog()
        {
            Button dismiss = profilePanel != null ? profilePanel.transform.Find("Scrim")?.GetComponent<Button>() : null;
            if (dismiss != null) dismiss.onClick.AddListener(HideProfilePanel);
            if (btnCloseProfile != null) btnCloseProfile.onClick.AddListener(HideProfilePanel);
            if (btnCopyUsername != null)
                btnCopyUsername.onClick.AddListener(() =>
                    CopyToClipboard(txtProfileUsername != null ? txtProfileUsername.text : null, "Username"));
            if (btnCopyTag != null)
                btnCopyTag.onClick.AddListener(() =>
                    CopyToClipboard(txtProfileTag != null ? txtProfileTag.text : null, "Tag"));
            if (btnCopyID != null)
                btnCopyID.onClick.AddListener(() =>
                    CopyToClipboard(txtProfileFullID != null ? txtProfileFullID.text : null, "Full ID"));
        }

        void ResolveProfileDialogReferences()
        {
            if (profilePanel == null) return;
            if (txtProfileUsername == null) txtProfileUsername = FindText(profilePanel.transform, "Card/Content/Username Row/Value");
            if (txtProfileTag == null) txtProfileTag = FindText(profilePanel.transform, "Card/Content/Tag Row/Value");
            if (txtProfileFullID == null) txtProfileFullID = FindText(profilePanel.transform, "Card/Content/Full ID Row/Value");
            if (btnCopyUsername == null) btnCopyUsername = FindButton(profilePanel.transform, "Card/Content/Username Row/Copy Button");
            if (btnCopyTag == null) btnCopyTag = FindButton(profilePanel.transform, "Card/Content/Tag Row/Copy Button");
            if (btnCopyID == null) btnCopyID = FindButton(profilePanel.transform, "Card/Content/Full ID Row/Copy Button");
            if (btnCloseProfile == null) btnCloseProfile = FindButton(profilePanel.transform, "Card/Content/Close Button");
            if (copyFeedback == null) copyFeedback = FindText(profilePanel.transform, "Card/Content/Copy Feedback");

            bool complete = profilePanel != null && txtProfileUsername != null && txtProfileTag != null
                && txtProfileFullID != null && btnCopyUsername != null && btnCopyTag != null
                && btnCopyID != null && btnCloseProfile != null && copyFeedback != null;
            if (!complete)
                Debug.LogError("[MainMenu] Serialized profile dialog is incomplete; check the MainMenu hierarchy.");
        }

        /// <summary>
        /// Polishes the authored dialog in place. This intentionally styles the existing Card/Content
        /// hierarchy; it does not create a second profile screen or replace any of its controls.
        /// Runtime styling also protects users opening a scene authored before the latest UI pass.
        /// </summary>
        void StyleProfileDialog()
        {
            if (profilePanel == null) return;

            Transform card = profilePanel.transform.Find("Card");
            Transform content = card != null ? card.Find("Content") : null;
            if (card == null || content == null) return;

            RectTransform cardRect = card as RectTransform;
            if (cardRect != null)
                cardRect.sizeDelta = new Vector2(500f, 500f);

            profileTitle = FindText(profilePanel.transform, "Card/Content/Title");
            if (profileTitle != null)
            {
                profileTitle.text = "<size=11><color=#29DFF2>PERSONAL SIGNAL  /  PLAYER IDENTITY</color></size>\nIDENTITY PROFILE";
                profileTitle.fontSize = 27f;
                profileTitle.color = new Color32(221, 232, 237, 255);
                profileTitle.characterSpacing = 2.5f;
                profileTitle.enableWordWrapping = false;
            }

            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                profileCardImage = cardImage;
                profileCardImage.color = new Color32(13, 28, 41, 255);
                profileCardImage.raycastTarget = true;
            }

            profileCardOutline = card.GetComponent<Outline>() ?? card.gameObject.AddComponent<Outline>();
            profileCardOutline.effectColor = new Color(0.16f, 0.87f, 0.95f, 0.58f);
            profileCardOutline.effectDistance = new Vector2(1.5f, -1.5f);
            profileCardOutline.useGraphicAlpha = true;

            StyleProfileRow(content, "Username Row", "CALLSIGN");
            StyleProfileRow(content, "Tag Row", "TAG");
            StyleProfileRow(content, "Full ID Row", "FULL ID");

            if (copyFeedback != null)
            {
                copyFeedback.fontSize = 11f;
                copyFeedback.characterSpacing = 1.2f;
                copyFeedback.alignment = TextAlignmentOptions.Center;
                copyFeedback.enableWordWrapping = false;
            }

            StyleProfileButton(btnCopyUsername);
            StyleProfileButton(btnCopyTag);
            StyleProfileButton(btnCopyID);
            StyleProfileButton(btnCloseProfile);
        }

        static void StyleProfileRow(Transform content, string rowName, string caption)
        {
            Transform row = content.Find(rowName);
            if (row == null) return;

            Image background = row.GetComponent<Image>() ?? row.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.16f, 0.21f, 0.92f);
            background.raycastTarget = false;

            TextMeshProUGUI label = FindText(row, "Caption");
            if (label != null)
            {
                label.text = caption;
                label.fontSize = 12f;
                label.characterSpacing = 1.2f;
                label.color = new Color32(130, 150, 169, 255);
                label.enableWordWrapping = false;
            }

            TextMeshProUGUI value = FindText(row, "Value");
            if (value != null)
            {
                value.fontSize = rowName == "Full ID Row" ? 16f : 19f;
                value.color = new Color32(221, 232, 237, 255);
                value.enableWordWrapping = false;
                value.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        static void StyleProfileButton(Button button)
        {
            if (button == null) return;
            RectTransform rect = button.transform as RectTransform;
            if (rect != null && button.gameObject.name == "Close Button")
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .08f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(150f, 42f);
            }
            else if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.89f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(74f, 30f);
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) return;
            label.enableWordWrapping = false;
            label.fontSize = button.gameObject.name == "Close Button" ? 13f : 11f;
            label.characterSpacing = 1.1f;
        }

        static Button FindButton(Transform root, string path)
        {
            return root.Find(path)?.GetComponent<Button>();
        }

        static TextMeshProUGUI FindText(Transform root, string path)
        {
            return root.Find(path)?.GetComponent<TextMeshProUGUI>();
        }

        void HideProfilePanel() => SetPanelActive(profilePanel, false);

        void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        void CopyToClipboard(string value, string what)
        {
            if (string.IsNullOrEmpty(value) || value == "—")
            {
                ReportCopy($"{what} is not available yet.");
                return;
            }

            GUIUtility.systemCopyBuffer = value;
            Debug.Log($"[MainMenu] Copied {what}: {value}");
            ReportCopy($"{what} copied.");
        }

        void ReportCopy(string message)
        {
            if (copyFeedback == null) return;
            copyFeedback.text = message;
            copyFeedback.canvasRenderer.SetAlpha(1f);
            copyFeedback.CrossFadeAlpha(0f, 1.6f, true);
        }
    }
}
