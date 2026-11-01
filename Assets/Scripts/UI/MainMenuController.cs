// Assets/Scripts/UI/MainMenuController.cs
using Overlink.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

            BindMenuBackdrop();

            StyleButton(btnPlay, new Color(0.11f, 0.42f, 0.78f), new Color(0.16f, 0.58f, 0.86f));
            StyleButton(btnDeckbuilder, new Color(0.035f, 0.105f, 0.16f), new Color(0.055f, 0.20f, 0.27f));
            StyleButton(btnCollection, new Color(0.035f, 0.105f, 0.16f), new Color(0.055f, 0.20f, 0.27f));
            StyleButton(btnSettings, new Color(0.035f, 0.105f, 0.16f), new Color(0.055f, 0.20f, 0.27f));
            StyleButton(btnNetDemo, new Color(0.035f, 0.105f, 0.16f), new Color(0.055f, 0.20f, 0.27f));
            StyleButton(btnQuit, new Color(0.50f, 0.12f, 0.12f), new Color(0.72f, 0.20f, 0.16f));

            UiKit.EnsureButtonFeedback(btnPlay);
            UiKit.EnsureButtonFeedback(btnDeckbuilder);
            UiKit.EnsureButtonFeedback(btnCollection);
            UiKit.EnsureButtonFeedback(btnSettings);
            UiKit.EnsureButtonFeedback(btnNetDemo);
            UiKit.EnsureButtonFeedback(btnQuit);

            BuildMenuAtmosphere();

            if (EventSystem.current != null && btnPlay != null)
                EventSystem.current.SetSelectedGameObject(btnPlay.gameObject);

            AddListener(btnPlay,        () => SceneLoader.Load(SceneIndex.Gameplay));
            AddListener(btnDeckbuilder, () => SceneLoader.Load(SceneIndex.Deckbuilder));
            // The binder and pack-opening prototype are separate destinations. The binder owns the
            // Collection button; its OPEN PACK action routes to the dedicated Gacha scene.
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

        /// <summary>
        /// Keeps the authored renderer as the replaceable art slot, while also binding that same material to
        /// the full-screen UI surface. The latter is a deliberate fallback for URP camera/quad combinations
        /// where a background mesh can be skipped by a scene camera or render feature; it cannot affect input
        /// or match state and guarantees the procedural backdrop is visible behind the menu.
        /// </summary>
        static void BindMenuBackdrop()
        {
            GameObject backdrop = GameObject.Find("MenuBackdrop");
            Renderer backdropRenderer = backdrop != null ? backdrop.GetComponent<Renderer>() : null;
            if (backdropRenderer == null || backdropRenderer.sharedMaterial == null) return;

            Canvas canvas = UiKit.FindOverlayCanvas();
            if (canvas == null) return;

            Transform backgroundTransform = canvas.transform.Find("Background");
            Image background = backgroundTransform != null
                ? backgroundTransform.GetComponent<Image>()
                : null;
            if (background == null) return;

            background.material = backdropRenderer.sharedMaterial;
            background.color = Color.white;
            background.raycastTarget = false;
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

        static void StyleButton(Button button, Color normal, Color highlighted)
        {
            if (button == null) return;

            Graphic graphic = button.targetGraphic != null
                ? button.targetGraphic
                : button.GetComponent<Graphic>();
            if (graphic != null) graphic.color = normal;

            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = highlighted,
                pressedColor = UiKit.Tint(normal, 0.72f),
                selectedColor = UiKit.Tint(highlighted, 0.88f),
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f),
                colorMultiplier = 1f,
                fadeDuration = 0.10f
            };
        }

        /// <summary>
        /// Adds a small screen-space identity line without replacing authored menu geometry. The label is
        /// deliberately non-interactable and is safe to rebuild in a scene that already has the account UI.
        /// </summary>
        void BuildMenuAtmosphere()
        {
            Canvas canvas = UiKit.FindOverlayCanvas();
            if (canvas == null || canvas.transform.Find("Menu Atmosphere") != null) return;

            GameObject root = UiKit.Child("Menu Atmosphere", canvas.transform);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, 272f);
            rootRect.sizeDelta = new Vector2(520f, 54f);

            TextMeshProUGUI status = UiKit.Text("Status", root.transform,
                "TACTICAL NETWORK  //  LINK ESTABLISHED", 13f, UiKit.Accent,
                TextAlignmentOptions.Center, FontStyles.Bold, 22f);
            status.characterSpacing = 8f;

            GameObject rule = UiKit.Child("Signal Rule", root.transform);
            RectTransform ruleRect = rule.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0.5f, 0f);
            ruleRect.anchorMax = new Vector2(0.5f, 0f);
            ruleRect.pivot = new Vector2(0.5f, 0f);
            ruleRect.anchoredPosition = new Vector2(0f, -2f);
            ruleRect.sizeDelta = new Vector2(190f, 1f);
            Image ruleImage = rule.AddComponent<Image>();
            ruleImage.color = new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.55f);
            ruleImage.raycastTarget = false;
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
