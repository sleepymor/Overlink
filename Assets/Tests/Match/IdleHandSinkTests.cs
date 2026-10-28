using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Cards;
using UnityEngine;
using UnityEngine.TestTools;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// Stage 3f: the resting hand gets out of the way of the two piles it sits on top of.
    /// <para>
    /// The deck and the discard are clickable tiles, and at the shipped framing both of them land inside the
    /// resting fan's screen rectangle - so in the one state a player is not looking at their hand, the two
    /// places cards come from and go to are invisible and unreachable. <see cref="HandIdleSinker"/> moves the
    /// hand instead of the board, because the board has no lane left to move them to.
    /// </para>
    /// <para>
    /// Two numbers decide whether that works: how far the fan has to fall before a pile is clear, and how much
    /// of the fan's resting footprint the pile columns eat, since a pointer sitting on a pile must not raise
    /// the hand back over it. Both are asserted here against the shipped rig - the shipped lens, the shipped
    /// fan dials and the tiles the presenter actually builds - because a contract remembered as prose is a
    /// contract that rots the day somebody resizes a pile.
    /// </para>
    /// <para>
    /// Every measured figure in this file came out of a temporary harvest harness that ran the same projection
    /// through a real camera at all seven supported aspects and across the zoom range, and was deleted once
    /// the table was written down. The harness is gone; the numbers it produced are the assertions below, and
    /// a failure message quotes the measured value next to the tolerance so a drift reads as a design change
    /// rather than as a mystery.
    /// </para>
    /// <para>
    /// The rule itself lives in three pure statics on the sinker, and the middle section of this file is
    /// ordinary unit tests against synthetic rectangles: what the eye is the pile's bottom edge, what happens
    /// when the drop needed is larger than the hand can pay, what outranks what. Those need no scene, no
    /// camera and no pointer, and they are the part a future change is most likely to break quietly.
    /// </para>
    /// </summary>
    public class IdleHandSinkTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();
        }

        // ---- The shipped rig ---------------------------------------------------------------

        // The fan's dials and its placement inside the camera, mirrored from Gameplay.unity
        // (HandController at L445-451, HandRoot at L1815) and restated by
        // BattlefieldCameraControllerTests, which uses the same six to derive the bench seam.
        // Mirrored rather than imported because what these tests need is the row and the lens
        // together - a property of both, owned by neither file alone. HandController's own
        // serialized defaults are NOT these values, and a test that forgot to set them would
        // measure a fan nobody has ever seen.
        const float ShippedFanSpacing = 0.74f;
        const float ShippedArcHeight = 0.18f;
        const float ShippedMaxYaw = 14f;
        const float ShippedMaxHandSpan = 6.96f;
        const float ShippedHandY = -1.65f;
        const float ShippedHandDepth = 6.7f;
        static readonly Vector3 ShippedCardSize = new Vector3(1f, 1.4615f, 0.06f);

        // The sinker's shipped defaults, restated so the contract is written in the numbers a player
        // actually gets. Drifting one of these is a design change and should show up here first.
        const float Margin = 0.008f;
        const float CapFraction = 0.5f;
        const float PointerPadding = 0.012f;
        const float Furniture = 0.05f;

        // Measured at the opening framing, from the harvest: the furnished pile rect's bottom edge and the
        // five-card fan's top edge. Named so the headline assertion reads as arithmetic over the table
        // instead of a magic number. Tolerances stay per-assertion, because they are not all the same size.
        const float PileBottomEdge = 0.145f;   // deck v, widened by the 0.05 furniture margin
        const float FanTopEdge = 0.303f;       // five-card fan v max

        /// <summary>
        /// Every aspect the game is expected to run at, copied from the status layer's sweep so a contract
        /// that only holds at 16:9 cannot pass here.
        /// </summary>
        static readonly Vector2[] SupportedScreens =
        {
            new Vector2(1920f, 1080f), new Vector2(1280f, 720f), new Vector2(2560f, 1440f),
            new Vector2(1366f, 768f), new Vector2(1024f, 768f), new Vector2(2560f, 1080f),
            new Vector2(3440f, 1440f),
        };

        BattlefieldCameraController shipped;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Real component, real defaults: reading the lens out of the shipped controller is what keeps
            // these tests honest when somebody retunes the framing in the scene. A hard-coded framing is a
            // framing that nobody re-checks.
            var go = new GameObject("IdleHandSinkFramingDefaults");
            spawned.Add(go);
            shipped = go.AddComponent<BattlefieldCameraController>();
        }

        /// <summary>
        /// A board of the test's own, built the way <see cref="BattlefieldPresenter.BuildBoard"/> builds one
        /// when there is no prefab to bind. Mirrors the helper of the same name in
        /// <c>PointerTargetingTests</c>, <c>DropTargetTests</c> and <c>BattlefieldHudTests</c> rather than
        /// sharing it: the four files would otherwise agree about each other's internals to save a line each.
        /// </summary>
        BattlefieldPresenter BuildThrowawayBoard(out Transform board)
        {
            var go = new GameObject("IdleHandSinkTestsPresenter");
            spawned.Add(go);

            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();
            board = presenter.transform.Find("Battlefield");
            Assert.IsNotNull(board, "BuildBoard must create the board root.");

            // The procedural build authors tiles in the board's own XY plane, so the root has to be pitched
            // upright before anything can be projected through it - the same line BattlefieldHudTests needs.
            presenter.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return presenter;
        }

        /// <summary>
        /// The observation lens at a chosen pitch and distance. Tagged MainCamera because that is what
        /// <c>Camera.main</c> matches on, and the hand measures its own depth against that camera: an
        /// untagged rig would leave the hand blind and every sink assertion below would pass, or fail, for a
        /// reason having nothing to do with the geometry.
        /// <para>
        /// Matrices do not refresh until the editor ticks, so every caller is a <see cref="UnityTestAttribute"/>
        /// that yields once before projecting. Asking a freshly posed camera for a viewport point otherwise
        /// returns garbage, and returns it confidently.
        /// </para>
        /// </summary>
        Camera NewRestCamera(float aspect, float pitch, float distance)
        {
            var rigGo = new GameObject("IdleHandSinkRig");
            spawned.Add(rigGo);
            rigGo.tag = "MainCamera";

            Quaternion rot = Quaternion.Euler(pitch, shipped.yaw, 0f);
            rigGo.transform.rotation = rot;
            rigGo.transform.position = shipped.focus - rot * Vector3.forward * distance;

            Camera cam = rigGo.AddComponent<Camera>();
            cam.fieldOfView = shipped.fieldOfView;
            cam.aspect = aspect;
            return cam;
        }

        /// <summary>A hand wired the way the scene wires it: the fan's root parented to the camera.</summary>
        HandController NewShippedHand(Camera cam)
        {
            var go = new GameObject("IdleHandSinkHand");
            spawned.Add(go);

            HandController hand = go.AddComponent<HandController>();
            hand.autoDraw = false;
            hand.cardSize = ShippedCardSize;
            hand.fanSpacing = ShippedFanSpacing;
            hand.arcHeight = ShippedArcHeight;
            hand.maxYaw = ShippedMaxYaw;
            hand.maxHandSpan = ShippedMaxHandSpan;
            hand.fanPivotsOnRivet = true;

            // The scene authors the root at handY zero and puts the whole -1.65 into HandRoot's local
            // position, because that is what makes the fan a child of the lens rather than of the table.
            hand.handY = 0f;

            var rootGo = new GameObject("HandRoot");
            spawned.Add(rootGo);
            rootGo.transform.SetParent(cam.transform, false);
            rootGo.transform.localPosition = new Vector3(0f, ShippedHandY, ShippedHandDepth);
            hand.handRoot = rootGo.transform;
            return hand;
        }

        /// <summary>
        /// The tile holder of one pile. Mirrors the sinker's own binding, including the Body test that keeps
        /// <see cref="HandController"/>'s anchors - which are also named DeckPile and DiscardPile, and are
        /// plain empty transforms - from being mistaken for tiles on the table.
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
        /// True when <paramref name="outer"/> fully covers <paramref name="inner"/>. Unity's Rect offers
        /// <c>Contains(Vector2)</c> and, in this version, nothing for rect-into-rect, so the four comparisons
        /// are spelled out here. Inclusive on both edges, because that is what a covering claim means: a tile
        /// whose edge exactly meets the row's is not sticking out of it.
        /// </summary>
        static bool Covers(Rect outer, Rect inner)
        {
            return outer.xMin <= inner.xMin && outer.xMax >= inner.xMax
                && outer.yMin <= inner.yMin && outer.yMax >= inner.yMax;
        }

        /// <summary>
        /// Rect comparison that names the axis which moved. NUnit's equality constraint on a struct would
        /// report "expected Rect(x, y, w, h)" and leave the reader to subtract four pairs of numbers.
        /// </summary>
        static void AssertSameRect(Rect a, Rect b, float tolerance, string what)
        {
            Assert.That(a.xMin, Is.EqualTo(b.xMin).Within(tolerance), what + " left edge");
            Assert.That(a.xMax, Is.EqualTo(b.xMax).Within(tolerance), what + " right edge");
            Assert.That(a.yMin, Is.EqualTo(b.yMin).Within(tolerance), what + " bottom edge");
            Assert.That(a.yMax, Is.EqualTo(b.yMax).Within(tolerance), what + " top edge");
        }

        // ---- The anchor --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RigProjectsTheDocumentedTabletopSpan()
        {
            // Everything below trusts this camera. The tabletop's near and far edges are the anchor the
            // framing was derived from and the seam the camera tests quote - v 0.096 to 0.8648, which is
            // 69 to 623 of 720 - so a rig that lands them anywhere else is a rig whose pile rects mean
            // nothing. Measuring fresh geometry against a known anchor is the difference between a number
            // and a guess, and an earlier pass of this stage learned that the hard way: one missing argument
            // poisoned every rect with NaN and made all four overlap assertions pass.
            BuildThrowawayBoard(out Transform board);
            Camera cam = NewRestCamera(16f / 9f, shipped.pitch, shipped.distance);
            yield return null;

            float near = float.MaxValue, far = float.MinValue;
            for (int cx = -1; cx <= 1; cx += 2)
            {
                for (int cy = -1; cy <= 1; cy += 2)
                {
                    float v = cam.WorldToViewportPoint(board.TransformPoint(new Vector3(
                        cx * BattlefieldPresenter.TableWidth * 0.5f,
                        BattlefieldPresenter.TableCenterY + cy * BattlefieldPresenter.TableDepth * 0.5f,
                        0f))).y;
                    near = Mathf.Min(near, v);
                    far = Mathf.Max(far, v);
                }
            }

            Assert.That(near, Is.EqualTo(0.096f).Within(0.002f),
                "The tabletop's near edge must sit where the framing record says it does.");
            Assert.That(far, Is.EqualTo(0.8648f).Within(0.002f),
                "The tabletop's far edge must sit where the framing record says it does.");
        }

        // ---- The measurement ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator RestingFan_StandsOverThePilesFromAThreeCardHandUp()
        {
            BuildThrowawayBoard(out Transform board);
            Camera cam = NewRestCamera(16f / 9f, shipped.pitch, shipped.distance);
            HandController hand = NewShippedHand(cam);
            yield return null;

            Transform deckTile = FindTile(board, "DeckPile");
            Transform discardTile = FindTile(board, "DiscardPile");
            Assert.IsNotNull(deckTile, "The board must carry a deck tile to bind.");
            Rect deck = HandIdleSinker.PileRect(deckTile, cam, Furniture);
            Rect discard = HandIdleSinker.PileRect(discardTile, cam, Furniture);

            // The pile's own screen rect, measured with the furniture margin: 119 by 123 reference pixels at
            // 16:9, sitting v 0.145 to 0.259. If this lands elsewhere the lens or the tile moved, and every
            // drop figure in this file has to be re-harvested rather than re-tuned.
            Assert.That(deck.yMin, Is.EqualTo(PileBottomEdge).Within(0.003f),
                "The deck pile's bottom edge left its measured place on the screen.");
            Assert.That(deck.yMax, Is.EqualTo(0.259f).Within(0.003f),
                "The deck pile's top edge left its measured place on the screen.");

            // The two piles are symmetric about the board's middle rank, so they land the same height and the
            // same width, mirrored across the middle of the frame. Not identical rects - one is on the left.
            Assert.That(discard.yMin, Is.EqualTo(deck.yMin).Within(0.0005f),
                "The two piles share a board rank, so they must land at the same height.");
            Assert.That(discard.width, Is.EqualTo(deck.width).Within(0.0005f),
                "and the same width.");
            Assert.That(Mathf.Abs(discard.center.x - 0.5f),
                Is.EqualTo(Mathf.Abs(deck.center.x - 0.5f)).Within(0.0005f),
                "and the same distance from the middle of the frame, on the other side of it.");

            // The conflict, stated as a fact: at the hand size the match opens with, the fan swallows both
            // piles whole - 105 by 112 pixels of tile behind 1003 by 410 of fan.
            Rect five = hand.RestingFootprintViewport(cam, 5);
            Assert.That(Covers(five, deck), "The resting five-card fan must cover the deck pile - that is "
                + "the bug 3f exists for, and if it stops being true the sink has nothing left to buy.");
            Assert.That(Covers(five, discard), "and the discard pile too.");

            // Two cards is the last hand size that owes the board nothing: the row is narrower than the gap
            // between the piles, so neither tile falls inside its x-band at all.
            Rect two = hand.RestingFootprintViewport(cam, 2);
            Assert.That(two.xMin, Is.GreaterThan(deck.xMax),
                "A two-card fan sits clear between the piles, which is why the sink is free in small hands.");

            // Three is where the wing tips first brush a tile - 16 of the tile's own 105 pixels, and the
            // fan's edge still inside the pile rather than past it.
            Rect three = hand.RestingFootprintViewport(cam, 3);
            Assert.That(three.xMin, Is.InRange(deck.xMin, deck.xMax),
                "A three-card fan leans over the outer sliver of the deck pile.");

            // Four is nearly total: 102 of 105 pixels of tile behind the row, with only the pile's outer edge
            // escaping. This is the hand a player reaches after one turn of playing cards. The share is
            // measured against the tile FACE rather than the furnished rect, because the claim being made is
            // about the card the player can see, and the furniture margin is a courtesy to the pointer.
            Rect four = hand.RestingFootprintViewport(cam, 4);
            Rect discardFace = HandIdleSinker.PileRect(discardTile, cam, 0f);
            float covered = Mathf.Min(four.xMax, discardFace.xMax) - discardFace.xMin;
            Assert.That(covered / discardFace.width, Is.GreaterThan(0.95f),
                "A four-card fan already hides almost the whole discard pile.");
            Assert.That(Covers(four, discard), Is.False,
                "and not quite all of it, which is why the sink's trigger has to be the footprint rather "
                + "than a binary covered-or-not flag.");
        }

        [UnityTest]
        public IEnumerator RestingFan_TopEdgeIsFlatFromThreeCardsUp()
        {
            BuildThrowawayBoard(out Transform board);
            Camera cam = NewRestCamera(16f / 9f, shipped.pitch, shipped.distance);
            HandController hand = NewShippedHand(cam);
            yield return null;

            // The row's highest point barely moves as it grows, and that is not luck: a wider fan rolls its
            // wings further out on a longer radius, so the arc drops them, while the same extra roll lifts
            // their outer corners. Measured spread across eight hand sizes: 0.302 to 0.306, four thousandths
            // of a frame. It is why one sink depth serves every hand, and why that depth can be quoted as a
            // fraction of the fan's own height instead of a per-size table.
            float highest = float.MinValue, lowest = float.MaxValue;
            for (int n = 3; n <= 10; n++)
            {
                Rect fan = hand.RestingFootprintViewport(cam, n);
                Assert.That(fan.yMax, Is.InRange(FanTopEdge - 0.005f, FanTopEdge + 0.005f),
                    $"A {n}-card fan's top edge left the measured band 0.302..0.306.");
                highest = Mathf.Max(highest, fan.yMax);
                lowest = Mathf.Min(lowest, fan.yMax);

                // The bottom, by contrast, walks away as the row lengthens: -0.057 at three cards and -0.126
                // at ten, all of it past the floor of the frame. The fan's usable height is therefore the
                // part above the floor, and the sink spends a share of the whole - which is the honest
                // denominator, because the buried part still has to travel.
                Assert.That(fan.yMin, Is.LessThan(0f),
                    $"A {n}-card fan's bottom edge should already be off the bottom of the frame.");
            }

            Assert.That(highest - lowest, Is.LessThan(0.005f),
                "The flatness of the top edge is what lets one depth serve every hand; a spread here means "
                + "the arc and the roll stopped cancelling.");

            // Small fans are the exception, and they are allowed to be: one card tops out at 0.340 and two at
            // 0.310, because with no neighbours there is no long radius to drop the wings back down. Neither
            // owes the piles anything - the tiles sit outside their x-band, which is what the next test pins.
            Assert.That(hand.RestingFootprintViewport(cam, 1).yMax, Is.InRange(0.33f, 0.35f),
                "A one-card fan is a single upright card and tops out well above the row's usual edge.");
        }

        [UnityTest]
        public IEnumerator EveryHandThatNeedsTheSinkCanAffordIt()
        {
            BuildThrowawayBoard(out Transform board);
            Camera cam = NewRestCamera(16f / 9f, shipped.pitch, shipped.distance);
            HandController hand = NewShippedHand(cam);
            Rect deck = HandIdleSinker.PileRect(FindTile(board, "DeckPile"), cam, Furniture);
            Rect discard = HandIdleSinker.PileRect(FindTile(board, "DiscardPile"), cam, Furniture);
            yield return null;

            // The whole-tile depth the design picked is a promise about every hand size, not about the
            // opening five: whatever the fan holds, it must be able to uncover both piles without spending
            // more of its own height than the cap allows. Measured: 0.165 to 0.169 of the frame for every
            // hand from three up, against caps of 0.180 to 0.214, so the cap is headroom and never the
            // thing that saves the framing.
            float worstSpend = 0f;
            for (int n = 1; n <= 10; n++)
            {
                Rect fan = hand.RestingFootprintViewport(cam, n);
                float required = Mathf.Max(HandIdleSinker.UncoverDrop(fan, deck, Margin),
                    HandIdleSinker.UncoverDrop(fan, discard, Margin));

                if (n <= 2)
                {
                    Assert.That(required, Is.Zero,
                        $"A {n}-card fan does not cover the piles, so it has nothing to withdraw.");
                    continue;
                }

                Assert.That(required, Is.InRange(0.16f, 0.175f),
                    $"A {n}-card fan's required drop left the measured band 0.165..0.169.");
                Assert.That(required, Is.LessThanOrEqualTo(CapFraction * fan.height),
                    $"A {n}-card fan cannot afford to uncover the piles under the shipped cap.");
                worstSpend = Mathf.Max(worstSpend, required / fan.height);
            }

            // The tightest spend is a three-card fan's 0.461 of its own height; a ten-card fan gets away with
            // 0.385 because the row is taller while the piles stay where they are. The bound that matters is
            // the cap itself, already asserted per size above - this one is the tripwire on the opposite side,
            // catching a sink that stopped being worth its while. Anything approaching the cap would mean the
            // hand is being buried to reach the board, and anything near zero would mean the piles are still
            // behind it.
            Assert.That(worstSpend, Is.GreaterThan(0.3f).And.LessThan(CapFraction),
                "The worst hand size in the game spends this much of its own height on the sink; measured "
                + "0.461 at three cards and 0.385 at ten.");
        }

        [UnityTest]
        public IEnumerator PilesAndFan_AgreeAtEverySupportedAspect()
        {
            BuildThrowawayBoard(out Transform board);

            // The vertical story is aspect-free and the horizontal one is free in RATIO, because the fan and
            // the piles both scale with 1/aspect: a pile's offset from the middle and the fan's half-width are
            // both a camera-space distance divided by depth times aspect, so their quotient never moves. That
            // is what lets the sink be written in fractions of the fan's own height and stay right at 21:9.
            float referenceTop = 0f, referenceDrop = 0f, referenceRatio = 0f, referenceWidth = 0f;
            foreach (Vector2 screen in SupportedScreens)
            {
                Camera cam = NewRestCamera(screen.x / screen.y, shipped.pitch, shipped.distance);
                HandController hand = NewShippedHand(cam);
                yield return null;

                Rect fan = hand.RestingFootprintViewport(cam, 5);
                Rect deck = HandIdleSinker.PileRect(FindTile(board, "DeckPile"), cam, Furniture);
                float drop = HandIdleSinker.UncoverDrop(fan, deck, Margin);

                // A pile's centre as a share of the fan's half-band, and its width as a share of the whole
                // band: the two figures that describe how much of the hand's resting footprint the pointer
                // has to give up in order to reach the board.
                float ratio = Mathf.Abs(deck.center.x - 0.5f) / (fan.width * 0.5f);
                float width = deck.width / fan.width;

                if (referenceTop == 0f)
                {
                    referenceTop = fan.yMax;
                    referenceDrop = drop;
                    referenceRatio = ratio;
                    referenceWidth = width;

                    // The headline figure of the stage: the piles need 0.166 of a frame at the opening
                    // framing - fan top 0.303, pile bottom 0.145, plus the uncover margin.
                    Assert.That(drop, Is.EqualTo(FanTopEdge - PileBottomEdge + Margin).Within(0.004f),
                        "The drop the piles need at the opening framing, with the furniture and uncover "
                        + "margins the sinker ships with.");
                    Assert.That(ratio, Is.InRange(0.70f, 0.76f),
                        "The pile columns sit at 0.728 of the fan's half-band, well inside the row and well "
                        + "outside its middle.");

                    // What the dead zone costs the player: two furnished columns, 0.119 of the band each, so
                    // three quarters of the resting footprint still raises the hand - including all of the
                    // centre, which is where a player looks to read a card.
                    float live = 1f - 2f * width;
                    Assert.That(live, Is.GreaterThan(0.7f),
                        "The pile columns must stay a minority of the fan's resting footprint, or the hand "
                        + "stops rising where the player is reading it.");
                    continue;
                }

                Assert.That(fan.yMax, Is.EqualTo(referenceTop).Within(0.0005f),
                    $"{screen.x}x{screen.y}: the fan's top edge moved with the aspect, so a sink depth quoted "
                    + "in frame fractions would stop meaning one thing at every window shape.");
                Assert.That(drop, Is.EqualTo(referenceDrop).Within(0.0005f),
                    $"{screen.x}x{screen.y}: the drop the piles need moved with the aspect.");
                Assert.That(ratio, Is.EqualTo(referenceRatio).Within(0.0005f),
                    $"{screen.x}x{screen.y}: the pile columns stopped sitting at a fixed share of the fan's "
                    + "band, which is what makes the pointer's dead zone describable at all.");
                Assert.That(width, Is.EqualTo(referenceWidth).Within(0.0005f),
                    $"{screen.x}x{screen.y}: a pile stopped covering a fixed share of the fan's width.");
            }
        }

        [UnityTest]
        public IEnumerator FanIsScreenFixedWhileTheBoardZoomsUnderIt()
        {
            BuildThrowawayBoard(out Transform board);

            // The fan hangs off the lens, so no amount of zoom or crane moves it one pixel; the board slides
            // underneath. That is why the depth has to be recomputed per frame rather than tuned once, and
            // why the cap exists at all: at the shallow ceiling the piles walk off the bottom of the screen,
            // and a hand asked to reach them would have to leave the frame.
            Rect reference = default;
            float dropAtOpening = 0f;
            for (int i = 0; i < 4; i++)
            {
                // 0: shallowest zoom-in the rig allows. 1: the opening framing. 2: deepest zoom-out.
                // 3: the bird's-eye end of the crane, where the whole board is visible at once.
                float pitch = i == 3 ? shipped.birdPitch : shipped.pitch;
                float distance = i == 0 ? shipped.minDistance
                    : (i == 1 ? shipped.distance : (i == 2 ? shipped.maxDistance : shipped.birdDistance));

                Camera cam = NewRestCamera(16f / 9f, pitch, distance);
                HandController hand = NewShippedHand(cam);
                yield return null;

                Rect fan = hand.RestingFootprintViewport(cam, 5);
                Rect deck = HandIdleSinker.PileRect(FindTile(board, "DeckPile"), cam, Furniture);
                float required = HandIdleSinker.UncoverDrop(fan, deck, Margin);
                string where = $"pitch {pitch} distance {distance}";

                if (i == 0)
                {
                    reference = fan;
                    Assert.That(required, Is.Zero,
                        $"At the {where} the pile has fallen below the fan's footprint entirely, so the hand "
                        + "owes it nothing.");
                }
                else
                {
                    AssertSameRect(fan, reference, 0.0005f, $"The resting fan moved with the camera at {where};");
                    Assert.That(required, Is.GreaterThan(0f),
                        $"At {where} the piles are on screen and behind the row, so they need uncovering.");
                    Assert.That(required, Is.LessThanOrEqualTo(CapFraction * fan.height),
                        $"At {where} the sink would have to be abandoned as unaffordable.");
                }

                if (i == 1)
                {
                    dropAtOpening = required;
                }

                if (i == 2)
                {
                    Assert.That(required, Is.LessThan(dropAtOpening),
                        "Zooming out slides the piles UP toward the fan's top edge, so the depth they need "
                        + "shrinks - the number has to follow the lens instead of being remembered.");
                }
            }
        }

        // ---- The rule, as pure arithmetic ---------------------------------------------------

        static readonly Rect Fan = Rect.MinMaxRect(0.24f, -0.08f, 0.76f, 0.30f);
        static readonly Rect Behind = Rect.MinMaxRect(0.28f, 0.15f, 0.34f, 0.25f);

        [Test]
        public void UncoverDrop_MeasuresAgainstThePilesBottomEdge()
        {
            // The number that matters is where the fan's top edge has to finish, which is below the pile's
            // bottom: anything less and the tile is still standing behind a card.
            Assert.That(HandIdleSinker.UncoverDrop(Fan, Behind, 0f),
                Is.EqualTo(Fan.yMax - Behind.yMin).Within(1e-6f),
                "The drop must be the fan's top edge minus the pile's bottom edge, and nothing else.");
            Assert.That(HandIdleSinker.UncoverDrop(Fan, Behind, 0.008f),
                Is.EqualTo(Fan.yMax - Behind.yMin + 0.008f).Within(1e-6f),
                "The uncover margin is air between the two, so it adds to the drop rather than subtracting.");
        }

        [Test]
        public void UncoverDrop_OwesNothingToAPileItDoesNotCover()
        {
            // Below the row means below its bottom edge, which in practice means off the bottom of the frame
            // - the fan's own footprint already runs past the floor, so a pile under it is unreachable by
            // definition and sinking further would reveal nothing.
            Rect below = Rect.MinMaxRect(0.28f, -0.30f, 0.34f, -0.10f);
            Assert.That(HandIdleSinker.UncoverDrop(Fan, below, 0f), Is.Zero,
                "A pile wholly below the fan is not behind it.");

            Rect beside = Rect.MinMaxRect(0.80f, 0.15f, 0.90f, 0.25f);
            Assert.That(HandIdleSinker.UncoverDrop(Fan, beside, 0f), Is.Zero,
                "A pile outside the fan's x-band is already reachable, whatever its height.");

            Rect above = Rect.MinMaxRect(0.28f, 0.40f, 0.34f, 0.50f);
            Assert.That(HandIdleSinker.UncoverDrop(Fan, above, 0f), Is.Zero,
                "and so is one above the row, which is where every socket the player must click lives.");

            Assert.That(HandIdleSinker.UncoverDrop(Fan, default, 0f), Is.Zero,
                "A pile that did not project - no tile, no camera - asks nothing of the hand.");
            Assert.That(HandIdleSinker.UncoverDrop(default, Behind, 0f), Is.Zero,
                "and an empty hand covers nothing, so it has nothing to withdraw.");
        }

        [Test]
        public void UncoverDrop_MeasuresOnlyTheVisiblePartOfAPile()
        {
            // A tile half below the frame's bottom edge is half not there: sinking the hand to the floor to
            // reveal the part under the screen would bury the hand for nothing. The comparison is against the
            // greater of the pile's bottom edge and the frame's, which is what makes a deep zoom cost the fan
            // nothing once the cap is applied on top of it.
            Rect straddling = Rect.MinMaxRect(0.28f, -0.05f, 0.34f, 0.10f);
            Assert.That(HandIdleSinker.UncoverDrop(Fan, straddling, 0f),
                Is.EqualTo(Fan.yMax).Within(1e-6f),
                "The drop for a pile running off the bottom of the frame must be measured to the frame's edge, "
                + "not to the pile's invisible corner.");
        }

        [Test]
        public void HoldsRisen_ThePileOutranksTheBand()
        {
            // A pointer on a pile wants the pile; a pointer anywhere else in the band wants to read a card.
            // Settling it in the pile's favour is also what stops the oscillation: sunk exposes the pile, the
            // pointer is still on the pile, so it stays sunk - a fixed point rather than a loop.
            Assert.That(HandIdleSinker.HoldsRisen(new Vector2(0.5f, 0.20f), Fan, Behind, Behind, 0f),
                "The middle of the row, away from any pile, is where a player reads a card.");
            Assert.That(HandIdleSinker.HoldsRisen(new Vector2(0.31f, 0.20f), Fan, Behind, Behind, 0f), Is.False,
                "A pointer sitting on a pile must not raise the hand back over it.");
            Assert.That(HandIdleSinker.HoldsRisen(new Vector2(0.5f, 0.60f), Fan, Behind, Behind, 0f), Is.False,
                "A pointer over the board is not reading its hand and should leave the fan out of the way.");
            Assert.That(HandIdleSinker.HoldsRisen(new Vector2(0.10f, 0.20f), Fan, Behind, Behind, 0f), Is.False,
                "and neither is one off to the side of the row.");
        }

        [Test]
        public void HoldsRisen_PaddingMakesThePileEasyToHit()
        {
            // The piles are about 105 reference pixels across at 16:9. A dead zone exactly their size would
            // make reaching one feel like threading a needle, so the pointer test runs on a padded rect.
            Vector2 justOutside = new Vector2(0.35f, 0.20f);
            Assert.That(HandIdleSinker.HoldsRisen(justOutside, Fan, Behind, Behind, 0f),
                "With no padding the tile's own edge is the boundary.");
            Assert.That(HandIdleSinker.HoldsRisen(justOutside, Fan, Behind, Behind, 0.02f), Is.False,
                "Two hundredths of a frame of padding has to swallow a point that close to the tile.");
            Assert.That(HandIdleSinker.HoldsRisen(new Vector2(0.5f, 0.20f), Fan, default, default, 0.5f),
                "Padding an absent pile must not carve a dead zone out of the middle of the row.");
        }

        [Test]
        public void DecideTarget_NeverFallsFurtherThanHalfTheHand()
        {
            // The cap is a guardrail, not a budget to spend: an uncovering that needs more than half the fan's
            // own height is abandoned outright rather than taken halfway, because a hand parked on the frame's
            // edge is neither readable nor out of the way. The action rail's pile buttons stay the way in.
            Rect low = Rect.MinMaxRect(0.28f, -0.30f, 0.34f, 0.10f);
            Assert.That(HandIdleSinker.DecideTarget(Fan, low, low, new Vector2(0.5f, 0.9f), false,
                    0f, CapFraction, PointerPadding), Is.Zero,
                "A pile whose visible part needs more than half the hand stays covered, and the hand stays up.");

            Assert.That(HandIdleSinker.DecideTarget(Fan, Behind, Behind, new Vector2(0.5f, 0.9f), false,
                    0f, CapFraction, PointerPadding), Is.EqualTo(Fan.yMax - Behind.yMin).Within(1e-5f),
                "An affordable pile is uncovered exactly as far as it needs, and no further.");
        }

        [Test]
        public void DecideTarget_NeverMovesUnderADrag()
        {
            // A fan that sank while a card was in the player's grip would teleport the drag plane out from
            // under the pointer mid-flight, since the drag maths is posed in the hand's own space.
            Assert.That(HandIdleSinker.DecideTarget(Fan, Behind, Behind, new Vector2(0.5f, 0.9f), true,
                0f, CapFraction, PointerPadding), Is.Zero, "A card in hand holds the fan where it is.");
        }

        [Test]
        public void DecideTarget_RisesForAPointerInTheBandAndSinksForOneAnywhereElse()
        {
            float sunk = HandIdleSinker.DecideTarget(Fan, Behind, Behind, new Vector2(0.5f, 0.9f), false,
                0f, CapFraction, PointerPadding);
            Assert.That(sunk, Is.GreaterThan(0f), "The pointer is over the board, so the hand owes it the way.");

            float risen = HandIdleSinker.DecideTarget(Fan, Behind, Behind, new Vector2(0.5f, 0.20f), false,
                0f, CapFraction, PointerPadding);
            Assert.That(risen, Is.Zero, "The pointer came home to the hand, so the hand comes home.");

            Assert.That(sunk, Is.LessThan(Fan.height),
                "A sink that took the whole fan's height would leave nothing to rise back to.");
        }

        // ---- The hand's side of the bargain --------------------------------------------------

        [UnityTest]
        public IEnumerator RestingFootprint_IgnoresHowFarTheHandHasFallen()
        {
            // The anti-oscillation contract, and the reason the footprint is answered at rest: a band that
            // tracked the live pose would uncover the piles as it fell, qualify for a rise on its own top
            // edge, and sit there fluttering for as long as the pointer stayed put.
            //
            // This one exercises the hand's mechanics alone - the target, the ease, the fixed footprint - so
            // unlike every test above it needs no board and no piles, only the lens and the row.
            Camera cam = NewRestCamera(16f / 9f, shipped.pitch, shipped.distance);
            HandController hand = NewShippedHand(cam);
            yield return null;

            Rect before = hand.RestingFootprintViewport(cam, 5);
            Vector3 rest = hand.handRoot.localPosition;
            Assert.That(hand.IsDragging, Is.False, "A hand with no cards in it is not dragging anything.");

            hand.SetIdleSinkViewport(0.166f);
            Assert.That(hand.IdleSinkTargetViewport, Is.EqualTo(0.166f).Within(1e-4f),
                "The request has to survive the round trip through camera units, or a depth decided in frame "
                + "fractions is not the depth the fan travels.");

            // EditMode never ticks a MonoBehaviour, so the ease is driven by hand. Thirty steps of a fifth of
            // a second is nine seconds of travel against a 0.18 s time constant, which is converged.
            for (int i = 0; i < 30; i++)
            {
                hand.TickIdleSink(0.2f);
            }

            Assert.That(hand.IdleSinkViewport, Is.EqualTo(0.166f).Within(1e-3f),
                "The fan did not travel to the depth it was asked for.");
            Assert.That(hand.handRoot.localPosition.y, Is.LessThan(rest.y),
                "The root did not move, so the sink is a no-op and the piles stay behind the row.");

            Rect after = hand.RestingFootprintViewport(cam, 5);
            AssertSameRect(after, before, 1e-6f,
                "The resting footprint answered for a sunken hand, which is the oscillation the whole design "
                + "exists to avoid:");

            hand.SetIdleSinkViewport(0f);
            for (int i = 0; i < 30; i++)
            {
                hand.TickIdleSink(0.2f);
            }

            // Component-wise, because NUnit's tolerance has nothing to grip a Unity vector with and would
            // otherwise be silently ignored - an assertion that cannot fail is worse than no assertion.
            Assert.That(hand.handRoot.localPosition.x, Is.EqualTo(rest.x).Within(1e-5f),
                "Sinking must not move the row sideways.");
            Assert.That(hand.handRoot.localPosition.y, Is.EqualTo(rest.y).Within(0.0005f),
                "Rising has to land the root back on its authored pose, or every sink drifts the hand a "
                + "little further down the frame and the fan is lost within a few minutes of play.");
            Assert.That(hand.handRoot.localPosition.z, Is.EqualTo(rest.z).Within(1e-5f),
                "and must not pull it toward or away from the lens, which would resize it.");
        }

        [Test]
        public void SinkIsZeroUntilSomethingAsksForIt()
        {
            // The hand obeys a target and never forms an opinion, which is what keeps a scene with no sinker
            // in it - the card preview, the gallery, every hand test that predates 3f - behaving exactly as it
            // did before the field existed. A hand that sank by default would move the preview's fan off the
            // bottom of its own frame and nobody would know which commit did it.
            var go = new GameObject("IdleHandSinkPlainHand");
            spawned.Add(go);
            HandController hand = go.AddComponent<HandController>();

            Assert.That(hand.IsDragging, Is.False);
            Assert.That(hand.IdleSinkViewport, Is.Zero);
            Assert.That(hand.IdleSinkTargetViewport, Is.Zero);

            Vector3 before = hand.transform.localPosition;
            hand.TickIdleSink(1f);
            Assert.That(hand.transform.localPosition, Is.EqualTo(before),
                "Ticking a hand that was never asked to sink must not move it a hundredth of a unit.");

            Assert.That(hand.RestingFootprintViewport(null, 5).width, Is.Zero,
                "A footprint asked of no camera must be degenerate rather than a lie.");
            Assert.That(hand.RestingFootprintViewport(null, 5).height, Is.Zero);
            Assert.That(hand.RestingFootprintViewport(Camera.main, 0).height, Is.Zero,
                "and an empty hand has no footprint to report.");
        }
    }
}
