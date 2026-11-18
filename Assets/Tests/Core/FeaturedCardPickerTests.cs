using System;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class FeaturedCardPickerTests
    {
        [Test]
        public void PickIndex_ReturnsRandomValueWithinRange()
        {
            int requested = -1;
            int index = FeaturedCardPicker.PickIndex(3, n => { requested = n; return 2; });

            Assert.That(requested, Is.EqualTo(3));
            Assert.That(index, Is.EqualTo(2));
        }

        [Test]
        public void PickIndex_OutOfRangeRandom_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => FeaturedCardPicker.PickIndex(3, n => n));
        }

        [Test]
        public void PickIndex_ZeroCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FeaturedCardPicker.PickIndex(0, n => 0));
        }

        [Test]
        public void ShortFactionToken_MatchesFactionCodes()
        {
            Assert.That(FeaturedCardPicker.ShortFactionToken(Faction.Fsti), Is.EqualTo("FSTI"));
            Assert.That(FeaturedCardPicker.ShortFactionToken(Faction.Frti), Is.EqualTo("FRTI"));
            Assert.That(FeaturedCardPicker.ShortFactionToken(Faction.Fpb), Is.EqualTo("FPB"));
        }

        [Test]
        public void FullFactionName_MatchesFactionNames()
        {
            Assert.That(FeaturedCardPicker.FullFactionName(Faction.Fsti), Is.EqualTo("Frontier Science and Technology Institute"));
            Assert.That(FeaturedCardPicker.FullFactionName(Faction.Frti), Is.EqualTo("Frontier Research and Technology Institute"));
            Assert.That(FeaturedCardPicker.FullFactionName(Faction.Fpb), Is.EqualTo("Frontier Planning Bureau"));
        }
    }
}
