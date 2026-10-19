using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
    }
}
