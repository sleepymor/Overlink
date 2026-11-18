using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class DeckValidatorTests
    {
        [Test]
        public void NullDeck_ReturnsError()
        {
            var result = DeckValidator.Validate(null);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Field == "Deck"), Is.True);
        }

        [Test]
        public void EmptyDeck_ReturnsError()
        {
            var deck = new List<CardDefinition>();

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("below the minimum")), Is.True);
        }

        [Test]
        public void DeckWith29Cards_ReturnsError()
        {
            var deck = BuildLegalDeck(Faction.Fsti, 29);

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("29") && i.Message.Contains("below the minimum")), Is.True);
        }

        [Test]
        public void DeckWith30Cards_NoErrors()
        {
            var deck = BuildLegalDeck(Faction.Fsti, 30);

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void DeckWith40Cards_NoErrors()
        {
            var deck = BuildLegalDeck(Faction.Fsti, 40);

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void DeckWith41Cards_ReturnsError()
        {
            var deck = BuildLegalDeck(Faction.Fsti, 41);

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("41") && i.Message.Contains("exceeds the maximum")), Is.True);
        }

        [Test]
        public void DeckWithFourCopiesOfOneCard_ReturnsError()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Fsti);
            var deck = new List<CardDefinition>();

            // Add the first card 4 times
            for (int i = 0; i < 4; i++)
            {
                deck.Add(cards[0]);
            }

            // Fill the rest to reach 30
            for (int i = 1; i < cards.Count && deck.Count < 30; i++)
            {
                for (int copy = 0; copy < 3 && deck.Count < 30; copy++)
                {
                    deck.Add(cards[i]);
                }
            }

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains(cards[0].Id) && i.Message.Contains("4 times")), Is.True);
        }

        [Test]
        public void DeckWithMixedFactions_ReturnsError()
        {
            var fstiCards = MvpCardLibrary.ByFaction(Faction.Fsti);
            var frtiCards = MvpCardLibrary.ByFaction(Faction.Frti);
            var deck = new List<CardDefinition>();

            // Mix Fsti and Frti cards
            for (int i = 0; i < 15; i++)
            {
                deck.Add(fstiCards[i]);
            }
            for (int i = 0; i < 15; i++)
            {
                deck.Add(frtiCards[i]);
            }

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("multiple factions")), Is.True);
        }

        [Test]
        public void SingleCardDeck_ReturnsError()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Fsti);
            var deck = new List<CardDefinition> { cards[0] };

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("below the minimum")), Is.True);
        }

        [Test]
        public void DeckWithNullEntry_ReturnsError()
        {
            var deck = BuildLegalDeck(Faction.Fsti, 30);
            deck[15] = null; // Inject a null card

            var result = DeckValidator.Validate(deck);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Field.Contains("[15]")), Is.True);
        }

        [Test]
        public void ValidateCounts_WithUnknownId_ReturnsError()
        {
            var counts = new Dictionary<string, int>
            {
                { "card_fsti_021", 3 },
                { "unknown_card_id", 2 }
            };

            var result = DeckValidator.ValidateCounts(counts, id =>
            {
                if (id == "card_fsti_021")
                {
                    return MvpCardLibrary.All().First(c => c.Id == id);
                }
                return null;
            });

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("unknown_card_id") && i.Message.Contains("could not be resolved")), Is.True);
        }

        [Test]
        public void ValidateCounts_WithZeroCount_ReturnsError()
        {
            var counts = new Dictionary<string, int>
            {
                { "card_fsti_021", 0 }
            };

            var result = DeckValidator.ValidateCounts(counts, id =>
            {
                return MvpCardLibrary.All().FirstOrDefault(c => c.Id == id);
            });

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("invalid count")), Is.True);
        }

        [Test]
        public void ValidateCounts_WithNegativeCount_ReturnsError()
        {
            var counts = new Dictionary<string, int>
            {
                { "card_fsti_021", -1 }
            };

            var result = DeckValidator.ValidateCounts(counts, id =>
            {
                return MvpCardLibrary.All().FirstOrDefault(c => c.Id == id);
            });

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Issues.Any(i => i.Severity == Severity.Error && i.Message.Contains("invalid count")), Is.True);
        }

        [Test]
        public void ValidateCounts_WithLegal30CardDeck_NoErrors()
        {
            var fstiCards = MvpCardLibrary.ByFaction(Faction.Fsti);
            var counts = new Dictionary<string, int>();

            // Build a legal 30-card deck: 10 cards x 3 copies each
            for (int i = 0; i < 10 && i < fstiCards.Count; i++)
            {
                counts[fstiCards[i].Id] = 3;
            }

            var result = DeckValidator.ValidateCounts(counts, id =>
            {
                return MvpCardLibrary.All().FirstOrDefault(c => c.Id == id);
            });

            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void ValidateCounts_WithNullCounts_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DeckValidator.ValidateCounts(null, id => null);
            });
        }

        [Test]
        public void ValidateCounts_WithNullLookup_ThrowsArgumentNullException()
        {
            var counts = new Dictionary<string, int>();

            Assert.Throws<ArgumentNullException>(() =>
            {
                DeckValidator.ValidateCounts(counts, null);
            });
        }

        private List<CardDefinition> BuildLegalDeck(Faction faction, int size)
        {
            var cards = MvpCardLibrary.ByFaction(faction);
            var deck = new List<CardDefinition>();

            int cardIndex = 0;
            int copies = 0;

            while (deck.Count < size && cardIndex < cards.Count)
            {
                deck.Add(cards[cardIndex]);
                copies++;

                if (copies >= 3)
                {
                    cardIndex++;
                    copies = 0;
                }
            }

            return deck;
        }
    }
}
