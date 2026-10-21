using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Overlink.Cards;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Builds the gameplay scene deterministically instead of hand-editing .unity YAML.
    /// Run from the menu (Overlink > Scenes > Build Gameplay Scene) or in batchmode:
    ///   Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod Overlink.EditorTools.BuildGameplayScene.BatchBuild
    /// </summary>
    public static class BuildGameplayScene
    {
        const string ScenePath = "Assets/Scenes/Gameplay.unity";

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

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            // Underlords / Auto-Chess framing: the board lies flat on a table and the camera looks down at a
            // pitched angle from behind the player's edge, with mild perspective so the opponent's side
            // recedes. Tune pitch/fov/distance here in one place.
            const float pitch = 52f;
            const float fov = 38f;
            const float distance = 16f;
            Vector3 focus = new Vector3(0f, 0f, 0.35f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = fov;
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
            rigSo.FindProperty("fieldOfView").floatValue = fov;
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
            // 5:7 portrait card face (width:height).
            so.FindProperty("cardSize").vector3Value = new Vector3(1.0f, 1.4f, 0.06f);
            // The coordinator drives the hand from MatchState; a self-deal from DeckController would
            // desync the view before the match even starts.
            so.FindProperty("autoDraw").boolValue = false;
            // Place the hand at the near edge, in front of the two bottom Node plates, and require a drag
            // up the board (local +Y) to play.
            so.FindProperty("handY").floatValue = -4.3f;
            so.FindProperty("playLineY").floatValue = -2.8f;
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
