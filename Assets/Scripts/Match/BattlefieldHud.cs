using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.Match
{
    /// <summary>
    /// The match event log as screen-space UI, in its own overlay <c>Canvas</c> rather than as tiles painted
    /// onto the tabletop.
    /// <para>
    /// On the table the log was geometry: it ate a column of the board, it shrank with the camera's distance,
    /// and it was unreadable in the bird's-eye tier for the same reason. As UI it is pinned to the screen,
    /// crisp at any zoom, and the tabletop keeps the space it was reserving for it.
    /// </para>
    /// <para>
    /// It is deliberately <b>not</b> interactable: there is no <c>GraphicRaycaster</c> and no <c>EventSystem</c>,
    /// so the panel cannot swallow a mouse-down. Card dragging in <c>HandController</c> works by raycasting
    /// from the pointer, and a UI block that intercepted clicks would break the one interaction the game has.
    /// </para>
    /// <para>
    /// Like the board, this is a <b>view</b>. It receives the strings <see cref="BattlefieldPresenter"/>
    /// reads out of <c>MatchState.EventLog</c> and never touches match state itself.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public class BattlefieldHud : MonoBehaviour
    {
        /// <summary>The name the scene ships this object under, and the name <see cref="Ensure"/> looks for.</summary>
        public const string RootName = "Battlefield HUD";

        [Header("Log")]
        [Tooltip("How many of the most recent events the panel keeps. Older lines scroll off the top.")]
        public int maxLines = 6;

        [Header("Panel placement (pixels at the reference resolution)")]
        [Tooltip("Anchored to the right edge and vertically centred, which is where the tabletop column used " +
                 "to sit, so the log reads from the same place it always did. Sized for the window above with " +
                 "its entries wrapped at this width: every pixel of this panel lies over the board, so a slab " +
                 "taller than its text hides sockets for no reason.")]
        public Vector2 panelSize = new Vector2(320f, 240f);
        public Vector2 panelOffset = new Vector2(-14f, 0f);
        public float panelPadding = 12f;
        // Distinctly lighter than the camera's clear colour, otherwise the slab is invisible against the void
        // and the log reads as text floating over the board rather than as a panel. Semi-transparent for the
        // opposite reason: it overlaps the right-hand node plates, and the tiles under it must stay visible.
        public Color panelColor = new Color(0.11f, 0.13f, 0.19f, 0.78f);

        [Header("Text")]
        public float fontSize = 15f;
        public float lineSpacing = 1.1f;
        public Color textColor = new Color(0.80f, 0.85f, 0.93f);
        public Color headerColor = new Color(0.62f, 0.70f, 0.84f);

        [Header("Floating card preview")]
        [Tooltip("The enlarged card that floats in the centre when a board or discard card is inspected. It is " +
                 "display-only: the transparent overlay has no raycaster, so card dragging still works behind it.")]
        public Vector2 readerSize = new Vector2(500f, 650f);
        public Vector2 readerOffset = Vector2.zero;
        public float readerPadding = 28f;
        public Color readerColor = new Color(0.07f, 0.09f, 0.15f, 0.98f);
        public Color readerAccentColor = new Color(0.95f, 0.70f, 0.25f, 1f);
        public float readerTitleSize = 32f;
        public float readerBodySize = 22f;
        public float readerFootSize = 16f;
        public Color readerTitleColor = new Color(0.95f, 0.97f, 1f);
        public Color readerFootColor = new Color(0.58f, 0.66f, 0.80f);

        Canvas canvas;
        Button endPhaseButton;
        TextMeshProUGUI endPhaseLabel;
        Coroutine readerMotion;

        public event Action EndPhaseRequested;

        // Serialized rather than plain private: the scene ships this object already built, and a reference
        // that is not serialized arrives null from disk. That would make every Ensure() call rewrite the
        // layout from its defaults - harmless in play mode, but in the editor it dirties the scene the tests
        // happen to have open.
        [SerializeField] RectTransform panel;
        [SerializeField] TextMeshProUGUI header;
        [SerializeField] TextMeshProUGUI body;

        // The reader is a second panel on the same canvas: one overlay is cheaper than two, and the canvas
        // has no raycaster so nothing about the drag contract changes.
        [SerializeField] RectTransform reader;
        [SerializeField] RectTransform readerCard;
        [SerializeField] TextMeshProUGUI readerTitle;
        [SerializeField] TextMeshProUGUI readerBody;
        [SerializeField] TextMeshProUGUI readerFoot;

        /// <summary>The panel's rectangle, exposed so the editor (and tests) can move it.</summary>
        public RectTransform Panel => panel;

        /// <summary>The floating preview overlay's rectangle, likewise.</summary>
        public RectTransform Reader => reader;

        /// <summary>
        /// True once the canvas has its panel and text objects resolved. A HUD loaded from the scene is not
        /// built until something calls <see cref="Build"/>, because the references below are private and
        /// therefore not serialized.
        /// </summary>
        public bool IsBuilt => panel != null && body != null;

        /// <summary>
        /// True once the details panel exists too. A HUD loaded from the scene has its log references
        /// serialized and therefore reports <see cref="IsBuilt"/>, but the reader is newer than the shipped
        /// scene file, so <see cref="Ensure"/> checks this separately rather than assuming a built log means
        /// a built reader.
        /// </summary>
        public bool IsReaderBuilt => reader != null && readerTitle != null && readerBody != null;

        /// <summary>
        /// What the panel currently shows. Exposed for tests and for anything that wants to copy the log out;
        /// the panel is the view, so this is derived state and never a source of truth.
        /// </summary>
        public string BodyText => body != null ? body.text : string.Empty;

        /// <summary>
        /// Returns the scene's HUD, building it as a new root object if there is none.
        /// <para>
        /// Creating is a side effect, so this belongs in play mode or in an explicit editor build: the board's
        /// own prefab build calls into the presenter too, and a HUD spawned there would be a stray root object
        /// left behind in whatever scene the editor happens to have open.
        /// </para>
        /// </summary>
        public static BattlefieldHud Ensure()
        {
            BattlefieldHud existing = Find();
            if (existing != null)
            {
                // The scene's copy arrives unbuilt. The panel and text references are private fields, so they
                // are not serialized, and only Awake wires them - which never runs in the editor. An EditMode
                // test (or an editor script) asking for the HUD would get an object with no panel and no way
                // to show anything, so Ensure completes it instead of assuming play mode.
                if (!existing.IsBuilt || !existing.IsReaderBuilt)
                {
                    existing.Build();
                }

                return existing;
            }

            var go = new GameObject(RootName, typeof(RectTransform));
            var hud = go.AddComponent<BattlefieldHud>();
            hud.Build();
            return hud;
        }

        /// <summary>
        /// The scene's HUD if it has one. Includes inactive objects, because a HUD an artist has switched off
        /// should be re-used rather than duplicated.
        /// </summary>
        public static BattlefieldHud Find()
        {
            // FindObjectsOfType sees scene objects only, never prefab assets, so this cannot accidentally bind
            // to the Project's copy of the HUD.
            BattlefieldHud[] all = FindObjectsOfType<BattlefieldHud>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == RootName && all[i].transform.parent == null)
                {
                    return all[i];
                }
            }

            return null;
        }

        // ---- Construction -------------------------------------------------------------

        void Awake()
        {
            if (panel == null)
            {
                Build();
            }
        }

        /// <summary>
        /// Builds the canvas, the scaler and the panel, or re-applies them to an object the scene already
        /// ships. Idempotent, and deliberately authoritative: the serialized fields on this component are the
        /// layout's source of truth, so moving the <c>RectTransform</c> by hand in the Inspector is undone on
        /// the next build - change <see cref="panelSize"/> and <see cref="panelOffset"/> instead.
        /// </summary>
        public void Build()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            // An overlay canvas is laid out in pixels against the viewport and is unaffected by the Match
            // root's 90-degree tilt, which is the whole point of moving the log off the tabletop.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsurePanel();
            EnsureReader();
            if (Application.isPlaying)
            {
                EnsurePhaseControls();
            }
        }

        void EnsurePhaseControls()
        {
            if (endPhaseButton != null) return;

            Transform existing = transform.Find("PhaseControls");
            GameObject root = existing != null ? existing.gameObject
                : new GameObject("PhaseControls", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            if (existing == null) root.transform.SetParent(transform, false);
            var nestedCanvas = root.GetComponent<Canvas>();
            nestedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            nestedCanvas.overrideSorting = true;
            nestedCanvas.sortingOrder = 11;

            Transform buttonTransform = root.transform.Find("EndPhaseButton");
            GameObject buttonObject = buttonTransform != null ? buttonTransform.gameObject
                : new GameObject("EndPhaseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            if (buttonTransform == null) buttonObject.transform.SetParent(root.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 175f);
            rect.sizeDelta = new Vector2(210f, 54f);
            buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.40f, 0.60f, 0.96f);
            endPhaseButton = buttonObject.GetComponent<Button>();
            endPhaseButton.onClick.AddListener(() => EndPhaseRequested?.Invoke());

            Transform labelTransform = rect.Find("Label");
            GameObject label = labelTransform != null ? labelTransform.gameObject
                : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (labelTransform == null) label.transform.SetParent(rect, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            endPhaseLabel = label.GetComponent<TextMeshProUGUI>();
            endPhaseLabel.alignment = TextAlignmentOptions.Center;
            endPhaseLabel.fontSize = 25f;
            endPhaseLabel.fontStyle = FontStyles.Bold;
            endPhaseLabel.raycastTarget = false;
            endPhaseLabel.text = "END PHASE";

            if (EventSystem.current == null)
            {
                new GameObject("Battlefield EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            SetEndPhaseAvailable(false);
        }

        public void SetEndPhaseAvailable(bool available)
        {
            if (endPhaseButton == null) return;
            endPhaseButton.gameObject.SetActive(available);
        }

        /// <summary>Only the small action button, not the log or reader, consumes board/hand pointer input.</summary>
        public bool IsPointerOverControls(Vector2 screenPosition)
        {
            return endPhaseButton != null && endPhaseButton.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(
                    endPhaseButton.GetComponent<RectTransform>(), screenPosition);
        }

        RectTransform EnsurePanel()
        {
            Transform existing = transform.Find("LogPanel");
            if (existing != null)
            {
                panel = existing as RectTransform;

                // The serialized fields are the layout's source of truth, so a recoloured panel survives a
                // reload the same way a moved one does.
                var existingImage = existing.GetComponent<Image>();
                if (existingImage != null)
                {
                    existingImage.color = panelColor;
                    existingImage.raycastTarget = false;
                }
            }
            else
            {
                // A RectTransform must be part of the GameObject's construction: a plain GameObject carries a
                // Transform, and Unity refuses to add a RectTransform alongside one.
                var panelGo = new GameObject("LogPanel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                panel = panelGo.GetComponent<RectTransform>();

                // Nothing here intercepts pointer input, but a Graphic would still claim to; switching it off
                // states the intent rather than relying on the missing raycaster.
                var image = panelGo.AddComponent<Image>();
                image.color = panelColor;
                image.raycastTarget = false;
            }

            // Right edge, vertically centred: anchored rather than free-positioned so the panel stays glued to
            // the bezel at any window size instead of drifting into the board.
            panel.anchorMin = new Vector2(1f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.sizeDelta = panelSize;
            panel.anchoredPosition = panelOffset;

            float headerHeight = fontSize * 2.2f;

            // The title band hugs the top of the panel and stretches across its full width.
            header = EnsureText("LogHeader", panel, fontSize + 1f, FontStyles.Bold, headerColor,
                TextAlignmentOptions.TopLeft, out RectTransform headerRect);
            header.text = "MATCH LOG";
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.sizeDelta = new Vector2(0f, headerHeight);
            headerRect.anchoredPosition = new Vector2(panelPadding, -panelPadding);

            // The body fills whatever the header leaves, reading downward from it. Bottom-alignment was tried
            // and rejected: with a short log it stranded the newest line at the foot of the panel under a
            // band of empty slab, which reads as a layout bug rather than as a log.
            body = EnsureText("LogText", panel, fontSize, FontStyles.Normal, textColor,
                TextAlignmentOptions.TopLeft, out RectTransform bodyRect);
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            SetRect(bodyRect, panelPadding, panelPadding, panelPadding, panelPadding + headerHeight);

            return panel;
        }

        /// <summary>
        /// The card-details / pile-browser panel on the left edge, mirrored from the log. Built alongside it
        /// but kept hidden until something is clicked, because an empty slab over the player's own Bench
        /// rank would hide sockets for no reason.
        /// </summary>
        RectTransform EnsureReader()
        {
            Transform existing = transform.Find("ReaderPanel");
            if (existing != null)
            {
                reader = existing as RectTransform;
                var image = existing.GetComponent<Image>();
                if (image != null)
                {
                    // ReaderPanel is now a transparent full-screen overlay. The visible card is its centered
                    // CardFace child, which lets the preview float over the table without tinting the whole view.
                    image.color = Color.clear;
                    image.raycastTarget = false;
                }
            }
            else
            {
                var go = new GameObject("ReaderPanel", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                reader = go.GetComponent<RectTransform>();
                var image = go.AddComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = false;
            }

            // The root covers the screen but does not intercept input. This keeps the child card responsive to
            // any resolution while the board, hand, and the transparent area around the card remain clickable.
            reader.anchorMin = Vector2.zero;
            reader.anchorMax = Vector2.one;
            reader.pivot = new Vector2(0.5f, 0.5f);
            reader.sizeDelta = Vector2.zero;
            reader.anchoredPosition = Vector2.zero;

            Transform existingCard = reader.Find("CardFace");
            if (existingCard != null)
            {
                readerCard = existingCard as RectTransform;
            }
            else
            {
                var cardGo = new GameObject("CardFace", typeof(RectTransform), typeof(Image), typeof(Outline));
                cardGo.transform.SetParent(reader, false);
                readerCard = cardGo.GetComponent<RectTransform>();
            }

            var cardImage = readerCard.GetComponent<Image>();
            cardImage.color = readerColor;
            cardImage.raycastTarget = false;
            var outline = readerCard.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 0.82f, 0.35f, 0.78f);
            outline.effectDistance = new Vector2(5f, 5f);
            outline.useGraphicAlpha = true;

            readerCard.anchorMin = new Vector2(0.5f, 0.5f);
            readerCard.anchorMax = new Vector2(0.5f, 0.5f);
            readerCard.pivot = new Vector2(0.5f, 0.5f);
            readerCard.sizeDelta = readerSize;
            readerCard.anchoredPosition = readerOffset;

            // A slim gold band gives the preview the visual language of a physical trading card before the
            // title and rules text are read.
            Transform accentTransform = readerCard.Find("Accent");
            GameObject accent = accentTransform != null ? accentTransform.gameObject
                : new GameObject("Accent", typeof(RectTransform), typeof(Image));
            if (accentTransform == null) accent.transform.SetParent(readerCard, false);
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(-readerPadding * 2f, 10f);
            accentRect.anchoredPosition = new Vector2(0f, -readerPadding + 4f);
            accent.GetComponent<Image>().color = readerAccentColor;
            accent.GetComponent<Image>().raycastTarget = false;

            float titleHeight = readerTitleSize * 2.4f;
            float footHeight = readerFootSize * 2.4f;

            // Older scene files put these text objects directly under ReaderPanel. Move them into CardFace so
            // the same serialized HUD upgrades in place instead of creating duplicate labels.
            MoveReaderTextIntoCard("ReaderTitle");
            MoveReaderTextIntoCard("ReaderBody");
            MoveReaderTextIntoCard("ReaderFoot");

            readerTitle = EnsureText("ReaderTitle", readerCard, readerTitleSize, FontStyles.Bold, readerTitleColor,
                TextAlignmentOptions.TopLeft, out RectTransform titleRect);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.sizeDelta = new Vector2(0f, titleHeight);
            titleRect.anchoredPosition = new Vector2(readerPadding, -readerPadding);

            readerBody = EnsureText("ReaderBody", readerCard, readerBodySize, FontStyles.Normal, textColor,
                TextAlignmentOptions.TopLeft, out RectTransform bodyRect);
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            SetRect(bodyRect, readerPadding, readerPadding + footHeight, readerPadding,
                readerPadding + titleHeight);

            // The footer carries the browsing affordance ("◄ ►" for a pile, "ESC to close" for a card), which
            // is the only way to advertise a control on a panel that cannot hold a button.
            readerFoot = EnsureText("ReaderFoot", readerCard, readerFootSize, FontStyles.Normal, readerFootColor,
                TextAlignmentOptions.BottomLeft, out RectTransform footRect);
            footRect.anchorMin = new Vector2(0f, 0f);
            footRect.anchorMax = new Vector2(1f, 0f);
            footRect.pivot = new Vector2(0f, 0f);
            footRect.sizeDelta = new Vector2(0f, footHeight);
            footRect.anchoredPosition = new Vector2(readerPadding, readerPadding);

            Transform hintTransform = readerCard.Find("ReaderHint");
            GameObject hint = hintTransform != null ? hintTransform.gameObject
                : new GameObject("ReaderHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (hintTransform == null) hint.transform.SetParent(readerCard, false);
            var hintText = hint.GetComponent<TextMeshProUGUI>();
            hintText.text = "ESC  /  CLICK ELSEWHERE  TO CLOSE";
            hintText.fontSize = Mathf.Max(11f, readerFootSize * 0.72f);
            hintText.color = new Color(0.62f, 0.69f, 0.82f, 0.9f);
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.raycastTarget = false;
            var hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(-readerPadding * 2f, readerFootSize * 1.8f);
            hintRect.anchoredPosition = new Vector2(0f, readerPadding * 0.35f);

            reader.gameObject.SetActive(false);
            return reader;
        }

        void MoveReaderTextIntoCard(string objectName)
        {
            Transform old = reader.Find(objectName);
            if (old != null && readerCard != null)
            {
                old.SetParent(readerCard, false);
            }
        }

        TextMeshProUGUI EnsureText(string name, RectTransform parent, float size, FontStyles style,
            Color color, TextAlignmentOptions alignment, out RectTransform rect)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                var found = existing.GetComponent<TextMeshProUGUI>();
                if (found != null)
                {
                    found.fontSize = size;
                    found.fontStyle = style;
                    found.color = color;
                    found.alignment = alignment;
                    found.lineSpacing = lineSpacing;
                    rect = found.rectTransform;
                    return found;
                }
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.lineSpacing = lineSpacing;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;
            rect = tmp.rectTransform;
            return tmp;
        }

        static void SetRect(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // ---- View API -----------------------------------------------------------------

        /// <summary>
        /// Shows the most recent <see cref="maxLines"/> entries of a chronological event log, oldest at the
        /// top, which is the order a reader expects to scan down into "what is happening now".
        /// <para>
        /// Unlike the tabletop version these lines are not truncated to a fixed character count: the panel
        /// wraps, so a long event stays legible instead of being cut off mid-word.
        /// </para>
        /// </summary>
        public void SetLog(IList<string> log)
        {
            if (body == null)
            {
                Build();
            }

            int count = log == null ? 0 : log.Count;
            int shown = Mathf.Min(Mathf.Max(1, maxLines), count);
            int start = count - shown;

            if (shown <= 0)
            {
                body.text = "";
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = start; i < count; i++)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(log[i]);
            }

            body.text = sb.ToString();
        }

        /// <summary>How many entries this panel is configured to keep.</summary>
        public int MaxLines => Mathf.Max(1, maxLines);

        // ---- Reader -------------------------------------------------------------------

        /// <summary>True while the details panel is on screen.</summary>
        public bool IsReaderVisible => reader != null && reader.gameObject.activeSelf;

        /// <summary>What the reader currently says, for tests and for anything that mirrors it.</summary>
        public string ReaderTitle => readerTitle != null ? readerTitle.text : string.Empty;
        public string ReaderBody => readerBody != null ? readerBody.text : string.Empty;
        public string ReaderFoot => readerFoot != null ? readerFoot.text : string.Empty;

        /// <summary>
        /// Puts a card in front of the player: the title band names it, the body carries its stats and text,
        /// and the footer says what the panel is being used for (browsing a pile, or nothing at all).
        /// <para>
        /// A null <paramref name="title"/> or body clears the panel instead, which is the same call path the
        /// dismiss uses - there is no second code path for hiding that can drift from it.
        /// </para>
        /// </summary>
        public void ShowReader(string title, string body, string foot = null, Color? accent = null)
        {
            if (readerTitle == null || readerBody == null)
            {
                Build();
            }

            if (reader == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            {
                HideReader();
                return;
            }

            readerTitle.text = title ?? string.Empty;
            readerBody.text = body ?? string.Empty;
            readerFoot.text = foot ?? string.Empty;
            if (readerCard != null)
            {
                Image cardImage = readerCard.GetComponent<Image>();
                if (cardImage != null)
                {
                    cardImage.color = readerColor;
                }

                Transform accentTransform = readerCard.Find("Accent");
                Image accentImage = accentTransform != null ? accentTransform.GetComponent<Image>() : null;
                if (accentImage != null)
                {
                    accentImage.color = accent ?? readerAccentColor;
                }
            }
            reader.gameObject.SetActive(true);

            if (Application.isPlaying && readerCard != null)
            {
                if (readerMotion != null)
                {
                    StopCoroutine(readerMotion);
                }

                readerMotion = StartCoroutine(FloatReaderIn());
            }
        }

        IEnumerator FloatReaderIn()
        {
            readerCard.localScale = Vector3.one * 0.84f;
            readerCard.anchoredPosition = readerOffset + new Vector2(0f, -42f);
            Vector2 start = readerCard.anchoredPosition;
            float elapsed = 0f;
            const float duration = 0.22f;

            while (elapsed < duration && readerCard != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                readerCard.localScale = Vector3.LerpUnclamped(Vector3.one * 0.84f, Vector3.one, eased);
                readerCard.anchoredPosition = Vector2.LerpUnclamped(start, readerOffset, eased);
                yield return null;
            }

            if (readerCard != null)
            {
                readerCard.localScale = Vector3.one;
                readerCard.anchoredPosition = readerOffset;
            }

            readerMotion = null;
        }

        /// <summary>Takes the details panel off screen. Safe when it was never shown.</summary>
        public void HideReader()
        {
            if (reader != null)
            {
                if (readerMotion != null)
                {
                    StopCoroutine(readerMotion);
                    readerMotion = null;
                }
                reader.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// True when this canvas would swallow a mouse-down. It must always be false: the one interaction the
        /// game has is dragging a card, and a panel that ate clicks over the right-hand third of the screen
        /// would break it. Exposed as a plain <c>bool</c> so the tests can assert it without referencing the
        /// UI assembly.
        /// </summary>
        public bool InterceptsPointerInput => GetComponent<GraphicRaycaster>() != null;
    }
}
