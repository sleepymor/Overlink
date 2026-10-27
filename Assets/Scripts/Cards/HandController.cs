using System;
using System.Collections;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    [RequireComponent(typeof(DeckController))]
    public class HandController : MonoBehaviour
    {
        /// <summary>How the hand arranges its cards.</summary>
        public enum HandLayoutMode
        {
            /// <summary>Overlapping, arched and yawed fan - the in-match hand.</summary>
            Fan,
            /// <summary>Uncrowded grid, one card per cell, no overlap or rotation. Used by the
            /// collection preview so each tier's frame and shader can be judged on its own.</summary>
            Gallery
        }

        [Header("Refs (auto-created/found if empty)")]
        public DeckController deck;
        public Transform deckPile;
        public Transform playSlot;
        public Transform discardPile;

        [Header("Played card, camera-attached (optional)")]
        [Tooltip("When set, a played card is flown in front of this transform - the match camera rig - and "
            + "held facing the lens instead of lying flat on the tabletop. The board is pitched 52 degrees, "
            + "so a card parked on playSlot is read at a steep angle and its text is unusable for the whole "
            + "resolve. Parented to the camera it also rides every zoom and pan for free rather than "
            + "sliding across the board underneath. Leave empty to keep the playSlot behaviour, which is "
            + "what the standalone preview scenes want: there is no board to read past, so the card should "
            + "stay in the fan's own plane.")]
        public Transform playCamera;
        [Tooltip("Where the card hangs in playCamera space. A camera looks down its own +Z, so +Z is the "
            + "distance in front of the lens and +Y is up on screen. The rotation is always identity there, "
            + "which is what makes it work: a card face is built on its local -Z side, and at that pose -Z "
            + "points back along the view axis into the camera, so the card squares to the screen at any "
            + "pitch. Distance is measured against the rig's 38 degree field of view: the visible frame is "
            + "0.689 * distance tall, so at 4.2 it is 2.89 units and a 1.4615 card at playCardScale occupies "
            + "just over half the screen height without swallowing the board behind it.")]
        public Vector3 playCardOffset = new Vector3(0f, 0.22f, 4.2f);
        [Tooltip("Scale of the card while it is being presented to the camera.")]
        public float playCardScale = 1.15f;
        [Tooltip("How long the card holds in front of the lens before flying to the discard pile, on top "
            + "of the resolveDuration tween. Defaults to the 0.45 s the tabletop path has always used, so "
            + "assigning playCamera changes nothing about timing unless this is raised with it; the "
            + "gameplay scene holds longer because a card worth centring is worth reading.")]
        public float playCardHoldSeconds = 0.45f;

        [Header("External match driving (optional)")]
        [Tooltip("When false, Start() does not auto-draw from DeckController. An external owner calls SetHand/AddCard instead.")]
        public bool autoDraw = true;

        [Header("Presentation")]
        public CardFrameCatalog frameCatalog;
        public CardTemplateCatalog templateCatalog;
        public CardPresentationFixture placeholderFixture;
        public CardTierMaterialSet tierMaterials;

        [Header("Hand space (optional)")]
        [Tooltip("Where the hand's CARDS live. Leave empty for the classic case, in which the hand lies in "
            + "this component's own plane - the tabletop when that plane is pitched. Assign a transform "
            + "parented to the match camera and the whole hand becomes screen-fixed: the cards stand square "
            + "to the lens and hold their place on the screen through every pan and zoom.\n\n"
            + "That is not cosmetic. The fan's arc and yaw were written for a hand seen head-on. On a table "
            + "pitched 52 degrees the arc is mostly a displacement ALONG the view axis, so perspective "
            + "resizes each card by a different amount and the row reads as scattered instead of fanned, "
            + "while yaw turns every non-central card away from the lens. In the camera's own plane both "
            + "tricks do exactly what they were written to do, with no change to the math.")]
        public Transform handRoot;

        [Header("Fan layout")]
        public int handSizeStart = 5;
        // Every frame is authored on the same 832x1216 canvas, so the whole hand keeps that
        // aspect. CardFaceView picks the per-tier inner art window; the root size never varies.
        public Vector3 cardSize = new Vector3(1.0f, 1.4615f, 0.06f);
        public float fanSpacing = 1.45f;
        public float arcHeight = 0.35f;
        public float maxYaw = 16f;
        [Tooltip("Fold the row onto one rivet, the way an actual folding fan is built. On, every card is "
            + "the centre card rotated about a single point below the row, so each neighbouring pair is "
            + "congruent: the overlap between cards is then identical along the whole row and at every "
            + "height, instead of opening at one corner and pinching shut at the other. Off, each card "
            + "rolls about its own middle, which is the older look. Ignored when the roll is zero. Note "
            + "that this overrides arcHeight - a fan of a given width and roll can only have one rise, and "
            + "the rivet computes it.")]
        public bool fanPivotsOnRivet;
        public float handY = -2.6f;
        [Tooltip("Widest the row of card CENTRES may get, in the hand's own units. Zero disables the "
            + "squeeze, so the fan keeps spreading by fanSpacing however many cards it holds - which is "
            + "what the standalone previews want, since nothing there shares its table with other "
            + "geometry. Set it where the hand lies beside a board, because nothing in the match caps the "
            + "hand size: past this width the spacing closes up instead of the row walking over whatever "
            + "sits at either end of it.")]
        public float maxHandSpan;

        [Header("Gallery layout (collection preview)")]
        [Tooltip("Fan is the in-match hand. Gallery lays the cards out as an uncrowded grid, one per row, "
            + "so a tier's frame and shader can be judged without a neighbour overlapping it.")]
        public HandLayoutMode layoutMode = HandLayoutMode.Fan;
        [Tooltip("Gallery only: one fixture per card, in row-major order. Empty falls back to placeholderFixture.")]
        public CardPresentationFixture[] galleryFixtures;
        public int galleryColumns = 4;
        public float galleryCellWidth = 1.16f;
        public float galleryCellHeight = 1.7f;
        // World position of the FIRST cell's centre; the grid grows right and down from there,
        // horizontally centred on x = 0.
        public float galleryTopY = -0.15f;

        [Header("Motion")]
        public float layoutDuration = 0.28f;
        public float drawDuration = 0.38f;
        public float hoverDuration = 0.12f;
        public float resolveDuration = 0.25f;
        public float hoverLift = 0.45f;
        public float hoverScale = 1.12f;
        [Tooltip("How far a hovered card travels along the hand's own depth axis. Negative is toward the "
            + "player in either space the hand is laid out in: up out of a table tilted 90 degrees, or back "
            + "down the view axis in the camera's own plane. The same number means very different things in "
            + "the two - one unit off a board sixteen units away is a nudge, while one unit off a hand hung "
            + "a handful of units from the lens is a lunge - so a camera-held hand wants this smaller.")]
        public float hoverDepth = -0.4f;
        [Tooltip("How far a picked-up card travels along the same axis, and the depth the drag holds it at.")]
        public float dragDepth = -1f;
        [Tooltip("How far a card pulled aside for inspection sits along the same axis. It travels there on "
            + "a tween rather than a snap, so this only has to be shallow enough not to swallow the screen.")]
        public float inspectDepth = -1.1f;
        [Tooltip("Seconds spent easing from the plane the pickup found the card on out to dragDepth. Zero "
            + "keeps the historic instant snap, which is invisible on a tabletop hand and reads as a jolt "
            + "when the hand hangs in front of the lens.")]
        public float dragPickupSeconds;
        public float playLineY = 1.2f;
        public float tiltFactor = 0.6f;
        public float maxTilt = 18f;

        [Header("Pointer interaction")]
        [Tooltip("Holding a card still for this long magnifies it; moving the pointer begins a drag instead.")]
        public float holdToZoomSeconds = 0.35f;
        public float dragThresholdPixels = 14f;
        public float heldScale = 1.85f;

        // Sorting order is global, so a hand of overlapping cards needs one slice of that space per
        // card. Without it card A's labels (local order 20) draw across card B's frame (local 0) and
        // art (local -10), which is the bleed visible when the fan stacks. Stride 100 leaves room
        // for the local offsets; the focus slice sits far above so a lifted card clears the hand.
        const int SortSliceStride = 100;
        const int DiscardSlice = 50;
        const int HoverSlice = 10000;
        const int DragSlice = 10100;
        const int InspectSlice = 10200;
        const int PlaySlice = 10300;

        readonly List<CardView> cards = new List<CardView>();
        CardView hovered;
        CardView dragged;
        CardView pressed;
        Vector2 pressPosition;
        float pressTime;
        bool showingHeldZoom;
        Camera cam;
        Plane dragPlane = new Plane(Vector3.forward, Vector3.zero);
        Vector3 grabOffset;
        float lastDragX;
        float dragFromDepth;
        float dragElapsed;

        /// <summary>
        /// Optional external authority for plays. When set, a card released above the play line is offered
        /// here first; returning false bounces the card back into the hand untouched. The match engine owns
        /// cost and legality, so the hand never decides those itself.
        /// </summary>
        public Func<CardData, bool> playResolver;

        /// <summary>
        /// Optional drop-target probe supplied by the match owner, set together with
        /// <see cref="playResolverWithSlot"/>. Called every frame a card is dragged with the card's current
        /// <em>world</em> position; it returns the board slot the play would land in, or null when the card
        /// is over nothing usable. The owner also draws the highlight for that slot, so the hand stays
        /// ignorant of the board's geometry.
        /// <para>
        /// A non-null result also lets a release deploy the card <em>without</em> crossing
        /// <see cref="playLineY"/>. That matters because the player's own Active and Bench ranks sit below
        /// that line: with the height test as the only gate, dropping onto the Bench was physically
        /// impossible and every unit silently went to the first free slot instead.
        /// </para>
        /// The hand cannot see the board itself - <c>Overlink.Cards</c> does not reference
        /// <c>Overlink.Match</c> - so the owner injects this exactly like <see cref="playResolver"/>.
        /// </summary>
        public Func<CardData, Vector3, BoardSlot> dropTargetResolver;

        /// <summary>
        /// Play gate that receives the slot <see cref="dropTargetResolver"/> nominated. Takes precedence
        /// over <see cref="playResolver"/> when set; the slot may be null on a plain above-the-line fling.
        /// </summary>
        public Func<CardData, BoardSlot, bool> playResolverWithSlot;

        /// <summary>
        /// Consulted ahead of <see cref="playResolverWithSlot"/> when a dragged card is released over a socket,
        /// and the opening lineup's way of saying that a card is not being <em>played</em>, it is being
        /// <em>fielded</em>. Returning true consumes the drag: the view is retired where it was dropped,
        /// <see cref="CardNominated"/> fires, and the play path never runs. No NP is spent, no card reaches the
        /// discard pile, and the engine is asked nothing at all - because a nomination can still be taken back.
        /// <para>
        /// It has to be its own terminal rather than a flag on the play gate, because the play gate's promise is
        /// that the engine accepted the card and the hand then sends the view onward to the pile it entered.
        /// Flying an offered card to the discard pile to nominate it would be a lie the player watches happen,
        /// and asking the engine to place a card while setup is open is refused by design.
        /// </para>
        /// </summary>
        public Func<CardData, BoardSlot, bool> nominateResolver;

        /// <summary>Clears the owner's drop highlight when a drag ends for any reason.</summary>
        public Action dropTargetCleared;

        /// <summary>Injected by the match UI to keep a screen-space button from also grabbing a hand card.</summary>
        public Func<Vector2, bool> pointerOverControls;

        // The slot under the card as of the last drag update, replayed to the gate on release.
        BoardSlot pendingDropSlot;

        /// <summary>Raised after a play was accepted by the resolver and has finished animating to the play slot.</summary>
        public event Action<CardData> CardPlayed;

        /// <summary>
        /// Raised after <see cref="nominateResolver"/> accepted a drop, carrying the socket it was accepted on.
        /// The view is already retired by then, so whatever the front end shows in its place - a preview body on
        /// that socket, say - is entirely its own business.
        /// </summary>
        public event Action<CardData, BoardSlot> CardNominated;

        public int CardCount => cards.Count;

        /// <summary>The live views, in slot order. Read-only; the gallery captions use it.</summary>
        public IReadOnlyList<CardView> Cards => cards;

        /// <summary>Snapshot of the data currently shown in the hand.</summary>
        public IReadOnlyList<CardData> HandData
        {
            get
            {
                var data = new List<CardData>(cards.Count);
                foreach (CardView card in cards)
                {
                    if (card != null)
                    {
                        data.Add(card.Data);
                    }
                }

                return data;
            }
        }

        /// <summary>
        /// The space the hand's cards are posed in: <see cref="handRoot"/> when one is assigned, this
        /// component's own transform otherwise.
        /// <para>
        /// Only the cards move. The deck, play and discard anchors stay children of the component, because
        /// they name places on the board - so a screen-fixed hand still draws from the pile on the table and
        /// still discards to the pile on the table. Every pose the layout, hover and drag code writes is a
        /// LOCAL one, which is what lets the fan keep its own math in either space.
        /// </para>
        /// </summary>
        Transform CardSpace => handRoot != null ? handRoot : transform;

        /// <summary>
        /// The pose of card <paramref name="i"/> in a row of <paramref name="n"/>.
        /// <para>
        /// The unpivoted form is a row of card CENTRES laid out at <paramref name="spacing"/>, each raised
        /// off a parabola peaking at <paramref name="arc"/> and rolled about its OWN middle by up to
        /// <paramref name="yawDeg"/>. That reads as a fan and is not one: because every card pivots about
        /// itself, card i+1 sits rotated about i's centre, so the edge they share opens out at one end and
        /// pinches shut at the other. The overlap is therefore different for every neighbouring pair, and
        /// different again between the top and the bottom of the same pair - which is the slanted slice
        /// across a name banner, and why it gets worse toward the ends of the row, where the roll is
        /// largest. <see cref="fanPivotsOnRivet"/> exists to cancel that.
        /// </para>
        /// </summary>
        public static void ComputeSlot(int i, int n, float spacing, float arc,
            float yawDeg, float y, out Vector3 pos, out Quaternion rot)
        {
            ComputeSlot(i, n, spacing, arc, yawDeg, y, false, out pos, out rot);
        }

        /// <summary>
        /// The same slot, optionally folded onto a single rivet.
        /// <para>
        /// Pivoted, every card is the centre card rotated about one point <em>r</em> below the row, so
        /// neighbouring pair (i, i+1) is exactly pair (i-1, i) turned by the same step through the same
        /// angle. Congruent by construction: the overlap between neighbours is then identical the whole
        /// length of the row, at every height, and the row reads as one folding fan instead of five cards
        /// tilting past each other. The roll still grows toward the ends - that is what makes it a fan -
        /// but it no longer changes how much of a card is hidden.
        /// </para>
        /// <para>
        /// The radius falls out of the two dials the row already has: the outermost card must both sit at
        /// the half-row width <c>spacing * (n - 1) / 2</c> and be rolled by the full
        /// <paramref name="yawDeg"/>, so <c>r = halfRow / sin(yawDeg)</c>. That also fixes the arc, which
        /// is why <paramref name="arc"/> is ignored here: the centre card stands <c>r * (1 - cos yaw)</c>
        /// above the outer ones, and that is the only rise a fan of that width and that roll can have.
        /// Setting it by hand is precisely the bug this replaces - an arc and a roll tuned independently
        /// cannot both be right.
        /// </para>
        /// </summary>
        public static void ComputeSlot(int i, int n, float spacing, float arc,
            float yawDeg, float y, bool pivoted, out Vector3 pos, out Quaternion rot)
        {
            float c = (n - 1) * 0.5f;
            float t = n <= 1 ? 0f : (i - c) / Mathf.Max(1f, c);
            float phi = -t * yawDeg;
            rot = Quaternion.Euler(0f, 0f, phi);

            // A degenerate roll has no rivet to fold onto (r would be infinite), and a lone card is its
            // own row; both fall back to the plain placement rather than dividing by zero.
            if (!pivoted || n < 2 || Mathf.Abs(yawDeg) < 1e-4f)
            {
                // z stays 0 for every card in the row. A per-card depth stair is the same bug as a per-layer
                // offset inside the face: it pushes each neighbour slightly along the view axis, so the
                // magnification differs across the row and the cards stop being congruent. Overlap order is
                // sortingOrder's business (HandController raises the baseline of a lifted card), not depth's.
                pos = new Vector3((i - c) * spacing, y + arc * (1f - t * t), 0f);
                return;
            }

            float halfRow = c * spacing;
            float r = halfRow / Mathf.Sin(Mathf.Abs(yawDeg) * Mathf.Deg2Rad);
            float rad = phi * Mathf.Deg2Rad;
            pos = new Vector3(-r * Mathf.Sin(rad), y - r * (1f - Mathf.Cos(rad)), 0f);
        }

        /// <summary>
        /// Grid slot for the collection gallery: row-major, horizontally centred, no arc and no
        /// yaw, so a card is never partly hidden behind its neighbour. Unlike the fan the cells are
        /// fixed size, so adding a tier fills the next cell instead of squeezing the row.
        /// </summary>
        public static void ComputeGallerySlot(int i, int n, int columns, float cellWidth,
            float cellHeight, float topY, out Vector3 pos, out Quaternion rot)
        {
            int cols = Mathf.Max(1, columns);
            int row = i / cols;
            int col = i % cols;
            // Centre each row on x = 0 using that row's own card count, so a short final row does
            // not hang off to one side.
            int rowStart = row * cols;
            int inRow = Mathf.Min(cols, n - rowStart);
            float x = (col - (inRow - 1) * 0.5f) * cellWidth;
            pos = new Vector3(x, topY - row * cellHeight, 0f);
            rot = Quaternion.identity;
        }

        /// <summary>Resolves slot <paramref name="i"/> under the active layout mode.</summary>
        void SlotFor(int i, out Vector3 pos, out Quaternion rot)
        {
            if (layoutMode == HandLayoutMode.Gallery)
            {
                ComputeGallerySlot(i, cards.Count, galleryColumns, galleryCellWidth,
                    galleryCellHeight, galleryTopY, out pos, out rot);
                return;
            }
            ComputeSlot(i, cards.Count, EffectiveFanSpacing(), arcHeight, maxYaw, handY,
                fanPivotsOnRivet, out pos, out rot);
        }

        /// <summary>
        /// The spacing the fan uses this frame: <see cref="fanSpacing"/>, closed up when the hand has
        /// grown past the corridor <see cref="maxHandSpan"/> describes.
        /// <para>
        /// Squeezing the spacing rather than capping the hand keeps the decision where it belongs. The
        /// match owns no hand-size rule, and a view that silently refused a seventh card would be a second
        /// authority inventing one; overlapping cards lose some of their text to the neighbour, which the
        /// click-to-read reader and the camera presentation are both there to solve, while a row that grew
        /// off the table would bury cards outright with no way to reach them.
        /// </para>
        /// </summary>
        float EffectiveFanSpacing()
        {
            if (maxHandSpan <= 0f || cards.Count < 2)
            {
                return fanSpacing;
            }

            // Span is measured between the two OUTER CENTRES, so n cards leave n-1 gaps in it.
            return Mathf.Min(fanSpacing, maxHandSpan / (cards.Count - 1));
        }

        void Awake()
        {
            cam = Camera.main;
            if (deck == null) deck = GetComponent<DeckController>();
            deckPile = EnsureAnchor(deckPile, "DeckPile", new Vector3(-4.2f, 0f, 0f));
            playSlot = EnsureAnchor(playSlot, "PlaySlot", new Vector3(0f, 2.6f, 0f));
            discardPile = EnsureAnchor(discardPile, "DiscardPile", new Vector3(4.2f, 0f, 0f));
        }

        void OnValidate()
        {
            cardSize.x = Mathf.Max(0.1f, cardSize.x);
            cardSize.y = Mathf.Max(0.1f, cardSize.y);
            cardSize.z = Mathf.Max(0.01f, cardSize.z);
            fanSpacing = Mathf.Max(0.05f, fanSpacing);
            maxHandSpan = Mathf.Max(0f, maxHandSpan);
        }

        public void ResizeHand(Vector3 newSize)
        {
            cardSize = new Vector3(
                Mathf.Max(0.1f, newSize.x),
                Mathf.Max(0.1f, newSize.y),
                Mathf.Max(0.01f, newSize.z));
            foreach (var card in cards)
            {
                if (card == null) continue;
                card.SetSize(cardSize);
            }
            LayoutAll();
        }

        Transform EnsureAnchor(Transform t, string anchorName, Vector3 pos)
        {
            if (t != null) return t;
            var go = new GameObject(anchorName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        /// <summary>
        /// Starts a drawn card small, at the deck pile, ready to tween into its slot.
        /// <para>
        /// Written in world space on purpose. The pile is a child of this component while the cards may live
        /// under a camera-held <see cref="handRoot"/>, so copying the pile's local position across would
        /// teleport the card to an unrelated point the moment the two spaces stop coinciding.
        /// </para>
        /// </summary>
        void PoseAtDeckPile(CardView card)
        {
            card.transform.position = deckPile != null ? deckPile.position : CardSpace.position;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one * 0.6f;
        }

        IEnumerator Start()
        {
            yield return null;
            if (layoutMode == HandLayoutMode.Gallery)
            {
                PopulateGallery();
                yield break;
            }
            if (!autoDraw)
            {
                // An external match owner is driving this hand; it will call SetHand/AddCard itself.
                yield break;
            }

            for (int i = 0; i < handSizeStart; i++)
            {
                DrawOne();
                yield return new WaitForSeconds(0.1f);
            }
        }

        void Update()
        {
            if (cam == null) return;
            if (dragged == null && pointerOverControls != null && pointerOverControls(Input.mousePosition))
            {
                if (hovered != null) { Unhover(hovered); hovered = null; }
                return;
            }

            // Debug draw/discard only makes sense for the standalone presentation scene. When a match
            // owns the hand, these keys would desync the view from the authoritative state. The gallery
            // is neither: it is a fixed set of fixture cards, so drawing or discarding would mutate the
            // very row being reviewed.
            if (autoDraw && layoutMode != HandLayoutMode.Gallery)
            {
                if (Input.GetKeyDown(KeyCode.Space)) DrawOne();
                if (Input.GetKeyDown(KeyCode.Backspace)) DiscardLast();
            }

            if (dragged != null)
            {
                UpdateDrag();
                if (Input.GetMouseButtonUp(0)) ReleaseDrag();
                return;
            }

            if (pressed != null)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    CardView heldCard = pressed;
                    bool wasZoomed = showingHeldZoom;
                    pressed = null;
                    showingHeldZoom = false;
                    if (wasZoomed && heldCard != null)
                    {
                        // A hold is a preview, not a new hover state. Return to the normal fan scale before
                        // the next hover pass so the card never remains permanently enlarged after release.
                        Unhover(heldCard);
                        Hover(heldCard);
                    }
                    UpdateHover();
                    return;
                }

                if ((new Vector2(Input.mousePosition.x, Input.mousePosition.y) - pressPosition).sqrMagnitude
                    > dragThresholdPixels * dragThresholdPixels)
                {
                    CardView card = pressed;
                    pressed = null;
                    showingHeldZoom = false;
                    BeginDrag(card);
                    UpdateDrag();
                    return;
                }

                if (!showingHeldZoom && Time.unscaledTime - pressTime >= holdToZoomSeconds)
                {
                    showingHeldZoom = true;
                    // An inspected card is read on its own, so it takes the topmost slice and no
                    // neighbour's frame or labels can cut across its art, rails or stats.
                    pressed.SetSortBaseline(InspectSlice);
                    SlotFor(pressed.SlotIndex,
                        out Vector3 pos, out _);
                    pos.y += hoverLift + 0.65f;
                    pos.z = inspectDepth;
                    pressed.Tween.Play(pos, Quaternion.identity, pressed.BaseScale * heldScale,
                        hoverDuration, CardEase.OutCubic);
                }
                return;
            }

            UpdateHover();
            if (hovered != null && Input.GetMouseButtonDown(0))
            {
                pressed = hovered;
                pressPosition = Input.mousePosition;
                pressTime = Time.unscaledTime;
            }
        }

        public void DrawOne()
        {
            if (deck == null) return;
            CardData data = deck.Draw();
            if (data == null)
            {
                Debug.Log("[Hand] Deck and discard are both empty — nothing to draw.");
                return;
            }
            CardView card = CreateView(data);
            PoseAtDeckPile(card);
            LayoutAll();
            SlotFor(card.SlotIndex,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, drawDuration, CardEase.OutCubic);
            Debug.Log($"[Hand] Drew {data.title} ({cards.Count} in hand).");
        }

        /// <summary>
        /// Replaces the hand contents with exactly <paramref name="data"/>, keeping already-shown views
        /// alive so their positions tween instead of popping. Intended for a match owner that treats the
        /// Core MatchState as authoritative.
        /// <para>
        /// Reuse depends on reference identity: the caller must pass the same <see cref="CardData"/>
        /// instances across calls (cache one per card instance), otherwise every sync rebuilds the hand.
        /// </para>
        /// </summary>
        public void SetHand(IReadOnlyList<CardData> data)
        {
            var wanted = new HashSet<CardData>();
            if (data != null)
            {
                foreach (CardData item in data)
                {
                    if (item != null)
                    {
                        wanted.Add(item);
                    }
                }
            }

            for (int i = cards.Count - 1; i >= 0; i--)
            {
                CardView card = cards[i];
                if (card == null || card.Data == null || !wanted.Contains(card.Data))
                {
                    RemoveView(card);
                }
            }

            var present = new HashSet<CardData>();
            foreach (CardView card in cards)
            {
                if (card != null) present.Add(card.Data);
            }

            if (data != null)
            {
                foreach (CardData item in data)
                {
                    if (item != null && !present.Contains(item))
                    {
                        CreateView(item);
                    }
                }
            }

            LayoutAll();
        }

        /// <summary>Adds a single card to the end of the hand with the normal draw animation.</summary>
        public void AddCard(CardData data)
        {
            if (data == null) return;
            CardView card = CreateView(data);
            PoseAtDeckPile(card);
            LayoutAll();
            SlotFor(card.SlotIndex,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, drawDuration, CardEase.OutCubic);
        }

        /// <summary>Immediately removes a card from the hand without the discard animation.</summary>
        public void RemoveCard(CardData data)
        {
            if (data == null) return;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i] != null && cards[i].Data == data)
                {
                    RemoveView(cards[i]);
                    LayoutAll();
                    return;
                }
            }
        }

        CardView CreateView(CardData data)
        {
            return CreateView(data, placeholderFixture);
        }

        /// <summary>
        /// Creates one hand view. <paramref name="fixture"/> supplies the face when <paramref name="data"/>
        /// carries no bound CardDefinition, which is exactly the gallery case: one fixture per tier.
        /// </summary>
        CardView CreateView(CardData data, CardPresentationFixture fixture)
        {
            var card = CardView.Create(data, CardSpace, cardSize, frameCatalog, templateCatalog, fixture, tierMaterials);
            card.SlotIndex = cards.Count;
            cards.Add(card);
            return card;
        }

        /// <summary>
        /// Builds the collection gallery: one card per <see cref="galleryFixtures"/> entry, laid out by
        /// <see cref="ComputeGallerySlot"/>. Cards are driven purely by their fixture (no CardData, no
        /// deck), so a row can show Common, Rare, Epic and Legendary side by side in one scene.
        /// </summary>
        void PopulateGallery()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null) Destroy(cards[i].gameObject);
            }
            cards.Clear();

            if (galleryFixtures == null || galleryFixtures.Length == 0)
            {
                if (placeholderFixture != null)
                {
                    CreateView(null, placeholderFixture);
                }
            }
            else
            {
                for (int i = 0; i < galleryFixtures.Length; i++)
                {
                    if (galleryFixtures[i] != null) CreateView(null, galleryFixtures[i]);
                }
            }

            LayoutAll();
            for (int i = 0; i < cards.Count; i++)
            {
                SlotFor(i, out Vector3 pos, out Quaternion rot);
                cards[i].transform.localPosition = pos;
                cards[i].transform.localRotation = rot;
            }
        }

        void RemoveView(CardView card)
        {
            if (card == null) return;
            cards.Remove(card);
            if (hovered == card) hovered = null;
            if (dragged == card) dragged = null;
            if (pressed == card) { pressed = null; showingHeldZoom = false; }
            Destroy(card.gameObject);
        }

        public void DiscardLast()
        {
            if (cards.Count == 0) return;
            var card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            if (hovered == card) hovered = null;
            LayoutAll();
            StartCoroutine(SendToDiscard(card));
        }

        void LayoutAll()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                card.SlotIndex = i;
                bool focused = card == dragged || card == hovered || card == pressed;
                // Resting order in the fan decides which card covers which. A focused card keeps the
                // raised slice Hover/Drag/Inspect gave it until it is released, otherwise a relayout
                // mid-inspect would drop it back under its neighbours.
                if (!focused) card.SetSortBaseline((i + 1) * SortSliceStride);
                if (focused) continue;
                SlotFor(i,
                    out Vector3 pos, out Quaternion rot);
                card.Tween.Play(pos, rot, card.BaseScale, layoutDuration, CardEase.OutCubic);
            }
        }

        void UpdateHover()
        {
            var hit = RaycastCard();
            if (hit != hovered)
            {
                if (hovered != null) Unhover(hovered);
                hovered = hit;
                if (hovered != null) Hover(hovered);
            }
        }

        void Hover(CardView card)
        {
            card.SetHovered(true);
            // Lift the whole card above every neighbour, not just its centre plane, so its own
            // labels stay on top of the cards behind it while it is inspected.
            card.SetSortBaseline(HoverSlice);
            SlotFor(card.SlotIndex,
                out Vector3 pos, out Quaternion rot);
            // A gallery cell is already isolated, so the fan's lift would only push the card into
            // the header; it grows in place instead.
            if (layoutMode != HandLayoutMode.Gallery)
            {
                pos.y += hoverLift;
            }
            pos.z = hoverDepth;
            card.Tween.Play(pos, rot, card.BaseScale * hoverScale, hoverDuration, CardEase.OutCubic);
        }

        void Unhover(CardView card)
        {
            card.SetHovered(false);
            card.SetSortBaseline((card.SlotIndex + 1) * SortSliceStride);
            SlotFor(card.SlotIndex,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, hoverDuration, CardEase.OutCubic);
        }

        void BeginDrag(CardView card)
        {
            if (hovered == card) hovered = null;
            pressed = null;
            showingHeldZoom = false;
            card.SetHovered(false);
            card.SetDragging(true);
            card.Tween.Stop();
            card.SetSortBaseline(DragSlice);
            dragged = card;
            dragPlane = new Plane(Vector3.forward, card.transform.localPosition);
            grabOffset = card.transform.localPosition - MouseOnPlane();
            lastDragX = card.transform.localPosition.x;
            pendingDropSlot = null;
            // The pickup records the plane it found the card on and lets UpdateDrag travel from there; it
            // must NOT move the card itself, or the eased leg would start from dragDepth and the card
            // would jump forward and then fall back. With dragPickupSeconds at zero UpdateDrag is called
            // in this same frame and puts the card on dragDepth outright, which is the historic snap.
            dragFromDepth = card.transform.localPosition.z;
            dragElapsed = 0f;
        }

        void UpdateDrag()
        {
            Vector3 target = MouseOnPlane() + grabOffset;
            dragElapsed += Time.deltaTime;
            target.z = dragPickupSeconds > 0f
                ? Mathf.Lerp(dragFromDepth, dragDepth, Mathf.Clamp01(dragElapsed / dragPickupSeconds))
                : dragDepth;
            float vx = (target.x - lastDragX) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastDragX = target.x;
            dragged.transform.localPosition = target;
            float tilt = Mathf.Clamp(-vx * tiltFactor, -maxTilt, maxTilt);
            dragged.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            // Ask the owner which socket this is over. It highlights the one it accepts, so a lit socket is
            // a promise the release can rely on, and a dark board means the play is not currently legal.
            pendingDropSlot = dropTargetResolver != null
                ? dropTargetResolver(dragged.Data, dragged.transform.position)
                : null;
        }

        void ReleaseDrag()
        {
            var card = dragged;
            dragged = null;
            card.SetDragging(false);
            dropTargetCleared?.Invoke();

            // Either a resolved socket or the classic fling past the play line. The socket route is what
            // makes the player's own ranks - which sit below that line - reachable at all.
            if (pendingDropSlot != null || (dropTargetResolver == null && card.transform.localPosition.y > playLineY)
                || (dropTargetResolver != null && card.transform.localPosition.y > playLineY
                    && playResolverWithSlot == null))
            {
                PlayCard(card);
            }
            else
            {
                SlotFor(card.SlotIndex,
                    out Vector3 pos, out Quaternion rot);
                card.Tween.Play(pos, rot, card.BaseScale, layoutDuration, CardEase.OutCubic);
            }
        }

        void PlayCard(CardView card)
        {
            // Remove the card from the hand first: the resolver may sync the hand against the match state,
            // and a view still present here would be recreated as a duplicate.
            cards.Remove(card);
            if (hovered == card) hovered = null;

            // The match engine is the authority on legality (turn, phase, NP, slot). The slot-aware gate is
            // preferred when installed, because it is the only one that can honour a targeted socket; with
            // no resolver at all the hand keeps its original standalone behaviour and plays anything.
            BoardSlot slot = pendingDropSlot;
            pendingDropSlot = null;

            // A nomination settles before the play gate is consulted at all: the card was never played, so it
            // must not be tweened to a play slot, charged for, or sent onward to a discard pile it never entered.
            // The view goes at the drop point and the front end decides what stands on the socket instead.
            CardData nominated = card.Data;
            if (slot != null && nominateResolver != null && nominateResolver(nominated, slot))
            {
                RemoveView(card);
                LayoutAll();
                CardNominated?.Invoke(nominated, slot);
                return;
            }

            bool accepted = playResolverWithSlot != null
                ? playResolverWithSlot(card.Data, slot)
                : playResolver == null || playResolver(card.Data);

            if (!accepted)
            {
                Debug.Log($"[Hand] {card.Data.title} was rejected by the match engine; returning it to the hand.");
                cards.Add(card);
                ReturnToHand(card);
                return;
            }

            LayoutAll();
            // While it resolves, the played card is the thing the player is watching: keep it above
            // the whole hand rather than letting a neighbour's rail cross it.
            card.SetSortBaseline(PlaySlice);
            StartCoroutine(ResolvePlay(card));
        }

        void ReturnToHand(CardView card)
        {
            card.SlotIndex = cards.Count;
            LayoutAll();
            SlotFor(card.SlotIndex,
                out Vector3 pos, out Quaternion rot);
            card.Tween.Play(pos, rot, card.BaseScale, layoutDuration, CardEase.OutCubic);
        }

        IEnumerator ResolvePlay(CardView card)
        {
            var done = false;
            Vector3 target = BeginPlayPresentation(card, out Quaternion rotation, out Vector3 scale);
            card.Tween.Play(target, rotation, scale, resolveDuration, CardEase.OutBack, () => done = true);
            yield return new WaitUntil(() => done);
            Debug.Log($"[Hand] Played {card.Data.title} (cost {card.Data.cost}): {card.Data.description}");
            CardPlayed?.Invoke(card.Data);
            yield return new WaitForSeconds(playCardHoldSeconds);
            StartCoroutine(SendToDiscard(card));
        }

        /// <summary>
        /// Puts a played card into the space it resolves in, and reports the pose to tween toward. The
        /// reparent and the pose are decided together on purpose: the pose is a <em>local</em> one, so it
        /// is meaningless in whichever space the card is not in.
        /// <para>
        /// With no <see cref="playCamera"/> the card stays in the hand's own plane and lands on the
        /// tabletop <see cref="playSlot"/>, exactly as it always has - which is what the preview scenes
        /// want, since there is no board there to read past. With one, the card is parented to the camera
        /// rig, so it holds its place on screen through every zoom and pan instead of sliding across the
        /// board, and the player can actually read it while the match resolves underneath.
        /// </para>
        /// </summary>
        Vector3 BeginPlayPresentation(CardView card, out Quaternion rotation, out Vector3 scale)
        {
            if (playCamera == null)
            {
                rotation = Quaternion.identity;
                scale = card.BaseScale * 1.35f;
                return playSlot.localPosition;
            }

            // worldPositionStays, so the frame the reparent lands on is visually identical and the tween
            // measures its start pose from where the card actually is rather than from a jumped one.
            card.transform.SetParent(playCamera, true);
            // The card left the hand before this ran, so its collider is only an obstacle now: centred in
            // front of the lens it would swallow the raycast that picks up the cards behind it.
            Collider col = card.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Identity is the one pose here that needs no maths: the card sits at +Z in camera space,
            // which is in front of a lens that looks down its own +Z, and a card face is built on its local
            // -Z side - so at identity that face points back into the camera, square to the screen, at any
            // pitch. Including the overhead bird's-eye view, where a tabletop card is seen edge-on.
            rotation = Quaternion.identity;
            scale = card.BaseScale * playCardScale;
            return playCardOffset;
        }

        /// <summary>
        /// Brings a card back into this component's own space, so the discard flight - which names a place on
        /// the board rather than a place on the screen - can be expressed in it. That covers both ways a card
        /// can have left home: the camera it was presented on, and a screen-fixed hand root it was laid out
        /// under. A no-op for a card that never left, which is what makes it safe to call on every path.
        /// </summary>
        void EndPlayPresentation(CardView card)
        {
            if (card.transform.parent == transform)
            {
                return;
            }

            card.transform.SetParent(transform, true);
            Collider col = card.GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }

        IEnumerator SendToDiscard(CardView card)
        {
            if (deck != null) deck.Discard(card.Data);
            // discardPile.localPosition is a point in the hand's plane, so the card has to stop tracking
            // the camera before it can be tweened to it.
            EndPlayPresentation(card);
            // Shrinking toward the discard pile, it must slide behind the hand instead of covering
            // the cards it is leaving.
            card.SetSortBaseline(DiscardSlice);
            var done = false;
            card.Tween.Play(discardPile.localPosition, Quaternion.identity,
                card.BaseScale * 0.4f, resolveDuration, CardEase.OutCubic, () => done = true);
            yield return new WaitUntil(() => done);
            Destroy(card.gameObject);
        }

        CardView RaycastCard()
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var view = hit.collider.GetComponent<CardView>();
                if (view != null && cards.Contains(view) && view != dragged) return view;
            }
            return null;
        }

        /// <summary>
        /// True while the pointer belongs to the hand: either a card is under it, or a drag is in flight.
        /// <para>
        /// The board's click layer asks this before it takes a click. The hand fan lies inside the tabletop's
        /// footprint, so a click on a card also lands on a socket behind it, and without this guard picking a
        /// card up would simultaneously select the unit standing on the tile beneath it.
        /// </para>
        /// </summary>
        public bool IsPointerOverHand()
        {
            if (dragged != null)
            {
                return true;
            }

            return pressed != null || cam != null && RaycastCard() != null;
        }

        Vector3 MouseOnPlane()
        {
            // Work in CardSpace, not this component's space, so the same drag math serves the upright
            // prototype hand, a hand laid flat on a pitched table, and a hand rooted in the camera's own
            // plane - whichever one the cards were actually parented to.
            Ray worldRay = cam.ScreenPointToRay(Input.mousePosition);
            Ray localRay = new Ray(
                CardSpace.InverseTransformPoint(worldRay.origin),
                CardSpace.InverseTransformDirection(worldRay.direction));
            if (dragPlane.Raycast(localRay, out float d)) return localRay.GetPoint(d);
            return dragged != null ? dragged.transform.localPosition : Vector3.zero;
        }
    }
}
