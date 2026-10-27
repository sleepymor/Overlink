using System;
using UnityEngine;
using Overlink.Core;

namespace Overlink.Cards
{
    [Serializable]
    public sealed class CardTierMaterial
    {
        public Rarity rarity;
        public Material customMaterial;
        public Material staticFallback;
    }

    [CreateAssetMenu(menuName = "Overlink/Cards/Tier Material Set", fileName = "CardTierMaterialSet")]
    public sealed class CardTierMaterialSet : ScriptableObject
    {
        public Material basicMaterial;
        public CardTierMaterial rare = new CardTierMaterial { rarity = Rarity.Rare };
        public CardTierMaterial epic = new CardTierMaterial { rarity = Rarity.Epic };
        public CardTierMaterial legendary = new CardTierMaterial { rarity = Rarity.Legendary };
        public bool tierEffectsEnabled = true;
        public bool useCustomMaterials = true;

        public Material Resolve(Rarity rarity, bool shaderSupported)
        {
            if (rarity == Rarity.Common) return basicMaterial;
            CardTierMaterial tier = rarity == Rarity.Rare ? rare : rarity == Rarity.Epic ? epic : legendary;
            if (tier == null) return basicMaterial;
            return tierEffectsEnabled && useCustomMaterials && shaderSupported && tier.customMaterial != null
                ? tier.customMaterial
                : tier.staticFallback;
        }
    }
}
