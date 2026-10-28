using System;
using System.Collections;
using System.Collections.Generic;
using Overlink.Core;
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
        [Tooltip("How many of the most recent events the panel keeps. Older lines scroll off the top. " +
                 "Four, not the six the tabletop column held: the panel now lives in the band above the " +
                 "board's far rank, which is 170 reference pixels tall at the shortest supported aspect, and " +
                 "that height buys a socket-clear slab rather than a longer history.")]
        public int maxLines = 4;

        [Header("Panel placement (pixels at the reference resolution)")]
        [Tooltip("Top-right corner, inside the band that holds no socket at any aspect (see NodeList). The " +
                 "width is what a log entry needs to wrap in two lines rather than three; the height is what " +
                 "four entries need at that width. Centred on the right edge, where the tabletop column used " +
                 "to sit, put it straight over the two right-hand Node sockets.")]
        public Vector2 panelSize = new Vector2(320f, 170f);
        public Vector2 panelOffset = new Vector2(-22f, -18f);
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

        [Header("Status panels")]
        [Tooltip("Everything that used to be painted onto the tabletop: the turn banner, the two player chips, " +
                 "the node list, the notice line and the result overlay. They are world-space text on a 52 " +
                 "degree pitch today, which foreshortens a 32 point header into a smear and shrinks it every " +
                 "time the camera pulls back; as UI they are pinned, crisp at any zoom, and they hand the board " +
                 "back the two bands of table they were standing on.\n" +
                 "32pt, not the 38 it shipped with, and 44 tall, not the 62 before it. Both numbers came off " +
                 "TMP's own layout rather than an estimate: the longest line this header can hold - TURN 99, " +
                 "CLEANUP, ACTIVE: OPPONENT, with the airy bullet separators the rest of the layer uses - lays " +
                 "out 819 reference pixels wide and 37 tall at 32pt, so the box keeps 80 pixels of width and 7 " +
                 "of height in hand. At 38pt that line needs about 973 of the 900 the box has, and Truncate " +
                 "overflow eats the tail without warning: the shipped setup banner lost its last two words to " +
                 "it. Width stays 900 because the corner panels own everything outside it at 4:3.")]
        public Vector2 bannerSize = new Vector2(900f, 44f);
        public Vector2 bannerOffset = new Vector2(0f, -16f);
        public float bannerFontSize = 32f;

        [Tooltip("The per-player readout (CORE, NP, NODES, HAND, DECK). Both chips hang in the near corners, " +
                 "which is the one region the hand fan does not own: the fan's resting box spans x 489..1431 at " +
                 "the reference resolution, so a 470 pixel slab at a 22 pixel margin touches it by three pixels " +
                 "at 16:9 - and that box is the maximum-span envelope, so at a normal hand size there is " +
                 "daylight. Neither chip is anywhere near a tile: the nearest clickable thing to either is " +
                 "the near Node Bench rank, 46.5 reference pixels away at 3440x1440, and that rank sits at " +
                 "0.185 of the frame height, well above a chip whose top edge is at 0.115 even at 4:3.\n" +
                 "112 tall, not 96. The five counters never fitted on one line: the text rect inside a 470 slab " +
                 "is 446 wide and the readout runs past it, so at the old height the readout had exactly one " +
                 "line of room and Truncate ate the last two counters outright - the shipped chips read CORE " +
                 "3/3, NP 3/10, NODES 0/4 and never mentioned hand or deck. The presenter breaks after NP: the " +
                 "two numbers that tick down through a turn carry the first line and the three stock counts the " +
                 "second, and the wider of the two lays out 432 of those 446 pixels while the pair fills 60 of " +
                 "a 69 pixel body. Breaking after NODES instead reads more tidily, but it is the ordering and " +
                 "not the width that decides here, and either seam this slab ships with is held to the same " +
                 "four pixels by the ink guard. The opponent's belongs at the bottom rather than " +
                 "the top because the far half of the frame is the board's, and the top band it used to share " +
                 "with the banner is where the node list and the log now live.")]
        public Vector2 chipSize = new Vector2(470f, 112f);
        public Vector2 chipMargin = new Vector2(22f, 18f);
        public float chipTitleSize = 18f;
        public float chipBodySize = 26f;

        [Tooltip("Node bonuses, which moved here from the four plate captions. The plates keep a bare NODE n " +
                 "tag so the list still reads against the board, but the wording is what was eating the plate " +
                 "corners and squinting at 52 degrees.\n" +
                 "Top-left corner, and the two numbers are the corners of the one region of the frame that " +
                 "holds no socket at any supported aspect: the topmost thing on the board is the far Node " +
                 "Bench rank, whose top edge projects to 0.841 of the frame height, and that fraction does " +
                 "not depend on the aspect because vertical projection does not. So everything above 0.841 is " +
                 "void at 1920x1080, 1024x768 and 2560x1080 alike - 172, 196 and 147 reference pixels of it " +
                 "respectively. A 170 pixel slab with an 18 pixel margin only fits inside that band at 4:3, and " +
                 "there it leaves an 8.7 pixel seam to the far Node Bench rank - the tightest this panel gets " +
                 "anywhere in the sweep. At 16:9 and wider it dips below the far rank's top edge and clears the " +
                 "Node columns sideways instead, by more than fifty pixels and growing as the frame widens, " +
                 "because those columns walk toward the centre. The 340 pixel width is the narrowest supported " +
                 "case: at 4:3 the leftmost Node column reaches x 235, so the slab overlaps it sideways by 127 " +
                 "pixels and has nothing to fall back on but the seam above it.")]
        public Vector2 nodeListSize = new Vector2(340f, 170f);
        public float nodeListFontSize = 15f;

        public Color chipColor = new Color(0.10f, 0.12f, 0.18f, 0.84f);
        public Color playerAccent = new Color(0.45f, 0.78f, 1.00f);
        public Color opponentAccent = new Color(1.00f, 0.62f, 0.55f);
        public Color noticeColor = new Color(1f, 0.82f, 0.45f);
        public Color resultColor = new Color(0.45f, 1f, 0.55f);
        public float noticeFontSize = 20f;
        public float resultFontSize = 84f;
        [Tooltip("The notice line's height as a fraction of the frame, not a rise in pixels. Every feature it " +
                 "has to miss - the opponent's Core Bench below it, the far Node Bench ranks beside it, the key " +
                 "legend above it - is a projection of the board, and a projection lands at the same fraction of " +
                 "the frame at every aspect while a fixed pixel offset does not.\n" +
                 "0.80 is as high as the line can sit without climbing into the legend and as low as it can sit " +
                 "without landing on the opponent's Core Bench, whose top edge projects to 0.762. Its own band " +
                 "does cross the far Node Bench ranks (0.796..0.841), which is why the line is narrow rather " +
                 "than wide and clears those two columns sideways.")]
        [Range(0f, 1f)] public float noticeAnchorY = 0.80f;

        [Tooltip("The key legend, which was a two-line 11pt caption on the table at 52 degrees — small enough " +
                 "to be unreadable and wide enough to run past the table edge. As UI it is crisp, and the on-" +
                 "screen buttons it no longer lists sit in the two side corridors.\n" +
                 "46 tall, and it was 62 on the strength of arithmetic that said the first authored line " +
                 "wraps once into three lines of 17pt. TMP says otherwise: the legend lays out as two lines, 851 " +
                 "pixels wide and 39 tall inside a 900 x 46 box, so seven of that height is spare and the 23 the " +
                 "old box carried were empty transparent slab reaching down into the board. " +
                 "Shrinking it here, and the banner above it with it, took this box's tightest seam - 6.8 pixels " +
                 "to a far Node Bench rank at 3440x1440, the narrowest gap in the whole status layer - to 40.8, " +
                 "and handed the title of tightest seam to the notice line at 6.3. The setup legend is the same " +
                 "shape and shorter (575 wide, still two lines), so it costs a wrap and not a hole.")]
        public Vector2 hintSize = new Vector2(900f, 46f);
        public float hintFontSize = 17f;
        public Color hintColor = new Color(0.60f, 0.68f, 0.80f);

        [Header("Button rail")]
        [Tooltip("The rail stands in the two corridors between a near Node plate and the Core Bench rank - the " +
                 "only places in the frame that hold no socket, no pile, no resting fan and no reader card at " +
                 "once. Measured at 1280x720 against the shipped layout rather than nudged by eye: the fan owns " +
                 "y 0..218, the near plates end at y 405, the Core Bench row starts at x 513 and the reader card " +
                 "at x 473, which leaves x 365..473 and its mirror. x is the offset from the centre line, " +
                 "mirrored onto the two bands; y is the bottom of slot 0 above the screen's bottom edge.")]
        public Vector2 railBandOffset = new Vector2(331f, 340f);
        public Vector2 railButtonSize = new Vector2(162f, 36f);

        [Tooltip("Slot pitch, so a strip of six buttons spans 246 of the 280 reference pixels the corridor has.")]
        public float railPitch = 42f;

        Canvas canvas;

        /// <summary>
        /// Every button on the rail, keyed by what it asks of the match rather than by where it sits.
        /// <para>
        /// The keys are the contract the two publishers write against: <see cref="GameCoordinator"/> owns the
        /// ones that fire a command on the board (the swings, the turn flow) and <see cref="BattlefieldInput"/>
        /// owns the ones that act on dialogue state (the picked-up unit's skill, the pile being read, letting
        /// the opponent's attacks land). Neither publisher remembers anything: both derive each key from the
        /// state every frame, so a button that disagrees with the rules needs a stale frame, not a missed event.
        /// </para>
        /// </summary>
        public enum RailKey
        {
            /// <summary>P: pass the phase. Shares its slot with DoneBlocking and Confirm, and the three are
            /// never wanted at once - setup has no phase to pass, and the block window only opens on the
            /// opponent's turn, when there is nothing to pass.</summary>
            EndPhase,

            /// <summary>Enter, during the opponent's Combat step: let their attacks land rather than wait out
            /// the window. The one button that is loud on purpose, because blocking is the only answer a player
            /// can give and the window is the only moment they can give it.</summary>
            DoneBlocking,

            /// <summary>1: swing with whoever stands in the Core Active.</summary>
            SwingCore,
            SwingNode1,
            SwingNode2,
            SwingNode3,
            SwingNode4,

            /// <summary>The picked-up unit's printed Activate skill.</summary>
            Activate,

            /// <summary>E: end the turn.</summary>
            EndTurn,

            /// <summary>Q: concede (GDD 2.1.8). Legal on either turn, so it is the one key the coordinator
            /// never gates on the active player.</summary>
            Concede,

            /// <summary>L: write the whole match state into the log.</summary>
            State,

            /// <summary>Left arrow: the older card in the pile the reader is showing.</summary>
            PilePrev,

            /// <summary>Right arrow: the newer card back.</summary>
            PileNext,

            /// <summary>Setup: pay NP for a whole new offer.</summary>
            Reroll,

            /// <summary>Setup: field the nomination and open the match.</summary>
            Confirm,

            Count
        }

        /// <summary>Which of the two rail corridors a button stands in.</summary>
        enum RailBand
        {
            /// <summary>The tactical strip, left of the Core battlefield: what the five battlefields may do.</summary>
            Left,

            /// <summary>The flow strip, right of it: what the turn itself may do.</summary>
            Right
        }

        /// <summary>
        /// One rail button and the last state published to it. Publishing runs every frame and the board
        /// repaints sixty times a second, so every write is guarded by a comparison here rather than by the
        /// caller remembering: SetActive rebuilds the canvas batch, a label write dirties a text mesh, and
        /// neither is free.
        /// </summary>
        sealed class RailSlot
        {
            public GameObject Object;
            public Button Button;
            public TextMeshProUGUI Label;
            public bool Visible = true;
            public bool Enabled = true;
            public string Text;
        }

        readonly RailSlot[] rail = new RailSlot[(int)RailKey.Count];

        // Every button on the rail, cached because the pointer test walks it on every frame a card is held and
        // a GetComponentsInChildren there would allocate. Toggling a button's availability does not invalidate
        // the cache, so it is rebuilt only when the rail is built.
        Button[] railButtons = new Button[0];
        Coroutine readerMotion;

        [SerializeField] RectTransform banner;
        [SerializeField] TextMeshProUGUI bannerText;
        [SerializeField] RectTransform hint;
        [SerializeField] TextMeshProUGUI hintText;
        [SerializeField] RectTransform opponentChip;
        [SerializeField] TextMeshProUGUI opponentTitle, opponentBody;
        [SerializeField] RectTransform playerChip;
        [SerializeField] TextMeshProUGUI playerTitle, playerBody;
        [SerializeField] RectTransform nodeList;
        [SerializeField] TextMeshProUGUI[] nodeRows;
        [SerializeField] RectTransform notice;
        [SerializeField] TextMeshProUGUI noticeText;
        [SerializeField] RectTransform result;
        [SerializeField] TextMeshProUGUI resultText;

        public event Action EndPhaseRequested;

        /// <summary>Raised by the setup rail's REROLL button. The coordinator owns the price and the NP check.</summary>
        public event Action RerollLineupRequested;

        /// <summary>Raised by the setup rail's CONFIRM button, whether the lineup is full or partly fielded.</summary>
        public event Action ConfirmLineupRequested;

        /// <summary>
        /// Raised by every other rail button, naming which. The three above predate it and keep their own
        /// signatures because they read as rules at the call site rather than as coordinates on a strip; this is
        /// the one the two consumers of the rail share, and each of them switches on the keys it owns and ignores
        /// the rest. A button that fires this does not know or care who answers it.
        /// </summary>
        public event Action<RailKey> RailPressed;

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
        /// True once the panels that moved off the tabletop exist. Separated from <see cref="IsBuilt"/> for
        /// the same reason the reader is: the shipped scene file has a log panel serialized but predates the
        /// status layer, so <see cref="Ensure"/> has to be able to tell the two apart and upgrade in place.
        /// </summary>
        public bool IsStatusBuilt => banner != null && bannerText != null && playerBody != null
            && hint != null && hintText != null
            && nodeRows != null && nodeRows.Length > 0;

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
                if (!existing.IsBuilt || !existing.IsReaderBuilt || !existing.IsStatusBuilt)
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
            EnsureStatus();

            // The rail used to be built only under Application.isPlaying, which kept the editor quiet and also
            // kept it unmeasurable: a rail label too long for its 162x36 slab walks out from under the button and
            // onto the board (raw TextMeshProUGUI, so TMP's default Overflow rather than Truncate), and that is
            // only something the ink guard can catch if EditMode can construct the buttons at all. HideRail()
            // still closes the build, so nothing here is drawn that a player would not otherwise see.
            EnsurePhaseControls();
        }

        void EnsurePhaseControls()
        {
            if (rail[(int)RailKey.EndPhase] != null) return;

            Transform existing = transform.Find("PhaseControls");
            GameObject root = existing != null ? existing.gameObject
                : new GameObject("PhaseControls", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            if (existing == null) root.transform.SetParent(transform, false);
            var nestedCanvas = root.GetComponent<Canvas>();
            nestedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            nestedCanvas.overrideSorting = true;
            nestedCanvas.sortingOrder = 11;

            // Stretched to the parent, and the one line in the rail that nothing on screen would have revealed
            // as missing. A root overlay canvas is resized to the viewport by Unity and ignores the size it was
            // serialized with - which is why the HUD root and every slab hanging off it land where their anchors
            // say. A NESTED canvas is not: it keeps its own rect, and a freshly constructed RectTransform is a
            // 100x100 box at the centre. Every button below is anchored to (0.5, 0), the bottom of its parent,
            // so without this the whole rail stands 490 reference pixels higher than its slot numbers mean - off
            // the top of a 1080 frame, invisible in a build and invisible to a screenshot tool that cannot
            // capture an overlay canvas in the first place.
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // ---- Right band: the turn's own flow. Slot 0 has three tenants and never more than one at a time -
            // CONFIRM and REROLL while setup is open, END PHASE on the player's own turn, DONE BLOCKING while
            // answering the opponent's - because each is the loudest available action at its moment.
            // The words and sizes are measured rather than judged. TMP lays these labels out at 16-18pt inside a
            // 162x36 slab (BattlefieldHudTests.StatusInk_FitsItsBoxes), and the two that came closest to the wall
            // are the two changed: "DONE BLOCKING" at 17pt is 155 of those 162 pixels, which is legal and thin,
            // so it is 16pt now; "CONFIRM LINEUP" at 17pt was 160 of 162, and the coordinator overwrites the label
            // with a counter the moment setup opens, so the authored word now matches the published one and leaves
            // LINEUP to the banner, which says it in 32pt.
            EnsureRail(RailKey.EndPhase, "EndPhaseButton", RailBand.Right, 0,
                new Color(0.18f, 0.40f, 0.60f, 0.96f), 18f, "END PHASE", () => EndPhaseRequested?.Invoke());
            EnsureRail(RailKey.DoneBlocking, "DoneBlockingButton", RailBand.Right, 0,
                new Color(0.70f, 0.36f, 0.10f, 0.98f), 16f, "DONE BLOCKING",
                () => RailPressed?.Invoke(RailKey.DoneBlocking));
            EnsureRail(RailKey.Confirm, "ConfirmLineupButton", RailBand.Right, 0,
                new Color(0.20f, 0.52f, 0.34f, 0.97f), 17f, "CONFIRM",
                () => ConfirmLineupRequested?.Invoke());
            EnsureRail(RailKey.Reroll, "RerollLineupButton", RailBand.Right, 1,
                new Color(0.34f, 0.30f, 0.52f, 0.96f), 16f, "REROLL - 2 NP",
                () => RerollLineupRequested?.Invoke());
            EnsureRail(RailKey.EndTurn, "EndTurnButton", RailBand.Right, 2,
                new Color(0.24f, 0.29f, 0.38f, 0.95f), 17f, "END TURN",
                () => RailPressed?.Invoke(RailKey.EndTurn));
            EnsureRail(RailKey.State, "StateButton", RailBand.Right, 3,
                new Color(0.19f, 0.21f, 0.27f, 0.92f), 16f, "MATCH STATE",
                () => RailPressed?.Invoke(RailKey.State));

            // Concede sits highest in the strip, furthest from every other button and from the board, because it
            // is the only one here that ends the match instead of advancing it.
            EnsureRail(RailKey.Concede, "ConcedeButton", RailBand.Right, 4,
                new Color(0.33f, 0.15f, 0.17f, 0.92f), 16f, "CONCEDE",
                () => RailPressed?.Invoke(RailKey.Concede));

            // ---- The two pile steps share slot 5 as a pair of half-width keys, because they are one control
            // (leafing through a pile) and the reader they serve is the tallest thing on screen.
            Vector2 arrowSize = new Vector2((railButtonSize.x - 6f) * 0.5f, railButtonSize.y);
            EnsureRail(RailKey.PilePrev, "PilePrevButton", RailBand.Right, 5,
                new Color(0.21f, 0.25f, 0.33f, 0.94f), 16f, "\u25c4",
                () => RailPressed?.Invoke(RailKey.PilePrev), arrowSize, -(arrowSize.x + 6f) * 0.5f);
            EnsureRail(RailKey.PileNext, "PileNextButton", RailBand.Right, 5,
                new Color(0.21f, 0.25f, 0.33f, 0.94f), 16f, "\u25ba",
                () => RailPressed?.Invoke(RailKey.PileNext), arrowSize, (arrowSize.x + 6f) * 0.5f);

            // ---- Left band: the five battlefields in key order, so the strip reads as the same list the number
            // keys fire. A swing button declares the attack the coordinator would choose for that socket, which
            // is the coarse version of clicking a unit and then a socket - it is here because 1-5 are here, not
            // because it is a better way to fight.
            Color swing = new Color(0.46f, 0.19f, 0.22f, 0.95f);
            EnsureRail(RailKey.SwingCore, "SwingCoreButton", RailBand.Left, 0,
                swing, 17f, "SWING CORE", () => RailPressed?.Invoke(RailKey.SwingCore));
            EnsureRail(RailKey.SwingNode1, "SwingNode1Button", RailBand.Left, 1,
                swing, 17f, "SWING NODE 1", () => RailPressed?.Invoke(RailKey.SwingNode1));
            EnsureRail(RailKey.SwingNode2, "SwingNode2Button", RailBand.Left, 2,
                swing, 17f, "SWING NODE 2", () => RailPressed?.Invoke(RailKey.SwingNode2));
            EnsureRail(RailKey.SwingNode3, "SwingNode3Button", RailBand.Left, 3,
                swing, 17f, "SWING NODE 3", () => RailPressed?.Invoke(RailKey.SwingNode3));
            EnsureRail(RailKey.SwingNode4, "SwingNode4Button", RailBand.Left, 4,
                swing, 17f, "SWING NODE 4", () => RailPressed?.Invoke(RailKey.SwingNode4));
            EnsureRail(RailKey.Activate, "ActivateButton", RailBand.Left, 5,
                new Color(0.50f, 0.38f, 0.15f, 0.96f), 16f, "ACTIVATE SKILL",
                () => RailPressed?.Invoke(RailKey.Activate));

            railButtons = root.transform.GetComponentsInChildren<Button>(true);

            if (EventSystem.current == null)
            {
                new GameObject("Battlefield EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Nothing is on screen until a publisher puts it there, so a rail with no match behind it is blank
            // rather than a wall of dim buttons. Both publishers run every frame and SetRail dedupes, so this
            // also self-heals after a domain reload wipes the store.
            HideRail();
        }

        /// <summary>
        /// Finds or creates one rail button in its corridor slot and registers it under its key.
        /// <para>
        /// The find-by-name half is what keeps this scene the tuning surface: a button somebody added to the
        /// <c>PhaseControls</c> canvas in the editor gets bound rather than duplicated. Layout, fill and text
        /// stay code-owned — the scene authors <em>that an object exists and is called this</em>, not where it
        /// sits, so a hand-nudged transform is rewritten on the next build the same way EndPhaseButton's
        /// always was.
        /// </para>
        /// <para>
        /// Position is derived from the band and the slot rather than written per button, because the corridors
        /// are 280 reference pixels tall and twelve buttons have to share two of them without covering a socket:
        /// reorder the rail by changing a slot number and the strip re-stacks itself inside the gap.
        /// </para>
        /// </summary>
        void EnsureRail(RailKey key, string objectName, RailBand band, int slot, Color fill, float fontSize,
            string text, Action onClick, Vector2? size = null, float xNudge = 0f)
        {
            Transform parent = transform.Find("PhaseControls");
            Transform found = parent.Find(objectName);
            GameObject buttonObject = found != null ? found.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            if (found == null) buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = size ?? railButtonSize;
            rect.anchoredPosition = new Vector2(
                (band == RailBand.Left ? -railBandOffset.x : railBandOffset.x) + xNudge,
                railBandOffset.y + slot * railPitch);
            buttonObject.GetComponent<Image>().color = fill;

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());

            Transform labelTransform = rect.Find("Label");
            GameObject labelObject = labelTransform != null ? labelTransform.gameObject
                : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (labelTransform == null) labelObject.transform.SetParent(rect, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            label.text = text;

            // Seeded from what the objects actually are, not from what we hope they are. The store is a cache of
            // the last state written, and SetRail writes only on a difference - so seeding a freshly constructed
            // (therefore active, therefore interactable) button as hidden/disabled plants a permanent "already
            // off" lie, and the HideRail() that closes the build becomes a no-op comparing cache to cache. That
            // is how a fifteen-button wall survived a build whose whole point was to show none of them.
            rail[(int)key] = new RailSlot
            {
                Object = buttonObject,
                Button = button,
                Label = label,
                Visible = buttonObject.activeSelf,
                Enabled = button.interactable,
                Text = text
            };
        }

        /// <summary>
        /// Publishes one rail button's state, writing only what actually changed. Both publishers derive their
        /// keys from the match state every frame, so this is the guard that keeps a sixty-hertz derivation from
        /// becoming a sixty-hertz canvas rebuild.
        /// </summary>
        /// <param name="text">Null means leave the words alone, which is how a publisher hides a button it has
        /// nothing new to say about — writing into a TMP text would blank one that is still, for this frame, on
        /// screen.</param>
        public void SetRail(RailKey key, bool visible, bool enabled = true, string text = null)
        {
            RailSlot slot = rail[(int)key];
            if (slot == null) return;

            if (slot.Visible != visible)
            {
                slot.Visible = visible;
                slot.Object.SetActive(visible);
            }

            if (slot.Enabled != enabled)
            {
                slot.Enabled = enabled;
                slot.Button.interactable = enabled;
            }

            if (text != null && slot.Text != text)
            {
                slot.Text = text;
                slot.Label.text = text;
            }
        }

        /// <summary>
        /// Takes every rail button off screen at once. Used when the rail is first built, so a HUD with no match
        /// behind it is blank rather than a wall of dim buttons, and again the moment a match ends, because the
        /// publishers derive their keys from a state that has stopped changing and would otherwise leave the last
        /// live frame standing there as an offer.
        /// </summary>
        public void HideRail()
        {
            for (int i = 0; i < rail.Length; i++)
            {
                SetRail((RailKey)i, false, false);
            }
        }

        /// <summary>
        /// Shows or hides the setup rail and writes what its two buttons say. Confirm carries the count of
        /// sockets the player has filled, because a partial lineup is legal and this is the only place that can
        /// tell them so; Reroll carries its own price, because the price escalates and a player who memorised
        /// "2 NP" would be wrong on the second click. An unaffordable Reroll stays visible and dimmed rather
        /// than vanishing: hiding it reads as a rule that does not exist instead of a cost not yet met.
        /// </summary>
        public void SetSetupBar(bool visible, string rerollText, bool rerollAffordable, string confirmText)
        {
            SetRail(RailKey.Reroll, visible, rerollAffordable, rerollText);
            SetRail(RailKey.Confirm, visible, true, confirmText);
        }

        /// <summary>
        /// True when the pointer is over any rail button, which is the drag contract's "never start a drag from
        /// under a button". All of them are walked in one pass, and the walk deliberately ignores whether a
        /// button is interactable: a dimmed Reroll still occupies screen space the player is aiming at, and
        /// letting a card slip through to the socket behind it would nominate something nobody chose.
        /// <para>
        /// The log, the reader and the status slabs are absent on purpose — that canvas carries no
        /// <c>GraphicRaycaster</c> and nothing on it is a button, so none of it swallows a mouse-down aimed at
        /// the board.
        /// </para>
        /// </summary>
        public bool IsPointerOverControls(Vector2 screenPosition)
        {
            Button[] buttons = railButtons;
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        button.GetComponent<RectTransform>(), screenPosition))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The panels that moved off the tabletop. Built on the same overlay canvas as the log and the reader,
        /// which is the point: that canvas has no <c>GraphicRaycaster</c> and every graphic here sets
        /// <c>raycastTarget = false</c>, so not one of these slabs can swallow a mouse-down aimed at a socket
        /// or a card. The only interactable thing on the HUD is the nested <c>PhaseControls</c> canvas.
        /// </summary>
        void EnsureStatus()
        {
            // ---- Turn banner, top centre. Was a 32pt bold label at table y 5.35, which is the furthest
            // thing from a 52 degree camera and therefore the most foreshortened text on the board.
            banner = EnsureBox("Banner", bannerSize, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                bannerOffset, Color.clear, out bannerText);
            bannerText.fontSize = bannerFontSize;
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.alignment = TextAlignmentOptions.Center;
            bannerText.color = new Color(0.95f, 0.97f, 1f);

            // ---- Key legend, directly under the banner. It is centred rather than left-pinned so it reads as
            // part of the banner it explains, and so its horizontal edges are predictable: the node list and the
            // log now flank it in the two top corners, and the gap between a centred box and an edge-pinned one
            // is the only measurement that survives an aspect change.
            hint = EnsureBox("ControlsHint", hintSize, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(bannerOffset.x, bannerOffset.y - bannerSize.y), Color.clear, out hintText);
            hintText.fontSize = hintFontSize;
            hintText.alignment = TextAlignmentOptions.Top;
            hintText.color = hintColor;
            hintText.enableWordWrapping = true;

            // ---- Player chips, both in the near corners, mirrored left for right.
            // The opponent's used to hang in the top-left band under the banner and the legend, which is the
            // only part of the frame the board cannot reach - and the board reaches it after all. Its three
            // far Node sockets project to 0.796..0.841 of the frame height, and a 96 pixel slab under a
            // 62 pixel banner and its legend lands at exactly that band at 16:9, hiding one of them outright.
            // The near corners are clear of every socket at every aspect, because the nearest thing to them is
            // the near Node Bench rank at 0.185 of the frame and the chips' top edge is at 0.106 even at 4:3.
            opponentChip = EnsureBox("OpponentChip", chipSize, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-chipMargin.x, chipMargin.y), chipColor, out opponentTitle);
            opponentBody = EnsureChipBody(opponentChip, opponentTitle, opponentAccent);
            opponentTitle.text = "OPPONENT";
            opponentTitle.color = opponentAccent;

            playerChip = EnsureBox("PlayerChip", chipSize, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(chipMargin.x, chipMargin.y), chipColor, out playerTitle);
            playerBody = EnsureChipBody(playerChip, playerTitle, playerAccent);
            playerTitle.text = "YOU";
            playerTitle.color = playerAccent;

            // ---- Node list, top-left corner; the log mirrors it top-right. Both live in the band above the
            // board's far rank - see the nodeListSize tooltip for why that band is the only one that is
            // socket-free at every aspect, and why its 170 pixel height is the ceiling rather than a preference.
            nodeList = EnsureBox("NodeList", nodeListSize, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(chipMargin.x, -chipMargin.y), chipColor, out TextMeshProUGUI nodeHeader);
            nodeHeader.text = "BATTLEFIELDS";
            // The chips can afford an 18 point title; this panel cannot. Its header shares the 170 pixel slab
            // with four rows, so it takes the log's convention instead — one point above the body it heads.
            float nodeHeaderSize = nodeListFontSize + 1f;
            nodeHeader.fontSize = nodeHeaderSize;
            nodeHeader.fontStyle = FontStyles.Bold;
            nodeHeader.color = headerColor;
            nodeHeader.alignment = TextAlignmentOptions.TopLeft;
            nodeHeader.raycastTarget = false;
            SetRect(nodeHeader.rectTransform, panelPadding, panelPadding, panelPadding, panelPadding);

            // One row per Node, in board order, so the list reads top-to-bottom against the four plates. The
            // Core's numbers are not repeated here — both chips already carry CORE x/y.
            // 12 + 16 * 2.1 + 4 * (15 * 2.05) = 168.6 of the slab's 170 pixels: the rows fill the panel exactly.
            nodeRows = new TextMeshProUGUI[Board.NodeCount];
            for (int n = 0; n < Board.NodeCount; n++)
            {
                float rowTop = panelPadding + nodeHeaderSize * 2.1f + n * (nodeListFontSize * 2.05f);
                nodeRows[n] = EnsureText($"NodeRow{n}", nodeList, nodeListFontSize, FontStyles.Normal,
                    textColor, TextAlignmentOptions.MidlineLeft, out RectTransform rowRect);
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0f, 1f);
                rowRect.sizeDelta = new Vector2(-panelPadding * 2f, nodeListFontSize * 2.05f);
                rowRect.anchoredPosition = new Vector2(panelPadding, -rowTop);
                nodeRows[n].text = string.Empty;
            }

                        // ---- Notice line, in the far half's dead band. Fractionally anchored, and 640 rather than the
            // 1100 it was: the wide box reached x 410..1510 at the reference resolution, which put it straight
            // over both far Node columns at every aspect. The binding case is the widest aspect in the sweep,
            // where the frame is under 930 reference pixels tall and the far Node Bench ranks come up to meet
            // this line's own band. Narrowing to 640 bought those columns so much sideways room that the seam
            // which now governs this box is the one *below* it, to the opponent's Core Bench: 6.3 reference
            // pixels at 3440x1440, measured by BattlefieldHudTests, and now the tightest seam in the whole status
            // layer. The height is 2.8 times the font size because two lines of a real notice lay out 46 pixels
            // at 20pt and the box is transparent, so the extra ten cost three pixels of that seam rather than a
            // hole; at 2.5 the box was 50 and had four pixels to its name, which is the same defect as none. See
            // the noticeAnchorY tooltip for why the height is a fraction rather than a rise.
            notice = EnsureBox("Notice", new Vector2(640f, noticeFontSize * 2.8f),
                new Vector2(0.5f, noticeAnchorY), new Vector2(0.5f, 0.5f), Vector2.zero,
                Color.clear, out noticeText);
            noticeText.enableWordWrapping = true;
            noticeText.fontSize = noticeFontSize;
            noticeText.fontStyle = FontStyles.Bold;
            noticeText.alignment = TextAlignmentOptions.Center;
            noticeText.color = noticeColor;

            // ---- Result overlay, dead centre, hidden until the match is over.
            // Authored in reference pixels, and the CanvasScaler turns those into whatever the window is, so
            // the bound this box has to respect is the narrowest viewport the scaler can produce - 1646
            // reference pixels, at 4:3. 1500 leaves 73 pixels of margin there and wraps nothing; going
            // narrower to chase a portrait window would risk wrapping the victory line for a configuration the
            // game does not support in the first place. BattlefieldHudTests sweeps the aspects.
            result = EnsureBox("Result", new Vector2(1500f, resultFontSize * 2.6f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                Color.clear, out resultText);
            resultText.fontSize = resultFontSize;
            resultText.fontStyle = FontStyles.Bold;
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = resultColor;
            result.gameObject.SetActive(false);
        }

        /// <summary>
        /// A named full-colour slab on the overlay canvas with a single text child filling it, found rather
        /// than rebuilt so a scene-authored HUD keeps its objects (and its Inspector tweaks to anything not
        /// set here) across a reload.
        /// </summary>
        RectTransform EnsureBox(string name, Vector2 size, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Color color, out TextMeshProUGUI text)
        {
            Transform existing = transform.Find(name);
            RectTransform rect = existing as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                rect = go.GetComponent<RectTransform>();
            }

            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            text = EnsureText(name + "Text", rect, 20f, FontStyles.Normal, textColor,
                TextAlignmentOptions.TopLeft, out RectTransform textRect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.raycastTarget = false;
            return rect;
        }

        /// <summary>
        /// Lays out a chip's interior: the title band across the top, a four-pixel accent rule at the foot,
        /// and the stat line filling what is left. The <paramref name="title"/> is the text <see cref="EnsureBox"/>
        /// made for the chip, styled here rather than rebuilt, so a scene-authored chip survives a reload.
        /// </summary>
        TextMeshProUGUI EnsureChipBody(RectTransform chip, TextMeshProUGUI title, Color accent)
        {
            title.fontSize = chipTitleSize;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.TopLeft;
            title.raycastTarget = false;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-panelPadding * 2f, chipTitleSize * 1.9f);
            title.rectTransform.anchoredPosition = new Vector2(panelPadding, -panelPadding * 0.6f);

            // The chip is a slab with an accent rule, not a bordered box: a border is what made this read
            // as a button on a panel that cannot be clicked.
            Transform rule = chip.Find("Accent");
            if (rule == null)
            {
                var ruleGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
                ruleGo.transform.SetParent(chip, false);
                rule = ruleGo.transform;
            }
            var ruleImage = rule.GetComponent<Image>();
            ruleImage.color = accent;
            ruleImage.raycastTarget = false;
            var ruleRect = rule.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0f, 0f);
            ruleRect.anchorMax = new Vector2(1f, 0f);
            ruleRect.pivot = new Vector2(0.5f, 0f);
            ruleRect.sizeDelta = new Vector2(0f, 4f);
            ruleRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI body = EnsureText("Body", chip, chipBodySize, FontStyles.Bold,
                new Color(0.93f, 0.96f, 1f), TextAlignmentOptions.BottomLeft, out RectTransform bodyRect);
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0f, 0f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.offsetMin = new Vector2(panelPadding, panelPadding * 0.7f);
            bodyRect.offsetMax = new Vector2(-panelPadding, -chipTitleSize * 1.9f);
            body.raycastTarget = false;
            return body;
        }

        // ---- Status writes. The presenter is the view of match state and this is the view of that view:
        // every one of these takes a finished string and never a MatchState.

        /// <summary>The turn banner: turn number, phase, and whose turn it is.</summary>
        public void SetBanner(string text)
        {
            if (bannerText != null) bannerText.text = text ?? string.Empty;
        }

        /// <summary>The key legend under the banner.</summary>
        public void SetHint(string text)
        {
            if (hintText != null) hintText.text = text ?? string.Empty;
        }

        /// <summary>The CORE / NP / NODES / HAND / DECK readout for one side.</summary>
        public void SetStatus(PlayerIndex who, string text)
        {
            TextMeshProUGUI target = who == PlayerIndex.Player ? playerBody : opponentBody;
            if (target != null) target.text = text ?? string.Empty;
        }

        /// <summary>
        /// Marks a chip as carrying a pointer target. The opponent's Core counter is the click target for an
        /// unblocked direct attack, and on the table that was a colour change on world text; here it is the
        /// same idea on a panel, so the accent rule and the body both light up.
        /// </summary>
        public void SetStatusMarked(PlayerIndex who, bool marked)
        {
            TextMeshProUGUI target = who == PlayerIndex.Player ? playerBody : opponentBody;
            RectTransform chip = who == PlayerIndex.Player ? playerChip : opponentChip;
            if (target == null || chip == null) return;

            Color accent = who == PlayerIndex.Player ? playerAccent : opponentAccent;
            target.color = marked ? new Color(1f, 0.55f, 0.35f) : new Color(0.93f, 0.96f, 1f);
            Transform rule = chip.Find("Accent");
            if (rule != null)
            {
                var image = rule.GetComponent<Image>();
                if (image != null) image.color = marked ? new Color(1f, 0.55f, 0.35f) : accent;
            }
        }

        /// <summary>One row of the node list, tinted by who holds it. Null or empty hides the row.</summary>
        public void SetNodeRow(int index, string text, Color tint)
        {
            if (nodeRows == null || index < 0 || index >= nodeRows.Length) return;
            if (nodeRows[index] == null) return;
            nodeRows[index].text = text ?? string.Empty;
            nodeRows[index].color = tint;
        }

        /// <summary>The transient instruction line ("Ready to block? Click one of your ...").</summary>
        public void SetNotice(string message)
        {
            if (noticeText != null) noticeText.text = message ?? string.Empty;
        }

        /// <summary>The win/loss overlay. Empty clears it.</summary>
        public void SetResult(string text, Color tint)
        {
            if (resultText == null || result == null) return;
            bool show = !string.IsNullOrEmpty(text);
            result.gameObject.SetActive(show);
            if (!show) return;
            resultText.text = text;
            resultText.color = tint;
        }

        /// <summary>Read-only view of the banner, for tests.</summary>
        public string BannerText => bannerText != null ? bannerText.text : string.Empty;

        /// <summary>Read-only view of the key legend, for tests.</summary>
        public string HintText => hintText != null ? hintText.text : string.Empty;

        /// <summary>Read-only view of one side's status readout, for tests.</summary>
        public string StatusText(PlayerIndex who)
        {
            TextMeshProUGUI target = who == PlayerIndex.Player ? playerBody : opponentBody;
            return target != null ? target.text : string.Empty;
        }

        /// <summary>Read-only view of one node row, for tests.</summary>
        public string NodeRowText(int index)
        {
            return nodeRows != null && index >= 0 && index < nodeRows.Length && nodeRows[index] != null
                ? nodeRows[index].text
                : string.Empty;
        }

        /// <summary>Read-only view of the notice line, for tests.</summary>
        public string NoticeText => noticeText != null ? noticeText.text : string.Empty;

        /// <summary>Read-only view of the result overlay, for tests.</summary>
        public string ResultText => resultText != null ? resultText.text : string.Empty;

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

            // Top-right corner, hanging off the same bezel the node list hangs off on the left. Anchored
            // rather than free-positioned so the panel stays glued to the corner at any window size instead of
            // drifting into the board.
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
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
        /// The last text this panel was told to display. SetLog is called from the presenter's paint pass, which
        /// runs sixty times a second, and the fit measurement below costs a mesh rebuild - so the string is
        /// compared before anything is measured and an unchanged log costs one string comparison.
        /// </summary>
        string logShown;

        /// <summary>
        /// Shows the most recent entries of a chronological event log, oldest at the top, which is the order a
        /// reader expects to scan down into "what is happening now" - and never more of them than the panel can
        /// actually draw.
        /// <para>
        /// Unlike the tabletop version these lines are not truncated to a fixed character count: the panel wraps,
        /// so a long event stays legible instead of being cut off mid-word. Wrapping is also what makes the entry
        /// count a promise this panel cannot keep. Four entries of the length the engine really writes lay out
        /// seven lines, the body holds six at 15pt, and TMP's Truncate overflow stops parsing at the wall and eats
        /// the tail - so the shipped panel silently dropped the newest event, the one line a player is reading
        /// for. The window therefore starts at <see cref="maxLines"/> and drops from the oldest end until the text
        /// measures whole. Losing a finished older line is a loss the player can see; losing the current one
        /// mid-word is a lie.
        /// </para>
        /// </summary>
        public void SetLog(IList<string> log)
        {
            if (body == null)
            {
                Build();
            }

            int count = log == null ? 0 : log.Count;
            int start = Mathf.Max(0, count - MaxLines);
            string joined = JoinEntries(log, start, count);
            if (joined == logShown)
            {
                return;
            }

            // characterCount is the post-parse total, so a shortfall against the joined length is the one signal
            // that cannot lie: characters that never reached the screen. One entry is as far as this backs off,
            // because a single event longer than the panel is a wording problem for the engine rather than a
            // reason to show the player nothing.
            while (start < count - 1)
            {
                logShown = joined;
                body.text = joined;
                body.ForceMeshUpdate();
                if (body.textInfo.characterCount >= joined.Length)
                {
                    return;
                }

                start++;
                joined = JoinEntries(log, start, count);
            }

            logShown = joined;
            body.text = joined;
        }

        /// <summary>The entries from <c>start</c> to <c>count</c>, newline-joined, oldest first.</summary>
        static string JoinEntries(IList<string> log, int start, int count)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < count; i++)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(log[i]);
            }
            return sb.ToString();
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
