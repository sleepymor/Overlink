using System.Collections.Generic;
using System.Linq;
using Overlink.Core;

namespace Overlink.Match
{
    /// <summary>
    /// Builds a legal single-faction match deck from the card pool: 30 cards, at most 3 copies of any card.
    /// The pool is taken in its registered order, so the same pool always gives the same deck.
    /// </summary>
    public static class MatchDeckBuilder
    {
        public const int DeckSize = 30;
        public const int CopyLimit = 3;

        public static List<CardDefinition> BuildStarterDeck(IEnumerable<CardDefinition> pool, Faction faction)
        {
            var source = pool.Where(c => c != null && c.Faction == faction).ToList();
            var deck = new List<CardDefinition>(DeckSize);
            if (source.Count == 0) return deck;

            int copies = 0;
            int index = 0;
            while (deck.Count < DeckSize)
            {
                deck.Add(source[index]);
                copies++;
                if (copies >= CopyLimit)
                {
                    copies = 0;
                    index = (index + 1) % source.Count;
                }
            }

            return deck;
        }
    }
}
