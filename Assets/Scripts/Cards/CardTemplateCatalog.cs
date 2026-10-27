using System;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    public sealed class CardTemplateMetadata : MonoBehaviour
    {
        public CardType type;
        public Rarity rarity;
        public bool isNeutralFallback;
    }

    [Serializable]
    public sealed class CardTemplateEntry
    {
        public CardType type;
        public Rarity rarity;
        public CardFaceView prefab;

        public CardTemplateEntry() { }
        public CardTemplateEntry(CardType type, Rarity rarity, CardFaceView prefab)
        {
            this.type = type;
            this.rarity = rarity;
            this.prefab = prefab;
        }
    }

    [CreateAssetMenu(menuName = "Overlink/Cards/Template Catalog", fileName = "CardTemplateCatalog")]
    public sealed class CardTemplateCatalog : ScriptableObject
    {
        public List<CardTemplateEntry> entries = new List<CardTemplateEntry>();
        public CardFaceView neutralFallback;

        public CardFaceView Get(CardType type, Rarity rarity)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                CardTemplateEntry entry = entries[i];
                if (entry != null && entry.type == type && entry.rarity == rarity)
                {
                    if (entry.prefab != null) return entry.prefab;
                    break;
                }
            }

            Debug.LogWarning($"[CardTemplateCatalog] Missing template key Type={type}, Rarity={rarity}; using neutral fallback.", this);
            return neutralFallback;
        }

        public bool HasExactlyOne(CardType type, Rarity rarity)
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                CardTemplateEntry entry = entries[i];
                if (entry != null && entry.type == type && entry.rarity == rarity) count++;
            }
            return count == 1;
        }
    }
}
