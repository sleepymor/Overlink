// Assets/Scripts/UI/MainMenuController.cs
using Overlink.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// The main menu: navigation, and the account surface the player uses to see their ID or leave it.
    /// <para>
    /// The six navigation buttons are authored in <c>Prefabs/MainMenu.prefab</c> and must stay wired
    /// there. The account widgets are the opposite case: they are optional, and when nothing is assigned
    /// <see cref="BuildAccountUi"/> (see the partial in <c>MainMenuController.Account.cs</c>) constructs
    /// them at runtime, so a fresh clone shows the signed-in player without an Inspector round trip.
    /// </para>
    /// </summary>
    public sealed partial class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button btnPlay;
        [SerializeField] Button btnDeckbuilder;
        [SerializeField] Button btnCollection;
        [SerializeField] Button btnSettings;
        [SerializeField] Button btnNetDemo;
        [SerializeField] Button btnQuit;

        [Header("Account Display")]
        [SerializeField] TextMeshProUGUI playerNameText;
        [SerializeField] Button btnProfile;
        [SerializeField] Button btnSignOut;

        [Header("Profile Panel")]
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
            if (playerNameText == null) BuildAccountUi();

            AddListener(btnPlay,        () => SceneLoader.Load(SceneIndex.Gameplay));
            AddListener(btnDeckbuilder, () => SceneLoader.Load(SceneIndex.Deckbuilder));
            AddListener(btnCollection,  () => SceneLoader.Load(SceneIndex.Collection));
            AddListener(btnSettings,    () => SceneLoader.Load(SceneIndex.Settings));
            AddListener(btnNetDemo,     () => SceneLoader.Load(SceneIndex.NetDemo));
            AddListener(btnQuit,        SceneLoader.Quit);

            AddOptional(btnProfile, ShowProfilePanel);
            AddOptional(btnSignOut, OnSignOutClicked);
            AddOptional(btnCopyUsername, () => CopyToClipboard(
                PlayerProfileService.Instance?.CurrentProfile?.username, "Username"));
            AddOptional(btnCopyTag, () => CopyToClipboard(
                PlayerProfileService.Instance?.CurrentProfile?.tag, "Tag"));
            AddOptional(btnCopyID, () => CopyToClipboard(
                PlayerProfileService.Instance?.CurrentProfile?.Identity.FullId, "Player ID"));
            AddOptional(btnCloseProfile, HideProfilePanel);
        }

        void Start()
        {
            if (PlayerProfileService.Instance != null)
                PlayerProfileService.Instance.ProfileLoaded += OnProfileLoaded;
            if (AccountManager.Instance != null)
                AccountManager.Instance.PlayerIdentityChanged += OnIdentityChanged;

            SetPanelActive(profilePanel, false);
            UpdatePlayerDisplay();
        }

        void OnDestroy()
        {
            if (PlayerProfileService.Instance != null)
                PlayerProfileService.Instance.ProfileLoaded -= OnProfileLoaded;
            if (AccountManager.Instance != null)
                AccountManager.Instance.PlayerIdentityChanged -= OnIdentityChanged;
        }

        void OnProfileLoaded(PlayerProfile profile) => UpdatePlayerDisplay();

        void OnIdentityChanged(PlayerIdentity identity) => UpdatePlayerDisplay();

        /// <summary>
        /// Pushes the loaded profile into the corner label and the detail rows. Every field is null
        /// checked because the authored prefab may wire some of them and not others.
        /// </summary>
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
            if (txtProfileFullID != null) txtProfileFullID.text = identity.IsValid ? identity.FullId : "—";
        }

        void ShowProfilePanel()
        {
            UpdatePlayerDisplay();
            SetPanelActive(profilePanel, true);
        }

        void HideProfilePanel() => SetPanelActive(profilePanel, false);

        void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        /// <summary>
        /// Copies one piece of the identity. Username, tag and full ID are separate actions on purpose:
        /// adding a friend needs the pair, but a support ticket usually needs only one of them.
        /// </summary>
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

        /// <summary>
        /// Ends the session and hands control back to the Auth scene. Refuses mid-match or mid-lobby:
        /// dropping the transport's credentials underneath a live connection leaves it half-authorised,
        /// which is a much worse failure than making the player click Leave first.
        /// </summary>
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

        void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(MainMenuController)}] A menu button is not wired.", this);
                return;
            }

            button.onClick.AddListener(action);
        }

        /// <summary>Wires an account control that may have been built at runtime, or absent entirely.</summary>
        void AddOptional(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        void OnValidate()
        {
            ValidateButton(btnPlay, nameof(btnPlay));
            ValidateButton(btnDeckbuilder, nameof(btnDeckbuilder));
            ValidateButton(btnCollection, nameof(btnCollection));
            ValidateButton(btnSettings, nameof(btnSettings));
            ValidateButton(btnNetDemo, nameof(btnNetDemo));
            ValidateButton(btnQuit, nameof(btnQuit));
        }

        void ValidateButton(Button button, string fieldName)
        {
            if (button == null)
                Debug.LogError($"[{nameof(MainMenuController)}] Assign {fieldName} in the Inspector.", this);
        }
    }
}
