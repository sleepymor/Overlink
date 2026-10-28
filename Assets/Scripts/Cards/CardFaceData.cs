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

        /// <summary>
        /// True when <paramref name="other"/> renders exactly what this one renders, field for field.
        /// <para>
        /// A face sitting on the tabletop is re-projected every repaint for values that move a handful of times
        /// per exchange, and a re-bind costs a full TMP layout, so callers ask this before they ask for one.
        /// </para>
        /// </summary>
        /// <remarks>
        /// An explicit comparison rather than a hash code, deliberately: a collision here is not a stutter or a
        /// wrong pixel, it is a card that keeps showing last turn's attack while its owner believes it is
        /// current. The board's whole feedback story rests on these numbers being the ones the engine holds, so
        /// the check that decides whether to refresh them is exact or it is not worth having.
        /// </remarks>
        public bool SameAs(CardFaceData other)
        {
            if (other == null)
            {
                return false;
            }

            return Id == other.Id
                && DisplayName == other.DisplayName
                && Type == other.Type
                && Faction == other.Faction
                && Rarity == other.Rarity
                && Cost == other.Cost
                && ReferenceEquals(ArtSprite, other.ArtSprite)
                && Nullable.Equals(Atk, other.Atk)
                && Nullable.Equals(Hp, other.Hp)
                && Nullable.Equals(Shield, other.Shield)
                && AbilityText == other.AbilityText;
        }

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
