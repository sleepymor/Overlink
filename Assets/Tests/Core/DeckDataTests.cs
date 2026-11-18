// Assets/Tests/Core/DeckDataTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class DeckDataTests
    {
        [Test]
        public void ToJson_EmptyDeck_ProducesValidJson()
        {
            var data = new DeckData
            {
                Version = 1,
                Faction = Faction.Fsti,
                Cards = new Dictionary<string, int>()
            };

            string json = DeckData.ToJson(data);

            Assert.That(json, Is.Not.Null);
            Assert.That(json, Does.Contain("\"version\":1"));
            Assert.That(json, Does.Contain("\"faction\":\"fsti\""));
            Assert.That(json, Does.Contain("\"cards\":{}"));
        }

        [Test]
        public void ToJson_DeckWithCards_SerializesCorrectly()
        {
            var data = new DeckData
            {
                Version = 1,
                Faction = Faction.Frti,
                Cards = new Dictionary<string, int>
                {
                    { "card_frti_021", 3 },
                    { "card_frti_022", 2 },
                    { "card_frti_023", 1 }
                }
            };

            string json = DeckData.ToJson(data);

            Assert.That(json, Does.Contain("\"version\":1"));
            Assert.That(json, Does.Contain("\"faction\":\"frti\""));
            Assert.That(json, Does.Contain("\"card_frti_021\":3"));
            Assert.That(json, Does.Contain("\"card_frti_022\":2"));
            Assert.That(json, Does.Contain("\"card_frti_023\":1"));
        }

        [Test]
        public void FromJson_EmptyDeck_DeserializesCorrectly()
        {
            string json = "{\"version\":1,\"faction\":\"fpb\",\"cards\":{}}";

            var data = DeckData.FromJson(json);

            Assert.That(data, Is.Not.Null);
            Assert.That(data.Version, Is.EqualTo(1));
            Assert.That(data.Faction, Is.EqualTo(Faction.Fpb));
            Assert.That(data.Cards, Is.Not.Null);
            Assert.That(data.Cards.Count, Is.EqualTo(0));
        }

        [Test]
        public void FromJson_DeckWithCards_DeserializesCorrectly()
        {
            string json = "{\"version\":1,\"faction\":\"fsti\",\"cards\":{\"card_fsti_021\":3,\"card_fsti_022\":2}}";

            var data = DeckData.FromJson(json);

            Assert.That(data, Is.Not.Null);
            Assert.That(data.Version, Is.EqualTo(1));
            Assert.That(data.Faction, Is.EqualTo(Faction.Fsti));
            Assert.That(data.Cards.Count, Is.EqualTo(2));
            Assert.That(data.Cards["card_fsti_021"], Is.EqualTo(3));
            Assert.That(data.Cards["card_fsti_022"], Is.EqualTo(2));
        }

        [Test]
        public void RoundTrip_PreservesData()
        {
            var original = new DeckData
            {
                Version = 1,
                Faction = Faction.Frti,
                Cards = new Dictionary<string, int>
                {
                    { "card_frti_021", 3 },
                    { "card_frti_025", 1 },
                    { "card_frti_030", 2 }
                }
            };

            string json = DeckData.ToJson(original);
            var deserialized = DeckData.FromJson(json);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Version, Is.EqualTo(original.Version));
            Assert.That(deserialized.Faction, Is.EqualTo(original.Faction));
            Assert.That(deserialized.Cards.Count, Is.EqualTo(original.Cards.Count));
            
            foreach (var kvp in original.Cards)
            {
                Assert.That(deserialized.Cards.ContainsKey(kvp.Key), Is.True);
                Assert.That(deserialized.Cards[kvp.Key], Is.EqualTo(kvp.Value));
            }
        }

        [Test]
        public void FromJson_NullString_ReturnsNull()
        {
            var data = DeckData.FromJson(null);
            Assert.That(data, Is.Null);
        }

        [Test]
        public void FromJson_EmptyString_ReturnsNull()
        {
            var data = DeckData.FromJson("");
            Assert.That(data, Is.Null);
        }

        [Test]
        public void FromJson_InvalidJson_ReturnsNull()
        {
            var data = DeckData.FromJson("not valid json");
            Assert.That(data, Is.Null);
        }

        [Test]
        public void FromJson_MissingFields_ReturnsDefaultValues()
        {
            // Minimal JSON with only version
            string json = "{\"version\":2}";

            var data = DeckData.FromJson(json);

            Assert.That(data, Is.Not.Null);
            Assert.That(data.Version, Is.EqualTo(2));
            Assert.That(data.Cards, Is.Not.Null);
            Assert.That(data.Cards.Count, Is.EqualTo(0));
        }

        [Test]
        public void ToJson_AllThreeFactions_SerializeCorrectly()
        {
            var fstiData = new DeckData { Faction = Faction.Fsti };
            var frtiData = new DeckData { Faction = Faction.Frti };
            var fpbData = new DeckData { Faction = Faction.Fpb };

            var fstiJson = DeckData.ToJson(fstiData);
            var frtiJson = DeckData.ToJson(frtiData);
            var fpbJson = DeckData.ToJson(fpbData);

            Assert.That(fstiJson, Does.Contain("\"faction\":\"fsti\""));
            Assert.That(frtiJson, Does.Contain("\"faction\":\"frti\""));
            Assert.That(fpbJson, Does.Contain("\"faction\":\"fpb\""));
        }

        [Test]
        public void FromJson_AllThreeFactions_DeserializeCorrectly()
        {
            var fstiData = DeckData.FromJson("{\"version\":1,\"faction\":\"fsti\",\"cards\":{}}");
            var frtiData = DeckData.FromJson("{\"version\":1,\"faction\":\"frti\",\"cards\":{}}");
            var fpbData = DeckData.FromJson("{\"version\":1,\"faction\":\"fpb\",\"cards\":{}}");

            Assert.That(fstiData.Faction, Is.EqualTo(Faction.Fsti));
            Assert.That(frtiData.Faction, Is.EqualTo(Faction.Frti));
            Assert.That(fpbData.Faction, Is.EqualTo(Faction.Fpb));
        }

        [Test]
        public void ToJson_LargeDeck_SerializesCorrectly()
        {
            var data = new DeckData
            {
                Version = 1,
                Faction = Faction.Fsti,
                Cards = new Dictionary<string, int>()
            };

            // Add 13 unique cards with 3 copies each (39 total)
            for (int i = 21; i <= 33; i++)
            {
                data.Cards[$"card_fsti_{i:000}"] = 3;
            }

            string json = DeckData.ToJson(data);
            var deserialized = DeckData.FromJson(json);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Cards.Count, Is.EqualTo(13));
            
            int totalCount = 0;
            foreach (var count in deserialized.Cards.Values)
            {
                totalCount += count;
            }
            Assert.That(totalCount, Is.EqualTo(39));
        }

        [Test]
        public void FromJson_CorruptJson_ReturnsNull()
        {
            // Missing closing brace
            var data1 = DeckData.FromJson("{\"version\":1,\"faction\":\"fsti\",\"cards\":{");
            Assert.That(data1, Is.Null);

            // Unmatched quotes
            var data2 = DeckData.FromJson("{\"version\":1,\"faction\":\"fsti,\"cards\":{}}");
            Assert.That(data2, Is.Null);

            // Invalid structure
            var data3 = DeckData.FromJson("{{\"version\":1}}");
            Assert.That(data3, Is.Null);

            // Random text
            var data4 = DeckData.FromJson("This is not JSON at all");
            Assert.That(data4, Is.Null);
        }

        [Test]
        public void FromJson_UnsupportedVersion_StillDeserializes()
        {
            // Future version should still parse (forward compatibility)
            string json = "{\"version\":99,\"faction\":\"frti\",\"cards\":{\"card_frti_001\":2}}";
            
            var data = DeckData.FromJson(json);
            
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Version, Is.EqualTo(99));
            Assert.That(data.Faction, Is.EqualTo(Faction.Frti));
            Assert.That(data.Cards["card_frti_001"], Is.EqualTo(2));
        }

        [Test]
        public void FromJson_FactionCaseSensitive_OnlyLowercaseWorks()
        {
            // Lowercase works (correct)
            var data1 = DeckData.FromJson("{\"version\":1,\"faction\":\"fsti\",\"cards\":{}}");
            Assert.That(data1, Is.Not.Null);
            Assert.That(data1.Faction, Is.EqualTo(Faction.Fsti));

            // Uppercase should fail (faction token is case-sensitive)
            var data2 = DeckData.FromJson("{\"version\":1,\"faction\":\"FSTI\",\"cards\":{}}");
            Assert.That(data2, Is.Not.Null);
            Assert.That(data2.Faction, Is.EqualTo(Faction.Fsti)); // Falls back to default from constructor

            // Mixed case should also fail
            var data3 = DeckData.FromJson("{\"version\":1,\"faction\":\"Frti\",\"cards\":{}}");
            Assert.That(data3, Is.Not.Null);
            Assert.That(data3.Faction, Is.EqualTo(Faction.Fsti)); // Falls back to default
        }

        [Test]
        public void RoundTrip_EmptyDeck_PreservesStructure()
        {
            var original = new DeckData
            {
                Version = 1,
                Faction = Faction.Fpb,
                Cards = new Dictionary<string, int>()
            };

            string json = DeckData.ToJson(original);
            var deserialized = DeckData.FromJson(json);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Version, Is.EqualTo(original.Version));
            Assert.That(deserialized.Faction, Is.EqualTo(original.Faction));
            Assert.That(deserialized.Cards, Is.Not.Null);
            Assert.That(deserialized.Cards.Count, Is.EqualTo(0));
        }

        [Test]
        public void RoundTrip_SingleCard_PreservesExactly()
        {
            var original = new DeckData
            {
                Version = 1,
                Faction = Faction.Fsti,
                Cards = new Dictionary<string, int> { { "card_fsti_042", 1 } }
            };

            string json = DeckData.ToJson(original);
            var deserialized = DeckData.FromJson(json);

            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized.Cards.Count, Is.EqualTo(1));
            Assert.That(deserialized.Cards.ContainsKey("card_fsti_042"), Is.True);
            Assert.That(deserialized.Cards["card_fsti_042"], Is.EqualTo(1));
        }

        [Test]
        public void FromJson_CardIdWithSpecialCharacters_HandlesCorrectly()
        {
            // Our card IDs only use alphanumeric and underscore, but test robustness
            string json = "{\"version\":1,\"faction\":\"frti\",\"cards\":{\"card_frti_001\":3,\"test_card\":1}}";
            
            var data = DeckData.FromJson(json);
            
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Cards.Count, Is.EqualTo(2));
            Assert.That(data.Cards["card_frti_001"], Is.EqualTo(3));
            Assert.That(data.Cards["test_card"], Is.EqualTo(1));
        }

        [Test]
        public void FromJson_ExtraWhitespace_ToleratesIt()
        {
            string json = "  {  \"version\" : 1 , \"faction\" : \"fpb\" , \"cards\" : { \"card_fpb_010\" : 2 }  }  ";
            
            var data = DeckData.FromJson(json);
            
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Version, Is.EqualTo(1));
            Assert.That(data.Faction, Is.EqualTo(Faction.Fpb));
            Assert.That(data.Cards["card_fpb_010"], Is.EqualTo(2));
        }

        [Test]
        public void FromJson_ZeroCardCount_IncludesEntry()
        {
            // Parser should include zero-count entries (even though invalid for deck rules)
            string json = "{\"version\":1,\"faction\":\"fsti\",\"cards\":{\"card_fsti_001\":0}}";
            
            var data = DeckData.FromJson(json);
            
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Cards.ContainsKey("card_fsti_001"), Is.True);
            Assert.That(data.Cards["card_fsti_001"], Is.EqualTo(0));
        }

        [Test]
        public void FromJson_NegativeCardCount_IncludesEntry()
        {
            // Parser should include negative counts (validation happens elsewhere)
            string json = "{\"version\":1,\"faction\":\"frti\",\"cards\":{\"card_frti_001\":-5}}";
            
            var data = DeckData.FromJson(json);
            
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Cards.ContainsKey("card_frti_001"), Is.True);
            Assert.That(data.Cards["card_frti_001"], Is.EqualTo(-5));
        }
    }
}
