using Overlink.Cards;
using UnityEngine;

namespace Overlink.Match
{
    /// <summary>
    /// Gets the resting hand out of the way of the two piles it sits on top of.
    /// <para>
    /// The deck and the discard are printed onto the tabletop at board x -3 and +3, and they are clickable:
    /// <see cref="BattlefieldInput"/> turns a click on one into the deck browser and the discard viewer. At
    /// the shipped framing they land inside the resting fan's screen rectangle - entirely, from a four-card
    /// hand upward, and the match opens by dealing five. There is no board-space answer: clearing the fan's
    /// x-span at the piles' depth needs |x| at least 4.5, and the node plates own that lane at exactly that
    /// depth, so moving a pile lands it on NODE 1. So the hand moves instead of the board.
    /// </para>
    /// <para>
    /// While the pointer is away from the fan it sinks, and the piles come out from under it; the pointer
    /// goes back to the hand and it rises, whole, to be read. Which is the whole design, and the two rules
    /// below are what keep it from fighting itself.
    /// </para>
    /// <para>
    /// <b>How far it falls is measured, not budgeted.</b> The requirement is set by the piles' screen rects:
    /// the fan's top edge has to finish below the lower pile's visible bottom edge. Measured against the
    /// shipped rig, a five-card hand pays 0.166 of the frame's height for that - 44 percent of its own height,
    /// of which 0.152 is the bare tile and the rest the furniture margin and the air the rule leaves between
    /// the two - and the same figure came out at all seven supported aspects, because the fan and the board
    /// scale together. What the piles need is therefore recomputed every frame from what the lens is actually
    /// showing, so it follows the zoom and the aspect without a constant being retuned for either.
    /// <c>IdleHandSinkTests</c> pins the table this paragraph quotes.
    /// </para>
    /// <para>
    /// <b>And it never falls further than half the hand.</b> Sinking is a trade of legibility for reachability
    /// and it is a bad trade at the wrong depth: at the opening framing the piles need 0.166 of the frame out
    /// of the 0.380 a five-card fan holds, but zoomed in they walk off the bottom of the screen - at
    /// minDistance seven the whole tile is below the frame - and the drop that would uncover a partly seen pile
    /// is larger than the fan itself. Rather than park the hand one card-height above the bottom of the frame
    /// for no benefit, an uncovering that needs more than <see cref="sinkCapFraction"/> of the fan's own height
    /// does not happen at all - the hand stays up, and the action rail's pile buttons, which exist for exactly
    /// this, stay the way in. Measured spend runs 0.461 of the fan's height at three cards down to 0.385 at
    /// ten, so the cap is headroom rather than the thing holding the framing up.
    /// </para>
    /// <para>
    /// Everything this component owns is Inspector surface: the four references can be dragged in from the
    /// hierarchy and the five numbers are live-tunable during a play session. Left empty, the references bind
    /// themselves by name off the board the presenter built, so a scene that has never been touched still
    /// works, and a scene whose piles were renamed or re-parented still works as long as one reference is set.
    /// </para>
    /// </summary>
    // After the camera rig (no explicit order, so 0) and after the coordinator (-100): the projection this
    // component measures must be the one the frame is about to render, and the hand's card count must be the
    // one the engine just synced. Both land before LateUpdate, where the fan actually travels.
    [DefaultExecutionOrder(-200)]
    public class HandIdleSinker : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The hand that gets out of the way. Bound from the scene's only HandController when left "
            + "empty, which is the same lookup the coordinator uses, so a hand placed anywhere in the "
            + "hierarchy is found either way.")]
        public HandController hand;

        [Tooltip("The lens the board is framed through. Left empty it takes Camera.main, which is what the "
            + "hand itself measures against - the two must agree, or the fan's footprint is answered for a "
            + "camera that is not the one drawing it.")]
        public Camera boardCamera;

        [Tooltip("The deck stack on the tabletop: the GameObject that holds the tile's Body cube. Drag the "
            + "DeckPile object in from the hierarchy to override the name binding, for instance after "
            + "renaming it or moving it out of the Piles group.")]
        public Transform deckPile;

        [Tooltip("The discard stack. Same binding rules as the deck.")]
        public Transform discardPile;

        [Header("Sink")]
        [Tooltip("Off switch. Turning it off retires the sink immediately rather than leaving the fan parked "
            + "at whatever depth the last enabled frame asked for.")]
        public bool sinkEnabled = true;

        [Tooltip("Air left between an uncovered pile and the fan's top edge, in fractions of the frame's "
            + "height. Zero means the two exactly touch, which reads as the pile being clipped by the hand "
            + "rather than cleared by it.")]
        public float uncoverMargin = 0.008f;

        [Tooltip("The most of the fan's own screen height this is allowed to spend on uncovering a pile. "
            + "Past it the sink is abandoned for that frame instead of half-done, because a hand buried to the "
            + "frame's edge is neither readable nor out of the way.")]
        [Range(0.05f, 1f)] public float sinkCapFraction = 0.5f;

        [Tooltip("How much wider than the tile the pointer's dead zone is, in viewport fractions. The piles "
            + "are small - about 105 reference pixels across at 16:9 - and a dead zone exactly their size "
            + "makes reaching one feel like threading a needle.")]
        public float pilePointerPadding = 0.012f;

        [Tooltip("Margin added around a tile's own footprint when projecting it, covering the furniture that "
            + "sticks out past the click maths: a pile's two offset stack cubes lean 0.048 board units out of "
            + "its top card's corner. One number covers both axes, as it does for the status layer's sweep.")]
        public float pileFurniture = 0.05f;

        // ---- The rule, as pure functions -----------------------------------------------

        /// <summary>
        /// How far the fan's top edge has to fall, in fractions of the frame's height, to uncover
        /// <paramref name="pile"/>; zero when the pile is already clear of it, off to one side of it, or
        /// absent.
        /// <para>
        /// The comparison is against the pile's <em>visible</em> bottom edge, because the part of a tile below
        /// the frame is not something anybody can click and sinking the hand to reveal it would be revealing
        /// nothing. That is what makes a deep zoom cost the hand nothing: the piles walk off the bottom of the
        /// screen on their own, and the drop needed for a partly seen pile is the one that gets capped away.
        /// </para>
        /// </summary>
        public static float UncoverDrop(Rect fan, Rect pile, float margin)
        {
            if (fan.height <= 0f || pile.height <= 0f)
            {
                return 0f;
            }

            // No overlap on either axis means the pile is already reachable, so the hand owes it nothing.
            if (pile.yMax <= fan.yMin || pile.yMin >= fan.yMax)
            {
                return 0f;
            }

            if (pile.xMax <= fan.xMin || pile.xMin >= fan.xMax)
            {
                return 0f;
            }

            return fan.yMax - Mathf.Max(pile.yMin, 0f) + margin;
        }

        /// <summary>
        /// True while the pointer is holding the hand up: inside the fan's resting footprint, and not over a
        /// pile.
        /// <para>
        /// The pile test outranks the band test, and that asymmetry is the whole point of the feature. A
        /// pointer sitting on a pile wants the pile; a pointer anywhere else in the band wants to read a card.
        /// Resolving it in favour of the pile also removes the oscillation a naive band would have: over a
        /// pile the hand is sunk, sunk means the pile is exposed, exposed means the pointer is still over a
        /// pile - a fixed point, not a loop.
        /// </para>
        /// </summary>
        public static bool HoldsRisen(Vector2 pointer, Rect fan, Rect pileA, Rect pileB, float padding)
        {
            if (!fan.Contains(pointer))
            {
                return false;
            }

            return !Pad(pileA, padding).Contains(pointer)
                && !Pad(pileB, padding).Contains(pointer);
        }

        /// <summary>
        /// The sink to ask the hand for this frame, in fractions of the frame's height. Zero means whole.
        /// <para>
        /// Spelled out as one function of seven values so the rule can be read, and argued with, without a
        /// scene, a camera or a pointer in existence: <c>dragged</c> is the only piece of state in here that
        /// is not geometry, and it is a veto rather than an input to the arithmetic.
        /// </para>
        /// </summary>
        public static float DecideTarget(Rect fan, Rect pileA, Rect pileB, Vector2 pointer,
            bool dragged, float margin, float capFraction, float pointerPadding)
        {
            if (dragged || fan.height <= 0f)
            {
                return 0f;
            }

            float required = Mathf.Max(UncoverDrop(fan, pileA, margin),
                UncoverDrop(fan, pileB, margin));
            if (required <= 0f)
            {
                return 0f;
            }

            if (required > capFraction * fan.height)
            {
                return 0f;
            }

            return HoldsRisen(pointer, fan, pileA, pileB, pointerPadding) ? 0f : required;
        }

        static Rect Pad(Rect r, float padding)
        {
            return padding <= 0f || r.width <= 0f
                ? r
                : Rect.MinMaxRect(r.xMin - padding, r.yMin - padding,
                    r.xMax + padding, r.yMax + padding);
        }

        // ---- Wiring ----------------------------------------------------------------------

        BattlefieldPresenter presenter;
        bool missingBoardLogged;

        void OnDisable()
        {
            Retire();
        }

        void OnDestroy()
        {
            Retire();
        }

        /// <summary>
        /// Sends the fan back up. Nobody else is going to: a sinker that has stopped running has also stopped
        /// asking, and the hand obeys its last target forever.
        /// </summary>
        void Retire()
        {
            if (hand != null)
            {
                hand.SetIdleSinkViewport(0f);
            }
        }

        void Update()
        {
            if (!sinkEnabled)
            {
                Retire();
                return;
            }

            if (!Bind())
            {
                return;
            }

            Rect fan = hand.RestingFootprintViewport(boardCamera);
            Rect deck = PileRect(deckPile, boardCamera, pileFurniture);
            Rect discard = PileRect(discardPile, boardCamera, pileFurniture);
            Vector2 pointer = boardCamera.ScreenToViewportPoint(Input.mousePosition);

            hand.SetIdleSinkViewport(DecideTarget(fan, deck, discard, pointer,
                hand.IsDragging, uncoverMargin, sinkCapFraction, pilePointerPadding));
        }

        /// <summary>
        /// Fills in whatever references the scene left empty. Idempotent and retried every frame while
        /// anything is still missing, because the board is built by the presenter's own first Update and a
        /// sinker that ran before it would otherwise cache a failure for the length of the match.
        /// </summary>
        bool Bind()
        {
            if (hand == null)
            {
                hand = FindObjectOfType<HandController>();
            }

            if (boardCamera == null)
            {
                boardCamera = Camera.main;
            }

            if (hand == null || boardCamera == null)
            {
                return false;
            }

            if (deckPile == null || discardPile == null)
            {
                if (presenter == null)
                {
                    presenter = FindObjectOfType<BattlefieldPresenter>();
                }

                if (presenter == null)
                {
                    return false;
                }

                if (deckPile == null)
                {
                    deckPile = FindTile(presenter.transform, "DeckPile");
                }

                if (discardPile == null)
                {
                    discardPile = FindTile(presenter.transform, "DiscardPile");
                }

                if ((deckPile == null || discardPile == null) && !missingBoardLogged)
                {
                    missingBoardLogged = true;
                    Debug.Log("[HandIdleSinker] The board has no pile tiles to bind, so the hand stays whole. "
                        + "Drag them onto the component if the tiles have been renamed.", this);
                }
            }

            return true;
        }

        /// <summary>
        /// The tile holder of one pile: an object of that name carrying a Body cube.
        /// <para>
        /// The Body test is what makes the name safe. <see cref="HandController"/> keeps its own children
        /// named DeckPile and DiscardPile - the anchors a drawn card starts life at - and those are plain
        /// empty transforms, so requiring a Body cube cannot mistake one of them for a tile on the table.
        /// </para>
        /// </summary>
        static Transform FindTile(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && t.Find("Body") != null)
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>
        /// One pile as the screen sees it: its four board-plane corners, widened by the furniture, projected
        /// through the lens.
        /// <para>
        /// The corners come off the tile's own Body scale rather than off a remembered size, so resizing a
        /// pile in the scene is followed here the same way the status layer's tile sweep follows it. The
        /// stack's two extra cubes sit behind the top card in depth and lean out of its corner; the depth is
        /// ignored and the lean is covered by the margin, which is the convention the HUD's own sweep uses.
        /// </para>
        /// </summary>
        public static Rect PileRect(Transform holder, Camera probe, float furniture)
        {
            if (holder == null || probe == null)
            {
                return default;
            }

            Transform body = holder.Find("Body");
            if (body == null)
            {
                return default;
            }

            Vector3 size = body.localScale;
            float hx = size.x * 0.5f + furniture;
            float hy = size.y * 0.5f + furniture;
            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            for (int cx = -1; cx <= 1; cx += 2)
            {
                for (int cy = -1; cy <= 1; cy += 2)
                {
                    Vector3 v = probe.WorldToViewportPoint(
                        holder.TransformPoint(new Vector3(cx * hx, cy * hy, 0f)));
                    xMin = Mathf.Min(xMin, v.x);
                    xMax = Mathf.Max(xMax, v.x);
                    yMin = Mathf.Min(yMin, v.y);
                    yMax = Mathf.Max(yMax, v.y);
                }
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
