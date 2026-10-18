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
    }
}
