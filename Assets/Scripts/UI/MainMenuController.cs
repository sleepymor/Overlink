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

        void Awake()
        {
            // Resolve authored account elements by path when not assigned in the Inspector.
            ResolveAccountReferences();

            // Wire command buttons by path (authored in the scene, not constructed at runtime). The leaf
            // names carry no slash: Transform.Find splits on '/', so a slash inside a leaf name would make
            // the path unresolvable and the button silently dead.
            WireButton("Canvas/Title and commands/Play commands/Matchmaking command", OnPlayClicked);
            WireButton("Canvas/Title and commands/Play commands/VS AI command", OnVsAIClicked);
            WireButton("Canvas/Title and commands/Play commands/Deckbuilder command", OnDeckbuilderClicked);
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
        }

        // ── Command handlers ────────────────────────────────────────────────────

        void OnPlayClicked() => SceneLoader.Load(SceneIndex.Gameplay);

        void OnVsAIClicked() => SceneLoader.Load(SceneIndex.Deckbuilder);

        void OnDeckbuilderClicked() => SceneLoader.Load(SceneIndex.Collection);

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
            var identity = profile != null && profile.IsComplete()
                ? profile.Identity
                : default;

            if (playerNameText != null)
                playerNameText.text = identity.IsValid ? identity.DisplayName : "Player";

            if (txtProfileUsername != null) txtProfileUsername.text = identity.Username;
            if (txtProfileTag != null) txtProfileTag.text = identity.Tag;
            if (txtProfileFullID != null)
                txtProfileFullID.text = identity.IsValid ? identity.FullId : "—";
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
            if (txtProfileUsername == null) txtProfileUsername = FindText(profilePanel.transform, "Username Row/Value");
            if (txtProfileTag == null) txtProfileTag = FindText(profilePanel.transform, "Tag Row/Value");
            if (txtProfileFullID == null) txtProfileFullID = FindText(profilePanel.transform, "Full ID Row/Value");
            if (btnCopyUsername == null) btnCopyUsername = FindButton(profilePanel.transform, "Username Row/Copy Button");
            if (btnCopyTag == null) btnCopyTag = FindButton(profilePanel.transform, "Tag Row/Copy Button");
            if (btnCopyID == null) btnCopyID = FindButton(profilePanel.transform, "Full ID Row/Copy Button");
            if (btnCloseProfile == null) btnCloseProfile = FindButton(profilePanel.transform, "Card/Content/Close Button");
            if (copyFeedback == null) copyFeedback = FindText(profilePanel.transform, "Card/Content/Copy Feedback");

            bool complete = profilePanel != null && txtProfileUsername != null && txtProfileTag != null
                && txtProfileFullID != null && btnCopyUsername != null && btnCopyTag != null
                && btnCopyID != null && btnCloseProfile != null && copyFeedback != null;
            if (!complete)
                Debug.LogError("[MainMenu] Serialized profile dialog is incomplete; check the MainMenu hierarchy.");
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
            if (string.IsNullOrEmpty(value))
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
