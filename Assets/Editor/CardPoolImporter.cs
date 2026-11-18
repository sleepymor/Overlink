using System.Collections.Generic;
using System.IO;
using Overlink.Cards;
using Overlink.Core;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>
    /// One-time import of <c>card_pool.json</c> into <see cref="CardAsset"/> files plus a <see cref="CardCatalog"/>.
    /// Idempotent: IDs that already have an asset are skipped. After import, CardAssets are the source of truth.
    /// </summary>
    public static class CardPoolImporter
    {
        public const string PoolPath = @"E:\Games\Overlink\Archive\cards\card_pool.json";
        public const string DataFolder = "Assets/Cards/Data";
        public const string CatalogPath = "Assets/Resources/Cards/CardCatalog.asset";

        [MenuItem("Overlink/Cards/Import Card Pool To Assets", false, 22)]
        public static void Import()
        {
            if (!File.Exists(PoolPath))
            {
                EditorUtility.DisplayDialog("Import Card Pool", "Pool file not found: " + PoolPath, "OK");
                return;
            }

            IReadOnlyList<CardDefinition> definitions = CardJson.Parse(File.ReadAllText(PoolPath));
            EnsureFolder(DataFolder);

            var existingIds = new HashSet<string>();
            foreach (CardAsset existing in CardExporter.FindAllCards())
            {
                if (existing != null && !string.IsNullOrEmpty(existing.id))
                {
                    existingIds.Add(existing.id);
                }
            }

            int created = 0;
            int skipped = 0;
            int missingArt = 0;
            foreach (CardDefinition definition in definitions)
            {
                if (existingIds.Contains(definition.Id))
                {
                    skipped++;
                    continue;
                }

                if (!string.IsNullOrEmpty(definition.ArtPath) && AssetDatabase.LoadAssetAtPath<Sprite>(definition.ArtPath) == null)
                {
                    missingArt++;
                    Debug.LogWarning("[CardPoolImporter] Art not found for " + definition.Id + ": " + definition.ArtPath);
                }

                var asset = ScriptableObject.CreateInstance<CardAsset>();
                asset.FromDefinition(definition);
                AssetDatabase.CreateAsset(asset, DataFolder + "/" + definition.Id + ".asset");
                created++;
            }

            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.cards = new List<CardAsset>(CardExporter.FindAllCards());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CardPoolImporter] Created " + created + ", skipped " + skipped + " existing, " + missingArt + " missing art. Catalog holds " + catalog.cards.Count + " card(s).");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
