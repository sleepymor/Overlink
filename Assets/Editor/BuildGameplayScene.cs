using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Overlink.Cards;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Bootstraps the gameplay scene deterministically, for a fresh clone or after a catastrophic loss.
    /// Run from the menu (Overlink &gt; Scenes &gt; Build Gameplay Scene) or in batchmode:
    ///   Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod Overlink.EditorTools.BuildGameplayScene.BatchBuild
    /// <para>
    /// This is NOT the tuning surface. <see cref="Build"/> starts from <c>NewScene</c> and saves over
    /// <see cref="ScenePath"/>, so anything hand-placed in the scene - a moved socket, a retimed tween, a
    /// different card size - is destroyed by running it. The scene is the thing that ships and the thing to
    /// edit; the builder exists so the scene can be recreated from nothing once, and its serialized values
    /// are kept current so that recreation does not regress the layout.
    /// </para>
    /// </summary>
    public static class BuildGameplayScene
    {
        const string ScenePath = "Assets/Scenes/Gameplay.unity";
        const string PresentationRoot = "Assets/Cards/Presentation";

        [MenuItem("Overlink/Scenes/Build Gameplay Scene")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Rebuild Gameplay Scene",
                    ScenePath + " already exists. Replace it?",
                    "Replace", "Cancel");
                if (!overwrite) return;
            }

            Build();
            EditorUtility.DisplayDialog("Gameplay Scene", "Built " + ScenePath, "OK");
        }

        // Prompt-free variant so automation (menu-item execution, CI) can rebuild without a modal
        // dialog wedging the editor. The interactive entry point above keeps its confirmation.
        [MenuItem("Overlink/Scenes/Rebuild Gameplay Scene (No Prompt)")]
        public static void RebuildFromMenuWithoutPrompt()
        {
            Build();
            Debug.Log("[Overlink] Rebuilt " + ScenePath + " (no prompt)");
        }

        public static void BatchBuild()
        {
            Build();
            Debug.Log("[Overlink] Gameplay scene built at " + ScenePath);
        }

        static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateKeyLight();
            CreateMatchRoot();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The camera's vertical field of view, in degrees. A class constant rather than a local of
        /// <see cref="CreateCamera"/> because the hand's corridor is derived from what the lens can actually
        /// show, and a number used by two systems has to be typed once.
        /// </summary>
        const float CameraFov = 38f;

        /// <summary>
        /// How far down the lens the hand hangs, in camera units. Shared by the holder's placement and the
        /// corridor math for the same reason: the row is limited by the frame it is seen through, and the
        /// frame it is seen through is this depth. It is also the only knob that sizes the cards, since a
        /// card's height on screen is its height here over the visible height at this depth - 1.4615 out of
        /// 0.689 * 6.7 = 4.61 units, so just under a third of the screen. Pushed rather than scaled,
        /// because depth is what the drag, the hover and the drop ray are all already measured in,
        /// and a scaled holder would quietly rescale every one of them.
        ///
        /// This depth is the closest one that keeps the row's loss below a quarter of a card, which is what
        /// the framing was signed off on. The riveted row's outer corner lands at camera-space y =
        /// -1.65 - 0.182 (the arc) - 0.830 (half a card, rolled 14 degrees) = -2.662, and that is fixed no
        /// matter where the row sits, while the frame reaches only to -tan(19) * z. So the card lost to the
        /// bottom edge is 2.662 - 0.3443 * z, and asking that to stay under 0.25 * 1.4615 = 0.365 puts
        /// z at 6.67 or more. At 6.7 the outer cards hang 24.3 percent of a card and the centre card 5.
        ///
        /// It was 7.9 while the row was being made congruent, and that was the wrong trade: at 7.9 a card
        /// is 190px tall on a 705px screen, which draws an 832x1216 frame at 6.4:1 minification, and the
        /// name plate em lands at 8px with the rules text under it at 5px. The frame's hairlines then
        /// alias to a different sub-pixel pattern on every card in the fan, which is what still read as
        /// the layout sliding after the geometry was fixed, and as the card being pixelated. 6.7 buys
        /// 18 percent more card. It also moves the row DOWN on screen - the whole fan sits below the lens
        /// axis, so magnifying it about the centre pushes its top edge from 235px to 212px of 705 - so the
        /// bigger cards cover less of the board, not more. Raising the holder instead would have unclipped
        /// the row by pushing 40px of fan up into the player's own ranks.
        /// </summary>
        const float HandDepth = 6.7f;

        /// <summary>
        /// How far below the lens axis the row hangs. Low enough that the board reads clear above the fan,
        /// high enough that the arc's drop does not take the ends of the row out of the frame - the two
        /// are the same dial as HandDepth, since what matters is the row's height against tan(fov/2) * z.
        /// </summary>
        const float HandHeight = -1.65f;

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            // Underlords / Auto-Chess framing: the board lies flat on a table and the camera looks down at a
            // pitched angle from behind the player's edge, with mild perspective so the opponent's side
            // recedes. Tune pitch/fov/distance here in one place.
            const float pitch = 52f;
            const float distance = 16f;
            Vector3 focus = new Vector3(0f, 0f, 0.35f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.07f, 0.10f, 1f);

            go.AddComponent<AudioListener>();

            // Runtime zoom (wheel) and pan (right/middle drag, WASD) rig. It repositions the camera every
            // frame from these framing values, so the scene is saved with the same initial framing too.
            var rig = go.AddComponent<Overlink.Match.BattlefieldCameraController>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("focus").vector3Value = focus;
            rigSo.FindProperty("pitch").floatValue = pitch;
            rigSo.FindProperty("yaw").floatValue = 0f;
            rigSo.FindProperty("distance").floatValue = distance;
            rigSo.FindProperty("fieldOfView").floatValue = CameraFov;
            // The opening framing IS the deepest tabletop zoom-out, so the first scroll out past it lifts
            // into the bird's-eye view instead of pulling back further. Seeded from the same constant here
            // rather than typed twice, so retuning the opening shot retunes the ceiling with it.
            rigSo.FindProperty("maxDistance").floatValue = distance;
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
            go.transform.rotation = rot;
            go.transform.position = focus - rot * Vector3.forward * distance;

            EnsureUniversalCameraData(cam);
        }

        static void CreateKeyLight()
        {
            var go = new GameObject("Key Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
        }

        static void CreateMatchRoot()
        {
            var root = new GameObject("Match");
            // Lay the whole match flat on a table (Underlords style). The root's local +Y runs up the board
            // (hand -> player line -> Nodes -> opponent line) and its local -Z, where card faces and labels
            // sit, points up at the pitched camera.
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            root.transform.position = Vector3.zero;

            var deck = root.AddComponent<DeckController>();
            var hand = root.AddComponent<HandController>();

            // Anchors sit in the Match root's local XY plane, which the board root shares, so the deck and
            // discard markers are seeded from the presenter's own pile coordinates: they end up underneath
            // the piles the board draws rather than floating where nothing is visible. The presenter also
            // re-aligns them once its board exists, so moving the Piles group in the editor moves the card
            // flight path with it.
            Transform deckPile = CreateAnchor(root.transform, "DeckPile",
                PileAnchor(Overlink.Match.BattlefieldPresenter.DeckPilePosition));
            // Kept even though playCamera below supersedes it: it is the tabletop pose the hand falls back
            // to the moment playCamera is cleared in the Inspector, and a scene with no PlaySlot anchor at
            // all would have nothing to fall back to.
            Transform playSlot = CreateAnchor(root.transform, "PlaySlot", new Vector3(0f, -0.35f, 0f));
            Transform discardPile = CreateAnchor(root.transform, "DiscardPile",
                PileAnchor(Overlink.Match.BattlefieldPresenter.DiscardPilePosition));

            // Assign the serialized references explicitly so the scene is self-describing
            // instead of relying on HandController.Awake() fallbacks.
            var so = new SerializedObject(hand);
            so.FindProperty("deck").objectReferenceValue = deck;
            so.FindProperty("deckPile").objectReferenceValue = deckPile;
            so.FindProperty("playSlot").objectReferenceValue = playSlot;
            so.FindProperty("discardPile").objectReferenceValue = discardPile;
            so.FindProperty("handSizeStart").intValue = 5;
            // Gameplay uses the delivered 832x1216 portrait frame; standard art remains 16:10.
            var handCardSize = new Vector3(1.0f, 1.4615f, 0.06f);
            so.FindProperty("cardSize").vector3Value = handCardSize;
            // The coordinator drives the hand from MatchState; a self-deal from DeckController would
            // desync the view before the match even starts.
            so.FindProperty("autoDraw").boolValue = false;

            // The delivered frame art and tier shaders. Without these the hand falls back to CardView's
            // legacy cube body: a flat accent-coloured slab with the labels printed on it, because
            // presentation mode is switched on by the presence of a catalog or a fixture. Every card a
            // match deals carries a real CardDefinition, so type, faction and rarity resolve all by
            // themselves once the catalogs are here.
            so.FindProperty("frameCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(PresentationRoot + "/CardFrameCatalog.asset");
            so.FindProperty("templateCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            so.FindProperty("tierMaterials").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(PresentationRoot + "/CardTierMaterialSet.asset");
            // The hand does not lie on the table: it hangs in the camera's own plane, so the fan reads as a
            // folding fan instead of a scattered row. HandController.handRoot carries the reasoning - the
            // arc and yaw were written for a hand seen head-on, and the screen plane is the only place on a
            // pitched board that still is.
            var playCam = GameObject.FindGameObjectWithTag("MainCamera");
            Transform handHome = null;
            if (playCam != null)
            {
                var holderGo = new GameObject("HandRoot");
                holderGo.transform.SetParent(playCam.transform, false);
                // HandDepth down the lens, HandHeight below the axis. At 38 degrees of field of view the
                // visible height there is 0.689 * 7.9 = 5.44 units, so the 1.4615 card stands about a
                // quarter of the screen tall, and the arc's drop off the ends of the row still lands
                // inside the frame rather than below it. Deep enough back that the player's own ranks
                // read well clear above the fan instead of behind it.
                holderGo.transform.localPosition = new Vector3(0f, HandHeight, HandDepth);
                handHome = holderGo.transform;
            }
            so.FindProperty("handRoot").objectReferenceValue = handHome;
            // handY is the row's height in the camera plane, and the holder already places it, so the row
            // sits on the holder's own origin.
            so.FindProperty("handY").floatValue = 0f;
            // The roll is what makes a fan, and it was also what broke the fan. Each card used to rotate
            // about its OWN middle, which puts card i+1 at a rotation of card i about i's centre: the edge
            // two neighbours share opens out at one corner and pinches shut at the other, so the overlap
            // differed from pair to pair and between the top and bottom of the same pair. That is the
            // slanted slice across a name plate, worst at the ends of the row where the roll is largest.
            //
            // Folding the row onto one rivet instead makes every neighbouring pair an exact rotation of the
            // one before it - congruent by construction, so the overlap is identical the whole length of the
            // row at every height, while the cards still roll outward into a fan.
            //
            // The rivet also takes the arc off the operator's hands, which is the second half of the old
            // bug: arcHeight and maxYaw were independent dials and nothing forced them to agree, so the
            // 0.35 beside a 16 degree roll described a fan that could not exist. A row of half-width
            // halfRow rolled to yaw sits at r = halfRow / sin yaw and rises r * (1 - cos yaw), which is
            // halfRow * tan(yaw / 2) - 0.183 at yaw 14 and the 1.48 half-row this spacing gives. arcHeight
            // is set to match so it still documents the real shape; the pivot ignores it.
            so.FindProperty("fanPivotsOnRivet").boolValue = true;
            so.FindProperty("maxYaw").floatValue = 14f;
            so.FindProperty("arcHeight").floatValue = 0.18f;
            // A folding fan overlaps: 0.26 of a card's width tucked behind its neighbour is what makes the
            // row read as one object instead of five slabs in a line, while every card still shows its own
            // cost badge, name plate and tier clear of the next card's corner.
            const float fanOverlap = 0.26f;
            so.FindProperty("fanSpacing").floatValue = handCardSize.x - fanOverlap;
            // A play is a drag UP the screen past this line. +Y is up in the camera plane and the row hangs
            // at HandHeight, so 0.6 asks for 2.25 units of travel - four tenths of the screen height -
            // which is far enough to be unambiguous at a card size that fills a quarter of it.
            so.FindProperty("playLineY").floatValue = 0.6f;
            // The lifts are shallow, because a depth in the camera's plane IS a fraction of the card's own
            // size, while the same number written for a hand lying on a table sixteen units away was
            // almost nothing: 0.36 back out of 7.9 is four and a half percent closer, where the 1.0 the
            // tabletop hand used would have been a sixth of the screen here. The pickup then spends
            // dragPickupSeconds travelling it rather than arriving in one frame, so grabbing a card never
            // moves it faster than the pointer does.
            so.FindProperty("hoverDepth").floatValue = -0.19f;
            so.FindProperty("dragDepth").floatValue = -0.36f;
            so.FindProperty("inspectDepth").floatValue = -0.41f;
            so.FindProperty("dragPickupSeconds").floatValue = 0.12f;
            // And the hover rises 0.36 up, which is six and a half percent of the visible height at this
            // depth: enough to clear the neighbour's frame without the card leaving the fan it belongs to.
            so.FindProperty("hoverLift").floatValue = 0.36f;
            // The corridor the row may not exceed is the screen now, not the board. Nothing in Core caps the
            // hand size, and a row wider than the frame loses its outer cards outright with no way to reach
            // them, so past this width the spacing closes up instead. Derived from the same lens the hand
            // hangs under, so retuning the field of view or the depth retunes it with them.
            // 16:9 is the aspect the framing was tuned at; a narrower window clips the outer cards and a
            // wider one simply leaves room the row does not use.
            const float screenMargin = 0.12f;
            float visibleHeight = 2f * Mathf.Tan(0.5f * CameraFov * Mathf.Deg2Rad) * HandDepth;
            float visibleWidth = visibleHeight * (16f / 9f);
            so.FindProperty("maxHandSpan").floatValue =
                Mathf.Max(0f, visibleWidth - handCardSize.x - 2f * screenMargin);

            // A played card is flown in front of the lens rather than left on the table. The board is
            // pitched 52 degrees, so a card lying in playSlot is seen at a steep angle and its text is
            // unreadable for the entire resolve; parented to the rig it also squares to the screen at any
            // pitch, including the overhead bird's-eye view, and rides zoom and pan for free instead of
            // sliding across the board underneath. Same transform the hand hangs from, so a played card
            // travels between two points in one space rather than crossing from the board to the lens.
            so.FindProperty("playCamera").objectReferenceValue = playCam != null ? playCam.transform : null;
            // 0.689 * 4.2 = 2.89 units of visible height at the rig's 38 degree field of view, so the
            // 1.4615 card at 1.15 covers just over half of it: centred and legible without hiding the
            // ranks it was played onto. +Z is in front of the lens (a camera looks down its own +Z), and
            // +0.22 on Y lifts it clear of the player's own row.
            so.FindProperty("playCardOffset").vector3Value = new Vector3(0f, 0.22f, 4.2f);
            so.FindProperty("playCardScale").floatValue = 1.15f;
            // Worth more time here than the tabletop beat got: centred is not the same as read.
            so.FindProperty("playCardHoldSeconds").floatValue = 1.1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The scene needs the match owner on it, otherwise the hand is only ever a legacy animation:
            // no commands, no AI, no board, no loop.
            var coordinator = root.AddComponent<Overlink.Match.GameCoordinator>();
            var coordinatorSo = new SerializedObject(coordinator);
            coordinatorSo.FindProperty("handController").objectReferenceValue = hand;
            coordinatorSo.ApplyModifiedPropertiesWithoutUndo();

            // The event log is screen-space UI rather than tabletop geometry: an overlay canvas pinned to the
            // right edge. It is built here, eagerly, so it is a real editable scene object you can move; the
            // presenter only binds to it (and creates one lazily if a scene has none).
            Overlink.Match.BattlefieldHud hud = Overlink.Match.BattlefieldHud.Ensure();

            // Phase C battlefield view: a read-only presentation of the Core match state. It is wired to
            // the coordinator so it never has to search for the match owner at runtime. The board geometry
            // itself ships as a prefab instance so it is a real, editable hierarchy object; the presenter
            // binds to it by name at startup (and rebuilds procedurally if it is ever missing).
            var presenter = root.AddComponent<Overlink.Match.BattlefieldPresenter>();
            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("coordinator").objectReferenceValue = coordinator;
            presenterSo.FindProperty("hud").objectReferenceValue = hud;
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            // The click layer: reading a card, picking an attacker, committing a block. It sits beside the
            // presenter rather than inside it because the presenter is geometry-only and judges no rules,
            // and it sits beside the coordinator because the coordinator owns the match.
            var input = root.AddComponent<Overlink.Match.BattlefieldInput>();
            var inputSo = new SerializedObject(input);
            inputSo.FindProperty("coordinator").objectReferenceValue = coordinator;
            inputSo.FindProperty("presenter").objectReferenceValue = presenter;
            inputSo.FindProperty("hand").objectReferenceValue = hand;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            // No account panel here: sign-in lives in the Auth scene behind ProtectedSceneGuard, so the
            // board never has to hide somebody else's UI again.

            GameObject boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattlefieldPrefabPath);
            if (boardPrefab != null)
            {
                var board = (GameObject)PrefabUtility.InstantiatePrefab(boardPrefab);
                board.name = "Battlefield";
                board.transform.SetParent(root.transform, false);
                board.transform.localPosition = Vector3.zero;
                board.transform.localRotation = Quaternion.identity;
                board.transform.localScale = Vector3.one;
            }
            else
            {
                Debug.Log(
                    "[Overlink] " + BattlefieldPrefabPath + " not found, so the board will be built " +
                    "procedurally at runtime. Run Overlink > Battlefields > Build Battlefield Prefab to " +
                    "author it as a prefab.");
            }
        }

        const string BattlefieldPrefabPath = "Assets/Prefabs/Battlefield.prefab";

        /// <summary>
        /// A pile position in the board's plane, as an anchor position in the Match root's plane. The two
        /// coincide because the board instance is placed at the identity under that root.
        /// </summary>
        static Vector3 PileAnchor(Vector2 pile)
        {
            return new Vector3(pile.x, pile.y, 0f);
        }

        static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        /// <summary>
        /// URP cameras need UniversalAdditionalCameraData. Resolved by reflection so this
        /// editor assembly does not hard-reference the URP package.
        /// </summary>
        static void EnsureUniversalCameraData(Camera cam)
        {
            var t = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t == null) return;
            if (cam.GetComponent(t) != null) return;
            cam.gameObject.AddComponent(t);
        }

        static void RegisterInBuildSettings()
        {
            // ProjectSettings/** is a protected path for this milestone, so the builder must never insert a
            // new entry here - only flip an existing one back on. If Gameplay.unity is not registered yet,
            // say so instead of silently dirtying a protected file.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(s => s.path == ScenePath);

            if (existing < 0)
            {
                Debug.Log(
                    "[Overlink] " + ScenePath + " is not registered in EditorBuildSettings and was left unregistered " +
                    "because ProjectSettings/** is protected. Register it manually when that gate is lifted.");
                return;
            }

            if (scenes[existing].enabled)
            {
                return;
            }

            scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
