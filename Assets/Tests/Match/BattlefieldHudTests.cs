using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The match event log used to be eight world-space text slabs bolted to the right column of the tabletop,
    /// where they ate board space and shrank every time the camera pulled back. They are screen-space UI now
    /// (<see cref="BattlefieldHud"/>), and these tests lock the properties that change of consequence: the
    /// panel must never intercept pointer input, and it must window the log rather than clip it.
    /// <para>
    /// The panel these tests exercise is their own, under a name <see cref="BattlefieldHud.Find"/> cannot
    /// match. An EditMode run happens with whatever scene the editor has open - in this project that is the
    /// gameplay scene, which ships a HUD - and a fixture that bound to it would be editing and then destroying
    /// a real scene object.
    /// </para>
    /// </summary>
    public class BattlefieldHudTests
    {
        const string TestRootName = BattlefieldHud.RootName + " (Test)";

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

        /// <summary>
        /// A HUD built from nothing, the way <see cref="BattlefieldHud.Ensure"/> builds one, but owned by the
        /// test rather than by the scene.
        /// </summary>
        BattlefieldHud NewHud()
        {
            var go = new GameObject(TestRootName, typeof(RectTransform));
            spawned.Add(go);

            BattlefieldHud hud = go.AddComponent<BattlefieldHud>();
            hud.Build();
            return hud;
        }

        static List<string> Events(params string[] lines)
        {
            return new List<string>(lines);
        }

        // ---- The panel ------------------------------------------------------------------

        [Test]
        public void Build_ProducesAnOverlayPanelPinnedToTheRightEdge()
        {
            BattlefieldHud hud = NewHud();

            var canvas = hud.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "The HUD must be a canvas to be UI rather than board geometry.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
                "An overlay canvas ignores the Match root's tilt, which is why the log left the tabletop.");

            Assert.IsNotNull(hud.Panel, "Build must produce the log panel.");
            Assert.IsTrue(hud.IsBuilt, "A built HUD reports itself built.");
            Assert.AreEqual(1f, hud.Panel.anchorMin.x, 1e-4f, "The panel is anchored to the right edge.");
            Assert.AreEqual(1f, hud.Panel.anchorMax.x, 1e-4f, "The panel is anchored to the right edge.");
            Assert.AreEqual(0.5f, hud.Panel.anchorMin.y, 1e-4f, "and vertically centred, where its tabletop column was.");
        }

        [Test]
        public void Build_CannotSwallowACardDrag()
        {
            // The one interaction the game has is dragging a card with the left mouse button. A raycaster on
            // this canvas would eat every click landing on the right-hand third of the screen, so its absence
            // is a contract rather than an omission.
            BattlefieldHud hud = NewHud();
            Assert.IsFalse(hud.InterceptsPointerInput,
                "The log panel must be display-only: no GraphicRaycaster, no EventSystem, no eaten drags.");
        }

        [Test]
        public void Build_IsIdempotent()
        {
            BattlefieldHud hud = NewHud();
            hud.Build();
            hud.Build();

            int panels = 0;
            for (int i = 0; i < hud.transform.childCount; i++)
            {
                if (hud.transform.GetChild(i).name == "LogPanel")
                {
                    panels++;
                }
            }

            Assert.AreEqual(1, panels, "Rebuilding must bind the existing panel, never stack a second one.");
        }

        [Test]
        public void Ensure_ReusesTheHudTheSceneAlreadyShips()
        {
            // The gameplay scene ships one, and Ensure exists so the presenter binds to that object rather
            // than building a duplicate over it. When no scene HUD is present there is nothing to reuse, and
            // the same call must still hand back a usable panel.
            BattlefieldHud scene = BattlefieldHud.Find();
            BattlefieldHud ensured = BattlefieldHud.Ensure();
            Assert.IsNotNull(ensured, "Ensure must never return nothing.");
            Assert.IsTrue(ensured.IsBuilt, "Ensure must hand back a built panel, not a bare component.");

            // The reader is newer than the shipped scene file, and a scene-loaded HUD reports IsBuilt from its
            // serialized log references alone - so Ensure has to check the second panel separately or a card
            // click in the editor opens nothing.
            Assert.IsTrue(ensured.IsReaderBuilt, "and the details panel must be there too.");

            if (scene != null)
            {
                Assert.AreSame(scene, ensured,
                    "A scene HUD must be re-bound rather than duplicated; it is not destroyed here, because " +
                    "it belongs to the open scene rather than to this test.");
            }
            else
            {
                spawned.Add(ensured.gameObject);
            }
        }

        // ---- Windowing ------------------------------------------------------------------

        [Test]
        public void SetLog_KeepsTheMostRecentEntriesOldestFirst()
        {
            BattlefieldHud hud = NewHud();
            hud.maxLines = 3;

            hud.SetLog(Events("one", "two", "three", "four", "five"));

            Assert.AreEqual("three\nfour\nfive", hud.BodyText,
                "The window is the tail of the log, read top to bottom into the present.");
        }

        [Test]
        public void SetLog_ShorterThanTheWindowShowsEverythingItHas()
        {
            BattlefieldHud hud = NewHud();
            hud.maxLines = 8;

            hud.SetLog(Events("match initialized", "turn 1"));
            Assert.AreEqual("match initialized\nturn 1", hud.BodyText);

            hud.SetLog(null);
            Assert.IsEmpty(hud.BodyText, "A missing log clears the panel rather than showing stale events.");
        }

        [Test]
        public void SetLog_DoesNotClipALongEventMidWord()
        {
            // The tabletop panel was 1.9 units wide, so the presenter truncated every entry to 34 characters
            // and a reader got "PlayerOne deployed Shadow operative to the Core B...". The UI panel wraps.
            BattlefieldHud hud = NewHud();
            const string longEvent =
                "Player One deployed Shadow Operative to Core Bench 3 and gained 1 NP from Node 2";

            hud.SetLog(Events(longEvent));

            StringAssert.Contains(longEvent, hud.BodyText,
                "The whole entry must reach the panel; wrapping is the renderer's job, not the view's.");
        }

        [Test]
        public void SetLog_BeforeBuildIsSafe()
        {
            // The presenter resolves its HUD lazily and a scene-loaded one is not built until something asks,
            // so a refresh arriving first must paint nothing rather than throw.
            var go = new GameObject(TestRootName, typeof(RectTransform));
            spawned.Add(go);
            BattlefieldHud hud = go.AddComponent<BattlefieldHud>();

            Assert.DoesNotThrow(() => hud.SetLog(Events("turn 1", "turn 2")));
            Assert.AreEqual("turn 1\nturn 2", hud.BodyText, "and the write must not be lost.");
        }

        // ---- The reader panel ---------------------------------------------------------------
        // Clicking a card, a pile, a Node or a Core counter opens a second panel on the opposite edge. It is
        // the same kind of object as the log - display only, no EventSystem anywhere in the project - so the
        // properties worth locking are that it cannot eat a drag, and that it can be dismissed by the caller
        // rather than by a button nobody can press.

        [Test]
        public void Build_AlsoProducesTheReader()
        {
            BattlefieldHud hud = NewHud();

            Assert.IsTrue(hud.IsReaderBuilt, "A built HUD must carry the details panel too.");
            Assert.IsFalse(hud.IsReaderVisible, "and it starts closed: nothing was clicked yet.");
            Assert.IsNotNull(hud.Reader);
            Assert.AreEqual(0f, hud.Reader.anchorMin.x, 1e-4f,
                "The reader is pinned to the left edge, opposite the log, so the two never cover each other.");
        }

        [Test]
        public void Ensure_CompletesASceneLoadedHud()
        {
            // The scene's copy has its log references serialized, so IsBuilt is true before anything has run -
            // and the reader is newer than the shipped scene file. Ensure checks both, or a scene-loaded HUD
            // would open a card and show nothing.
            BattlefieldHud hud = BattlefieldHud.Ensure();
            Assert.IsTrue(hud.IsBuilt, "the log panel");
            Assert.IsTrue(hud.IsReaderBuilt, "the details panel");
        }

        [Test]
        public void ShowReader_OpensThePanelAndWritesAllThreeParts()
        {
            BattlefieldHud hud = NewHud();

            hud.ShowReader("Shadow Operative", "ATK 3   HP 4", "DISCARD  2 / 9");

            Assert.IsTrue(hud.IsReaderVisible);
            Assert.AreEqual("Shadow Operative", hud.ReaderTitle);
            Assert.AreEqual("ATK 3   HP 4", hud.ReaderBody);
            Assert.AreEqual("DISCARD  2 / 9", hud.ReaderFoot);
        }

        [Test]
        public void ShowReader_WithNothingToSayClosesItInstead()
        {
            // Dismissal has one code path, so the "clear" a caller issues cannot drift from the hide the footer
            // advertises. A null body is a close, not an empty panel left on screen.
            BattlefieldHud hud = NewHud();
            hud.ShowReader("Shadow Operative", "ATK 3   HP 4");
            Assert.IsTrue(hud.IsReaderVisible);

            hud.ShowReader(null, null);
            Assert.IsFalse(hud.IsReaderVisible, "an empty write must dismiss, not blank the panel in place");
        }

        [Test]
        public void HideReader_IsSafeBeforeAnythingWasShown()
        {
            BattlefieldHud hud = NewHud();
            Assert.DoesNotThrow(hud.HideReader);
            Assert.IsFalse(hud.IsReaderVisible);
        }

        [Test]
        public void ShowReader_BeforeBuildIsSafe()
        {
            // Same reasoning as SetLog: a scene-loaded HUD has no reader until something builds it, and the
            // first write has to trigger that build rather than throw.
            var go = new GameObject(TestRootName, typeof(RectTransform));
            spawned.Add(go);
            BattlefieldHud hud = go.AddComponent<BattlefieldHud>();

            Assert.DoesNotThrow(() => hud.ShowReader("Relay", "Holds Node 2.", "YOUR NODE 2 ACTIVE"));
            Assert.AreEqual("Relay", hud.ReaderTitle);
            Assert.IsTrue(hud.IsReaderVisible);
        }

        [Test]
        public void Reader_CannotSwallowACardDrag()
        {
            // The reader sits over the left half of the screen, which is where the hand's own column is. One
            // GraphicRaycaster here would eat card drags for the rest of the match.
            BattlefieldHud hud = NewHud();
            hud.ShowReader("Shadow Operative", "ATK 3   HP 4");

            Assert.IsFalse(hud.InterceptsPointerInput,
                "Both panels are display-only: dismissal is a click elsewhere or Escape, never a button.");
        }

        // ---- The status layer ------------------------------------------------------------

        /// <summary>
        /// The seven slabs that used to be painted on the tabletop. Named here rather than reached for through
        /// an accessor, because the names are the contract a designer edits against: these are the objects in
        /// the scene's <c>Battlefield HUD</c>, and renaming one in the editor has to be a deliberate act.
        /// </summary>
        static readonly string[] StatusBoxNames =
        {
            "Banner", "ControlsHint", "OpponentChip", "PlayerChip", "NodeList", "Notice", "Result"
        };

        /// <summary>
        /// The resolution the boxes are authored against. They are fixed reference-pixel rectangles, and the
        /// <c>CanvasScaler</c> maps reference pixels onto whatever the window is, so what a box has to fit is
        /// never the screen itself but the viewport the scaler turns that screen into.
        /// </summary>
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1080f;

        /// <summary>
        /// The scaler's width/height blend, read off the scene's HUD canvas. The tests hard-code it rather
        /// than reading the component so that changing the Inspector value cannot quietly change what the
        /// layout is judged against.
        /// </summary>
        const float MatchWidthOrHeight = 0.5f;

        static RectTransform Box(BattlefieldHud hud, string name)
        {
            Transform found = hud.transform.Find(name);
            Assert.IsNotNull(found, $"{name} must be a direct child of the HUD canvas, not buried in a panel.");
            return (RectTransform)found;
        }

        /// <summary>
        /// The pixels a box occupies, expressed in reference pixels, on a screen of the given size.
        /// <para>
        /// Computed rather than read off <c>worldCorners</c> on purpose: an EditMode run has no determined
        /// screen size, so the only way to ask what these boxes do <em>at</em> a resolution is to do the
        /// layout maths against one. Valid only because every status box is a point anchor rather than a
        /// stretched one, which the containment test asserts before trusting this.
        /// </para>
        /// </summary>
        static Rect ScreenRect(RectTransform rect, float width, float height)
        {
            Vector2 anchor = new Vector2(rect.anchorMin.x * width, rect.anchorMin.y * height);
            Vector2 size = rect.rect.size;
            Vector2 offsetMin = rect.anchoredPosition - Vector2.Scale(rect.pivot, size);
            return new Rect(anchor + offsetMin, size);
        }

        /// <summary>
        /// The reference-pixel viewport a screen size actually maps to.
        /// <para>
        /// The boxes are authored in reference pixels and the <c>CanvasScaler</c> turns them into screen
        /// pixels, so asking whether a box "fits the screen" means asking whether it fits <em>this</em>. The
        /// formula is Unity's own <c>MatchWidthOrHeight</c> interpolation, a plain linear lerp between the
        /// width and height scale factors, and it is not a guess: it was solved for against the running
        /// editor, whose 780&times;439 game view reports <c>scaleFactor 0.406365722</c> against a
        /// 1920&times;1080 reference at match 0.5, and
        /// <c>0.5&middot;(780/1920) + 0.5&middot;(439/1080) = 0.40636574</c> agrees to seven digits. The
        /// harmonic mean the documentation is often read as describing does not match that number.
        /// </para>
        /// </summary>
        static Vector2 ReferenceViewport(float screenWidth, float screenHeight)
        {
            float scale = (1f - MatchWidthOrHeight) * (screenWidth / ReferenceWidth)
                        + MatchWidthOrHeight * (screenHeight / ReferenceHeight);
            return new Vector2(screenWidth / scale, screenHeight / scale);
        }

        /// <summary>
        /// The screen sizes the layout has to survive. Every 16:9 size collapses to the reference viewport
        /// exactly, so what is being swept is the <em>aspect</em>: 4:3 squeezes the width to 1646 reference
        /// pixels and 21:9 squeezes the height to 926, and those are the two directions a fixed-size box can
        /// be caught by. Portrait is deliberately absent - see <see cref="StatusLayer_FitsEverySupportedAspect"/>.
        /// </summary>
        static readonly Vector2[] SupportedScreens =
        {
            new Vector2(1920f, 1080f),
            new Vector2(1280f, 720f),
            new Vector2(2560f, 1440f),
            new Vector2(1366f, 768f),
            new Vector2(1024f, 768f),
            new Vector2(2560f, 1080f),
            new Vector2(3440f, 1440f),
        };

        static bool Disjoint(RectTransform a, RectTransform b, Vector2 viewport)
        {
            Rect ra = ScreenRect(a, viewport.x, viewport.y);
            Rect rb = ScreenRect(b, viewport.x, viewport.y);
            return ra.xMax <= rb.xMin || rb.xMax <= ra.xMin
                || ra.yMax <= rb.yMin || rb.yMax <= ra.yMin;
        }

        [Test]
        public void Build_AlsoProducesTheStatusLayer()
        {
            // The presenter treats a HUD missing its status layer as a HUD needing a rebuild, so IsStatusBuilt
            // has to be true straight out of Build() or every Refresh would re-enter the builder.
            BattlefieldHud hud = NewHud();

            Assert.IsTrue(hud.IsStatusBuilt, "The status layer is part of Build, not a second call.");
            foreach (string name in StatusBoxNames)
            {
                Assert.IsNotNull(Box(hud, name), $"{name} must exist: it is a board label that moved inside.");
            }
        }

        [Test]
        public void StatusLayer_CannotSwallowACardDrag()
        {
            // The log occupied the right-hand column. These seven do not: the banner and the notice straddle
            // the middle of the screen exactly where the sockets are, and the chips sit over the near ranks.
            // One raycastTarget among them would swallow board clicks and card drags alike, since the hand
            // resolves a drag by raycasting from the pointer.
            BattlefieldHud hud = NewHud();

            Assert.IsFalse(hud.InterceptsPointerInput,
                "The overlay canvas must carry no GraphicRaycaster at all, status layer included.");

            foreach (string name in StatusBoxNames)
            {
                RectTransform box = Box(hud, name);
                Graphic[] graphics = box.GetComponentsInChildren<Graphic>(true);

                Assert.Greater(graphics.Length, 0, $"{name} must actually draw something.");
                foreach (Graphic graphic in graphics)
                {
                    Assert.IsFalse(graphic.raycastTarget,
                        $"{graphic.gameObject.name} sits over the board and must not intercept the pointer.");
                }
            }
        }

        [Test]
        public void StatusSetters_WriteWhereTheyAreRead()
        {
            // The presenter only ever speaks to this layer through the setters, and the accessors beside them
            // are the only way a test can hear the reply. A setter that writes to a field the accessor does
            // not read is a panel that silently never updates in game.
            BattlefieldHud hud = NewHud();

            hud.SetBanner("TURN 3 - COMBAT");
            hud.SetHint("1-5 attack   Q/E switch   R retreat");
            hud.SetStatus(PlayerIndex.Player, "CORE 20/20   NP 4/8");
            hud.SetStatus(PlayerIndex.Opponent, "CORE 14/20   NP 2/8");
            hud.SetNodeRow(2, "Node 3 - contested", Color.cyan);
            hud.SetNotice("Not enough NP for that.");
            hud.SetResult("VICTORY", Color.green);

            Assert.AreEqual("TURN 3 - COMBAT", hud.BannerText);
            Assert.AreEqual("1-5 attack   Q/E switch   R retreat", hud.HintText);
            Assert.AreEqual("CORE 20/20   NP 4/8", hud.StatusText(PlayerIndex.Player));
            Assert.AreEqual("CORE 14/20   NP 2/8", hud.StatusText(PlayerIndex.Opponent));
            Assert.AreEqual("Node 3 - contested", hud.NodeRowText(2));
            Assert.AreEqual("Not enough NP for that.", hud.NoticeText);
            Assert.AreEqual("VICTORY", hud.ResultText);
        }

        [Test]
        public void SetStatusMarked_ChangesOnlyTheTintAndTheRule()
        {
            // Marking a chip is the successor to the table's Core highlight: it has to be legible without
            // rewriting the numbers, or the readout flickers every time a click target moves.
            BattlefieldHud hud = NewHud();
            hud.SetStatus(PlayerIndex.Opponent, "CORE 14/20   NP 2/8");

            hud.SetStatusMarked(PlayerIndex.Opponent, true);
            Assert.AreEqual("CORE 14/20   NP 2/8", hud.StatusText(PlayerIndex.Opponent),
                "Marking must not touch the text.");
            Assert.AreNotEqual(hud.StatusText(PlayerIndex.Player), hud.StatusText(PlayerIndex.Opponent),
                "and it must not smear one side's readout onto the other.");

            hud.SetStatusMarked(PlayerIndex.Opponent, false);
            Assert.AreEqual("CORE 14/20   NP 2/8", hud.StatusText(PlayerIndex.Opponent));
        }

        [Test]
        public void SetResult_EmptyTextHidesTheOverlay()
        {
            // A result slab that stays up after the match is over sits over the fan for good.
            BattlefieldHud hud = NewHud();

            hud.SetResult("DEFEAT", Color.red);
            Assert.IsTrue(Box(hud, "Result").gameObject.activeSelf, "A result has to be visible.");

            hud.SetResult(string.Empty, Color.red);
            Assert.IsFalse(Box(hud, "Result").gameObject.activeSelf,
                "Clearing it has to take the overlay off the screen, not just off the label.");
        }

        [Test]
        public void StatusSetters_BeforeBuildAreSafe()
        {
            // The presenter resolves its HUD and may paint before the scene's canvas has finished building,
            // and a domain reload hands it a HUD whose fields are all null. Nothing here may throw.
            var go = new GameObject(TestRootName, typeof(RectTransform));
            spawned.Add(go);
            BattlefieldHud hud = go.AddComponent<BattlefieldHud>();

            hud.SetBanner("x");
            hud.SetHint("x");
            hud.SetStatus(PlayerIndex.Player, "x");
            hud.SetStatus(PlayerIndex.Opponent, "x");
            hud.SetStatusMarked(PlayerIndex.Player, true);
            hud.SetNodeRow(0, "x", Color.white);
            hud.SetNodeRow(99, "x", Color.white);
            hud.SetNotice("x");
            hud.SetResult("x", Color.white);

            Assert.IsFalse(hud.IsStatusBuilt, "Untouched, so still unbuilt - but nothing threw.");
        }

        [Test]
        public void StatusBoxes_ArePointAnchorsSoTheirPixelsCanBePredicted()
        {
            // The layout maths in ScreenRect is only meaningful for a box pinned to a single anchor. If a
            // stretched box ever appears here these tests are measuring its rect at build time rather than
            // what it draws, and they would pass while the panel clipped on screen.
            BattlefieldHud hud = NewHud();
            foreach (string name in StatusBoxNames)
            {
                RectTransform box = Box(hud, name);
                Assert.AreEqual(box.anchorMin, box.anchorMax,
                    $"{name} must be pinned to one anchor: its size is authored in pixels.");
                Assert.AreEqual(box.sizeDelta, box.rect.size,
                    $"{name}'s size is its sizeDelta, which is what ScreenRect assumes.");
            }
        }

        [Test]
        public void StatusLayer_FitsEverySupportedAspect()
        {
            // The acceptance test for the whole move: information that used to be geometry on the board is
            // now a slab of screen-space UI, and a slab that hangs off an edge or lies under its neighbour has
            // simply become unreadable. Sweeping aspects rather than resolutions is the point - a 16:9 window
            // of any size maps to the same reference viewport, so only 4:3 and 21:9 can actually catch
            // something.
            BattlefieldHud hud = NewHud();

            foreach (Vector2 screen in SupportedScreens)
            {
                Vector2 vp = ReferenceViewport(screen.x, screen.y);
                string at = $" at {(int)screen.x}x{(int)screen.y} (viewport {vp.x:F0}x{vp.y:F0})";

                foreach (string name in StatusBoxNames)
                {
                    Rect r = ScreenRect(Box(hud, name), vp.x, vp.y);
                    Assert.GreaterOrEqual(r.xMin, 0f, $"{name} runs off the left edge{at}.");
                    Assert.GreaterOrEqual(r.yMin, 0f, $"{name} runs off the bottom edge{at}.");
                    Assert.LessOrEqual(r.xMax, vp.x, $"{name} runs off the right edge{at}.");
                    Assert.LessOrEqual(r.yMax, vp.y, $"{name} runs off the top edge{at}.");
                }

                // The top-left band - banner, legend, opponent chip - is stacked vertically with 2 pixel seams
                // rather than separated by horizontal gaps, because a horizontal gap between a centred fixed-width
                // box and an edge-pinned one closes as soon as the viewport narrows. These three assertions are
                // what that stacking buys, and they are the only ones that can still be true at 4:3.
                Assert.IsTrue(Disjoint(Box(hud, "Banner"), Box(hud, "OpponentChip"), vp),
                    "The banner must clear the opponent chip" + at);
                Assert.IsTrue(Disjoint(Box(hud, "ControlsHint"), Box(hud, "OpponentChip"), vp),
                    "The key legend must clear the opponent chip" + at);
                Assert.IsTrue(Disjoint(Box(hud, "PlayerChip"), Box(hud, "NodeList"), vp),
                    "The left column's own members must not stack on each other" + at);
            }
        }

        [Test]
        public void StatusLayer_IsAuthoredAgainstALandscapeViewport()
        {
            // The honest boundary of the design, stated rather than pretended away. Every box here is a fixed
            // reference width, and the narrowest viewport the scaler can produce for a landscape window is the
            // 4:3 one - 1646 reference pixels across. Portrait maps 1080x1920 to only 923, which would cut the
            // notice and the result overlay, but the board is a 16.5 x 10.3 landscape tabletop and its camera
            // framing is derived from it, so portrait is not a configuration this game supports. What this
            // locks is the width the layout does need, which makes widening any box past the tightest
            // supported viewport a deliberate act rather than a slip.
            BattlefieldHud hud = NewHud();

            const float narrowest = 1646f;  // 4:3, the tightest landscape viewport in the sweep
            foreach (string name in StatusBoxNames)
            {
                RectTransform box = Box(hud, name);
                Assert.LessOrEqual(box.rect.width, narrowest,
                    $"{name} is wider than the narrowest viewport the layout is authored for.");
            }
        }
    }
}
