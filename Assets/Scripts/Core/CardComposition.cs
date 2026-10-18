using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    public sealed class CardComposition
    {
        public int Total { get; private set; }
        public int Common { get; private set; }
        public int Rare { get; private set; }
        public int Epic { get; private set; }
        public int Legendary { get; private set; }
        public int Fsti { get; private set; }
        public int Frti { get; private set; }
        public int Fpb { get; private set; }

        public static CardComposition FromCards(IEnumerable<CardDefinition> cards)
        {
            var composition = new CardComposition();
            if (cards == null)
            {
                return composition;
            }

            foreach (var card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                composition.Total++;

                switch (card.Rarity)
                {
                    case Rarity.Common: composition.Common++; break;
                    case Rarity.Rare: composition.Rare++; break;
                    case Rarity.Epic: composition.Epic++; break;
                    case Rarity.Legendary: composition.Legendary++; break;
                }

                switch (card.Faction)
                {
                    case Faction.Fsti: composition.Fsti++; break;
                    case Faction.Frti: composition.Frti++; break;
                    case Faction.Fpb: composition.Fpb++; break;
                }
            }

            return composition;
        }

        public int CountFor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return Common;
                case Rarity.Rare: return Rare;
                case Rarity.Epic: return Epic;
                case Rarity.Legendary: return Legendary;
                default: return 0;
            }
        }

        public int CountFor(Faction faction)
        {
            switch (faction)
            {
                case Faction.Fsti: return Fsti;
                case Faction.Frti: return Frti;
                case Faction.Fpb: return Fpb;
                default: return 0;
            }
        }
    }
}