using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using UnityEditor;
using UnityEngine;

namespace Overlink.Match.Tests
{
    /// <summary>
    /// The board used to be one palette of code constants and nothing else: changing its skin meant
    /// recompiling the presenter. Five serialized materials now sit on the component, one per family of surface,
    /// with thirteen albedos beside them so a skin has something to be tinted <em>by</em>. These lock the three
    /// properties that make that arrangement safe rather than merely possible.
    /// <para>
    /// <b>The defaults are the shipped frame.</b> Every field starts at the value the framing was measured and
    /// signed off with, and every slot starts empty, so a scene that has never heard of the feature renders
    /// exactly as the one that shipped. A serialized field is only an improvement over a constant if somebody
    /// proves the two agree on day one.
    /// </para>
    /// <para>
    /// <b>The five families partition the board, with nothing left over.</b> A body that falls through to the
    /// socket family because its name drifted is a tile that silently stops taking its skin, and nothing logs
    /// it. So the counts are asserted against numbers derived from the same board constants the presenter lays
    /// out from, and the total is asserted too - an unclassified cube cannot hide inside a correct-looking
    /// family.
    /// </para>
    /// <para>
    /// <b>A skin multiplies, it does not replace.</b> The per-frame repaint still overrides the colour through a
    /// MaterialPropertyBlock, which is why five materials can serve thirteen colours. That is a claim about the
    /// interaction between two subsystems, and it is the first thing a future refactor breaks.
    /// </para>
    /// </summary>
    public class BoardSkinTests
    {
        // ---- The shipped frame, copied rather than read --------------------------------
        // A test that asks the presenter for its tableTint and then asserts the presenter's tableTint matches
        // what it just said proves nothing at all. These are the numbers the shipped board was judged on;
        // changing one deliberately means changing it here in the same commit, which is the point.

        static readonly Color ShippedTableTint = new Color(0.09f, 0.11f, 0.15f);
        static readonly Color ShippedActiveEmpty = new Color(0.30f, 0.27f, 0.19f);
        static readonly Color ShippedBenchEmpty = new Color(0.13f, 0.15f, 0.19f);
        static readonly Color ShippedProtocolEmpty = new Color(0.23f, 0.18f, 0.31f);
        static readonly Color ShippedActiveRim = new Color(0.80f, 0.64f, 0.28f);
        static readonly Color ShippedBenchRim = new Color(0.28f, 0.39f, 0.54f);
        static readonly Color ShippedProtocolRim = new Color(0.58f, 0.36f, 0.78f);
        static readonly Color ShippedPlayerTint = new Color(0.20f, 0.42f, 0.72f);
        static readonly Color ShippedOpponentTint = new Color(0.62f, 0.24f, 0.24f);
        static readonly Color ShippedCorePlateTint = new Color(0.16f, 0.18f, 0.25f);
        static readonly Color ShippedNodeFree = new Color(0.26f, 0.28f, 0.32f);
        static readonly Color ShippedNodePlayer = new Color(0.14f, 0.48f, 0.28f);
        static readonly Color ShippedNodeOpponent = new Color(0.56f, 0.38f, 0.12f);
        static readonly Color ShippedDeckTint = new Color(0.13f, 0.19f, 0.25f);
        static readonly Color ShippedDiscardTint = new Color(0.21f, 0.17f, 0.14f);

        // Mirrored from the presenter's construction, for the same reason the colours are: the board's shape is
        // what the families have to account for, and reading it back off the built board would let a body
        // disappear from one family into another with both sides agreeing.
        const int ProtocolSockets = 2;          // one per half, beside that half's Active
        const int BuriedLayersPerPile = 2;      // MakePile stacks two cubes behind the top card

        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<Material> probes = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (Material probe in probes)
            {
                if (probe != null)
                {
                    Object.DestroyImmediate(probe);
                }
            }

            probes.Clear();

            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            spawned.Clear();
        }

        // ---- Rig -----------------------------------------------------------------------

        /// <summary>
        /// A board of the test's own, built the way the presenter builds one with no prefab to bind, and pitched
        /// the way the scene pitches it. Mirrors the throwaway-board helper in <c>BattlefieldHudTests</c>: the
        /// duplication is a comment deep and the two files would otherwise agree about each other's internals.
        /// </summary>
        BattlefieldPresenter NewBoard(out Transform board)
        {
            var go = new GameObject("BoardSkinTestsPresenter");
            spawned.Add(go);

            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.BuildBoard();
            board = presenter.transform.Find("Battlefield");
            Assert.IsNotNull(board, "BuildBoard must create the board root.");
            return presenter;
        }

        /// <summary>
        /// A material of the board's own shader, so a probe answers <c>HasProperty("_BaseColor")</c> exactly the
        /// way the generated ones do and the skin tests exercise the real property path.
        /// </summary>
        Material NewProbe(Transform board)
        {
            Renderer[] renderers = board.GetComponentsInChildren<Renderer>(true);
            Shader shader = null;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.name == "Body" && renderer.sharedMaterial != null)
                {
                    shader = renderer.sharedMaterial.shader;
                    break;
                }
            }

            Assert.IsNotNull(shader, "A built board must carry a material to take its shader from.");

            var probe = new Material(shader);
            probes.Add(probe);
            return probe;
        }

        /// <summary>
        /// Every cube on the board. <c>MakeBody</c> names the cube <c>Body</c> under a holder that carries the
        /// tile's name, and the labels are TextMeshPro, so this is the body inventory and nothing else.
        /// </summary>
        static List<Renderer> Bodies(Transform board)
        {
            var bodies = new List<Renderer>();
            foreach (Renderer renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Body")
                {
                    bodies.Add(renderer);
                }
            }

            return bodies;
        }

        static BattlefieldPresenter.Surface FamilyOf(Renderer body)
        {
            return BattlefieldPresenter.SurfaceFor(body.transform.parent.name);
        }

        static int CountFamily(List<Renderer> bodies, BattlefieldPresenter.Surface surface)
        {
            int count = 0;
            foreach (Renderer body in bodies)
            {
                if (FamilyOf(body) == surface)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The one cube whose holder is named <paramref name="holder"/>. Asserts uniqueness rather than taking
        /// the first match, because a duplicated tile name is precisely the accident that would put two sockets
        /// on one identity.
        /// </summary>
        static Renderer BodyNamed(List<Renderer> bodies, string holder)
        {
            Renderer found = null;
            int matches = 0;
            foreach (Renderer body in bodies)
            {
                if (body.transform.parent.name == holder)
                {
                    found = body;
                    matches++;
                }
            }

            Assert.AreEqual(1, matches, $"{holder} must be exactly one body on the board.");
            Assert.IsNotNull(found, $"{holder} must exist.");
            return found;
        }

        /// <summary>
        /// How many bodies a family is owed, derived from the same board the presenter lays itself out from.
        /// </summary>
        static int ExpectedCount(BattlefieldPresenter.Surface surface)
        {
            switch (surface)
            {
                case BattlefieldPresenter.Surface.Table:
                    return 1;
                case BattlefieldPresenter.Surface.Plate:
                    return 2 + Board.NodeCount;                        // two Core plates, one per Node
                case BattlefieldPresenter.Surface.Rim:
                    return 4 * (2 * (Board.CoreActiveSlots + Board.CoreBenchSlots + 1)
                        + Board.NodeCount * 2 * (Board.NodeActiveSlots + Board.NodeBenchSlots));
                case BattlefieldPresenter.Surface.Pile:
                    return 2 * (1 + BuriedLayersPerPile);               // two tops, stacked
                default:
                    return 2 * Board.CoreActiveSlots + 2 * Board.CoreBenchSlots + ProtocolSockets
                        + Board.NodeCount * 2 * (Board.NodeActiveSlots + Board.NodeBenchSlots);
            }
        }

        static BattlefieldPresenter.Surface[] AllFamilies()
        {
            return new[]
            {
                BattlefieldPresenter.Surface.Table,
                BattlefieldPresenter.Surface.Socket,
                BattlefieldPresenter.Surface.Plate,
                BattlefieldPresenter.Surface.Pile,
                BattlefieldPresenter.Surface.Rim,
            };
        }

        static void AssertSame(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), $"{what} red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), $"{what} green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), $"{what} blue");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), $"{what} alpha");
        }

        // ---- The defaults --------------------------------------------------------------

        [Test]
        public void Albedos_AreTheShippedFrame()
        {
            BattlefieldPresenter presenter = NewBoard(out _);

            AssertSame(presenter.tableTint, ShippedTableTint, "tableTint");
            AssertSame(presenter.activeEmpty, ShippedActiveEmpty, "activeEmpty");
            AssertSame(presenter.benchEmpty, ShippedBenchEmpty, "benchEmpty");
            AssertSame(presenter.protocolEmpty, ShippedProtocolEmpty, "protocolEmpty");
            AssertSame(presenter.activeRim, ShippedActiveRim, "activeRim");
            AssertSame(presenter.benchRim, ShippedBenchRim, "benchRim");
            AssertSame(presenter.protocolRim, ShippedProtocolRim, "protocolRim");
            AssertSame(presenter.playerTint, ShippedPlayerTint, "playerTint");
            AssertSame(presenter.opponentTint, ShippedOpponentTint, "opponentTint");
            AssertSame(presenter.corePlateTint, ShippedCorePlateTint, "corePlateTint");
            AssertSame(presenter.nodeFree, ShippedNodeFree, "nodeFree");
            AssertSame(presenter.nodePlayer, ShippedNodePlayer, "nodePlayer");
            AssertSame(presenter.nodeOpponent, ShippedNodeOpponent, "nodeOpponent");
            AssertSame(presenter.deckTint, ShippedDeckTint, "deckTint");
            AssertSame(presenter.discardTint, ShippedDiscardTint, "discardTint");
        }

        [Test]
        public void Slots_StartEmptySoAnUntouchedSceneLooksUntouched()
        {
            // The whole "defaults preserve the frame" claim rests on this. Five non-null defaults would mean
            // every existing scene and prefab silently started rendering the same single material, and the
            // board would look broken on somebody else's machine with no edit to explain it.
            BattlefieldPresenter presenter = NewBoard(out _);

            Assert.IsNull(presenter.tableSkin, "tableSkin");
            Assert.IsNull(presenter.socketSkin, "socketSkin");
            Assert.IsNull(presenter.plateSkin, "plateSkin");
            Assert.IsNull(presenter.pileSkin, "pileSkin");
            Assert.IsNull(presenter.rimSkin, "rimSkin");
        }

        // ---- The partition -------------------------------------------------------------

        [Test]
        public void FallbackBoardShader_HasPassAcceptedByThe2DRenderer()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/OverlinkBoardFallback.shader");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            probes.Add(material);
            int pass = material.FindPass("ForwardLit");
            Assert.That(pass, Is.GreaterThanOrEqualTo(0));
            var lightMode = shader.FindPassTagValue(pass, new UnityEngine.Rendering.ShaderTagId("LightMode"));
            Assert.AreEqual(new UnityEngine.Rendering.ShaderTagId("SRPDefaultUnlit"), lightMode,
                "Renderer2D skips UniversalForward passes, making all fallback board surfaces invisible.");
        }

        [Test]
        public void EveryBodyOnTheBoardIsClaimedByExactlyOneFamily()
        {
            NewBoard(out Transform board);
            List<Renderer> bodies = Bodies(board);

            int classified = 0;
            foreach (BattlefieldPresenter.Surface surface in AllFamilies())
            {
                int count = CountFamily(bodies, surface);
                classified += count;
                Assert.AreEqual(ExpectedCount(surface), count,
                    $"{surface} owns {count} bodies and is owed {ExpectedCount(surface)}. A name that drifted " +
                    "into another family stops taking that family's skin, silently.");
            }

            Assert.AreEqual(bodies.Count, classified,
                "Every cube on the board must belong to one of the five families. An unclaimed body is a tile " +
                "no skin can reach, and a double-counted one is a tile that changes colour twice over.");
        }

        [Test]
        public void SurfaceFor_TellsTheRimApartFromASocket()
        {
            // The gold border cube is literally named "Socket", which is why the rim test has to precede the
            // socket fallback in SurfaceFor. Flip those two lines and every rim takes the socket skin while the
            // Active border stops glowing - and the board still renders, which is why it needs a name-level test.
            Assert.AreEqual(BattlefieldPresenter.Surface.Rim, BattlefieldPresenter.SurfaceFor("Socket"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Table, BattlefieldPresenter.SurfaceFor("Table"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Pile, BattlefieldPresenter.SurfaceFor("DeckPile"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Pile, BattlefieldPresenter.SurfaceFor("DiscardPile"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Pile, BattlefieldPresenter.SurfaceFor("Stack2"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Plate, BattlefieldPresenter.SurfaceFor("NodePlate3"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Plate, BattlefieldPresenter.SurfaceFor("OppCorePlate"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Socket, BattlefieldPresenter.SurfaceFor("PlayProtocol"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Socket,
                BattlefieldPresenter.SurfaceFor("NodePlayBench1_1"));
            Assert.AreEqual(BattlefieldPresenter.Surface.Socket, BattlefieldPresenter.SurfaceFor(null));
            Assert.AreEqual(BattlefieldPresenter.Surface.Socket, BattlefieldPresenter.SurfaceFor(""));
        }

        // ---- No skin -------------------------------------------------------------------

        [Test]
        public void NoSkin_RendersTheGeneratedMaterialForItsOwnColour()
        {
            // The palette reaches the screen through one name-keyed resolver shared by the build path and the
            // prefab-bind path. This walks every branch of it, so a colour that stops being read - or a name
            // that starts matching the wrong branch - fails here rather than on a screenshot.
            NewBoard(out Transform board);
            List<Renderer> bodies = Bodies(board);

            var expected = new (string Holder, Color Colour)[]
            {
                ("Table", ShippedTableTint),
                ("DeckPile", ShippedDeckTint),
                ("DiscardPile", ShippedDiscardTint),
                ("PlayCorePlate", ShippedCorePlateTint),
                ("OppCorePlate", ShippedCorePlateTint),
                ("NodePlate0", ShippedNodeFree),
                ("PlayCoreActive0", ShippedActiveEmpty),
                ("PlayCoreBench0", ShippedBenchEmpty),
                ("PlayProtocol", ShippedProtocolEmpty),
            };

            foreach (var pair in expected)
            {
                Renderer body = BodyNamed(bodies, pair.Holder);
                Material material = body.sharedMaterial;
                Assert.IsNotNull(material, $"{pair.Holder} renders with no material at all.");

                // Real assets, not in-memory materials: an in-memory one collapses onto a single shared
                // material the moment the board is saved as a prefab, which is the hazard EditorMaterial
                // exists to avoid.
                string path = AssetDatabase.GetAssetPath(material);
                Assert.IsFalse(string.IsNullOrEmpty(path),
                    $"{pair.Holder}'s material is not an asset on disk, so a prefab save would flatten it.");
                Assert.AreEqual("Slot_" + ColorUtility.ToHtmlStringRGB(pair.Colour), material.name,
                    $"{pair.Holder} is wearing the wrong generated colour.");
            }

            // Active retains the historical Socket holder name; every card-bearing slot has a frame, while
            // Bench and Protocol use SlotFrame so the editable hierarchy exposes their role.
            Renderer rim = null;
            foreach (Renderer body in bodies)
            {
                Transform cursor = body.transform;
                while (cursor != null && cursor != board)
                {
                    if (cursor.name == "Socket")
                    {
                        rim = body;
                        break;
                    }
                    cursor = cursor.parent;
                }

                if (rim != null)
                {
                    break;
                }
            }

            Assert.IsNotNull(rim, "Every Active socket carries a frame cube, so a board with none is incomplete.");
            Assert.AreEqual("Slot_" + ColorUtility.ToHtmlStringRGB(ShippedActiveRim), rim.sharedMaterial.name,
                "The Active rim is the one border that survives deployment; it cannot take the socket colour.");

            int frames = 0;
            foreach (Renderer body in bodies)
            {
                if (body.transform.parent.name.StartsWith("Frame"))
                {
                    frames++;
                }
            }

            int expectedFrames = 4 * (2 * (Board.CoreActiveSlots + Board.CoreBenchSlots + 1)
                + Board.NodeCount * 2 * (Board.NodeActiveSlots + Board.NodeBenchSlots));
            Assert.AreEqual(expectedFrames, frames,
                "Every card slot must have four visible editable edge frames.");

            Renderer benchFrame = null;
            foreach (Renderer body in bodies)
            {
                if (body.transform.parent.name.StartsWith("Frame")
                    && body.transform.parent.parent != null)
                {
                    string slotName = body.transform.parent.parent.parent != null
                        ? body.transform.parent.parent.parent.name
                        : string.Empty;
                    if (slotName.Contains("Bench"))
                    {
                        benchFrame = body;
                        break;
                    }
                }
            }

            Assert.IsNotNull(benchFrame, "A Bench slot must carry a SlotFrame.");
            Assert.AreEqual("Slot_" + ColorUtility.ToHtmlStringRGB(ShippedBenchRim), benchFrame.sharedMaterial.name,
                "Bench frames use the cool bench accent.");
        }

        // ---- Skin ----------------------------------------------------------------------

        [Test]
        public void ASkinClaimsItsWholeFamilyAndNotOneBodyOutsideIt()
        {
            BattlefieldPresenter presenter = NewBoard(out Transform board);
            List<Renderer> bodies = Bodies(board);

            // Five distinct probes at once, so a body wearing the wrong one is a positive statement about which
            // two families overlap. Testing them one at a time would let a leak pass as an untouched neighbour.
            var skin = new Dictionary<BattlefieldPresenter.Surface, Material>
            {
                { BattlefieldPresenter.Surface.Table, NewProbe(board) },
                { BattlefieldPresenter.Surface.Socket, NewProbe(board) },
                { BattlefieldPresenter.Surface.Plate, NewProbe(board) },
                { BattlefieldPresenter.Surface.Pile, NewProbe(board) },
                { BattlefieldPresenter.Surface.Rim, NewProbe(board) },
            };

            presenter.tableSkin = skin[BattlefieldPresenter.Surface.Table];
            presenter.socketSkin = skin[BattlefieldPresenter.Surface.Socket];
            presenter.plateSkin = skin[BattlefieldPresenter.Surface.Plate];
            presenter.pileSkin = skin[BattlefieldPresenter.Surface.Pile];
            presenter.rimSkin = skin[BattlefieldPresenter.Surface.Rim];
            presenter.ApplySkins();

            foreach (Renderer body in bodies)
            {
                BattlefieldPresenter.Surface family = FamilyOf(body);
                Assert.AreSame(skin[family], body.sharedMaterial,
                    $"{body.transform.parent.name} is a {family} body and must wear the {family} skin.");
            }
        }

        [Test]
        public void ApplySkins_IsOneDirectional()
        {
            // Both halves of the contract, because the second one is a deliberate asymmetry and not an
            // oversight. A cleared slot cannot recover the colour that generated the material underneath -
            // guessing it from the current material would trade a hand-authored per-tile material for a
            // generated one of the same shade, which is the opposite of what an artist just did by hand. So a
            // set slot acts and an empty one stays out of the way, and the way back to the shipped look is
            // rebuilding the board, which consults the palette from scratch.
            BattlefieldPresenter presenter = NewBoard(out Transform board);
            List<Renderer> bodies = Bodies(board);

            Renderer table = BodyNamed(bodies, "Table");
            Renderer deck = BodyNamed(bodies, "DeckPile");
            Material authoredTable = table.sharedMaterial;
            Material authoredDeck = deck.sharedMaterial;

            presenter.pileSkin = NewProbe(board);
            presenter.ApplySkins();

            Assert.AreSame(authoredTable, table.sharedMaterial,
                "An empty slot must not reach outside its family: the tabletop is not a pile, and a skin that " +
                "changed it would be a bug nobody could see coming.");
            Assert.AreNotSame(authoredDeck, deck.sharedMaterial,
                "sanity: the pile slot has to have taken, or this test proves nothing at all.");

            Material probe = deck.sharedMaterial;
            presenter.pileSkin = null;
            presenter.ApplySkins();

            Assert.AreSame(probe, deck.sharedMaterial,
                "Clearing a slot leaves the body wearing what it was wearing. This is the documented " +
                "asymmetry: rebuilding is what restores a generated material.");
        }

        [Test]
        public void TableSkin_ReceivesTableTintOnBuildAndSkinChanges()
        {
            var go = new GameObject("TintedTableBoard");
            spawned.Add(go);
            var presenter = go.AddComponent<BattlefieldPresenter>();
            presenter.tableTint = new Color(0.045f, 0.07f, 0.12f, 1f);
            var skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Battlefield/Fallback_Table.mat");
            Assert.IsNotNull(skin);
            presenter.tableSkin = skin;
            presenter.BuildBoard();

            Transform board = presenter.transform.Find("Battlefield");
            Renderer table = BodyNamed(Bodies(board), "Table");
            var block = new MaterialPropertyBlock();
            table.GetPropertyBlock(block);
            AssertSame(block.GetColor("_BaseColor"), presenter.tableTint, "built table tint");

            presenter.tableTint = new Color(0.1f, 0.15f, 0.2f, 1f);
            presenter.ApplySkins();
            table.GetPropertyBlock(block);
            AssertSame(block.GetColor("_BaseColor"), presenter.tableTint, "reapplied table tint");
        }

        [Test]
        public void ASkinSharesOneMaterialAcrossColoursThatDiffer()
        {
            // This is the multiplication contract, and it is a claim about two subsystems at once. The socket
            // family holds three different albedos - Active, Bench and Protocol are deliberately three hues so a
            // row never reads as three identical slots - so one material cannot be what puts those hues on
            // screen. They still arrive, through the per-frame MaterialPropertyBlock in SetBodyColor. If a future
            // refactor moves the colour into the material, this is the test that goes red, and it should.
            BattlefieldPresenter presenter = NewBoard(out Transform board);
            List<Renderer> bodies = Bodies(board);

            Assert.IsFalse(SameShade(ShippedActiveEmpty, ShippedBenchEmpty),
                "The three socket albedos are expected to differ; if they do not, this test is vacuous.");
            Assert.IsFalse(SameShade(ShippedBenchEmpty, ShippedProtocolEmpty), "As above, the other seam.");
            Assert.IsFalse(SameShade(ShippedProtocolEmpty, ShippedActiveEmpty), "As above, the closing seam.");

            // Uncollapsible baseline: with no skin, three colours mean three materials.
            Renderer active = BodyNamed(bodies, "PlayCoreActive0");
            Renderer bench = BodyNamed(bodies, "PlayCoreBench0");
            Renderer protocol = BodyNamed(bodies, "PlayProtocol");
            Assert.AreNotSame(active.sharedMaterial, bench.sharedMaterial,
                "Generated materials are keyed by colour, so two shades cannot share one.");

            presenter.socketSkin = NewProbe(board);
            presenter.ApplySkins();

            Assert.AreSame(active.sharedMaterial, bench.sharedMaterial,
                "One skin, one material, the whole family - which is also what keeps the family in one draw " +
                "batch instead of one material instance per tile.");
            Assert.AreSame(bench.sharedMaterial, protocol.sharedMaterial, "The family is all three.");
        }

        [Test]
        public void ASkinDroppedAfterTheBuildLandsWithoutRebuilding()
        {
            // The per-frame check is what makes the slots a tuning surface instead of a build-time input: a
            // material dropped on a live board has to show up, or the five Inspector boxes only mean anything
            // to whoever remembers to rebuild the board afterwards.
            BattlefieldPresenter presenter = NewBoard(out Transform board);
            List<Renderer> bodies = Bodies(board);
            Renderer table = BodyNamed(bodies, "Table");
            Material shipped = table.sharedMaterial;

            // Nothing has moved, so the first pass must leave the board exactly as the build left it.
            presenter.ApplySkinsIfChanged();
            Assert.AreSame(shipped, table.sharedMaterial,
                "An untouched slot set must not walk the board at all.");

            Material probe = NewProbe(board);
            presenter.tableSkin = probe;
            presenter.ApplySkinsIfChanged();
            Assert.AreSame(probe, table.sharedMaterial, "A slot that moved has to reach the board.");

            // The dedupe, observed through its consequence rather than through a counter: hand the tabletop a
            // second material and ask for another pass. If the snapshot had not followed the first walk, this
            // call would walk again and overwrite it with the probe - which is exactly the per-frame cost the
            // five reference reads exist to avoid.
            Material other = NewDistinctProbe(board);
            table.sharedMaterial = other;
            presenter.ApplySkinsIfChanged();
            Assert.AreSame(other, table.sharedMaterial,
                "The slots have not moved since the last walk, so the board must not be walked again.");
        }

        [Test]
        public void ApplySkins_BeforeABoardExistsIsHarmless()
        {
            // The bind path calls it the moment a board resolves, and a component whose board has not been built
            // yet is a normal state for a hand scene - so the call has to be a no-op rather than a null crash.
            var go = new GameObject("BoardSkinTestsBare");
            spawned.Add(go);
            var presenter = go.AddComponent<BattlefieldPresenter>();

            presenter.pileSkin = NewProbeForTest();
            presenter.ApplySkins();

            Assert.IsNull(presenter.transform.Find("Battlefield"),
                "Applying a skin must not build a board as a side effect.");
        }

        /// <summary>
        /// A second probe, deliberately a different object from <see cref="NewProbe"/>. The dedupe test reads
        /// reference identity off one renderer, so two materials that merely look alike would do; asking for a
        /// different shader as well is the belt, since it also puts a second shader through the property-id
        /// choice in SetBodyColor.
        /// </summary>
        Material NewDistinctProbe(Transform board)
        {
            Shader boardShader = null;
            foreach (Renderer renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Body" && renderer.sharedMaterial != null)
                {
                    boardShader = renderer.sharedMaterial.shader;
                    break;
                }
            }

            Shader other = Shader.Find("Universal Render Pipeline/Lit");
            if (other == boardShader)
            {
                other = Shader.Find("Standard");
            }

            if (other == null || other == boardShader)
            {
                // No second shader in the project to borrow: a fresh instance of the first is still a distinct
                // object, which is the property the test actually asserts.
                return NewProbe(board);
            }

            var probe = new Material(other);
            probes.Add(probe);
            return probe;
        }

        Material NewProbeForTest()
        {
            // No board to borrow a shader from here, so ask for the same one the generated materials use.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Assert.IsNotNull(shader, "The project must have a lit shader to build a board material from.");

            var probe = new Material(shader);
            probes.Add(probe);
            return probe;
        }

        static bool SameShade(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.0001f && Mathf.Abs(a.g - b.g) < 0.0001f
                && Mathf.Abs(a.b - b.b) < 0.0001f;
        }
    }
}
