using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlink.Core
{
    /// <summary>
    /// Validates deck composition rules for MVP: 30-40 cards, max 3 copies per card, single faction.
    /// </summary>
    public static class DeckValidator
    {
        /// <summary>Minimum deck size for constructed decks.</summary>
        public const int MinDeckSize = 30;

        /// <summary>Maximum deck size for constructed decks.</summary>
        public const int MaxDeckSize = 40;

        /// <summary>Maximum copies of any single card allowed in a deck.</summary>
        public const int MaxCopiesPerCard = 3;

        /// <summary>
        /// Validates a deck represented as a flat list where each entry is one copy of a card.
        /// </summary>
        /// <param name="deck">The deck to validate, one entry per copy.</param>
        /// <returns>A validation result with errors for any rule violations.</returns>
        public static ValidationResult Validate(IEnumerable<CardDefinition> deck)
        {
            var issues = new List<ValidationIssue>();

            if (deck == null)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Deck", "Deck cannot be null."));
                return new ValidationResult(issues);
            }

            var list = deck.ToList();

            // Check for null entries
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                {
                    issues.Add(new ValidationIssue(Severity.Error, "Deck[" + i + "]", "Deck contains null entry at index " + i + "."));
                }
            }

            var nonNullCards = list.Where(c => c != null).ToList();
            int totalCount = nonNullCards.Count;

            // Check size constraints
            if (totalCount < MinDeckSize)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Deck", "Deck size " + totalCount + " is below the minimum of " + MinDeckSize + "."));
            }

            if (totalCount > MaxDeckSize)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Deck", "Deck size " + totalCount + " exceeds the maximum of " + MaxDeckSize + "."));
            }

            // Check copy count per card
            var copyCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var card in nonNullCards)
            {
                string id = card.Id ?? string.Empty;
                if (!copyCount.ContainsKey(id))
                {
                    copyCount[id] = 0;
                }
                copyCount[id]++;
            }

            foreach (var kvp in copyCount)
            {
                if (kvp.Value > MaxCopiesPerCard)
                {
                    issues.Add(new ValidationIssue(Severity.Error, "Deck", "Card " + kvp.Key + " appears " + kvp.Value + " times, exceeding the maximum of " + MaxCopiesPerCard + " copies."));
                }
            }

            // Check single-faction constraint
            var factions = new HashSet<Faction>();
            foreach (var card in nonNullCards)
            {
                factions.Add(card.Faction);
            }

            if (factions.Count > 1)
            {
                var factionList = string.Join(", ", factions.OrderBy(f => f.ToString()));
                issues.Add(new ValidationIssue(Severity.Error, "Deck", "Deck contains multiple factions: " + factionList + ". Only single-faction decks are allowed."));
            }

            return new ValidationResult(issues);
        }

        /// <summary>
        /// Validates a deck represented as counts per card ID.
        /// </summary>
        /// <param name="counts">A collection of (CardId, Count) pairs.</param>
        /// <param name="lookup">Function to resolve a card ID to its definition.</param>
        /// <returns>A validation result with errors for any rule violations.</returns>
        public static ValidationResult ValidateCounts(IEnumerable<KeyValuePair<string, int>> counts, Func<string, CardDefinition> lookup)
        {
            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }

            if (lookup == null)
            {
                throw new ArgumentNullException(nameof(lookup));
            }

            var issues = new List<ValidationIssue>();
            var countList = counts.ToList();

            int totalCount = 0;
            var resolvedCards = new List<CardDefinition>();

            foreach (var kvp in countList)
            {
                string id = kvp.Key;
                int count = kvp.Value;

                if (count <= 0)
                {
                    issues.Add(new ValidationIssue(Severity.Error, "Counts", "Card " + id + " has invalid count " + count + ". Count must be positive."));
                    continue;
                }

                CardDefinition card = lookup(id);
                if (card == null)
                {
                    issues.Add(new ValidationIssue(Severity.Error, "Counts", "Card " + id + " could not be resolved."));
                    continue;
                }

                totalCount += count;

                // Check copy limit
                if (count > MaxCopiesPerCard)
                {
                    issues.Add(new ValidationIssue(Severity.Error, "Counts", "Card " + id + " appears " + count + " times, exceeding the maximum of " + MaxCopiesPerCard + " copies."));
                }

                // Add card to resolved list for faction check
                for (int i = 0; i < count; i++)
                {
                    resolvedCards.Add(card);
                }
            }

            // Check size constraints
            if (totalCount < MinDeckSize)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Counts", "Deck size " + totalCount + " is below the minimum of " + MinDeckSize + "."));
            }

            if (totalCount > MaxDeckSize)
            {
                issues.Add(new ValidationIssue(Severity.Error, "Counts", "Deck size " + totalCount + " exceeds the maximum of " + MaxDeckSize + "."));
            }

            // Check single-faction constraint
            var factions = new HashSet<Faction>();
            foreach (var card in resolvedCards)
            {
                factions.Add(card.Faction);
            }

            if (factions.Count > 1)
            {
                var factionList = string.Join(", ", factions.OrderBy(f => f.ToString()));
                issues.Add(new ValidationIssue(Severity.Error, "Counts", "Deck contains multiple factions: " + factionList + ". Only single-faction decks are allowed."));
            }

            return new ValidationResult(issues);
        }
    }
}
