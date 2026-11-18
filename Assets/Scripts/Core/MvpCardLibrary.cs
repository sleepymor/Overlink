using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    /// <summary>
    /// MVP card pool access. The pool is authored as CardAsset files in Unity; Core has no engine references,
    /// so the Cards assembly hands the converted definitions in through <see cref="Register"/>.
    /// Expected shape: 150 cards (75 Common / 45 Rare / 22 Epic / 8 Legendary), 50 per faction.
    /// </summary>
    public static class MvpCardLibrary
    {
        private static IReadOnlyList<CardDefinition> s_all;

        public static bool IsRegistered => s_all != null;

        /// <summary>
        /// Replaces the pool. Null entries are dropped.
        /// </summary>
        public static void Register(IEnumerable<CardDefinition> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            var list = new List<CardDefinition>();
            foreach (CardDefinition card in cards)
            {
                if (card != null)
                {
                    list.Add(card);
                }
            }

            s_all = list;
        }

        /// <summary>
        /// Returns all cards in the MVP pool. Throws if no pool has been registered yet.
        /// </summary>
        public static IReadOnlyList<CardDefinition> All()
        {
            if (s_all == null)
            {
                throw new InvalidOperationException("MvpCardLibrary has no card pool registered. CardLibraryBootstrap registers Resources/Cards/CardCatalog at runtime.");
            }

            return s_all;
        }

        /// <summary>
        /// Returns all cards belonging to the specified faction.
        /// </summary>
        public static IReadOnlyList<CardDefinition> ByFaction(Faction faction)
        {
            var result = new List<CardDefinition>();
            foreach (var card in All())
            {
                if (card.Faction == faction)
                {
                    result.Add(card);
                }
            }
            return result;
        }
    }
}
