using System;
using System.Collections.Generic;

namespace Overlink.Core
{
    /// <summary>A validated, detached copy of a deck for a future match.</summary>
    public sealed class BattleDeckSnapshot
    {
        readonly DeckData deck;

        BattleDeckSnapshot(DeckData deck) { this.deck = deck; }

        /// <summary>
        /// Validates every card and the declared faction before capturing. A rejected draft
        /// cannot replace an earlier snapshot. Ownership must be checked by the caller.
        /// </summary>
        public static bool TryCreate(DeckData candidate, Func<string, CardDefinition> lookup,
            out BattleDeckSnapshot snapshot)
        {
            snapshot = null;
            if (candidate?.Cards == null || lookup == null) return false;

            var definitions = new Dictionary<string, CardDefinition>();
            foreach (var entry in candidate.Cards)
            {
                // Bound work before expanding counts in DeckValidator.ValidateCounts.
                if (string.IsNullOrEmpty(entry.Key) || entry.Value < 1 ||
                    entry.Value > DeckValidator.MaxCopiesPerCard) return false;
                CardDefinition card = lookup(entry.Key);
                if (card == null || card.Id != entry.Key || card.Faction != candidate.Faction) return false;
                definitions.Add(entry.Key, card);
            }
            if (DeckValidator.ValidateCounts(candidate.Cards, id => definitions[id]).HasErrors) return false;

            snapshot = new BattleDeckSnapshot(Copy(candidate));
            return true;
        }

        /// <summary>Returns a new copy; callers cannot mutate the stored snapshot.</summary>
        public DeckData ToDeckData() => Copy(deck);

        static DeckData Copy(DeckData source) => new DeckData
        {
            Version = source.Version,
            Faction = source.Faction,
            Cards = new Dictionary<string, int>(source.Cards)
        };
    }
}
