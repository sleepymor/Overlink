using System;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    [Serializable]
    public sealed class CardFrameEntry
    {
        public CardType type;
        public Faction faction;
        public Rarity rarity;
        public Sprite sprite;

        public CardFrameEntry() { }
        public CardFrameEntry(CardType type, Faction faction, Rarity rarity, Sprite sprite)
        {
            this.type = type;
            this.faction = faction;
            this.rarity = rarity;
            this.sprite = sprite;
        }
    }

    [CreateAssetMenu(menuName = "Overlink/Cards/Frame Catalog", fileName = "CardFrameCatalog")]
    public sealed class CardFrameCatalog : ScriptableObject
    {
        public List<CardFrameEntry> entries = new List<CardFrameEntry>();
        public Sprite neutralFallback;

        public Sprite Get(CardType type, Faction faction, Rarity rarity)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                CardFrameEntry entry = entries[i];
                if (entry != null && entry.type == type && entry.faction == faction && entry.rarity == rarity)
                {
                    if (entry.sprite != null) return entry.sprite;
                    break;
                }
            }

            Debug.LogWarning($"[CardFrameCatalog] Missing frame key Type={type}, Faction={faction}, Rarity={rarity}; using neutral fallback.", this);
            return neutralFallback;
        }

        public int CountValidEntries()
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].sprite != null) count++;
            }
            return count;
        }

        public bool HasExactlyOne(CardType type, Faction faction, Rarity rarity)
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                CardFrameEntry entry = entries[i];
                if (entry != null && entry.type == type && entry.faction == faction && entry.rarity == rarity)
                    count++;
            }
            return count == 1;
        }
    }
}
