using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    public class CardDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Faction Faction { get; set; }
        public Rarity Rarity { get; set; }
        public CardType Type { get; set; }
        public int Cost { get; set; }
        public int Atk { get; set; }
        public int Hp { get; set; }
        public int Shield { get; set; }
        public IReadOnlyList<Keyword> Keywords { get; set; } = Array.Empty<Keyword>();
        public string AbilityText { get; set; }
        public string DesignNote { get; set; }
        public IReadOnlyList<EffectDescriptor> Effects { get; set; } = Array.Empty<EffectDescriptor>();
        public string ArtPath { get; set; }
    }
}
