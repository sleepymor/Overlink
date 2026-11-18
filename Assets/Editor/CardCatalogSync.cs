using System.Collections.Generic;
using Overlink.Cards;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Fills each CardAsset's art sprite from its artPath and points the catalog at the shared card back.
    /// Run after importing art or changing artPath.
    /// </summary>
    public static class CardCatalogSync
    {
        public const string CardBackPath = "Assets/Cards/Frame/OverlinkBack/Card_Back.png";

        [MenuItem("Overlink/Cards/Sync Card Art And Back", false, 23)]
        public static void Sync()
        {
            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(CardLibraryBootstrap.CatalogAssetPath);
            if (catalog == null)
            {
                Debug.LogError("[CardCatalogSync] No catalog at " + CardLibraryBootstrap.CatalogAssetPath);
                return;
            }

            int linked = 0;
            var missing = new List<string>();
            foreach (CardAsset asset in catalog.cards)
            {
                if (asset == null) continue;
                Sprite sprite = string.IsNullOrEmpty(asset.artPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(asset.artPath);
                if (sprite == null)
                {
                    missing.Add(asset.id);
                    continue;
                }

                if (asset.art != sprite)
                {
                    asset.art = sprite;
                    EditorUtility.SetDirty(asset);
                }
                linked++;
            }

            Sprite back = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackPath);
            if (catalog.cardBack != back)
            {
                catalog.cardBack = back;
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[CardCatalogSync] Linked art for " + linked + " card(s). Missing art: " + missing.Count + ". Card back: " + (back != null ? "set" : "MISSING") + ".");
            foreach (string id in missing)
            {
                Debug.LogWarning("[CardCatalogSync] No art sprite for " + id);
            }
        }
    }
}
