using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class MvpCardLibraryTests
    {
        [Test]
        public void All_Returns150Cards()
        {
            var cards = MvpCardLibrary.All();

            Assert.That(cards.Count, Is.EqualTo(150));
        }

        [Test]
        public void All_IdsAreUnique()
        {
            var cards = MvpCardLibrary.All();
            var ids = cards.Select(c => c.Id).ToList();
            var uniqueIds = new HashSet<string>(ids, StringComparer.Ordinal);

            Assert.That(uniqueIds.Count, Is.EqualTo(150));
        }

        [Test]
        public void All_IdsMatchExpectedPattern()
        {
            var cards = MvpCardLibrary.All();

            foreach (var card in cards)
            {
                Assert.That(card.Id, Does.Match(@"^card_(fsti|frti|fpb)_\d{3}$"));

                // Verify faction token matches Faction enum
                string expectedToken = CardJson.FactionToken(card.Faction);
                Assert.That(card.Id, Does.Contain("_" + expectedToken + "_"));
            }
        }

        [Test]
        public void All_RarityBudgetIs75_45_22_8()
        {
            var cards = MvpCardLibrary.All();
            var composition = CardComposition.FromCards(cards);

            Assert.That(composition.Common, Is.EqualTo(75));
            Assert.That(composition.Rare, Is.EqualTo(45));
            Assert.That(composition.Epic, Is.EqualTo(22));
            Assert.That(composition.Legendary, Is.EqualTo(8));
        }

        [Test]
        public void All_CostsWithinRange()
        {
            var cards = MvpCardLibrary.All();

            foreach (var card in cards)
            {
                Assert.That(card.Cost, Is.InRange(0, CardValidator.MaxCost));
            }
        }

        [Test]
        public void All_AllThreeFactionsPresent()
        {
            var cards = MvpCardLibrary.All();
            var composition = CardComposition.FromCards(cards);

            Assert.That(composition.Fsti, Is.EqualTo(50));
            Assert.That(composition.Frti, Is.EqualTo(50));
            Assert.That(composition.Fpb, Is.EqualTo(50));
        }

        [Test]
        public void ByFaction_Fsti_HasMinimumTypeCoverage()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Fsti);

            var units = cards.Where(c => c.Type == CardType.Unit).Select(c => c.Id).Distinct().Count();
            var abilities = cards.Where(c => c.Type == CardType.Ability).Select(c => c.Id).Distinct().Count();
            var relays = cards.Where(c => c.Type == CardType.Relay).Select(c => c.Id).Distinct().Count();
            var protocols = cards.Where(c => c.Type == CardType.Protocol).Select(c => c.Id).Distinct().Count();

            Assert.That(units, Is.GreaterThanOrEqualTo(6));
            Assert.That(abilities, Is.GreaterThanOrEqualTo(2));
            Assert.That(relays, Is.GreaterThanOrEqualTo(2));
            Assert.That(protocols, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ByFaction_Frti_HasMinimumTypeCoverage()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Frti);

            var units = cards.Where(c => c.Type == CardType.Unit).Select(c => c.Id).Distinct().Count();
            var abilities = cards.Where(c => c.Type == CardType.Ability).Select(c => c.Id).Distinct().Count();
            var relays = cards.Where(c => c.Type == CardType.Relay).Select(c => c.Id).Distinct().Count();
            var protocols = cards.Where(c => c.Type == CardType.Protocol).Select(c => c.Id).Distinct().Count();

            Assert.That(units, Is.GreaterThanOrEqualTo(6));
            Assert.That(abilities, Is.GreaterThanOrEqualTo(2));
            Assert.That(relays, Is.GreaterThanOrEqualTo(2));
            Assert.That(protocols, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ByFaction_Fpb_HasMinimumTypeCoverage()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Fpb);

            var units = cards.Where(c => c.Type == CardType.Unit).Select(c => c.Id).Distinct().Count();
            var abilities = cards.Where(c => c.Type == CardType.Ability).Select(c => c.Id).Distinct().Count();
            var relays = cards.Where(c => c.Type == CardType.Relay).Select(c => c.Id).Distinct().Count();
            var protocols = cards.Where(c => c.Type == CardType.Protocol).Select(c => c.Id).Distinct().Count();

            Assert.That(units, Is.GreaterThanOrEqualTo(6));
            Assert.That(abilities, Is.GreaterThanOrEqualTo(2));
            Assert.That(relays, Is.GreaterThanOrEqualTo(2));
            Assert.That(protocols, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ValidatePool_ReturnsZeroErrors()
        {
            var cards = MvpCardLibrary.All();

            var result = CardValidator.ValidatePool(cards, checkRarityBudget: true);

            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void All_EveryCardPassesValidation()
        {
            var cards = MvpCardLibrary.All();

            foreach (var card in cards)
            {
                var result = CardValidator.Validate(card, cards);
                Assert.That(result.HasErrors, Is.False, "Card " + card.Id + " has errors: " + string.Join("; ", result.Issues.Where(i => i.Severity == Severity.Error).Select(i => i.Message)));
            }
        }

        [Test]
        public void All_IsDeterministic()
        {
            var firstCall = MvpCardLibrary.All();
            var secondCall = MvpCardLibrary.All();

            Assert.That(firstCall.Count, Is.EqualTo(secondCall.Count));

            for (int i = 0; i < firstCall.Count; i++)
            {
                Assert.That(firstCall[i].Id, Is.EqualTo(secondCall[i].Id));
                Assert.That(firstCall[i].Name, Is.EqualTo(secondCall[i].Name));
                Assert.That(firstCall[i].Cost, Is.EqualTo(secondCall[i].Cost));
                Assert.That(firstCall[i].Atk, Is.EqualTo(secondCall[i].Atk));
                Assert.That(firstCall[i].Hp, Is.EqualTo(secondCall[i].Hp));
                Assert.That(firstCall[i].Shield, Is.EqualTo(secondCall[i].Shield));
                Assert.That(firstCall[i].Rarity, Is.EqualTo(secondCall[i].Rarity));
                Assert.That(firstCall[i].Type, Is.EqualTo(secondCall[i].Type));
                Assert.That(firstCall[i].Faction, Is.EqualTo(secondCall[i].Faction));
            }
        }

        [Test]
        public void ByFaction_Fsti_Returns50Cards()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Fsti);

            Assert.That(cards.Count, Is.EqualTo(50));
        }

        [Test]
        public void ByFaction_Frti_Returns50Cards()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Frti);

            Assert.That(cards.Count, Is.EqualTo(50));
        }

        [Test]
        public void ByFaction_Fpb_Returns50Cards()
        {
            var cards = MvpCardLibrary.ByFaction(Faction.Fpb);

            Assert.That(cards.Count, Is.EqualTo(50));
        }

        [Test]
        public void ByFaction_OnlyReturnsCardsOfRequestedFaction()
        {
            foreach (Faction faction in new[] { Faction.Fsti, Faction.Frti, Faction.Fpb })
            {
                var cards = MvpCardLibrary.ByFaction(faction);

                foreach (var card in cards)
                {
                    Assert.That(card.Faction, Is.EqualTo(faction));
                }
            }
        }
    }
}
