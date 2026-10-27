using System.IO;
using Overlink.Cards;
using Overlink.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Builds the standalone card presentation preview scene through Unity Editor APIs.
    /// The scene intentionally has no AuthFlowController or ProtectedSceneGuard.
    /// </summary>
    public static class BuildCardPresentationPreviewScene
    {
        const string ScenePath = "Assets/Scenes/CardPresentationPreview.unity";
        const string LegendaryScenePath = "Assets/Scenes/CardPresentationLegendaryPreview.unity";
        const string CollectionScenePath = "Assets/Scenes/CardCollectionPreview.unity";
        const string PresentationRoot = "Assets/Cards/Presentation";
        const string FixturePath = "Assets/Cards/PlaceholderFixtures/CardPresentationFixture.asset";
        const string LegendaryFixturePath = "Assets/Cards/PlaceholderFixtures/CardPresentationFixture_Legendary.asset";

        static readonly Faction[] AllFactions = { Faction.Fsti, Faction.Frti, Faction.Fpb };
        static readonly CardType[] AllTypes =
            { CardType.Unit, CardType.Ability, CardType.Relay, CardType.Protocol };
        static readonly Rarity[] AllRarities =
            { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

        [MenuItem("Overlink/Scenes/Build Card Presentation Preview")]
        public static void BuildFromMenu()
        {
            Build(ScenePath, FixturePath, false);
            Debug.Log("[Overlink] Card presentation preview scene built at " + ScenePath);
        }

        [MenuItem("Overlink/Scenes/Build Legendary Card Preview")]
        public static void BuildLegendaryFromMenu()
        {
            CardPresentationEditor.CreateLegendaryPreviewFixture();
            Build(LegendaryScenePath, LegendaryFixturePath, true);
            Debug.Log("[Overlink] Legendary card presentation preview scene built at " + LegendaryScenePath);
        }

        /// <summary>
        /// Builds the collection preview: every rarity as a flat gallery row instead of a hand fan,
        /// so a tier's frame and shader can be compared side by side with no card overlapping another.
        /// </summary>
        [MenuItem("Overlink/Scenes/Build Card Collection Preview")]
        public static void BuildCollectionFromMenu()
        {
            BuildFactionCollection(Faction.Fsti);
        }

        /// <summary>
        /// One collection row per faction, so the FRTI and FPB frames get the same readable
        /// side-by-side treatment the FSTI row always had. FSTI keeps writing to the historical
        /// CardCollectionPreview.unity so existing references and muscle memory survive.
        /// </summary>
        [MenuItem("Overlink/Scenes/Build All Faction Collections")]
        public static void BuildAllFactionCollections()
        {
            foreach (Faction faction in AllFactions)
            {
                BuildFactionCollection(faction);
            }
            Debug.Log("[Overlink] Built a 4-tier collection row for all three factions.");
        }

        public static void BuildFactionCollection(Faction faction)
        {
            CardPresentationEditor.CreateAllTierPreviewFixtures(faction);
            string[] paths = new string[AllRarities.Length];
            for (int i = 0; i < AllRarities.Length; i++)
            {
                paths[i] = CardPresentationEditor.FixturePathFor(AllRarities[i], faction);
            }
            string scenePath = faction == Faction.Fsti
                ? CollectionScenePath
                : "Assets/Scenes/CardCollectionPreview_" + faction.ToString().ToUpperInvariant() + ".unity";
            Build(scenePath, paths[0], false, paths);
            Debug.Log("[Overlink] " + faction + " collection preview built at " + scenePath);
        }

        /// <summary>
        /// The full frame matrix: one scene per card type, each holding 3 factions x 4 tiers. All 48
        /// frames will not fit one viewport legibly - 12 cards at readable size is already the limit -
        /// so the type axis becomes a scene axis instead of a fourth and fifth row of postage stamps.
        /// Rows are factions (top to bottom: FSTI, FRTI, FPB), columns are tiers (left to right:
        /// Common, Rare, Epic, Legendary), which is the same column order as the per-faction
        /// collection rows, so a tier reads at the same x in either scene.
        /// </summary>
        [MenuItem("Overlink/Scenes/Build All Frame Matrix Previews")]
        public static void BuildAllFrameMatricesFromMenu()
        {
            foreach (CardType type in AllTypes)
            {
                BuildFrameMatrix(type);
            }
            Debug.Log("[Overlink] Built all four frame-matrix previews (3 factions x 4 tiers each).");
        }

        [MenuItem("Overlink/Scenes/Build Faction Matrix Preview")]
        public static void BuildFactionMatrixFromMenu()
        {
            BuildFrameMatrix(CardType.Unit);
        }

        public static void BuildFrameMatrix(CardType type)
        {
            CardPresentationEditor.CreateFullFrameMatrixFixtures();
            // Build() takes an array of fixture paths, one card per entry, in row-major order.
            string[] paths = new string[AllFactions.Length * AllRarities.Length];
            int n = 0;
            foreach (Faction faction in AllFactions)
            {
                foreach (Rarity rarity in AllRarities)
                {
                    paths[n++] = CardPresentationEditor.FixturePathFor(rarity, faction, type);
                }
            }
            // Card 0.66 wide, cells 0.80 x 1.22 -> a 3.20 x 3.66 grid plus caption room.
            // The vertical budget is spent, not guessed: labels hang downward from their top edge and
            // are sized by line count, so at ortho 2.30 a two-line header occupies 0.414 and its foot
            // lands at 1.783; the subtitle starts below that and ends at 1.615; the top row's crown is
            // at 1.532; the last row's two-line caption ends at -2.132, inside the -2.30 edge. Ortho
            // 2.05 could not hold all four, which is why the bottom captions used to run off frame.
            Build(MatrixScenePathFor(type), paths[0], false, paths,
                new PreviewFraming
                {
                    orthoSize = 2.30f,
                    cardWidth = 0.66f,
                    cellWidth = 0.80f,
                    cellHeight = 1.22f,
                    topY = 1.05f,
                    columns = AllRarities.Length,
                    headerLift = 0.955f,
                    subtitleLift = 0.76f
                });
            Debug.Log("[Overlink] " + type + " frame matrix preview built at " + MatrixScenePathFor(type));
        }

        static string MatrixScenePathFor(CardType type)
        {
            // Uniform naming: no matrix scene predates this, so there is no legacy path to preserve
            // and a fourth type does not need a special case.
            return "Assets/Scenes/CardFrameMatrixPreview_" + type + ".unity";
        }

        public static void BatchBuild()
        {
            Build(ScenePath, FixturePath, false);
            Debug.Log("[Overlink] Card presentation preview scene built at " + ScenePath);
        }

        /// <summary>Framing overrides for a gallery scene. Defaults reproduce the original 1.35 ortho,
        /// full-size four-tier row, so every existing caller is unaffected by leaving them null.</summary>
        sealed class PreviewFraming
        {
            public float orthoSize = 1.35f;
            public float cardWidth = 1f;
            public float cellWidth = 1.16f;
            public float cellHeight = 1.7f;
            public float topY = -0.12f;
            public float headerLift = 0.88f;
            // Top edge of the subtitle, which must clear the header's two lines: at 1.35 ortho those
            // lines occupy 0.243, putting the header's foot at 0.945 and ruling out 0.72.
            public float subtitleLift = 0.68f;
            // 0 means "one row of everything", which is the per-faction collection behaviour the
            // gallery was written for. The matrix sets this to the tier count so the grid wraps into
            // one row per faction.
            public int columns;
        }

        static void Build(string scenePath, string fixturePath, bool legendary,
            string[] galleryFixturePaths = null, PreviewFraming framing = null)
        {
            framing = framing ?? new PreviewFraming();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(framing);
            CreateLighting();
            CreatePreviewRoot(fixturePath, legendary, galleryFixturePaths, framing);
            CreateHeader();

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void CreateCamera(PreviewFraming framing)
        {
            GameObject go = new GameObject("Preview Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -12f);
            go.transform.rotation = Quaternion.identity;

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            // Resolution is a framing problem as much as an import problem. The old ortho 3.1 drew
            // a 1216px frame texture into ~190 screen pixels. 1.35 fills ~585px per card at 1080p
            // and the five-card fan still fits horizontally (4.28 wide vs 4.80 visible).
            camera.orthographicSize = framing.orthoSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f, 1f);
            go.AddComponent<AudioListener>();
            EnsureUniversalCameraData(camera);
        }

        static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Preview Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
            lightObject.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
        }

        static void CreatePreviewRoot(string fixturePath, bool legendary, string[] galleryFixturePaths,
            PreviewFraming framing)
        {
            GameObject root = new GameObject("Card Presentation Preview");
            CardPresentationPreviewScene preview = root.AddComponent<CardPresentationPreviewScene>();

            DeckController deck = root.AddComponent<DeckController>();
            HandController hand = root.AddComponent<HandController>();

            bool gallery = galleryFixturePaths != null && galleryFixturePaths.Length > 0;

            // Anchors sit inside the 1.35 ortho view (visible x +/-2.40, y +/-1.35 at 16:9) so
            // draw/play/discard animations stay on screen instead of flying in from off-view.
            Transform deckPile = CreateAnchor(root.transform, "DeckPile", new Vector3(-2.05f, -0.95f, 0f));
            Transform playSlot = CreateAnchor(root.transform, "PlaySlot", new Vector3(0f, 0.55f, 0f));
            Transform discardPile = CreateAnchor(root.transform, "DiscardPile", new Vector3(2.05f, -0.95f, 0f));

            CardFrameCatalog frameCatalog = AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(
                PresentationRoot + "/CardFrameCatalog.asset");
            CardTemplateCatalog templateCatalog = AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(
                PresentationRoot + "/CardTemplateCatalog.asset");
            CardPresentationFixture fixture = AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(fixturePath);
            CardTierMaterialSet tierMaterials = AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(
                PresentationRoot + "/CardTierMaterialSet.asset");

            SerializedObject handSerialized = new SerializedObject(hand);
            handSerialized.FindProperty("deck").objectReferenceValue = deck;
            handSerialized.FindProperty("deckPile").objectReferenceValue = deckPile;
            handSerialized.FindProperty("playSlot").objectReferenceValue = playSlot;
            handSerialized.FindProperty("discardPile").objectReferenceValue = discardPile;
            handSerialized.FindProperty("frameCatalog").objectReferenceValue = frameCatalog;
            handSerialized.FindProperty("templateCatalog").objectReferenceValue = templateCatalog;
            handSerialized.FindProperty("placeholderFixture").objectReferenceValue = fixture;
            handSerialized.FindProperty("tierMaterials").objectReferenceValue = tierMaterials;
            handSerialized.FindProperty("autoDraw").boolValue = !gallery;

            if (gallery)
            {
                // Gallery: one fixture per tier, laid out left to right with a clear gap, so no card
                // covers another and each tier's shader is judged on its own. The faction matrix uses
                // the same path with four per row and one row per faction.
                handSerialized.FindProperty("layoutMode").enumValueIndex =
                    (int)HandController.HandLayoutMode.Gallery;
                var fixtures = handSerialized.FindProperty("galleryFixtures");
                fixtures.arraySize = galleryFixturePaths.Length;
                for (int i = 0; i < galleryFixturePaths.Length; i++)
                {
                    fixtures.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(galleryFixturePaths[i]);
                }
                // Columns is capped at the tier count: a four-fixture gallery is one row, and the
                // twelve-fixture matrix is three rows of four rather than one absurd row.
                handSerialized.FindProperty("galleryColumns").intValue =
                    framing.columns > 0 ? framing.columns : galleryFixturePaths.Length;
                handSerialized.FindProperty("galleryCellWidth").floatValue = framing.cellWidth;
                handSerialized.FindProperty("galleryCellHeight").floatValue = framing.cellHeight;
                handSerialized.FindProperty("galleryTopY").floatValue = framing.topY;
                handSerialized.FindProperty("cardSize").vector3Value =
                    new Vector3(framing.cardWidth, framing.cardWidth * 1.4615f, 0.06f);
                handSerialized.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject previewSerialized = new SerializedObject(preview);
                previewSerialized.FindProperty("hand").objectReferenceValue = hand;
                previewSerialized.FindProperty("legendaryPreview").boolValue = false;
                previewSerialized.FindProperty("galleryPreview").boolValue = true;
                previewSerialized.FindProperty("headerLift").floatValue = framing.headerLift;
                previewSerialized.FindProperty("subtitleLift").floatValue = framing.subtitleLift;
                previewSerialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            handSerialized.FindProperty("handSizeStart").intValue = 5;
            // Every supplied frame is authored on an 832x1216 portrait canvas, so both tiers use the
            // same root size; CardFaceView swaps the inner art window per tier.
            handSerialized.FindProperty("cardSize").vector3Value = new Vector3(1f, 1.4615f, 0.06f);
            // Cards are 1.4615 tall, so at ortho 1.35 (visible y +/-1.35) the hand centres at
            // -0.45 to keep the whole fan on screen: cards span y -1.18..0.28 plus the arc lift.
            handSerialized.FindProperty("handY").floatValue = -0.45f;
            // Keep five portrait cards readable in the preview without the old wide-card overlap.
            handSerialized.FindProperty("fanSpacing").floatValue = 0.82f;
            handSerialized.FindProperty("arcHeight").floatValue = 0.12f;
            handSerialized.FindProperty("maxYaw").floatValue = legendary ? 7f : 6f;
            handSerialized.FindProperty("playLineY").floatValue = 0.55f;
            handSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject previewRoot = new SerializedObject(preview);
            previewRoot.FindProperty("hand").objectReferenceValue = hand;
            previewRoot.FindProperty("legendaryPreview").boolValue = legendary;
            previewRoot.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateHeader()
        {
            // The root owns the runtime labels; this method exists as a named seam for future preview controls.
        }

        static Transform CreateAnchor(Transform parent, string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        static void EnsureUniversalCameraData(Camera camera)
        {
            System.Type type = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type != null && camera.GetComponent(type) == null)
            {
                camera.gameObject.AddComponent(type);
            }
        }
    }
}
