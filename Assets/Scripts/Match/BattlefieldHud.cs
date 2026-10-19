using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

        [Header("Reader")]
        [Tooltip("The card-details panel, pinned to the left edge where the log's column is mirrored. It shows " +
                 "whatever the player clicked on: a board unit's full card, or one card out of the discard " +
                 "pile. Like the log it is display-only - no close button, because a button needs a raycaster " +
                 "and a raycaster would eat card drags. Click anywhere else, or press Escape, to dismiss it.")]
        public Vector2 readerSize = new Vector2(340f, 300f);
        public Vector2 readerOffset = new Vector2(14f, 0f);
        public float readerPadding = 14f;
        public Color readerColor = new Color(0.10f, 0.12f, 0.18f, 0.90f);
        public float readerTitleSize = 22f;
        public float readerBodySize = 16f;
        public float readerFootSize = 13f;
        public Color readerTitleColor = new Color(0.95f, 0.97f, 1f);
        public Color readerFootColor = new Color(0.58f, 0.66f, 0.80f);

        Canvas canvas;

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
        [SerializeField] TextMeshProUGUI readerTitle;
        [SerializeField] TextMeshProUGUI readerBody;
        [SerializeField] TextMeshProUGUI readerFoot;

        /// <summary>The panel's rectangle, exposed so the editor (and tests) can move it.</summary>
        public RectTransform Panel => panel;

        /// <summary>The details panel's rectangle, likewise.</summary>
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
                    image.color = readerColor;
                    image.raycastTarget = false;
                }
            }
            else
            {
                var go = new GameObject("ReaderPanel", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                reader = go.GetComponent<RectTransform>();
                var image = go.AddComponent<Image>();
                image.color = readerColor;
                image.raycastTarget = false;
            }

            // Left edge, vertically centred: the mirror of the log, so the two panels read as one instrument
            // row flanking the table rather than as two unrelated popups.
            reader.anchorMin = new Vector2(0f, 0.5f);
            reader.anchorMax = new Vector2(0f, 0.5f);
            reader.pivot = new Vector2(0f, 0.5f);
            reader.sizeDelta = readerSize;
            reader.anchoredPosition = readerOffset;

            float titleHeight = readerTitleSize * 2.4f;
            float footHeight = readerFootSize * 2.4f;

            readerTitle = EnsureText("ReaderTitle", reader, readerTitleSize, FontStyles.Bold, readerTitleColor,
                TextAlignmentOptions.TopLeft, out RectTransform titleRect);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.sizeDelta = new Vector2(0f, titleHeight);
            titleRect.anchoredPosition = new Vector2(readerPadding, -readerPadding);

            readerBody = EnsureText("ReaderBody", reader, readerBodySize, FontStyles.Normal, textColor,
                TextAlignmentOptions.TopLeft, out RectTransform bodyRect);
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            SetRect(bodyRect, readerPadding, readerPadding + footHeight, readerPadding,
                readerPadding + titleHeight);

            // The footer carries the browsing affordance ("◄ ►" for a pile, "ESC to close" for a card), which
            // is the only way to advertise a control on a panel that cannot hold a button.
            readerFoot = EnsureText("ReaderFoot", reader, readerFootSize, FontStyles.Normal, readerFootColor,
                TextAlignmentOptions.BottomLeft, out RectTransform footRect);
            footRect.anchorMin = new Vector2(0f, 0f);
            footRect.anchorMax = new Vector2(1f, 0f);
            footRect.pivot = new Vector2(0f, 0f);
            footRect.sizeDelta = new Vector2(0f, footHeight);
            footRect.anchoredPosition = new Vector2(readerPadding, readerPadding);

            reader.gameObject.SetActive(false);
            return reader;
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
        public void ShowReader(string title, string body, string foot = null)
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
            reader.gameObject.SetActive(true);
        }

        /// <summary>Takes the details panel off screen. Safe when it was never shown.</summary>
        public void HideReader()
        {
            if (reader != null)
            {
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
