using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Overlink.Cards;
using Overlink.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>Non-destructive editor tools for the placeholder card presentation matrix.</summary>
    public static class CardPresentationEditor
    {
        const string FrameRoot = "Assets/Cards/Frame";
        const string PresentationRoot = "Assets/Cards/Presentation";
        const string TemplateRoot = PresentationRoot + "/Templates";
        const string FixtureRoot = "Assets/Cards/PlaceholderFixtures";
        const string TestArtRoot = "Assets/Cards/Test_art";
        const string LegendaryArtPath = TestArtRoot + "/overlink_card_00315_legendary.png";
        const string StandardArtPath = TestArtRoot + "/overlink_creature_00045_basic.png";

        [MenuItem("Overlink/Cards/Refresh Frame Catalog", false, 30)]
        public static void RefreshFrameCatalog()
        {
            EnsureFolder("Assets/Cards");
            EnsureFolder(PresentationRoot);
            RefreshArtTextureQuality();
            string path = PresentationRoot + "/CardFrameCatalog.asset";
            CardFrameCatalog catalog = AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardFrameCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            Undo.RecordObject(catalog, "Refresh Card Frame Catalog");
            catalog.entries.Clear();
            Sprite first = null;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FrameRoot });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureFrameImporter(assetPath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null) continue;
                if (first == null) first = sprite;

                string typeToken = new DirectoryInfo(Path.GetDirectoryName(assetPath)).Name;
                string fileToken = Path.GetFileNameWithoutExtension(assetPath);
                string[] parts = fileToken.Split('_');
                if (!Enum.TryParse(typeToken, true, out CardType type) || parts.Length != 2) continue;
                if (!TryNormalizeFaction(parts[0], out Faction faction)) continue;
                if (!Enum.TryParse(parts[1], true, out Rarity rarity)) continue;
                catalog.entries.Add(new CardFrameEntry(type, faction, rarity, sprite));
            }

            catalog.neutralFallback = first;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardPresentationEditor] Frame catalog refreshed: {catalog.CountValidEntries()} sprites.");
        }

        [MenuItem("Overlink/Cards/Create/Update Template Prefabs", false, 31)]
        public static void CreateOrUpdateTemplatePrefabs()
        {
            EnsureFolder(PresentationRoot);
            EnsureFolder(TemplateRoot);
            CardTemplateCatalog catalog = GetOrCreateAsset<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            Undo.RecordObject(catalog, "Create Card Template Matrix");
            catalog.entries.Clear();

            foreach (CardType type in Enum.GetValues(typeof(CardType)))
            {
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                {
                    string prefabPath = $"{TemplateRoot}/Card_{type}_{rarity}.prefab";
                    CardFaceView face = AssetDatabase.LoadAssetAtPath<CardFaceView>(prefabPath);
                    if (face == null || HasMissingScripts(prefabPath) || face.ArtImage == null || face.FrameImage == null || face.NameValue == null
                        || face.TypeValue == null || face.AbilityValue == null || face.IdValue == null)
                    {
                        // Earlier generated templates were empty shells and therefore not useful for
                        // hierarchy editing. Replace only those shells; authored templates with fields
                        // remain intact.
                        if (face != null) AssetDatabase.DeleteAsset(prefabPath);
                        GameObject go = CreateEditableTemplate(type, rarity);
                        face = PrefabUtility.SaveAsPrefabAsset(go, prefabPath).GetComponent<CardFaceView>();
                        UnityEngine.Object.DestroyImmediate(go);
                    }

                    AssignPreviewFrame(prefabPath, type, rarity);
                    face = AssetDatabase.LoadAssetAtPath<CardFaceView>(prefabPath);
                    catalog.entries.Add(new CardTemplateEntry(type, rarity, face));
                }
            }

            catalog.neutralFallback = catalog.entries.Count > 0 ? catalog.entries[0].prefab : null;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[CardPresentationEditor] Template matrix created/updated: 16 entries.");
        }

        [MenuItem("Overlink/Cards/Create/Rebuild Clean Template Prefabs", false, 30)]
        public static void RebuildCleanTemplatePrefabs()
        {
            // These are generated presentation templates, so rebuilding this folder is safe and
            // removes obsolete CardFace/legacy components without hand-editing Unity YAML.
            foreach (CardType type in Enum.GetValues(typeof(CardType)))
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                string path = $"{TemplateRoot}/Card_{type}_{rarity}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.Refresh();
            CreateOrUpdateTemplatePrefabs();
            Debug.Log("[CardPresentationEditor] Rebuilt all 16 clean editable card templates.");
        }

        static bool HasMissingScripts(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                        return true;
                }
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void RemoveMissingScripts(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                for (int i = 0; i < count; i++)
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        static void AssignPreviewFrame(string prefabPath, CardType type, Rarity rarity)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                CardFaceView face = prefabRoot.GetComponent<CardFaceView>();
                if (face == null) return;

                // Use an actual frame asset so the prefab has a visible live preview in Prefab Mode.
                // Runtime binding still replaces this sprite with the exact faction/type/rarity frame.
                string previewPath = string.Format("{0}/{1}/{2}_{3}.png", FrameRoot, type,
                    Faction.Fsti.ToString().ToUpperInvariant(), rarity);
                Sprite preview = AssetDatabase.LoadAssetAtPath<Sprite>(previewPath);
                if (preview == null)
                {
                    CardFrameCatalog frames = AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(
                        PresentationRoot + "/CardFrameCatalog.asset");
                    if (frames != null) preview = frames.Get(type, Faction.Fsti, rarity);
                }
                if (face.FrameImage != null) face.FrameImage.sprite = preview;
                CardTierMaterialSet tierMaterials = AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(
                    PresentationRoot + "/CardTierMaterialSet.asset");
                if (face.FrameImage != null && tierMaterials != null)
                {
                    face.FrameImage.sharedMaterial = tierMaterials.Resolve(
                        rarity, Shader.Find("Overlink/CardTier/" + rarity) != null);
                }

                // Populate a representative face and run the real layout, so the prefab shows the
                // framed card (art window, mask, labels) in Prefab Mode instead of a bare frame.
                face.PreviewRarity = rarity;
                Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(
                    rarity == Rarity.Legendary ? LegendaryArtPath : StandardArtPath);
                if (art != null && face.ArtImage != null) face.ArtImage.sprite = art;
                face.CostValue.text = "7";
                face.NameValue.text = rarity == Rarity.Legendary ? "Legendary Test Creature" : "Test Creature";
                face.TypeValue.text = type.ToString();
                face.AbilityValue.text = "Placeholder ability text for layout preview.";
                face.IdValue.text = "overlink_00000";
                if (face.AtkValue != null) face.AtkValue.text = "4";
                if (face.AtkLabel != null) face.AtkLabel.text = "Attack";
                if (face.HpValue != null) face.HpValue.text = "5";
                if (face.ShieldValue != null) face.ShieldValue.text = "2";
                face.SetSize(CardFaceView.CanonicalFaceSize);

                // Never save a prefab that contains a stale Missing MonoBehaviour from an older
                // generated template. This also repairs the exact error reported by Unity.
                RemoveMissingScripts(prefabRoot);
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static GameObject CreateEditableTemplate(CardType type, Rarity rarity)
        {
            GameObject root = new GameObject($"Card_{type}_{rarity}");
            CardFaceView face = root.AddComponent<CardFaceView>();

            // Create the face children in the prefab itself, rather than relying on runtime
            // EnsureFields(). Designers can select and edit these objects in the prefab hierarchy.
            // Every label is styled through CardFaceView.StyleLabel so the prefab matches runtime.
            face.ArtImage = CreateTemplateSprite(root.transform, "ArtImage", Vector3.zero, -10);
            face.FrameImage = CreateTemplateSprite(root.transform, "FrameImage", Vector3.zero, 0);
            face.ArtMask = CreateTemplateMask(root.transform);

            CardFaceView.FaceZones zones = CardFaceView.ZonesFor(rarity);
            Vector3 size = CardFaceView.CanonicalFaceSize;
            face.CostValue = CreateTemplateText(root.transform, "CostValue", zones.Cost, size, false);
            face.NameValue = CreateTemplateText(root.transform, "NameValue", zones.Name, size, false);
            face.TypeValue = CreateTemplateText(root.transform, "TypeValue", zones.Type, size, false);
            face.AbilityValue = CreateTemplateText(root.transform, "AbilityValue", zones.Ability, size, true);
            face.IdValue = CreateTemplateText(root.transform, "IdValue", zones.Id, size, false);

            GameObject stats = new GameObject("CombatStats");
            stats.transform.SetParent(root.transform, false);
            face.CombatStatsRoot = stats;
            face.AtkValue = CreateTemplateText(stats.transform, "AtkValue", zones.Atk, size, false);
            // Only created when the tier's frame needs the word spelled out; frames that bake
            // "Attack" into the bar leave the zone empty, matching the runtime face.
            if (CardFaceView.FaceZones.HasZone(zones.AtkLabel))
            {
                face.AtkLabel = CreateTemplateText(stats.transform, "AtkLabel", zones.AtkLabel, size, false);
            }
            face.HpValue = CreateTemplateText(stats.transform, "HpValue", zones.Hp, size, false);
            face.ShieldValue = CreateTemplateText(stats.transform, "ShieldValue", zones.Shield, size, false);
            stats.SetActive(type == CardType.Unit);
            return root;
        }

        static SpriteMask CreateTemplateMask(Transform parent)
        {
            GameObject go = new GameObject("ArtMask");
            go.transform.SetParent(parent, false);
            SpriteMask mask = go.AddComponent<SpriteMask>();
            mask.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            mask.frontSortingOrder = -9;
            mask.backSortingOrder = -11;
            return mask;
        }

        static SpriteRenderer CreateTemplateSprite(Transform parent, string name, Vector3 position, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static TextMeshPro CreateTemplateText(Transform parent, string name,
            CardFaceView.LabelZone zone, Vector3 faceSize, bool multiLine)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.color = Color.white;
            text.sortingOrder = 20;
            // Same code path as the runtime face, so what designers see in prefab mode is what
            // ships. No per-label magic numbers remain here.
            CardFaceView.StyleLabel(text, faceSize,
                CardFaceView.PixelPosition(faceSize, zone.X, zone.Y), zone, multiLine);
            return text;
        }

        [MenuItem("Overlink/Cards/Validate Template Matrix", false, 32)]
        public static void ValidateTemplateMatrix()
        {
            CardFrameCatalog frameCatalog = AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(PresentationRoot + "/CardFrameCatalog.asset");
            CardTemplateCatalog templateCatalog = AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            int errors = 0;
            if (frameCatalog == null) { Debug.LogError("[CardPresentationEditor] Missing CardFrameCatalog asset."); errors++; }
            else
            {
                foreach (CardType type in Enum.GetValues(typeof(CardType)))
                foreach (Faction faction in Enum.GetValues(typeof(Faction)))
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                    if (!frameCatalog.HasExactlyOne(type, faction, rarity))
                    { Debug.LogError($"[CardPresentationEditor] Missing/duplicate frame key {type}/{faction}/{rarity}."); errors++; }
            }

            if (templateCatalog == null) { Debug.LogError("[CardPresentationEditor] Missing CardTemplateCatalog asset."); errors++; }
            else
            {
                foreach (CardType type in Enum.GetValues(typeof(CardType)))
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                    if (!templateCatalog.HasExactlyOne(type, rarity))
                    { Debug.LogError($"[CardPresentationEditor] Missing/duplicate template key {type}/{rarity}."); errors++; }
            }

            if (errors == 0) Debug.Log("[CardPresentationEditor] Template matrix valid: 48 frames and 16 templates.");
        }

        [MenuItem("Overlink/Cards/Preview Tier Materials", false, 33)]
        public static void PreviewTierMaterials()
        {
            EnsureFolder(PresentationRoot);
            CardTierMaterialSet set = GetOrCreateAsset<CardTierMaterialSet>(PresentationRoot + "/CardTierMaterialSet.asset");
            // Static tier materials are assigned to SpriteRenderers, so prefer the sprite
            // shader to preserve the frame texture and alpha.
            Shader plain = Shader.Find("Sprites/Default");
            if (plain == null) plain = Shader.Find("Universal Render Pipeline/Unlit");
            set.basicMaterial = GetOrCreateMaterial(PresentationRoot + "/CardTier_Basic_Common.mat", plain, new Color(0.75f, 0.78f, 0.84f));
            set.rare.staticFallback = GetOrCreateMaterial(PresentationRoot + "/CardTier_Rare_Static.mat", plain, new Color(0.25f, 0.55f, 1f));
            set.epic.staticFallback = GetOrCreateMaterial(PresentationRoot + "/CardTier_Epic_Static.mat", plain, new Color(0.65f, 0.30f, 1f));
            set.legendary.staticFallback = GetOrCreateMaterial(PresentationRoot + "/CardTier_Legendary_Static.mat", plain, new Color(1f, 0.55f, 0.12f));
            set.rare.customMaterial = FindOrFallbackMaterial("Overlink/CardTier/Rare", set.rare.staticFallback);
            set.epic.customMaterial = FindOrFallbackMaterial("Overlink/CardTier/Epic", set.epic.staticFallback);
            set.legendary.customMaterial = FindOrFallbackMaterial("Overlink/CardTier/Legendary", set.legendary.staticFallback);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[Overlink] Tier materials refreshed. Basic/Common remains plain. Custom tier "
                + "materials keep their tuned shader properties; only _Color (forced white) and "
                + "_EffectEnabled are rewritten here, so the Legendary tint cannot come back.");
        }

        [MenuItem("Overlink/Cards/Wire Placeholder Presentation", false, 34)]
        public static void WirePlaceholderPresentation()
        {
            string scenePath = "Assets/Scenes/Gameplay.unity";
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            HandController hand = UnityEngine.Object.FindObjectOfType<HandController>();
            if (hand == null)
            {
                Debug.LogError("[CardPresentationEditor] Gameplay scene has no HandController.");
                return;
            }

            CardFrameCatalog frameCatalog = AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(PresentationRoot + "/CardFrameCatalog.asset");
            CardTemplateCatalog templateCatalog = AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            CardPresentationFixture fixture = AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(FixtureRoot + "/CardPresentationFixture.asset");
            SerializedObject serializedHand = new SerializedObject(hand);
            serializedHand.FindProperty("frameCatalog").objectReferenceValue = frameCatalog;
            serializedHand.FindProperty("templateCatalog").objectReferenceValue = templateCatalog;
            serializedHand.FindProperty("placeholderFixture").objectReferenceValue = fixture;
            serializedHand.FindProperty("tierMaterials").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(
                PresentationRoot + "/CardTierMaterialSet.asset");
            serializedHand.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CardPresentationEditor] Wired Gameplay HandController to frame/template/fixture catalogs.");
        }

        [MenuItem("Overlink/Cards/Create Placeholder Fixture", false, 35)]
        public static void CreatePlaceholderFixture()
        {
            EnsureFolder(FixtureRoot);
            string path = FixtureRoot + "/CardPresentationFixture.asset";
            CardPresentationFixture fixture = AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(path);
            if (fixture == null)
            {
                fixture = ScriptableObject.CreateInstance<CardPresentationFixture>();
                AssetDatabase.CreateAsset(fixture, path);
            }

            AssignTestArt(fixture);
            EditorUtility.SetDirty(fixture);
            AssetDatabase.SaveAssets();
            Selection.activeObject = fixture;
        }

        [MenuItem("Overlink/Cards/Create Legendary Preview Fixture", false, 37)]
        public static void CreateLegendaryPreviewFixture()
        {
            CreateTierFixture(Rarity.Legendary);
        }

        [MenuItem("Overlink/Cards/Create All Tier Preview Fixtures", false, 38)]
        public static void CreateAllTierPreviewFixtures()
        {
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                CreateTierFixture(rarity);
            }
            Debug.Log("[CardPresentationEditor] Created presentation fixtures for all four rarities.");
        }

        /// <summary>All twelve faction/rarity fixtures for one faction, as Unit cards. The frame catalog
        /// is authored per faction, so this is what makes FRTI and FPB frames visible outside a match.</summary>
        [MenuItem("Overlink/Cards/Create All Faction Tier Fixtures", false, 39)]
        public static void CreateAllFactionTierPreviewFixtures()
        {
            foreach (Faction faction in new[] { Faction.Fsti, Faction.Frti, Faction.Fpb })
            {
                CreateAllTierPreviewFixtures(faction);
            }
            Debug.Log("[CardPresentationEditor] Created presentation fixtures for 3 factions x 4 rarities.");
        }

        public static void CreateAllTierPreviewFixtures(Faction faction)
        {
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                CreateTierFixture(rarity, faction);
            }
        }

        /// <summary>
        /// The full 48-fixture matrix: every card type x faction x rarity the frame catalog holds.
        /// Each fixture drives one preview card, so building this once means every scene below can be
        /// rebuilt without touching assets.
        /// </summary>
        [MenuItem("Overlink/Cards/Create Full Frame Matrix Fixtures", false, 40)]
        public static void CreateFullFrameMatrixFixtures()
        {
            int made = 0;
            foreach (CardType type in System.Enum.GetValues(typeof(CardType)))
            foreach (Faction faction in new[] { Faction.Fsti, Faction.Frti, Faction.Fpb })
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                CreateTierFixture(rarity, faction, type);
                made++;
            }
            Debug.Log("[CardPresentationEditor] Created " + made + " frame-matrix fixtures (4 types x 3 factions x 4 rarities).");
        }

        /// <summary>
        /// Creates or refreshes the presentation fixture for one rarity. Common keeps the unsuffixed
        /// asset path it has always used so existing scene references survive; the other tiers get a
        /// suffixed sibling.
        /// </summary>
        public static CardPresentationFixture CreateTierFixture(Rarity rarity)
        {
            return CreateTierFixture(rarity, Faction.Fsti);
        }

        /// <summary>
        /// Creates or refreshes the presentation fixture for one rarity of one faction. Every frame in
        /// the catalog is authored per faction, so without this the preview scenes could only ever
        /// show FSTI and the other two frames were never seen outside the match.
        /// </summary>
        public static CardPresentationFixture CreateTierFixture(Rarity rarity, Faction faction)
        {
            return CreateTierFixture(rarity, faction, CardType.Unit);
        }

        /// <summary>
        /// Creates or refreshes one presentation fixture, addressed by the same triple the frame
        /// catalog is keyed by: rarity, faction and card type. The frame art and the template are both
        /// selected from this triple, so a fixture that omits the type silently previews the Unit
        /// frame under an Ability or Relay card.
        /// </summary>
        public static CardPresentationFixture CreateTierFixture(Rarity rarity, Faction faction, CardType type)
        {
            EnsureFolder(FixtureRoot);
            string path = FixturePathFor(rarity, faction, type);
            CardPresentationFixture fixture = AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(path);
            if (fixture == null)
            {
                fixture = ScriptableObject.CreateInstance<CardPresentationFixture>();
                AssetDatabase.CreateAsset(fixture, path);
            }

            bool fullArt = rarity == Rarity.Legendary;
            string artPath = fullArt ? LegendaryArtPath : StandardArtPath;
            string tierName = rarity.ToString();

            fixture.displayName = tierName + " Test " + (type == CardType.Unit ? "Creature" : type.ToString());
            fixture.id = "test_" + type.ToString().ToLowerInvariant() + "_"
                + faction.ToString().ToLowerInvariant() + "_"
                + tierName.ToLowerInvariant() + "_" + CostFor(rarity).ToString("00000");
            fixture.type = type;
            fixture.faction = faction;
            fixture.rarity = rarity;
            fixture.cost = CostFor(rarity);
            fixture.atk = 3 + (int)rarity * 2;
            fixture.hp = 5 + (int)rarity * 3;
            fixture.shield = 1 + (int)rarity;
            fixture.abilityText = AbilityFor(rarity);

            ConfigureArtImporter(artPath);
            AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceSynchronousImport);
            fixture.artSprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
            if (fixture.artSprite == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { TestArtRoot });
                if (guids.Length > 0)
                    fixture.artSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            EditorUtility.SetDirty(fixture);
            AssetDatabase.SaveAssets();
            return fixture;
        }

        public static string FixturePathFor(Rarity rarity)
        {
            return FixturePathFor(rarity, Faction.Fsti, CardType.Unit);
        }

        public static string FixturePathFor(Rarity rarity, Faction faction)
        {
            return FixturePathFor(rarity, faction, CardType.Unit);
        }

        /// <summary>
        /// Fixture asset path for a rarity / faction / type triple. FSTI Unit keeps the historical
        /// spelling - the unsuffixed asset for Common and `_<Rarity>` for the rest - so every existing
        /// scene and template reference survives. Other combinations append the non-default axes in a
        /// fixed order (rarity, then faction, then type), which is why it is
        /// `..._Epic_FRTI_Relay.asset` and never `..._Relay_FRTI_Epic.asset`.
        /// </summary>
        public static string FixturePathFor(Rarity rarity, Faction faction, CardType type)
        {
            string path = rarity == Rarity.Common
                ? FixtureRoot + "/CardPresentationFixture"
                : FixtureRoot + "/CardPresentationFixture_" + rarity.ToString();
            if (faction != Faction.Fsti) path += "_" + faction.ToString().ToUpperInvariant();
            if (type != CardType.Unit) path += "_" + type.ToString();
            return path + ".asset";
        }

        static int CostFor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return 4;
                case Rarity.Epic: return 5;
                case Rarity.Legendary: return 7;
                default: return 2;
            }
        }

        // The ability line names the tier's own treatment, so a screenshot of the gallery is also a
        // record of which effect each card is supposed to be showing.
        static string AbilityFor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return "Rare: a neutral stamped foil sweep across the card, no holo film.";
                case Rarity.Epic: return "Epic: neutral prismatic-crackle holo over the frame, no colour cast.";
                case Rarity.Legendary: return "Legendary: full-bleed art under a neutral foil sweep and glinting dust.";
                default: return "Common: no tier effect, the frame is shown exactly as authored.";
            }
        }

        [MenuItem("Overlink/Cards/Assign Test Art To Fixture", false, 36)]
        public static void AssignTestArtToFixture()
        {
            CardPresentationFixture fixture = AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(
                FixtureRoot + "/CardPresentationFixture.asset");
            if (fixture == null)
            {
                CreatePlaceholderFixture();
                fixture = AssetDatabase.LoadAssetAtPath<CardPresentationFixture>(
                    FixtureRoot + "/CardPresentationFixture.asset");
            }

            AssignTestArt(fixture);
            EditorUtility.SetDirty(fixture);
            AssetDatabase.SaveAssets();
            Debug.Log("[CardPresentationEditor] Assigned Test_art creature art to the placeholder fixture.");
        }

        static void AssignTestArt(CardPresentationFixture fixture)
        {
            if (fixture == null) return;
            ConfigureArtImporter(StandardArtPath);
            AssetDatabase.ImportAsset(StandardArtPath, ImportAssetOptions.ForceSynchronousImport);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StandardArtPath);
            if (sprite == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { TestArtRoot });
                if (guids.Length > 0)
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (sprite != null)
            {
                fixture.artSprite = sprite;
                fixture.displayName = "Test Creature";
                fixture.id = "test_creature_00045";
                fixture.type = CardType.Unit;
                fixture.rarity = Rarity.Common;
                fixture.abilityText = "Placeholder creature art from Test_art.";
            }
            else
            {
                Debug.LogWarning("[CardPresentationEditor] No Sprite assets found under " + TestArtRoot + ".");
            }
        }

        public static bool TryNormalizeFaction(string token, out Faction faction)
        {
            switch ((token ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "FSTI": faction = Faction.Fsti; return true;
                case "FRTI": faction = Faction.Frti; return true;
                case "FPB": faction = Faction.Fpb; return true;
                default: faction = default(Faction); return false;
            }
        }

        [MenuItem("Overlink/Cards/Refresh Art Texture Quality", false, 29)]
        public static void RefreshArtTextureQuality()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TestArtRoot });
            int refreshed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (ConfigureArtImporter(path)) refreshed++;
            }
            if (refreshed > 0) AssetDatabase.Refresh();
            Debug.Log($"[CardPresentationEditor] Art texture quality refreshed: {refreshed} assets.");
        }

        static bool ConfigureArtImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;
            // Reapply platform overrides as well as the source importer settings. Existing assets
            // may have been imported with compressed Standalone/WebGL overrides even when the
            // default platform was already lossless.
            bool changed = true;
            if (!changed) return false;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // Mips ON, deliberately against the usual sprite advice. A card face is not a UI image:
            // it is an 832x1216 sprite on a 3D quad that the hand lens draws at roughly 190-225 px,
            // so it is minified about six to one. With a single mip level that much reduction is
            // aliased, and two neighbours rolled to different angles sample the frame's hairlines at
            // different sub-pixel phases, which is what read as "the layout shifting" plus "the card
            // is pixelated" even after the row itself was made congruent. Trilinear plus aniso keeps
            // the pre-filtered level stable across the fan and on the pitched board tiles.
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            ConfigurePlatformQuality(importer, "DefaultTexturePlatform", 4096);
            ConfigurePlatformQuality(importer, "Standalone", 4096);
            ConfigurePlatformQuality(importer, "WebGL", 4096);
            ConfigurePlatformQuality(importer, "Android", 4096);
            ConfigurePlatformQuality(importer, "Windows Store Apps", 4096);
            importer.SaveAndReimport();
            return true;
        }

        static void ConfigurePlatformQuality(TextureImporter importer, string platformName, int maxSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }

        static void ConfigureFrameImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            // Reapply all platform overrides so the delivered frame pixels stay lossless in the
            // Editor and every supported player target.
            bool changed = true;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // Same reasoning as ConfigureArtImporter: the frame is a 3D quad minified about six to
            // one in the hand, so it needs a mip chain. The frame's hairline rails and plate borders
            // are exactly the thin high-contrast detail that aliases without one.
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 832f;
            ConfigurePlatformQuality(importer, "DefaultTexturePlatform", 2048);
            ConfigurePlatformQuality(importer, "Standalone", 2048);
            ConfigurePlatformQuality(importer, "WebGL", 2048);
            ConfigurePlatformQuality(importer, "Android", 2048);
            ConfigurePlatformQuality(importer, "Windows Store Apps", 2048);
            importer.SaveAndReimport();
        }

        static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material GetOrCreateMaterial(string path, Shader shader, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null) material.shader = shader;
            material.color = color;
            return material;
        }

        // Refreshes one custom tier material. This used to DeleteAsset + recreate the material, which
        // threw away every tuned shader property and re-baked the *fallback's* flat tier colour into
        // _Color - that is what turned the Legendary frame orange and left Rare/Epic with
        // _Iridescence 0 (an invisible foil). Rules now:
        //   1. Keep the existing asset and its serialized properties when it already has the right shader.
        //   2. _Color is [PerRendererData]: it tints the sprite before any effect runs, so it stays
        //      white. Tier identity lives in _AccentColor and the effect terms, never in _Color.
        static Material FindOrFallbackMaterial(string shaderName, Material fallback)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) return fallback;
            string path = PresentationRoot + "/" + shaderName.Substring(shaderName.LastIndexOf('/') + 1) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                // Fresh from shader defaults, so this tier's tuned finish is NOT here: the crackle
                // shader ships _HoloOpacity 0.04 / _PrismStrength 0, which is effectively invisible.
                // Restore the values in docs/CARD-FRAME-SPEC.md section 13 before judging the look.
                Debug.LogWarning("[Overlink] Created " + path + " from shader defaults - its tuned tier "
                    + "properties are missing. See CARD-FRAME-SPEC.md section 13 for the values.");
            }
            else if (material.shader != shader)
            {
                Undo.RecordObject(material, "Set Tier Shader");
                material.shader = shader;
            }
            Undo.RecordObject(material, "Refresh Tier Material");
            material.SetColor("_Color", Color.white);
            if (material.HasProperty("_EffectEnabled")) material.SetFloat("_EffectEnabled", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
