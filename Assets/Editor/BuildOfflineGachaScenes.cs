using System.Collections.Generic;
using System.IO;
using Overlink.Cards;
using Overlink.Core;
using Overlink.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Authors the offline Gacha prototype screens directly into their scenes, matching the
    /// <c>BuildCardPresentationPreviewScene</c> pattern: the widget tree lives in the saved
    /// <c>.unity</c> file, and the runtime controllers only bind data and listeners.
    ///
    /// The prototype is two scenes now: the Signal Bay shop (<c>Gacha.unity</c>) is hand-authored -
    /// its Figma bay plates are not generated here - while the reveal screen (<c>RevealGacha.unity</c>)
    /// and the binder (<c>Collection.unity</c>) are authored by this builder. The bay crosses into the
    /// reveal through <c>OfflineSignalBayController</c>; see the convert menu for the one-time surgery
    /// that stripped the reveal out of the shop scene.
    ///
    /// Rebuilding is destructive by design — it replaces the scene with a freshly authored tree and
    /// re-wires every serialized controller reference, so hand edits to the authored scenes should be
    /// folded back into this builder instead of the scene file.
    /// </summary>
    public static class BuildOfflineGachaScenes
    {
        const string GachaScenePath = "Assets/Scenes/Gacha.unity";
        const string RevealGachaScenePath = "Assets/Scenes/RevealGacha.unity";
        const string CollectionScenePath = "Assets/Scenes/Collection.unity";

        // Presentation catalog + fixture roots, shared with the card-manager / preview builders.
        const string PresentationRoot = "Assets/Cards/Presentation";
        const string CardBackMaterialPath = "Assets/Cards/Presentation/GachaCardBack.mat";
        const string CardBackTrimMaterialPath = "Assets/Cards/Presentation/GachaCardBackTrim.mat";

        // World-space framing for the pack row. Ortho + z=-12 matches the presentation-preview
        // convention; the spacing/width keep five portrait cards inside a 16:9 view with margin.
        const float CardOrthoSize = 1.95f;
        const float CardWidth = 1.05f;
        const float CardAspect = 1.4615f;
        const float SlotSpacing = 1.30f;
        const float CardRowY = 0.10f;
        static readonly Rarity[] AllRarities = { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

        // Palette mirrors the internal UiKit constants; the builder cannot reach them across assemblies,
        // so the values are duplicated here and must be changed in both places if the kit's look shifts.
        static readonly Color Background = new Color(0.039f, 0.055f, 0.102f); // #0A0E1A
        static readonly Color Panel = new Color(0.106f, 0.149f, 0.204f, 0.96f); // #1B2634
        static readonly Color DetailPanel = new Color(0.106f, 0.149f, 0.204f, 1f); // #1B2634
        static readonly Color CardBack = new Color(0.039f, 0.055f, 0.102f, 1f); // #0A0E1A
        static readonly Color TileOwned = new Color(0.106f, 0.149f, 0.204f, 1f); // #1B2634
        static readonly Color Primary = new Color(0.161f, 0.875f, 0.949f); // #29DFF2
        static readonly Color Ghost = new Color(1f, 1f, 1f, 0.06f);
        static readonly Color Accent = Primary;
        static readonly Color TextBright = new Color(0.871f, 0.910f, 0.933f); // #DDE8ED
        static readonly Color TextDim = new Color(0.510f, 0.588f, 0.663f); // #8296A9

        [MenuItem("Overlink/Scenes/Build Reveal Gacha Scene")]
        public static void BuildRevealGachaScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The pack row is world-space and renders through the real card pipeline (URP), so the
            // scene needs an ortho card camera plus a directional light; the overlay HUD canvas then
            // sits on top of both. Ortho at z=-12 matches the presentation-preview convention.
            CreateWorldCardCamera("Gacha Card Camera", CardOrthoSize);
            CreateDirectionalLight("Gacha Light");
            CreateEventSystem();

            // Card backs are authored world quads (no card-back sprite exists in the project), so two
            // persistent URP-safe materials are ensured once and reused across every rebuild. A quad's
            // mesh is a built-in asset and its material is a saved asset, so both survive scene save
            // (a runtime-generated sprite would not).
            Material backMaterial = EnsureSolidMaterial(CardBackMaterialPath, CardBack);
            Material trimMaterial = EnsureSolidMaterial(CardBackTrimMaterialPath, Accent);

            // World-space row the card camera sees. Slots hang off this root so the hover/flip
            // animation moves them in world units; the controller binds each slot's parts.
            GameObject rowRoot = new GameObject("Pack Card Row");
            OfflineCollectionData.PackCard[] manifest = OfflineCollectionData.PackManifest;
            var slotAnchors = new List<Transform>();
            var slotPivots = new List<Transform>();
            var slotBacks = new List<GameObject>();
            var slotFaceHolders = new List<Transform>();
            var slotResults = new List<TextMeshPro>();
            for (int i = 0; i < manifest.Length; i++)
            {
                float x = (i - (manifest.Length - 1) * 0.5f) * SlotSpacing;
                CreateWorldSlot(rowRoot.transform, i, x, backMaterial, trimMaterial,
                    out Transform slotAnchor, out Transform slotPivot, out GameObject slotBack,
                    out Transform slotFaceHolder, out TextMeshPro slotResult);
                slotAnchors.Add(slotAnchor);
                slotPivots.Add(slotPivot);
                slotBacks.Add(slotBack);
                slotFaceHolders.Add(slotFaceHolder);
                slotResults.Add(slotResult);
            }

            Canvas canvas = CreateCanvas("Offline Pack Canvas");

            // The full-screen PackScreen owns the swipe gesture and the controller. It is transparent
            // so the world card row shows through (the camera clear colour provides the backdrop), but
            // it stays a raycast target so the swipe-up gesture still has a surface to land on.
            GameObject screen = CreatePanel("RevealScreen", canvas.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            OfflinePackOpeningController controller = screen.AddComponent<OfflinePackOpeningController>();

            CreatePanel("Accent", screen.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -4f), new Vector2(0f, 8f), Accent);
            CreateText("Eyebrow", screen.transform, "OVERLINK // COLLECTION LAB", 18f, TextDim,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -42f), new Vector2(540f, 30f));
            CreateText("Title", screen.transform, "SIGNAL PACK // FIRST LINK", 38f, TextBright,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -76f), new Vector2(850f, 55f));
            // The header carries no explanatory sentence — one live accent in the bay's own vocabulary
            // (SEAL INTACT → SEAL BROKEN → LINK COMPLETE) states the pack's condition instead.
            TextMeshProUGUI headerAccent = CreateText("Header Accent", screen.transform, "SEAL INTACT", 17f, Accent,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -128f), new Vector2(920f, 32f));

            TextMeshProUGUI progressText = CreateText("Progress", screen.transform, string.Empty, 20f, Accent,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-560f, -54f), new Vector2(500f, 34f));
            TextMeshProUGUI collectionText = CreateText("Collection", screen.transform, string.Empty, 16f, TextDim,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-560f, -92f), new Vector2(500f, 30f));
            // Rolling readouts ride the same nodes; the controller resolves them as components in its
            // Awake, so the serialized widget contract the verification checks stays unchanged.
            progressText.gameObject.AddComponent<UiCounter>();
            collectionText.gameObject.AddComponent<UiCounter>();

            // Pack mode is chosen in the Signal Bay shop and handed over through ModeKey; this screen
            // only states which resolution is running. The ten-pack is ten real standard packs
            // (fifty cards), never one compressed roll.
            TextMeshProUGUI packHeader = CreateText("Pack Header", screen.transform, string.Empty, 20f, TextBright,
                TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(900f, 30f));

            // Pack queue strip: one capsule per pack in the bundle (plus loyalty bonus packs), cloned at
            // runtime from this authored template so sealed/open/done state stays visible while revealing.
            GameObject queue = CreatePanel("Pack Queue", screen.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -226f), new Vector2(1180f, 64f), Panel);
            HorizontalLayoutGroup queueLayout = queue.AddComponent<HorizontalLayoutGroup>();
            queueLayout.spacing = 10f;
            queueLayout.childAlignment = TextAnchor.MiddleCenter;
            queueLayout.childControlWidth = false;
            queueLayout.childControlHeight = false;
            GameObject capsuleTemplate = CreatePanel("Capsule Template", queue.transform, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(96f, 56f), new Color(0.05f, 0.07f, 0.11f, 1f));
            CreateText("Number", capsuleTemplate.transform, "00", 18f, Accent, TextAlignmentOptions.Center,
                new Vector2(0f, 0.42f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            CreateText("State", capsuleTemplate.transform, "SEALED", 10f, TextDim, TextAlignmentOptions.Center,
                new Vector2(0f, 0.08f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
            capsuleTemplate.SetActive(false);

            // The world card row is authored above (world-space, not UGUI). Status/instructions sit
            // below it in the overlay HUD; status is pulled further down so it never collides with the
            // per-slot world-space result lines that hang under each card.
            TextMeshProUGUI statusText = CreateText("Status", screen.transform, string.Empty, 18f, TextBright,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(1500f, 34f));
            TextMeshProUGUI instructionText = CreateText("Instructions", screen.transform, "SWIPE UP  •  SPACE / ENTER  •  GAMEPAD SUBMIT", 15f, TextDim,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 94f), new Vector2(900f, 28f));

            // The bottom row alternates between two mutually exclusive pairs on the same centred
            // footprint: while cards remain, REVEAL / REVEAL ALL own the row (there is no mid-reveal
            // exit — leaving is only offered once the pack is resolved), and once everything is
            // revealed the controller swaps in SIGNAL BAY / OPEN AGAIN. Reduced motion moved to the
            // Settings screen; RESET DEMO lives in the editor tools only.
            Button revealNext = CreateButton("Reveal Next", screen.transform, "REVEAL", true,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 38f), new Vector2(280f, 52f));
            Button revealRemaining = CreateButton("Reveal Remaining", screen.transform, "REVEAL ALL", false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 38f), new Vector2(280f, 52f));
            Button back = CreateButton("Back", screen.transform, "SIGNAL BAY", false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 38f), new Vector2(280f, 52f));
            Button openAgain = CreateButton("Open Again", screen.transform, "OPEN AGAIN", true,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 38f), new Vector2(280f, 52f));
            back.gameObject.SetActive(false);
            openAgain.gameObject.SetActive(false);

            // Final summary (§6.3): every card grouped under its pack boundary, plus the guarantee and
            // loyalty audit lines. Authored inactive; the controller auto-opens it on completion in both
            // modes — a single pack included — since the standalone SUMMARY button is gone.
            GameObject summaryPanel = CreatePanel("Bundle Summary", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1240f, 860f), DetailPanel);
            CreateText("Summary Title", summaryPanel.transform, "RESOLUTION SUMMARY  •  CARD AUDIT", 30f, TextBright,
                TextAlignmentOptions.TopLeft, new Vector2(0.06f, 0.90f), new Vector2(0.80f, 0.98f), Vector2.zero, Vector2.zero);
            Button summaryClose = CreateButton("Summary Close", summaryPanel.transform, "CLOSE", false,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -40f), new Vector2(110f, 40f));
            TextMeshProUGUI summaryGuarantee = CreateText("Summary Guarantee", summaryPanel.transform, string.Empty, 17f, Accent,
                TextAlignmentOptions.Left, new Vector2(0.06f, 0f), new Vector2(0.94f, 0f), new Vector2(0f, 88f), new Vector2(1140f, 30f));
            TextMeshProUGUI summaryLoyalty = CreateText("Summary Loyalty", summaryPanel.transform, string.Empty, 17f, TextDim,
                TextAlignmentOptions.Left, new Vector2(0.06f, 0f), new Vector2(0.94f, 0f), new Vector2(0f, 54f), new Vector2(1140f, 30f));

            GameObject viewport = CreateStretchPanel("Summary Viewport", summaryPanel.transform, Vector2.zero, Vector2.one,
                new Vector2(40f, 120f), new Vector2(-40f, -90f), new Color(0.04f, 0.05f, 0.08f, 1f));
            viewport.AddComponent<Mask>().showMaskGraphic = true;
            GameObject content = new GameObject("Summary Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 2000f);
            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 2f;
            contentLayout.padding = new RectOffset(24, 24, 14, 14);
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 42f;

            GameObject summaryLine = CreateText("Summary Line Template", content.transform, string.Empty, 16f, TextBright,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 28f)).gameObject;
            summaryLine.SetActive(false);
            summaryPanel.AddComponent<UiFxPanel>(); // wake beat when the audit opens
            summaryPanel.SetActive(false);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("progressText").objectReferenceValue = progressText;
            so.FindProperty("collectionText").objectReferenceValue = collectionText;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("instructionText").objectReferenceValue = instructionText;
            so.FindProperty("headerAccent").objectReferenceValue = headerAccent;
            so.FindProperty("revealNextButton").objectReferenceValue = revealNext;
            so.FindProperty("revealRemainingButton").objectReferenceValue = revealRemaining;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("openAgainButton").objectReferenceValue = openAgain;

            // Milestone 3 wiring: queue strip and the grouped resolution summary.
            so.FindProperty("packHeader").objectReferenceValue = packHeader;
            so.FindProperty("queueRoot").objectReferenceValue = queue.transform;
            so.FindProperty("capsuleTemplate").objectReferenceValue = capsuleTemplate;
            so.FindProperty("summaryPanel").objectReferenceValue = summaryPanel;
            so.FindProperty("summaryCloseButton").objectReferenceValue = summaryClose;
            so.FindProperty("summaryContent").objectReferenceValue = content.transform;
            so.FindProperty("summaryLineTemplate").objectReferenceValue = summaryLine;
            so.FindProperty("summaryGuaranteeText").objectReferenceValue = summaryGuarantee;
            so.FindProperty("summaryLoyaltyText").objectReferenceValue = summaryLoyalty;

            // Presentation references: the real catalogs and the per-rarity fixtures that supply art.
            so.FindProperty("templateCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            so.FindProperty("frameCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(PresentationRoot + "/CardFrameCatalog.asset");
            so.FindProperty("tierMaterials").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(PresentationRoot + "/CardTierMaterialSet.asset");
            so.FindProperty("cardWidth").floatValue = CardWidth;

            // One fixture per rarity, indexed by (int)rarity to match the controller's lookup. These
            // only supply placeholder art / ability copy; the manifest drives every real field.
            SerializedProperty fixturesProp = so.FindProperty("fixturesByRarity");
            fixturesProp.arraySize = AllRarities.Length;
            for (int i = 0; i < AllRarities.Length; i++)
            {
                fixturesProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(CardPresentationEditor.FixturePathFor(AllRarities[i]));
            }

            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = manifest.Length;
            for (int i = 0; i < manifest.Length; i++)
            {
                SerializedProperty element = slotsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("anchor").objectReferenceValue = slotAnchors[i];
                element.FindPropertyRelative("pivot").objectReferenceValue = slotPivots[i];
                element.FindPropertyRelative("back").objectReferenceValue = slotBacks[i];
                element.FindPropertyRelative("faceHolder").objectReferenceValue = slotFaceHolders[i];
                element.FindPropertyRelative("result").objectReferenceValue = slotResults[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            SaveScene(scene, RevealGachaScenePath);
            Debug.Log("[Overlink] Reveal Gacha scene authored at " + RevealGachaScenePath);
        }

        [MenuItem("Overlink/Scenes/Build Collection Binder Scene")]
        public static void BuildCollectionScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera collectionCardCamera = CreateCollectionCardCamera("Main Camera");
            CreateEventSystem();
            Canvas canvas = CreateCanvas("Offline Collection Canvas");

            GameObject collectionCardRoot = new GameObject("Collection Card Faces");

            GameObject screen = CreatePanel("CollectionScreen", canvas.transform,
                // World-space CardFaceView prefabs are rendered by Main Camera. Keep the screen shell
                // transparent just like Gacha; an opaque overlay would hide every SpriteRenderer before
                // the UI layer had a chance to display the binder.
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            OfflineCollectionController controller = screen.AddComponent<OfflineCollectionController>();

            CreatePanel("Accent", screen.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -4f), new Vector2(0f, 8f), Accent);
            CreateText("Eyebrow", screen.transform, "OVERLINK  //  SIGNAL ARCHIVE", 18f, TextDim,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -42f), new Vector2(700f, 30f));
            CreateText("Title", screen.transform, "CARD BINDER", 38f, TextBright,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -76f), new Vector2(900f, 55f));
            CreateText("Subtitle", screen.transform, "TACTICAL NETWORK  //  CARD INDEX ONLINE", 17f, TextDim,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -128f), new Vector2(1100f, 32f));

            TextMeshProUGUI countText = CreateText("Count", screen.transform, string.Empty, 20f, Accent,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -52f), new Vector2(520f, 34f));
            TextMeshProUGUI fragmentText = CreateText("Fragments", screen.transform, string.Empty, 16f, TextDim,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -90f), new Vector2(520f, 30f));
            TextMeshProUGUI deckText = CreateText("Deck", screen.transform, string.Empty, 16f, Accent,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -124f), new Vector2(520f, 30f));

            GameObject filters = CreatePanel("Filters", screen.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -176f), new Vector2(-108f, 58f), new Color(0.106f, 0.149f, 0.204f, 0.72f));
            Button typeFilter = CreateButton("Type Filter", filters.transform, "TYPE  •  ANY", false,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(200f, 40f));
            Button factionFilter = CreateButton("Faction Filter", filters.transform, "FACTION  •  ANY", false,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(284f, 0f), new Vector2(200f, 40f));
            Button rarityFilter = CreateButton("Rarity Filter", filters.transform, "RARITY  •  ANY", false,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(504f, 0f), new Vector2(200f, 40f));
            Button setFilter = CreateButton("Set Filter", filters.transform, "SET  •  ANY", false,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(724f, 0f), new Vector2(200f, 40f));
            Button ownershipFilter = CreateButton("Ownership Filter", filters.transform, "OWNERSHIP  •  ALL", false,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(944f, 0f), new Vector2(200f, 40f));
            Button sort = CreateButton("Sort", filters.transform, "SORT  •  CATALOG", false,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1164f, 0f), new Vector2(200f, 40f));

            // Stretch-with-offsets: a sizeDelta on mixed anchors would centre the panel on the anchor
            // rect and push its top edge over the header, so the insets are expressed as offsets.
            // The viewport is only a clipped window. The content owns the grid, so portrait prefab
            // faces flow into three columns and remain scroll-safe at smaller aspect ratios.
            GameObject grid = CreateStretchPanel("BinderGrid", screen.transform, new Vector2(0f, 0f), new Vector2(0.68f, 1f),
                new Vector2(48f, 78f), new Vector2(-72f, -294f), new Color(0f, 0f, 0f, 0f));
            Mask gridMask = grid.AddComponent<Mask>();
            gridMask.showMaskGraphic = false;
            GameObject gridContent = new GameObject("BinderContent", typeof(RectTransform));
            gridContent.transform.SetParent(grid.transform, false);
            RectTransform gridContentRect = gridContent.GetComponent<RectTransform>();
            gridContentRect.anchorMin = new Vector2(0f, 1f);
            gridContentRect.anchorMax = new Vector2(1f, 1f);
            gridContentRect.pivot = new Vector2(0.5f, 1f);
            gridContentRect.anchoredPosition = Vector2.zero;
            gridContentRect.sizeDelta = new Vector2(0f, 1000f);
            GridLayoutGroup gridLayout = gridContent.AddComponent<GridLayoutGroup>();
            gridLayout.padding = new RectOffset(28, 28, 24, 24);
            gridLayout.spacing = new Vector2(18f, 18f);
            // Three columns keep the six authored cards compact at the reference resolution. The
            // runtime face sizing also clamps to this cell height, preserving a clean second row.
            gridLayout.cellSize = new Vector2(325f, 320f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            ScrollRect gridScroll = grid.AddComponent<ScrollRect>();
            gridScroll.viewport = grid.GetComponent<RectTransform>();
            gridScroll.content = gridContentRect;
            gridScroll.horizontal = false;
            gridScroll.vertical = true;
            gridScroll.movementType = ScrollRect.MovementType.Elastic;
            gridScroll.scrollSensitivity = 48f;

            TextMeshProUGUI empty = CreateText("Empty", grid.transform, "NO CARDS MATCH THE CURRENT FILTERS", 18f, TextDim,
                TextAlignmentOptions.Center, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.58f), Vector2.zero, Vector2.zero);
            LayoutElement emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
            emptyLayout.ignoreLayout = true;

            GameObject tileTemplate = CreateTileTemplate(gridContent.transform);

            GameObject divider = CreatePanel("Inspector Divider", screen.transform, new Vector2(0.69f, 0f), new Vector2(0.69f, 1f),
                new Vector2(0f, 78f), new Vector2(1f, -372f), new Color(0.149f, 0.227f, 0.290f, 0.9f));
            divider.GetComponent<Image>().raycastTarget = false;

            GameObject detail = CreateStretchPanel("CardDetail", screen.transform, new Vector2(0.70f, 0f), new Vector2(1f, 1f),
                new Vector2(-48f, 78f), new Vector2(-72f, -294f), DetailPanel);
            CreateText("DetailEyebrow", detail.transform, "FOCUSED CARD  //  INSPECTOR", 14f, TextDim, TextAlignmentOptions.TopLeft,
                new Vector2(0.08f, 0.91f), new Vector2(0.92f, 0.98f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI detailTitle = CreateText("DetailTitle", detail.transform, "SELECT A CARD", 30f, TextBright, TextAlignmentOptions.TopLeft,
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.9f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI detailOwnership = CreateText("DetailOwnership", detail.transform, string.Empty, 17f, Accent, TextAlignmentOptions.TopLeft,
                new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.76f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI detailBody = CreateText("DetailBody", detail.transform,
                "Choose a card sleeve to inspect its tactical profile, rules, and acquisition state.", 17f, TextDim,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.64f), Vector2.zero, Vector2.zero);

            // Deck-builder seam: the label states the exact reason (not owned / copy limit / deck full)
            // when the selected card cannot be added, so the disabled state stays explainable.
            Button addToDeck = CreateButton("AddToDeck", detail.transform, "ADD TO DECK  •  SELECT A CARD", false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(340f, 48f));

            Button openPack = CreateButton("OpenPack", screen.transform, "SIGNAL BAY", true,
                new Vector2(0.70f, 0f), new Vector2(0.70f, 0f), new Vector2(90f, 38f), new Vector2(190f, 46f));
            Button back = CreateButton("Back", screen.transform, "BACK  //  MAIN MENU", false,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(54f, 38f), new Vector2(170f, 46f));

            // Collection keeps its production guard; the AuthFlow preview bypass list covers editor testing.
            GameObject guard = new GameObject("Auth Guard");
            guard.AddComponent<ProtectedSceneGuard>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("countText").objectReferenceValue = countText;
            so.FindProperty("fragmentText").objectReferenceValue = fragmentText;
            so.FindProperty("typeFilterButton").objectReferenceValue = typeFilter;
            so.FindProperty("factionFilterButton").objectReferenceValue = factionFilter;
            so.FindProperty("rarityFilterButton").objectReferenceValue = rarityFilter;
            so.FindProperty("setFilterButton").objectReferenceValue = setFilter;
            so.FindProperty("ownershipFilterButton").objectReferenceValue = ownershipFilter;
            so.FindProperty("sortButton").objectReferenceValue = sort;
            so.FindProperty("gridRoot").objectReferenceValue = gridContent.transform;
            so.FindProperty("tileTemplate").objectReferenceValue = tileTemplate;
            so.FindProperty("emptyText").objectReferenceValue = empty;
            so.FindProperty("worldCardRoot").objectReferenceValue = collectionCardRoot.transform;
            so.FindProperty("worldCardCamera").objectReferenceValue = collectionCardCamera;
            so.FindProperty("templateCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            so.FindProperty("frameCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(PresentationRoot + "/CardFrameCatalog.asset");
            so.FindProperty("tierMaterials").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(PresentationRoot + "/CardTierMaterialSet.asset");
            SerializedProperty collectionFixtures = so.FindProperty("fixturesByRarity");
            collectionFixtures.arraySize = AllRarities.Length;
            for (int i = 0; i < AllRarities.Length; i++)
            {
                collectionFixtures.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(CardPresentationEditor.FixturePathFor(AllRarities[i]));
            }
            so.FindProperty("detailTitle").objectReferenceValue = detailTitle;
            so.FindProperty("detailOwnership").objectReferenceValue = detailOwnership;
            so.FindProperty("detailBody").objectReferenceValue = detailBody;
            so.FindProperty("openPackButton").objectReferenceValue = openPack;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("deckText").objectReferenceValue = deckText;
            so.FindProperty("addToDeckButton").objectReferenceValue = addToDeck;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            SaveScene(scene, CollectionScenePath);
            Debug.Log("[Overlink] Collection binder scene authored at " + CollectionScenePath);
        }

        /// <summary>
        /// One-time surgery for the scene split: strips the reveal half (the world card row and the
        /// reveal UI tree) out of the hand-authored shop scene, replaces the pack controller with the
        /// bay controller, and wires the bay's three buttons. Idempotent - a second run finds nothing
        /// left to strip and only re-asserts the bay wiring, so it is safe to re-run after a bad save.
        /// The shop keeps the card camera, the light and the EventSystem: the camera's solid clear
        /// colour is the bay's backdrop, and the other two cost nothing.
        /// </summary>
        [MenuItem("Overlink/Scenes/Convert Gacha To Signal Bay Shop")]
        public static void ConvertGachaSceneToShop()
        {
            Scene scene = EditorSceneManager.GetSceneByPath(GachaScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(GachaScenePath, OpenSceneMode.Single);
            }

            int stripped = 0;
            foreach (string name in new[] { "Reveal UI", "Pack Card Row" })
            {
                // "Reveal UI" hangs off the screen node, not the scene root, so the lookup walks the
                // whole tree; "Pack Card Row" is a world-space root.
                GameObject node = FindInScene(scene, name);
                if (node == null) continue;
                UnityEngine.Object.DestroyImmediate(node);
                stripped++;
            }

            OfflinePackOpeningController legacy = UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>();
            GameObject host = legacy != null ? legacy.gameObject : null;
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            if (host == null)
            {
                GameObject canvas = FindInScene(scene, "Offline Pack Canvas");
                Transform screen = canvas != null ? canvas.transform.Find("RevealScreen") ?? canvas.transform.Find("PackScreen") : null;
                if (screen == null)
                {
                    Debug.LogError("[Overlink] " + GachaScenePath + " has no screen node to host the bay controller.");
                    return;
                }
                host = screen.gameObject;
            }

            OfflineSignalBayController shop = host.GetComponent<OfflineSignalBayController>();
            if (shop == null) shop = host.AddComponent<OfflineSignalBayController>();
            Transform bay = host.transform.Find("Figma Signal Bay");
            SerializedObject so = new SerializedObject(shop);
            so.FindProperty("openOneButton").objectReferenceValue = BayButton(bay, "Figma Open One");
            so.FindProperty("openTenButton").objectReferenceValue = BayButton(bay, "Figma Open Ten");
            so.FindProperty("returnButton").objectReferenceValue = BayButton(bay, "Figma Return");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);

            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, GachaScenePath);
            Debug.Log("[Overlink] " + GachaScenePath + " is now the Signal Bay shop only: "
                + stripped + " reveal root(s) stripped, bay controller wired on " + host.name + ".");
        }

        /// <summary>Depth-first name lookup across every root of a scene. Authored nodes are not all
        /// roots - the reveal tree hangs off the screen node - so stripping by root name alone misses
        /// them.</summary>
        static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindChild(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        static GameObject FindChild(Transform node, string name)
        {
            if (node.name == name) return node.gameObject;
            foreach (Transform child in node)
            {
                GameObject found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static Button BayButton(Transform bay, string childName)
        {
            Transform child = bay != null ? bay.Find(childName) : null;
            return child != null ? child.GetComponent<Button>() : null;
        }

        /// <summary>
        /// Authors one world-space pack slot: an anchor (rest position, hover offset), a flip pivot, a
        /// face-down card back, a face holder the controller spawns the real <see cref="CardFaceView"/>
        /// template into, and a world-space result line hanging under the card. Returns each part so the
        /// builder can serialize them onto the controller.
        /// </summary>
        static void CreateWorldSlot(Transform parent, int index, float x, Material backMat, Material trimMat,
            out Transform anchor, out Transform pivot, out GameObject back, out Transform faceHolder,
            out TextMeshPro result)
        {
            GameObject anchorGo = new GameObject("Card " + (index + 1));
            anchorGo.transform.SetParent(parent, false);
            anchorGo.transform.localPosition = new Vector3(x, CardRowY, 0f);
            anchor = anchorGo.transform;

            GameObject pivotGo = new GameObject("Pivot");
            pivotGo.transform.SetParent(anchor, false);
            pivotGo.transform.localPosition = Vector3.zero;
            pivot = pivotGo.transform;

            // Card back: a trim border, a dark face, and an accent diamond, all co-planar quads offset in
            // z just enough to avoid z-fighting. Faces the camera at rest until the pivot flips it away.
            GameObject backGo = new GameObject("Back");
            backGo.transform.SetParent(pivot, false);
            backGo.transform.localPosition = Vector3.zero;
            float cardHeight = CardWidth * CardAspect;
            CreateBackQuad("Trim", backGo.transform, new Vector3(0f, 0f, 0.015f),
                new Vector3(CardWidth + 0.09f, cardHeight + 0.09f, 1f), 0f, trimMat);
            CreateBackQuad("Face", backGo.transform, Vector3.zero,
                new Vector3(CardWidth, cardHeight, 1f), 0f, backMat);
            CreateBackQuad("Emblem", backGo.transform, new Vector3(0f, 0.06f, -0.02f),
                new Vector3(CardWidth * 0.36f, CardWidth * 0.36f, 1f), 45f, trimMat);
            back = backGo;

            // The controller instantiates the real CardFaceView template here, pre-rotated 180 so the
            // face reads upright once the pivot completes its flip.
            GameObject faceHolderGo = new GameObject("FaceHolder");
            faceHolderGo.transform.SetParent(pivot, false);
            faceHolderGo.transform.localPosition = Vector3.zero;
            faceHolder = faceHolderGo.transform;

            // The result line is a child of the anchor (not the pivot) so it stays upright and never
            // flips; it is sorted high so a neighbour card cannot draw over it.
            result = CreateWorldText("Result", anchor, string.Empty, 26f, Accent,
                new Vector3(0f, -0.90f, 0f), 0.009f, new Vector2(95f, 80f), TextAlignmentOptions.Center);
            result.sortingOrder = 600;
        }

        /// <summary>A single world-space quad for the card back. Uses the built-in mesh and a saved
        /// material asset so it survives scene save (a runtime-generated sprite would not).</summary>
        static void CreateBackQuad(string name, Transform parent, Vector3 localPos, Vector3 scale,
            float rotZ, Material mat)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            quad.transform.localScale = scale;
            MeshRenderer meshRenderer = quad.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.sharedMaterial = mat;
            Collider quadCollider = quad.GetComponent<Collider>();
            if (quadCollider != null) UnityEngine.Object.DestroyImmediate(quadCollider);
        }

        /// <summary>World-space (3D) TextMeshPro. A large rect scaled down keeps the fontSize readable in
        /// world units, matching the project's <c>new GameObject + AddComponent&lt;TextMeshPro&gt;</c> pattern.</summary>
        static TextMeshPro CreateWorldText(string name, Transform parent, string value, float fontSize,
            Color color, Vector3 localPosition, float scale, Vector2 rectSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = value;
            tmp.fontSize = fontSize;
            tmp.color = color;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Rajdhani-SemiBold");
            if (font != null) tmp.font = font;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            tmp.richText = false;
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localRotation = Quaternion.identity;
            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = rectSize;
            return tmp;
        }

        /// <summary>Loads or creates a persistent solid-colour material asset. Prefers the URP Unlit
        /// shader (a built-in <c>Unlit/Color</c> renders magenta under URP) and falls back to the sprite
        /// shader the project already relies on.</summary>
        static Material EnsureSolidMaterial(string path, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Orthographic URP camera that renders the world-space pack row; the overlay HUD canvas
        /// draws on top of it. Mirrors the presentation-preview camera (z=-12, solid-colour clear).</summary>
        static void CreateWorldCardCamera(string name, float orthoSize)
        {
            GameObject go = new GameObject(name);
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -12f);
            go.transform.rotation = Quaternion.identity;
            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.allowMSAA = false;
            go.AddComponent<AudioListener>();
            EnsureUniversalCameraData(camera);
        }

        /// <summary>Directional light so lit card-tier materials render as they do in the presentation
        /// preview; harmless to the unlit sprite and card-back quads.</summary>
        static void CreateDirectionalLight(string name)
        {
            GameObject go = new GameObject(name);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
            go.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
        }

        /// <summary>Adds URP's additional camera data via reflection so the builder needs no hard
        /// assembly reference to the render pipeline (same approach as the preview builder).</summary>
        static void EnsureUniversalCameraData(Camera camera)
        {
            System.Type type = System.Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type != null && camera.GetComponent(type) == null)
            {
                camera.gameObject.AddComponent(type);
            }
        }

        /// <summary>
        /// The binder tile the controller clones per catalog card. Stays inactive in the saved scene;
        /// child names are the lookup contract used by <c>OfflineCollectionController.SetTileText</c>.
        /// </summary>
        static GameObject CreateTileTemplate(Transform parent)
        {
            GameObject tile = new GameObject("TileTemplate", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
            tile.transform.SetParent(parent, false);
            tile.GetComponent<Image>().color = TileOwned;
            Outline outline = tile.GetComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2f, -2f);
            Button button = tile.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = tile.GetComponent<Image>();

            CreateText("Silhouette", tile.transform, "?", 54f, new Color(0.28f, 0.34f, 0.43f), TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
            CreateText("Rarity", tile.transform, "COMMON", 13f, TextDim, TextAlignmentOptions.TopLeft,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            CreateText("Name", tile.transform, "UNKNOWN SIGNAL", 20f, TextBright, TextAlignmentOptions.BottomLeft,
                new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.30f), Vector2.zero, Vector2.zero);
            CreateText("Meta", tile.transform, "TYPE  •  FACTION  •  OWNED 0", 12f, TextDim, TextAlignmentOptions.BottomLeft,
                new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.14f), Vector2.zero, Vector2.zero);

            tile.SetActive(false);
            return tile;
        }

        static Camera CreateCollectionCardCamera(string name)
        {
            GameObject go = new GameObject(name);
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f, 1f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            go.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            return camera;
        }

        static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static Canvas CreateCanvas(string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = (anchorMin + anchorMax) * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>
        /// A panel stretched between two different anchors with edge insets. Mixed anchors make
        /// <c>sizeDelta</c> relative to the anchor span, so a "size" there silently recentres the panel
        /// over the header; offsets from each anchor corner are the only correct expression of insets.
        /// </summary>
        static GameObject CreateStretchPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return go;
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = (anchorMin + anchorMax) * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            ConfigureText(tmp, value, size, color);
            tmp.alignment = alignment;
            return tmp;
        }

        static void ConfigureText(TextMeshProUGUI tmp, string value, float size, Color color)
        {
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Rajdhani-SemiBold");
            if (font != null) tmp.font = font;
            tmp.richText = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
        }

        /// <summary>Ghost/primary button matching the UiKit look: tint transition, accent top edge, bold label.</summary>
        static Button CreateButton(string name, Transform parent, string label, bool primary,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = (anchorMin + anchorMax) * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Color normal = primary ? Primary : Ghost;
            Image image = go.GetComponent<Image>();
            image.color = normal;

            Button button = go.AddComponent<Button>();
            go.AddComponent<UiFxButton>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Tint(normal, 1.22f),
                pressedColor = Tint(normal, 0.78f),
                selectedColor = Tint(normal, 1.10f),
                disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            GameObject edge = new GameObject("AccentEdge", typeof(RectTransform));
            edge.transform.SetParent(go.transform, false);
            RectTransform edgeRect = edge.GetComponent<RectTransform>();
            edgeRect.anchorMin = new Vector2(0f, 1f);
            edgeRect.anchorMax = Vector2.one;
            edgeRect.pivot = new Vector2(0.5f, 1f);
            edgeRect.sizeDelta = new Vector2(0f, 2f);
            Image edgeImage = edge.AddComponent<Image>();
            edgeImage.color = primary ? Accent : new Color(Accent.r, Accent.g, Accent.b, 0.3f);
            edgeImage.raycastTarget = false;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            TextMeshProUGUI tmp = labelGo.AddComponent<TextMeshProUGUI>();
            ConfigureText(tmp, label, primary ? 16f : 14f, primary ? TextBright : Accent);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.fontStyle = FontStyles.Bold;
            return button;
        }

        static Color Tint(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }

        static void SaveScene(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── Bay live counters ─────────────────────────────────────────────────────────────────────
        // The approved bay art is a flat Figma export: its credit readout, milestone number and track
        // fill are baked pixels, so nothing can roll over them. This menu derives a base texture with
        // exactly those four regions erased (each row lerps between the pixels just outside its left
        // and right edges, so the art's faint gradients survive the patch seam-free), swaps it under
        // the same stretched Image, and authors live overlay nodes — UiCounter readouts plus a UiBar
        // track/fill pair — on top of the erased rects. Idempotent: re-runs refresh the texture but
        // never duplicate the overlay nodes.

        const string BayArtPath = "Assets/UI/Example/Signal bay.png";
        const string BayBasePath = "Assets/UI/Sprites/SignalBayBase.png";

        [MenuItem("Overlink/Scenes/Wire Bay Live Counters")]
        public static void WireBayLiveCounters()
        {
            Scene scene = EditorSceneManager.GetSceneByPath(GachaScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(GachaScenePath, OpenSceneMode.Single);
            }

            GameObject packScreen = FindInScene(scene, "PackScreen");
            if (packScreen == null)
            {
                Debug.LogError("[Overlink] " + GachaScenePath + " has no PackScreen to re-skin.");
                return;
            }

            Image screenImage = packScreen.GetComponent<Image>();
            // Refresh the derived image only while the scene actually uses it. Once the bay's
            // hierarchy replaces the raster, this maintenance command must not resurrect it.
            string currentSpritePath = screenImage != null && screenImage.sprite != null
                ? AssetDatabase.GetAssetPath(screenImage.sprite) : null;
            bool usesRaster = currentSpritePath == BayBasePath || currentSpritePath == BayArtPath;
            if (usesRaster)
            {
                if (!DeriveBayBaseTexture()) return;
                Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BayBasePath);
                if (baseSprite != null) screenImage.sprite = baseSprite;
            }

            if (packScreen.transform.Find("Credit Live") == null) AuthorBayOverlays(packScreen.transform);
            EnsureBarSprites(packScreen.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, GachaScenePath);
            Debug.Log("[Overlink] Bay live counters wired on " + GachaScenePath + ".");
        }

        static bool DeriveBayBaseTexture()
        {
            if (!File.Exists(BayArtPath))
            {
                Debug.LogError("[Overlink] Bay art missing: " + BayArtPath);
                return false;
            }

            Texture2D tex = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(BayArtPath)))
            {
                Debug.LogError("[Overlink] Could not decode " + BayArtPath);
                return false;
            }

            // Erased rects in native 1600x900 texture space (y up): credit digits, milestone number,
            // milestone bar, and the derived "more pulls" line. The credit rect starts clear of the
            // baked diamond icon — sampling the icon edge would streak its white across the patch.
            Patch(tex, 1162, 811, 1236, 849);
            Patch(tex, 1202, 126, 1274, 164);
            Patch(tex, 380, 103, 1274, 122);
            Patch(tex, 380, 81, 594, 102);

            Directory.CreateDirectory(Path.GetDirectoryName(BayBasePath));
            File.WriteAllBytes(BayBasePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(BayBasePath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(BayBasePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return true;
        }

        /// <summary>Erases a rect by interpolating each row between the pixels just outside its left
        /// and right edges, keeping horizontal gradients in the art alive across the patch.</summary>
        static void Patch(Texture2D tex, int left, int bottom, int right, int top)
        {
            for (int y = bottom; y <= top; y++)
            {
                Color edgeL = tex.GetPixel(Mathf.Max(0, left - 3), y);
                Color edgeR = tex.GetPixel(Mathf.Min(tex.width - 1, right + 3), y);
                for (int x = left; x <= right; x++)
                {
                    tex.SetPixel(x, y, Color.Lerp(edgeL, edgeR, (x - left) / (float)(right - left)));
                }
            }
        }

        static void AuthorBayOverlays(Transform screen)
        {
            // Credit readout over the erased digits; rolls up to the balance on bay entry.
            TextMeshProUGUI credit = OverlayText(screen, "Credit Live", "0", 34f, TextBright,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(432f, 456f), new Vector2(200f, 48f));
            credit.gameObject.AddComponent<UiCounter>();

            // Milestone number: cyan progress against a dim threshold in one rich-text readout.
            TextMeshProUGUI milestone = OverlayText(screen, "Milestone Number Live", "0", 27f, Accent,
                TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(566f, -366f), new Vector2(170f, 44f));
            milestone.richText = true;
            milestone.gameObject.AddComponent<UiCounter>();

            // Derived remaining-pulls line under the track.
            TextMeshProUGUI remaining = OverlayText(screen, "Remaining Live", "0", 15f, TextDim,
                TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(-500f, -429f), new Vector2(320f, 26f));
            remaining.gameObject.AddComponent<UiCounter>();

            // Track + fill pair; the UiBar rides the parent and is bound to the fill at author time.
            GameObject barRoot = new GameObject("Milestone Bar Live", typeof(RectTransform));
            barRoot.transform.SetParent(screen, false);
            RectTransform barRect = (RectTransform)barRoot.transform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2(32f, -405f);
            barRect.sizeDelta = new Vector2(1068f, 8f);
            Image track = BarLayer(barRoot.transform, "Track", new Color(0.149f, 0.227f, 0.290f)); // art's #263A4A
            Image fill = BarLayer(barRoot.transform, "Fill", Accent);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            barRoot.AddComponent<UiBar>().Bind(fill, null);
        }

        /// <summary>Self-heal for scenes authored before bar layers carried a sprite: a Filled Image
        /// with no sprite ignores fillAmount and paints the whole quad, so any null-sprite layer on
        /// the live track picks up the shared white sprite here.</summary>
        static void EnsureBarSprites(Transform screen)
        {
            Transform bar = screen.Find("Milestone Bar Live");
            if (bar == null) return;
            Sprite white = BuildVfxAssets.WhiteSprite();
            if (white == null) return;
            foreach (string layer in new[] { "Track", "Fill" })
            {
                Transform child = bar.Find(layer);
                Image image = child != null ? child.GetComponent<Image>() : null;
                if (image != null && image.sprite == null) image.sprite = white;
            }
        }

        static TextMeshProUGUI OverlayText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment, Vector2 pivot, Vector2 position, Vector2 dimensions)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            ConfigureText(tmp, value, size, color);
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        static Image BarLayer(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1068f, 8f);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = BuildVfxAssets.WhiteSprite();
            image.raycastTarget = false;
            return image;
        }
    }
}
