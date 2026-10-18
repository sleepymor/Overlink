using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class CardCompositionTests
    {
        [Test]
        public void FromCards_CountsRarityAndFaction()
        {
            var cards = new List<CardDefinition>
            {
                MakeCard("card_fsti_001", Faction.Fsti, Rarity.Common),
                MakeCard("card_fsti_002", Faction.Fsti, Rarity.Rare),
                MakeCard("card_frti_001", Faction.Frti, Rarity.Epic),
                MakeCard("card_fpb_001", Faction.Fpb, Rarity.Legendary)
            };

            CardComposition composition = CardComposition.FromCards(cards);

            Assert.That(composition.Total, Is.EqualTo(4));
            Assert.That(composition.Common, Is.EqualTo(1));
            Assert.That(composition.Rare, Is.EqualTo(1));
            Assert.That(composition.Epic, Is.EqualTo(1));
            Assert.That(composition.Legendary, Is.EqualTo(1));
            Assert.That(composition.Fsti, Is.EqualTo(2));
            Assert.That(composition.Frti, Is.EqualTo(1));
            Assert.That(composition.Fpb, Is.EqualTo(1));
        }

        [Test]
        public void FromCards_IgnoresNullEntries()
        {
            var cards = new List<CardDefinition>
            {
                null,
                MakeCard("card_fsti_001", Faction.Fsti, Rarity.Common),
                null
            };

            CardComposition composition = CardComposition.FromCards(cards);

            Assert.That(composition.Total, Is.EqualTo(1));
            Assert.That(composition.Common, Is.EqualTo(1));
        }

        [Test]
        public void FromCards_NullPoolIsEmpty()
        {
            CardComposition composition = CardComposition.FromCards(null);

            Assert.That(composition.Total, Is.EqualTo(0));
            Assert.That(composition.CountFor(Rarity.Common), Is.EqualTo(0));
            Assert.That(composition.CountFor(Faction.Fsti), Is.EqualTo(0));
        }

        private static CardDefinition MakeCard(string id, Faction faction, Rarity rarity)
        {
            return new CardDefinition
            {
                Id = id,
                Name = id,
                Faction = faction,
                Rarity = rarity,
                Type = CardType.Unit,
                Atk = 1,
                Hp = 1,
                ArtPath = "art.png"
            };
        }
    }
}