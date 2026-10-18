using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    [TestFixture]
    public class CardJsonTests
    {
        [Test]
        public void Serialize_WritesFormatVersion()
        {
            string json = CardJson.Serialize(new List<CardDefinition>(), null);

            StringAssert.Contains("\"formatVersion\":" + CardJson.FormatVersion, json);
        }

        [Test]
        public void Serialize_OrdersCardsById()
        {
            var cards = new List<CardDefinition>
            {
                MakeCard("card_fsti_002"),
                MakeCard("card_fsti_001")
            };

            string json = CardJson.Serialize(cards, null);

            int first = json.IndexOf("card_fsti_001", StringComparison.Ordinal);
            int second = json.IndexOf("card_fsti_002", StringComparison.Ordinal);

            Assert.That(first, Is.GreaterThanOrEqualTo(0));
            Assert.That(second, Is.GreaterThan(first));
        }

        [Test]
        public void Serialize_WritesFactionAsLowercaseToken()
        {
            var cards = new List<CardDefinition> { MakeCard("card_fsti_001") };

            string json = CardJson.Serialize(cards, null);

            StringAssert.Contains("\"faction\":\"fsti\"", json);
            StringAssert.Contains("\"rarity\":\"Common\"", json);
            StringAssert.Contains("\"type\":\"Unit\"", json);
            StringAssert.Contains("\"target\":\"EnemyActive\"", json);
        }

        [Test]
        public void Serialize_AllFactionTokens()
        {
            Assert.That(CardJson.FactionToken(Faction.Fsti), Is.EqualTo("fsti"));
            Assert.That(CardJson.FactionToken(Faction.Frti), Is.EqualTo("frti"));
            Assert.That(CardJson.FactionToken(Faction.Fpb), Is.EqualTo("fpb"));
        }

        [Test]
        public void TryParseFactionToken_AllTokens()
        {
            Assert.That(CardJson.TryParseFactionToken("fsti", out Faction f1), Is.True);
            Assert.That(f1, Is.EqualTo(Faction.Fsti));

            Assert.That(CardJson.TryParseFactionToken("frti", out Faction f2), Is.True);
            Assert.That(f2, Is.EqualTo(Faction.Frti));

            Assert.That(CardJson.TryParseFactionToken("fpb", out Faction f3), Is.True);
            Assert.That(f3, Is.EqualTo(Faction.Fpb));

            Assert.That(CardJson.TryParseFactionToken("Fsti", out _), Is.False);
            Assert.That(CardJson.TryParseFactionToken("unknown", out _), Is.False);
            Assert.That(CardJson.TryParseFactionToken("", out _), Is.False);
        }

        [Test]
        public void Serialize_WritesCompositionCounts()
        {
            var cards = new List<CardDefinition> { MakeCard("card_fsti_001") };

            string json = CardJson.Serialize(cards, null);

            StringAssert.Contains("\"total\":1", json);
            StringAssert.Contains("\"common\":1", json);
            StringAssert.Contains("\"fsti\":1", json);
        }

        [Test]
        public void Serialize_EscapesQuotesAndBackslashes()
        {
            CardDefinition card = MakeCard("card_fsti_001");
            card.Name = "Quote \" and backslash \\";

            string json = CardJson.Serialize(new List<CardDefinition> { card }, null);

            StringAssert.Contains("Quote \\\" and backslash \\\\", json);
        }

        [Test]
        public void Serialize_EmptyPoolIsValidShape()
        {
            string json = CardJson.Serialize(new List<CardDefinition>(), null);

            StringAssert.Contains("\"cards\":[]", json);
            StringAssert.Contains("\"total\":0", json);
        }

        [Test]
        public void Serialize_NullPoolThrows()
        {
            Assert.Throws<ArgumentNullException>(() => CardJson.Serialize(null, null));
        }

        [Test]
        public void Escape_BackslashAndQuote()
        {
            Assert.That(CardJson.Escape("a\\b"), Is.EqualTo("a\\\\b"));
            Assert.That(CardJson.Escape("a\"b"), Is.EqualTo("a\\\"b"));
        }

        [Test]
        public void Escape_Backspace()
        {
            Assert.That(CardJson.Escape("a\bb"), Is.EqualTo("a\\bb"));
        }

        [Test]
        public void Escape_Formfeed()
        {
            Assert.That(CardJson.Escape("a\fb"), Is.EqualTo("a\\fb"));
        }

        [Test]
        public void Escape_Newline()
        {
            Assert.That(CardJson.Escape("a\nb"), Is.EqualTo("a\\nb"));
        }

        [Test]
        public void Escape_CarriageReturn()
        {
            Assert.That(CardJson.Escape("a\rb"), Is.EqualTo("a\\rb"));
        }

        [Test]
        public void Escape_Tab()
        {
            Assert.That(CardJson.Escape("a\tb"), Is.EqualTo("a\\tb"));
        }

        [Test]
        public void Escape_AllControlCharacters_U0000_to_U001F()
        {
            for (int i = 0; i <= 0x1F; i++)
            {
                char c = (char)i;
                string escaped = CardJson.Escape(c.ToString());

                switch (c)
                {
                    case '\b': Assert.That(escaped, Is.EqualTo("\\b"), "U+" + i.ToString("X4")); break;
                    case '\f': Assert.That(escaped, Is.EqualTo("\\f"), "U+" + i.ToString("X4")); break;
                    case '\n': Assert.That(escaped, Is.EqualTo("\\n"), "U+" + i.ToString("X4")); break;
                    case '\r': Assert.That(escaped, Is.EqualTo("\\r"), "U+" + i.ToString("X4")); break;
                    case '\t': Assert.That(escaped, Is.EqualTo("\\t"), "U+" + i.ToString("X4")); break;
                    default:
                        string expected = "\\u" + i.ToString("x4");
                        Assert.That(escaped, Is.EqualTo(expected), "U+" + i.ToString("X4"));
                        break;
                }
            }
        }

        [Test]
        public void Escape_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(CardJson.Escape(null), Is.EqualTo(string.Empty));
            Assert.That(CardJson.Escape(""), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Escape_PrintableAscii_PassesThrough()
        {
            Assert.That(CardJson.Escape("Hello World!"), Is.EqualTo("Hello World!"));
        }

        [Test]
        public void Parse_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => CardJson.Parse(null));
        }

        [Test]
        public void Parse_EmptyCards()
        {
            string json = CardJson.Serialize(new List<CardDefinition>(), null);
            var result = CardJson.Parse(json);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Parse_MalformedJson_Throws()
        {
            Assert.Throws<FormatException>(() => CardJson.Parse("{broken"));
            Assert.Throws<FormatException>(() => CardJson.Parse(""));
            Assert.Throws<FormatException>(() => CardJson.Parse("{\"formatVersion\":1,\"cards\":[{\"faction\":\"Unknown\"}]}"));
        }

        [Test]
        public void Parse_FutureVersion_Throws()
        {
            string json = "{\"formatVersion\":999,\"cards\":[]}";
            var ex = Assert.Throws<FormatException>(() => CardJson.Parse(json));
            StringAssert.Contains("999", ex.Message);
        }

        [Test]
        public void Parse_MissingFormatVersion_Throws()
        {
            Assert.Throws<FormatException>(() => CardJson.Parse("{\"cards\":[]}"));
        }

        [Test]
        public void Parse_UnknownEnum_Throws()
        {
            string json = "{\"formatVersion\":1,\"cards\":[{\"id\":\"x\",\"name\":\"x\",\"faction\":\"fsti\",\"rarity\":\"Mythic\",\"type\":\"Unit\",\"cost\":0,\"atk\":0,\"hp\":0,\"shield\":0,\"keywords\":[],\"abilityText\":null,\"designNote\":null,\"effects\":[],\"artPath\":null}]}";
            var ex = Assert.Throws<FormatException>(() => CardJson.Parse(json));
            StringAssert.Contains("Mythic", ex.Message);
        }

        [Test]
        public void Parse_Roundtrip_SingleCard()
        {
            var original = new List<CardDefinition> { MakeCard("card_fsti_001") };
            string json = CardJson.Serialize(original, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed.Count, Is.EqualTo(1));
            AssertCardEqual(original[0], parsed[0]);
        }

        [Test]
        public void Parse_Roundtrip_MultipleCards()
        {
            var original = new List<CardDefinition>
            {
                MakeCard("card_fsti_001"),
                MakeCard("card_frti_002", Faction.Frti),
                MakeCard("card_fpb_003", Faction.Fpb)
            };

            string json = CardJson.Serialize(original, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed.Count, Is.EqualTo(3));
            for (int i = 0; i < original.Count; i++)
            {
                var orig = original.OrderBy(c => c.Id, StringComparer.Ordinal).ToList()[i];
                AssertCardEqual(orig, parsed[i]);
            }
        }

        [Test]
        public void Parse_Roundtrip_AllEffectFields()
        {
            var card = MakeCard("card_fsti_001");
            card.Effects = new[]
            {
                new EffectDescriptor(EffectType.Summon, TargetKind.AlliedActive, 1, 2, -1, "card_fsti_002", true)
            };
            card.Keywords = new[] { Keyword.Fast };

            var original = new List<CardDefinition> { card };
            string json = CardJson.Serialize(original, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed.Count, Is.EqualTo(1));
            var e = parsed[0].Effects[0];
            Assert.That(e.Type, Is.EqualTo(EffectType.Summon));
            Assert.That(e.Target, Is.EqualTo(TargetKind.AlliedActive));
            Assert.That(e.Amount, Is.EqualTo(1));
            Assert.That(e.Amount2, Is.EqualTo(2));
            Assert.That(e.Duration, Is.EqualTo(-1));
            Assert.That(e.ReferenceId, Is.EqualTo("card_fsti_002"));
            Assert.That(e.Fast, Is.True);
        }

        [Test]
        public void Parse_Roundtrip_NullStrings()
        {
            var card = MakeCard("card_fsti_001");
            card.AbilityText = null;
            card.DesignNote = null;
            card.ArtPath = null;

            string json = CardJson.Serialize(new List<CardDefinition> { card }, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed[0].AbilityText, Is.Null);
            Assert.That(parsed[0].DesignNote, Is.Null);
            Assert.That(parsed[0].ArtPath, Is.Null);
        }

        [Test]
        public void Parse_Roundtrip_ControlCharactersAndQuotes()
        {
            var card = MakeCard("card_fsti_001");
            card.Name = "Tab\there\nnewline";
            card.AbilityText = "Quote\"mid and backslash\\end";
            card.DesignNote = "NUL\u0000BEL\u0007BS\bFF\fCR\rESC\u001b";

            string json = CardJson.Serialize(new List<CardDefinition> { card }, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed[0].Name, Is.EqualTo(card.Name));
            Assert.That(parsed[0].AbilityText, Is.EqualTo(card.AbilityText));
            Assert.That(parsed[0].DesignNote, Is.EqualTo(card.DesignNote));
        }

        [Test]
        public void Parse_Roundtrip_AllControlCharacters()
        {
            for (int i = 0; i <= 0x1F; i++)
            {
                var card = MakeCard("card_fsti_001");
                card.DesignNote = "x" + (char)i + "y";

                string json = CardJson.Serialize(new List<CardDefinition> { card }, null);
                var parsed = CardJson.Parse(json);

                Assert.That(parsed[0].DesignNote, Is.EqualTo(card.DesignNote),
                    "Roundtrip failed for U+" + i.ToString("X4"));
            }
        }

        [Test]
        public void Parse_Roundtrip_EmptyEffectsAndKeywords()
        {
            var card = MakeCard("card_fsti_001");
            card.Effects = Array.Empty<EffectDescriptor>();
            card.Keywords = Array.Empty<Keyword>();

            string json = CardJson.Serialize(new List<CardDefinition> { card }, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed[0].Effects.Count, Is.EqualTo(0));
            Assert.That(parsed[0].Keywords.Count, Is.EqualTo(0));
        }

        [Test]
        public void Parse_Roundtrip_NullReferenceId()
        {
            var card = MakeCard("card_fsti_001");
            card.Effects = new[]
            {
                new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, 1, 0, 0, null, false)
            };

            string json = CardJson.Serialize(new List<CardDefinition> { card }, null);
            var parsed = CardJson.Parse(json);

            Assert.That(parsed[0].Effects[0].ReferenceId, Is.Null);
        }

        [Test]
        public void Parse_Roundtrip_AllFactions()
        {
            foreach (Faction faction in Enum.GetValues(typeof(Faction)))
            {
                string token = CardJson.FactionToken(faction);
                var card = new CardDefinition
                {
                    Id = "card_" + token + "_001",
                    Name = "Test",
                    Faction = faction,
                    Rarity = Rarity.Common,
                    Type = CardType.Unit,
                    Cost = 1,
                    Atk = 1,
                    Hp = 1,
                    ArtPath = "art.png",
                    AbilityText = "Test."
                };

                string json = CardJson.Serialize(new List<CardDefinition> { card }, null);
                var parsed = CardJson.Parse(json);

                Assert.That(parsed[0].Faction, Is.EqualTo(faction), "Roundtrip failed for " + faction);
            }
        }

        private static void AssertCardEqual(CardDefinition expected, CardDefinition actual)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Faction, Is.EqualTo(expected.Faction));
            Assert.That(actual.Rarity, Is.EqualTo(expected.Rarity));
            Assert.That(actual.Type, Is.EqualTo(expected.Type));
            Assert.That(actual.Cost, Is.EqualTo(expected.Cost));
            Assert.That(actual.Atk, Is.EqualTo(expected.Atk));
            Assert.That(actual.Hp, Is.EqualTo(expected.Hp));
            Assert.That(actual.Shield, Is.EqualTo(expected.Shield));
            Assert.That(actual.AbilityText, Is.EqualTo(expected.AbilityText));
            Assert.That(actual.DesignNote, Is.EqualTo(expected.DesignNote));
            Assert.That(actual.ArtPath, Is.EqualTo(expected.ArtPath));

            Assert.That(actual.Keywords.Count, Is.EqualTo(expected.Keywords?.Count ?? 0));
            for (int i = 0; i < (expected.Keywords?.Count ?? 0); i++)
            {
                Assert.That(actual.Keywords[i], Is.EqualTo(expected.Keywords[i]));
            }

            Assert.That(actual.Effects.Count, Is.EqualTo(expected.Effects?.Count ?? 0));
            for (int i = 0; i < (expected.Effects?.Count ?? 0); i++)
            {
                Assert.That(actual.Effects[i].Type, Is.EqualTo(expected.Effects[i].Type));
                Assert.That(actual.Effects[i].Target, Is.EqualTo(expected.Effects[i].Target));
                Assert.That(actual.Effects[i].Amount, Is.EqualTo(expected.Effects[i].Amount));
                Assert.That(actual.Effects[i].Amount2, Is.EqualTo(expected.Effects[i].Amount2));
                Assert.That(actual.Effects[i].Duration, Is.EqualTo(expected.Effects[i].Duration));
                Assert.That(actual.Effects[i].ReferenceId, Is.EqualTo(expected.Effects[i].ReferenceId));
                Assert.That(actual.Effects[i].Fast, Is.EqualTo(expected.Effects[i].Fast));
            }
        }

        private static CardDefinition MakeCard(string id, Faction faction = Faction.Fsti)
        {
            return new CardDefinition
            {
                Id = id,
                Name = "Test Card",
                Faction = faction,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 1,
                Atk = 1,
                Hp = 1,
                AbilityText = "Test.",
                ArtPath = "art.png",
                Effects = new[]
                {
                    new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, amount: 1)
                }
            };
        }
    }
}
