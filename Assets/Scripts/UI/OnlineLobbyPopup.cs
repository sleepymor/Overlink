using System;
using System.Collections.Generic;
using Overlink.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>Persistent, dismissible view over the existing lobby session.</summary>
    public sealed class OnlineLobbyPopup : MonoBehaviour
    {
        static OnlineLobbyPopup instance;
        LobbyManager lobby;
        Button template, queue, create, join, ready, leave, minimize, indicator;
        readonly Dictionary<string, Sprite> actionIcons = new Dictionary<string, Sprite>();
        readonly Dictionary<Button, ColorBlock> defaultButtonColors = new Dictionary<Button, ColorBlock>();
        readonly Dictionary<Button, Color> defaultTargetColors = new Dictionary<Button, Color>();
        static readonly Color ActionColor = new Color32(221, 232, 237, 255);
        static readonly Color WarningColor = new Color32(242, 190, 54, 255);
        static readonly Color DangerColor = new Color32(244, 105, 105, 255);
        static Material monochromeIconMaterial;

        enum ButtonTone
        {
            Normal,
            Warning,
            Danger
        }
        TMP_InputField code;
        TextMeshProUGUI title, status;
        GameObject dialog;
        float searchStarted;
        bool hadSearch;

        public static void Show(LobbyManager manager, Button menuButton)
        {
            if (manager == null || menuButton == null) return;
            if (instance == null)
            {
                var root = new GameObject("Online Lobby Overlay", typeof(RectTransform));
                DontDestroyOnLoad(root);
                instance = root.AddComponent<OnlineLobbyPopup>();
                instance.Build(manager, menuButton);
            }
            instance.Open();
        }

        void Build(LobbyManager manager, Button menuButton)
        {
            lobby = manager;

            // Capture the authored icon sprites before the popup is detached from the scene. The
            // popup clones the authored matchmaking button for its visual treatment, but every
            // action must have its own symbol instead of inheriting the swords icon.
            CaptureActionIcons(menuButton);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Clone the authored menu control, including its typography and selectable treatment.
            template = Instantiate(menuButton, transform);
            template.gameObject.name = "Menu Button Template";
            template.onClick = new Button.ButtonClickedEvent();
            template.gameObject.SetActive(false);

            dialog = Panel(transform, "Multiplayer Dialog", new Color(0, 0, 0, .75f));
            Stretch(dialog.GetComponent<RectTransform>());
            var card = Panel(dialog.transform, "Card", new Color32(13, 28, 41, 255));
            Place(card.GetComponent<RectTransform>(), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 760));
            var outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(.16f, .87f, .95f, .58f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            title = Label(card.transform, "Title", "MULTIPLAYER", 30, new Vector2(0, 325), new Vector2(550, 55));
            status = Label(card.transform, "Status", "Choose a mode", 17, new Vector2(0, 255), new Vector2(550, 70));
            queue = Action(card.transform, "MATCHMAKING", 160, Icon("matchmaking"), ButtonTone.Normal, () => { searchStarted = Time.realtimeSinceStartup; hadSearch = true; lobby.QuickPlay(); Refresh(); });
            create = Action(card.transform, "CREATE LOBBY", 65, Icon("create"), ButtonTone.Normal, () => lobby.CreateLobby());
            join = Action(card.transform, "JOIN LOBBY", -125, Icon("join"), ButtonTone.Normal, () => lobby.JoinLobbyByCode(code.text));
            code = CreateInput(card.transform);
            ready = Action(card.transform, "READY", -220, null, ButtonTone.Normal, () =>
            {
                if (lobby.GameplayPreparationFailed) lobby.RetryGameplayTransition();
                else lobby.SetReady(!lobby.LocalReady);
            });
            SetButtonCheckGlyph(ready);
            leave = Action(card.transform, "CANCEL", -220, Icon("leave"), ButtonTone.Danger, () => { if (lobby.CurrentLobby != null) lobby.LeaveLobby(); else lobby.CancelSearch(); });
            minimize = Action(card.transform, "CLOSE", -315, null, ButtonTone.Danger, Close);
            SetButtonCloseGlyph(minimize);
            indicator = Action(transform, "ONLINE", 0, null, ButtonTone.Normal, Open);
            Place((RectTransform)indicator.transform, new Vector2(1, 1), new Vector2(-180, -65), new Vector2(280, 65));
            lobby.StateChanged += Refresh;
            lobby.LobbyChanged += Refresh;
            lobby.StatusChanged += OnStatus;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Refresh();
        }

        void OnDestroy()
        {
            if (lobby != null)
            {
                lobby.StateChanged -= Refresh;
                lobby.LobbyChanged -= Refresh;
                lobby.StatusChanged -= OnStatus;
            }
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = scene.name != SceneIndex.Gameplay;
            if (scene.name == SceneIndex.Gameplay) dialog.SetActive(false);
            Refresh();
        }

        void OnStatus(string _)
        {
            // A minimized lobby must not hide the action needed to recover a failed save.
            if (lobby != null && lobby.GameplayPreparationFailed) dialog.SetActive(true);
            Refresh();
        }
        void Open()
        {
            dialog.SetActive(true);
            Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject((queue.gameObject.activeSelf ? queue : minimize).gameObject);
        }
        void Close() { dialog.SetActive(false); Refresh(); }
        void Update()
        {
            if (lobby != null && lobby.IsQuickPlay && hadSearch && lobby.State != LobbyState.Idle)
                RefreshStatus();
        }

        void Refresh()
        {
            if (lobby == null || dialog == null) return;
            bool idle = lobby.State == LobbyState.Idle;
            bool quick = lobby.IsQuickPlay;
            bool inLobby = lobby.CurrentLobby != null;
            queue.gameObject.SetActive(idle);
            create.gameObject.SetActive(idle);
            join.gameObject.SetActive(idle);
            code.gameObject.SetActive(idle);
            ready.gameObject.SetActive(lobby.GameplayPreparationFailed || (inLobby && !quick && lobby.State == LobbyState.InLobby));
            SetButtonText(ready, lobby.GameplayPreparationFailed ? "RETRY SAVE" : lobby.LocalReady ? "UNREADY" : "READY");
            leave.gameObject.SetActive(!idle);
            // Separate the two controls in a private lobby.
            if ((inLobby && !quick) || lobby.GameplayPreparationFailed) Place((RectTransform)leave.transform, new Vector2(.5f, .5f), new Vector2(0, -125), new Vector2(530, 80));
            else Place((RectTransform)leave.transform, new Vector2(.5f, .5f), new Vector2(0, -220), new Vector2(530, 80));
            SetButtonText(leave, inLobby && !quick ? "LEAVE LOBBY" : "CANCEL SEARCH");
            SetButtonText(minimize, idle ? "CLOSE" : "MINIMIZE");
            SetButtonTone(minimize, idle ? ButtonTone.Danger : ButtonTone.Warning);
            indicator.gameObject.SetActive(!dialog.activeSelf && !idle);
            RefreshStatus();
        }

        void RefreshStatus()
        {
            if (lobby == null || status == null) return;
            if (lobby.GameplayPreparationFailed) { status.text = lobby.Status; return; }
            if (lobby.IsQuickPlay && hadSearch && lobby.State == LobbyState.Searching)
            {
                var elapsed = TimeSpan.FromSeconds(Mathf.Max(0, Time.realtimeSinceStartup - searchStarted));
                status.text = $"SEARCHING FOR OPPONENTS  {elapsed.Minutes:00}:{elapsed.Seconds:00}\n{lobby.Status}";
            }
            else if (lobby.CurrentLobby != null)
            {
                string phase = lobby.State == LobbyState.Starting
                    ? "STARTING MATCH"
                    : lobby.State == LobbyState.InGame
                        ? "MATCH FOUND / CONNECTING"
                        : lobby.IsQuickPlay
                            ? "MATCH FOUND / HANDSHAKE"
                            : "LOBBY / READY CHECK";
                status.text = $"{phase}   •   {lobby.MemberCount}/{LobbyManager.MaxPlayers}\n{lobby.Status}";
            }
            else status.text = string.IsNullOrEmpty(lobby.Status) ? "CHOOSE A MODE" : lobby.Status;
        }

        void CaptureActionIcons(Button menuButton)
        {
            if (menuButton == null || menuButton.transform.root == null) return;

            Image[] images = menuButton.transform.root.GetComponentsInChildren<Image>(true);
            CaptureIcon(images, "matchmaking", "Icon / swords");
            CaptureIcon(images, "create", "Icon / layers");
            CaptureIcon(images, "join", "Icon / scan-line");
            CaptureIcon(images, "leave", "Log-out icon");
        }

        void CaptureIcon(Image[] images, string action, string objectName)
        {
            foreach (Image image in images)
            {
                if (image != null && image.gameObject.name == objectName && image.sprite != null)
                {
                    actionIcons[action] = image.sprite;
                    return;
                }
            }

            Debug.LogWarning($"[OnlineLobbyPopup] Authored icon '{objectName}' was not found; action '{action}' will use no icon.");
        }

        Sprite Icon(string action)
        {
            return actionIcons.TryGetValue(action, out Sprite sprite) ? sprite : null;
        }

        Button Action(Transform parent, string text, float y, Sprite icon, ButtonTone tone, UnityEngine.Events.UnityAction callback)
        {
            var button = Instantiate(template, parent);
            button.name = text;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(callback);
            defaultButtonColors[button] = button.colors;
            if (button.targetGraphic != null)
                defaultTargetColors[button] = button.targetGraphic.color;
            SetButtonIcon(button, icon);
            SetButtonTone(button, tone);
            Place((RectTransform)button.transform, new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(530, 80));
            SetButtonText(button, text);
            button.gameObject.SetActive(true);
            return button;
        }

        static void SetButtonIcon(Button button, Sprite sprite)
        {
            if (button == null) return;

            Image icon = null;
            Image[] images = button.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image != null && image.gameObject.name.StartsWith("Icon /", StringComparison.Ordinal))
                {
                    icon = image;
                    break;
                }
            }

            if (icon == null) return;
            icon.sprite = sprite;
            // Keep each authored silhouette, but render its alpha in one shared neutral colour.
            // This avoids cyan-on-cyan icons without adding a stencil mask or overlay geometry.
            Material material = MonochromeIconMaterial();
            icon.material = material;
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(sprite != null);
        }

        static Material MonochromeIconMaterial()
        {
            if (monochromeIconMaterial != null) return monochromeIconMaterial;

            // A Resources reference keeps the shader available in player builds as well as Editor.
            Shader shader = Resources.Load<Shader>("Shaders/MonochromeIcon");
            if (shader == null)
            {
                Debug.LogWarning("[OnlineLobbyPopup] Monochrome icon shader is unavailable.");
                return null;
            }

            monochromeIconMaterial = new Material(shader)
            {
                name = "Online Lobby Monochrome Icon"
            };
            monochromeIconMaterial.hideFlags = HideFlags.HideAndDontSave;
            monochromeIconMaterial.SetColor("_Color", ActionColor);
            return monochromeIconMaterial;
        }

        static void SetButtonCloseGlyph(Button button)
        {
            if (button == null) return;

            TextMeshProUGUI title = null;
            TextMeshProUGUI[] texts = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                if (text != null && text.gameObject.name == "Authored title")
                {
                    title = text;
                    break;
                }
            }

            var glyphObject = new GameObject("Icon / close", typeof(RectTransform), typeof(TextMeshProUGUI));
            glyphObject.transform.SetParent(button.transform, false);
            var glyph = glyphObject.GetComponent<TextMeshProUGUI>();
            glyph.text = "×";
            glyph.font = title != null ? title.font : null;
            glyph.fontSize = title != null ? title.fontSize + 4f : 24f;
            glyph.color = ActionColor;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.raycastTarget = false;
            Place((RectTransform)glyphObject.transform, new Vector2(1, .5f), new Vector2(-26, 0), new Vector2(34, 48));
        }

        static void SetButtonCheckGlyph(Button button)
        {
            if (button == null) return;

            TextMeshProUGUI title = null;
            foreach (TextMeshProUGUI text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null && text.gameObject.name == "Authored title")
                {
                    title = text;
                    break;
                }
            }

            var glyphObject = new GameObject("Icon / check", typeof(RectTransform), typeof(TextMeshProUGUI));
            glyphObject.transform.SetParent(button.transform, false);
            var glyph = glyphObject.GetComponent<TextMeshProUGUI>();
            glyph.text = "✓";
            glyph.font = title != null ? title.font : null;
            glyph.fontSize = title != null ? title.fontSize + 2f : 22f;
            glyph.color = ActionColor;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.raycastTarget = false;
            Place((RectTransform)glyphObject.transform, new Vector2(1, .5f), new Vector2(-26, 0), new Vector2(34, 48));
        }

        void SetButtonText(Button button, string text)
        {
            if (button == null) return;
            var texts = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in texts)
            {
                if (label.gameObject.name == "Authored title") label.text = text;
                else if (label.gameObject.name == "Authored detail") label.text = "";
                if (label.gameObject.name == "Authored title" || label.gameObject.name == "Authored detail")
                    label.color = ActionColor;
            }
        }

        void SetButtonTone(Button button, ButtonTone tone)
        {
            if (button == null) return;

            if (!defaultButtonColors.TryGetValue(button, out ColorBlock defaultColors))
                defaultColors = button.colors;

            ColorBlock colors = defaultColors;

            if (tone != ButtonTone.Normal)
            {
                Color toneColor = tone == ButtonTone.Danger ? DangerColor : WarningColor;
                colors.normalColor = toneColor;
                colors.highlightedColor = Color.Lerp(toneColor, Color.white, .12f);
                colors.pressedColor = Color.Lerp(toneColor, Color.black, .18f);
                colors.selectedColor = colors.highlightedColor;
            }

            button.colors = tone == ButtonTone.Normal ? defaultColors : colors;
            if (button.targetGraphic != null)
                button.targetGraphic.color = tone == ButtonTone.Normal ?
                    (defaultTargetColors.TryGetValue(button, out Color targetColor) ? targetColor : colors.normalColor) : Color.white;
        }

        static TMP_InputField CreateInput(Transform parent)
        {
            var root = Panel(parent, "Lobby Code", new Color32(23, 40, 51, 255));
            Place(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(0, -30), new Vector2(530, 65));
            var input = root.AddComponent<TMP_InputField>();
            var text = Label(root.transform, "Text", "", 22, Vector2.zero, new Vector2(490, 55));
            text.alignment = TextAlignmentOptions.Center;
            var placeholder = Label(root.transform, "Placeholder", "ENTER LOBBY CODE", 18, Vector2.zero, new Vector2(490, 55));
            placeholder.color = new Color32(135, 157, 171, 255);
            input.textViewport = root.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 12;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }
        static TextMeshProUGUI Label(Transform parent, string name, string value, float size, Vector2 position, Vector2 bounds)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(.5f, .5f), position, bounds);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = new Color32(221, 232, 237, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
