using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    public static class CardLibraryBootstrap
    {
        public const string CatalogResourcePath = "Cards/CardCatalog";
        public const string CatalogAssetPath = "Assets/Resources/Cards/CardCatalog.asset";

        private static readonly Dictionary<string, Sprite> s_art = new Dictionary<string, Sprite>();
        private static Sprite s_cardBack;

        public static Sprite CardBack => s_cardBack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterFromResources()
        {
            var catalog = Resources.Load<CardCatalog>(CatalogResourcePath);
            if (catalog == null)
            {
                Debug.LogError("[CardLibraryBootstrap] No CardCatalog found at Resources/" + CatalogResourcePath + ". The card pool is empty.");
                return;
            }

            Register(catalog);
        }

        public static Sprite ArtFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return s_art.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        public static void Register(CardCatalog catalog)
        {
            var definitions = new List<CardDefinition>(catalog.cards.Count);
            s_art.Clear();
            foreach (CardAsset asset in catalog.cards)
            {
                if (asset == null) continue;
                definitions.Add(asset.ToDefinition());
                if (asset.art != null) s_art[asset.id] = asset.art;
            }

            s_cardBack = catalog.cardBack;
            MvpCardLibrary.Register(definitions);
        }
    }
}
