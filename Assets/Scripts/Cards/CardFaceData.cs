using System;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Cards
{
    /// <summary>Immutable presentation projection for a single card face.</summary>
    public sealed class CardFaceData
    {
        public string Id { get; }
        public string DisplayName { get; }
        public CardType Type { get; }
        public Faction Faction { get; }
        public Rarity Rarity { get; }
        public int Cost { get; }
        public Sprite ArtSprite { get; }
        public int? Atk { get; }
        public int? Hp { get; }
        public int? Shield { get; }
        public string AbilityText { get; }

        public bool HasCombatStats => Atk.HasValue && Hp.HasValue && Shield.HasValue;

        public CardFaceData(string id, string displayName, CardType type, Faction faction, Rarity rarity,
            int cost, Sprite artSprite, int? atk, int? hp, int? shield, string abilityText)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Type = type;
            Faction = faction;
            Rarity = rarity;
            Cost = cost;
            ArtSprite = artSprite;
            Atk = atk;
            Hp = hp;
            Shield = shield;
            AbilityText = abilityText ?? string.Empty;
        }
    }
}
