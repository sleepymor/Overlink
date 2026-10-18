using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Overlink.Core
{
    public static class CardJson
    {
        public const int FormatVersion = 1;

        private const string Quote = "\"";

        public static string FactionToken(Faction faction)
        {
            switch (faction)
            {
                case Faction.Fsti: return "fsti";
                case Faction.Frti: return "frti";
                case Faction.Fpb: return "fpb";
                default: throw new ArgumentOutOfRangeException(nameof(faction), faction, "Unknown faction.");
            }
        }

        public static bool TryParseFactionToken(string token, out Faction faction)
        {
            if (token == "fsti") { faction = Faction.Fsti; return true; }
            if (token == "frti") { faction = Faction.Frti; return true; }
            if (token == "fpb") { faction = Faction.Fpb; return true; }
            faction = default;
            return false;
        }

        public static string Serialize(IEnumerable<CardDefinition> cards, CardComposition composition)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            var ordered = cards.Where(c => c != null)
                .OrderBy(c => c.Id ?? string.Empty, StringComparer.Ordinal)
                .ToList();

            if (composition == null)
            {
                composition = CardComposition.FromCards(ordered);
            }

            var builder = new StringBuilder(4096);
            builder.Append("{");
            AppendNumber(builder, "formatVersion", FormatVersion, true);
            builder.Append(",");
            AppendKey(builder, "cards");
            builder.Append("[");

            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                AppendCard(builder, ordered[i]);
            }

            builder.Append("]");
            builder.Append(",");
            AppendKey(builder, "composition");
            AppendComposition(builder, composition);
            builder.Append("}");
            return builder.ToString();
        }

        public static IReadOnlyList<CardDefinition> Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            int pos = 0;
            SkipWhitespace(json, ref pos);
            Expect(json, ref pos, '{');

            int version = -1;
            List<CardDefinition> cards = null;

            while (true)
            {
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == '}')
                {
                    pos++;
                    break;
                }

                if (version != -1 || cards != null)
                {
                    Expect(json, ref pos, ',');
                    SkipWhitespace(json, ref pos);
                }

                string key = ReadString(json, ref pos);
                SkipWhitespace(json, ref pos);
                Expect(json, ref pos, ':');
                SkipWhitespace(json, ref pos);

                if (key == "formatVersion")
                {
                    version = ReadInt(json, ref pos);
                    if (version > FormatVersion)
                    {
                        throw new FormatException("Unsupported formatVersion " + version + ". Maximum supported is " + FormatVersion + ".");
                    }
                }
                else if (key == "cards")
                {
                    cards = ParseCards(json, ref pos);
                }
                else if (key == "composition")
                {
                    SkipValue(json, ref pos);
                }
                else
                {
                    SkipValue(json, ref pos);
                }
            }

            if (version < 0)
            {
                throw new FormatException("Missing formatVersion.");
            }

            return cards ?? new List<CardDefinition>();
        }

        private static List<CardDefinition> ParseCards(string json, ref int pos)
        {
            var list = new List<CardDefinition>();
            Expect(json, ref pos, '[');
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return list;
            }

            while (true)
            {
                SkipWhitespace(json, ref pos);
                list.Add(ParseCard(json, ref pos));
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                }
                else
                {
                    break;
                }
            }

            Expect(json, ref pos, ']');
            return list;
        }

        private static CardDefinition ParseCard(string json, ref int pos)
        {
            Expect(json, ref pos, '{');
            var card = new CardDefinition();
            bool first = true;
            List<Keyword> keywords = null;
            List<EffectDescriptor> effects = null;

            while (true)
            {
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == '}')
                {
                    pos++;
                    break;
                }

                if (!first)
                {
                    Expect(json, ref pos, ',');
                    SkipWhitespace(json, ref pos);
                }
                first = false;

                string key = ReadString(json, ref pos);
                SkipWhitespace(json, ref pos);
                Expect(json, ref pos, ':');
                SkipWhitespace(json, ref pos);

                switch (key)
                {
                    case "id": card.Id = ReadNullableString(json, ref pos); break;
                    case "name": card.Name = ReadNullableString(json, ref pos); break;
                    case "faction":
                        string ft = ReadNullableString(json, ref pos);
                        if (!TryParseFactionToken(ft, out Faction faction))
                        {
                            throw new FormatException("Unknown faction token: '" + ft + "'.");
                        }
                        card.Faction = faction;
                        break;
                    case "rarity": card.Rarity = ParseEnum<Rarity>(ReadNullableString(json, ref pos)); break;
                    case "type": card.Type = ParseEnum<CardType>(ReadNullableString(json, ref pos)); break;
                    case "cost": card.Cost = ReadInt(json, ref pos); break;
                    case "atk": card.Atk = ReadInt(json, ref pos); break;
                    case "hp": card.Hp = ReadInt(json, ref pos); break;
                    case "shield": card.Shield = ReadInt(json, ref pos); break;
                    case "keywords": keywords = ParseKeywordArray(json, ref pos); break;
                    case "abilityText": card.AbilityText = ReadNullableString(json, ref pos); break;
                    case "designNote": card.DesignNote = ReadNullableString(json, ref pos); break;
                    case "effects": effects = ParseEffectArray(json, ref pos); break;
                    case "artPath": card.ArtPath = ReadNullableString(json, ref pos); break;
                    default: SkipValue(json, ref pos); break;
                }
            }

            if (keywords != null)
            {
                card.Keywords = keywords.ToArray();
            }

            if (effects != null)
            {
                card.Effects = effects.ToArray();
            }

            return card;
        }

        private static List<Keyword> ParseKeywordArray(string json, ref int pos)
        {
            var list = new List<Keyword>();
            Expect(json, ref pos, '[');
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return list;
            }

            while (true)
            {
                SkipWhitespace(json, ref pos);
                list.Add(ParseEnum<Keyword>(ReadString(json, ref pos)));
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                }
                else
                {
                    break;
                }
            }

            Expect(json, ref pos, ']');
            return list;
        }

        private static List<EffectDescriptor> ParseEffectArray(string json, ref int pos)
        {
            var list = new List<EffectDescriptor>();
            Expect(json, ref pos, '[');
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return list;
            }

            while (true)
            {
                SkipWhitespace(json, ref pos);
                list.Add(ParseEffect(json, ref pos));
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                }
                else
                {
                    break;
                }
            }

            Expect(json, ref pos, ']');
            return list;
        }

        private static EffectDescriptor ParseEffect(string json, ref int pos)
        {
            Expect(json, ref pos, '{');

            EffectType type = default;
            TargetKind target = default;
            int amount = 0;
            int amount2 = 0;
            int duration = 0;
            string referenceId = null;
            bool fast = false;
            bool first = true;

            while (true)
            {
                SkipWhitespace(json, ref pos);
                if (pos < json.Length && json[pos] == '}')
                {
                    pos++;
                    break;
                }

                if (!first)
                {
                    Expect(json, ref pos, ',');
                    SkipWhitespace(json, ref pos);
                }
                first = false;

                string key = ReadString(json, ref pos);
                SkipWhitespace(json, ref pos);
                Expect(json, ref pos, ':');
                SkipWhitespace(json, ref pos);

                switch (key)
                {
                    case "type": type = ParseEnum<EffectType>(ReadNullableString(json, ref pos)); break;
                    case "target": target = ParseEnum<TargetKind>(ReadNullableString(json, ref pos)); break;
                    case "amount": amount = ReadInt(json, ref pos); break;
                    case "amount2": amount2 = ReadInt(json, ref pos); break;
                    case "duration": duration = ReadInt(json, ref pos); break;
                    case "referenceId": referenceId = ReadNullableString(json, ref pos); break;
                    case "fast": fast = ReadBool(json, ref pos); break;
                    default: SkipValue(json, ref pos); break;
                }
            }

            return new EffectDescriptor(type, target, amount, amount2, duration, referenceId, fast);
        }

        private static T ParseEnum<T>(string name) where T : struct
        {
            if (name == null)
            {
                throw new FormatException("Expected enum name for " + typeof(T).Name + " but found null.");
            }

            if (Enum.TryParse<T>(name, false, out T result))
            {
                return result;
            }

            throw new FormatException("Unknown " + typeof(T).Name + " value: '" + name + "'.");
        }

        private static void SkipWhitespace(string json, ref int pos)
        {
            while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n'))
            {
                pos++;
            }
        }

        private static void Expect(string json, ref int pos, char expected)
        {
            if (pos >= json.Length)
            {
                throw new FormatException("Unexpected end of input. Expected '" + expected + "'.");
            }

            if (json[pos] != expected)
            {
                throw new FormatException("Expected '" + expected + "' at position " + pos + " but found '" + json[pos] + "'.");
            }

            pos++;
        }

        private static string ReadNullableString(string json, ref int pos)
        {
            if (pos + 3 < json.Length && json[pos] == 'n' && json[pos + 1] == 'u' && json[pos + 2] == 'l' && json[pos + 3] == 'l')
            {
                pos += 4;
                return null;
            }

            return ReadString(json, ref pos);
        }

        private static string ReadString(string json, ref int pos)
        {
            if (pos >= json.Length || json[pos] != '"')
            {
                throw new FormatException("Expected '\"' at position " + pos + ".");
            }

            pos++;
            var sb = new StringBuilder();

            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == '"')
                {
                    pos++;
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    pos++;
                    if (pos >= json.Length)
                    {
                        throw new FormatException("Unexpected end of input in string escape.");
                    }

                    char esc = json[pos];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 >= json.Length)
                            {
                                throw new FormatException("Unexpected end of input in \\u escape.");
                            }
                            string hex = json.Substring(pos + 1, 4);
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
                            {
                                throw new FormatException("Invalid \\u escape: \\u" + hex + ".");
                            }
                            sb.Append((char)codePoint);
                            pos += 4;
                            break;
                        default:
                            throw new FormatException("Unknown escape sequence: \\" + esc + ".");
                    }

                    pos++;
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }

            throw new FormatException("Unterminated string.");
        }

        private static int ReadInt(string json, ref int pos)
        {
            int start = pos;
            if (pos < json.Length && json[pos] == '-')
            {
                pos++;
            }

            if (pos >= json.Length || json[pos] < '0' || json[pos] > '9')
            {
                throw new FormatException("Expected integer at position " + start + ".");
            }

            while (pos < json.Length && json[pos] >= '0' && json[pos] <= '9')
            {
                pos++;
            }

            string raw = json.Substring(start, pos - start);
            if (!int.TryParse(raw, NumberStyles.None | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException("Invalid integer: '" + raw + "'.");
            }

            return value;
        }

        private static bool ReadBool(string json, ref int pos)
        {
            if (pos + 3 < json.Length && json[pos] == 't' && json[pos + 1] == 'r' && json[pos + 2] == 'u' && json[pos + 3] == 'e')
            {
                pos += 4;
                return true;
            }

            if (pos + 4 < json.Length && json[pos] == 'f' && json[pos + 1] == 'a' && json[pos + 2] == 'l' && json[pos + 3] == 's' && json[pos + 4] == 'e')
            {
                pos += 5;
                return false;
            }

            throw new FormatException("Expected boolean at position " + pos + ".");
        }

        private static void SkipValue(string json, ref int pos)
        {
            SkipWhitespace(json, ref pos);
            if (pos >= json.Length)
            {
                throw new FormatException("Unexpected end of input while skipping value.");
            }

            char c = json[pos];
            if (c == '"')
            {
                ReadString(json, ref pos);
            }
            else if (c == '{')
            {
                SkipObject(json, ref pos);
            }
            else if (c == '[')
            {
                SkipArray(json, ref pos);
            }
            else if (c == 't' || c == 'f')
            {
                ReadBool(json, ref pos);
            }
            else if (c == 'n')
            {
                if (pos + 3 < json.Length && json[pos + 1] == 'u' && json[pos + 2] == 'l' && json[pos + 3] == 'l')
                {
                    pos += 4;
                }
                else
                {
                    throw new FormatException("Unexpected token at position " + pos + ".");
                }
            }
            else if (c == '-' || (c >= '0' && c <= '9'))
            {
                ReadInt(json, ref pos);
            }
            else
            {
                throw new FormatException("Unexpected token '" + c + "' at position " + pos + ".");
            }
        }

        private static void SkipObject(string json, ref int pos)
        {
            Expect(json, ref pos, '{');
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == '}')
            {
                pos++;
                return;
            }

            bool first = true;
            while (true)
            {
                if (!first)
                {
                    SkipWhitespace(json, ref pos);
                    Expect(json, ref pos, ',');
                }
                first = false;

                SkipWhitespace(json, ref pos);
                ReadString(json, ref pos);
                SkipWhitespace(json, ref pos);
                Expect(json, ref pos, ':');
                SkipValue(json, ref pos);
                SkipWhitespace(json, ref pos);

                if (pos < json.Length && json[pos] == '}')
                {
                    pos++;
                    return;
                }
            }
        }

        private static void SkipArray(string json, ref int pos)
        {
            Expect(json, ref pos, '[');
            SkipWhitespace(json, ref pos);

            if (pos < json.Length && json[pos] == ']')
            {
                pos++;
                return;
            }

            bool first = true;
            while (true)
            {
                if (!first)
                {
                    SkipWhitespace(json, ref pos);
                    Expect(json, ref pos, ',');
                }
                first = false;

                SkipValue(json, ref pos);
                SkipWhitespace(json, ref pos);

                if (pos < json.Length && json[pos] == ']')
                {
                    pos++;
                    return;
                }
            }
        }

        private static void AppendCard(StringBuilder builder, CardDefinition card)
        {
            builder.Append("{");
            AppendString(builder, "id", card.Id, true);
            AppendString(builder, "name", card.Name, false);
            AppendString(builder, "faction", FactionToken(card.Faction), false);
            AppendString(builder, "rarity", card.Rarity.ToString(), false);
            AppendString(builder, "type", card.Type.ToString(), false);
            AppendNumber(builder, "cost", card.Cost, false);
            AppendNumber(builder, "atk", card.Atk, false);
            AppendNumber(builder, "hp", card.Hp, false);
            AppendNumber(builder, "shield", card.Shield, false);
            AppendKeywords(builder, card.Keywords);
            AppendString(builder, "abilityText", card.AbilityText, false);
            AppendString(builder, "designNote", card.DesignNote, false);
            AppendEffects(builder, card.Effects);
            AppendString(builder, "artPath", card.ArtPath, false);
            builder.Append("}");
        }

        private static void AppendKeywords(StringBuilder builder, IReadOnlyList<Keyword> keywords)
        {
            builder.Append(",");
            AppendKey(builder, "keywords");
            builder.Append("[");

            if (keywords != null)
            {
                for (int i = 0; i < keywords.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(Quote).Append(keywords[i].ToString()).Append(Quote);
                }
            }

            builder.Append("]");
        }

        private static void AppendEffects(StringBuilder builder, IReadOnlyList<EffectDescriptor> effects)
        {
            builder.Append(",");
            AppendKey(builder, "effects");
            builder.Append("[");

            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    var effect = effects[i];
                    builder.Append("{");
                    AppendString(builder, "type", effect.Type.ToString(), true);
                    AppendString(builder, "target", effect.Target.ToString(), false);
                    AppendNumber(builder, "amount", effect.Amount, false);
                    AppendNumber(builder, "amount2", effect.Amount2, false);
                    AppendNumber(builder, "duration", effect.Duration, false);
                    AppendString(builder, "referenceId", effect.ReferenceId, false);
                    AppendBool(builder, "fast", effect.Fast, false);
                    builder.Append("}");
                }
            }

            builder.Append("]");
        }

        private static void AppendKey(StringBuilder builder, string name)
        {
            builder.Append(Quote).Append(name).Append(Quote).Append(":");
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool first)
        {
            if (!first)
            {
                builder.Append(",");
            }

            AppendKey(builder, name);
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append(Quote).Append(Escape(value)).Append(Quote);
        }

        private static void AppendNumber(StringBuilder builder, string name, int value, bool first)
        {
            if (!first)
            {
                builder.Append(",");
            }

            AppendKey(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendBool(StringBuilder builder, string name, bool value, bool first)
        {
            if (!first)
            {
                builder.Append(",");
            }

            AppendKey(builder, name);
            builder.Append(value ? "true" : "false");
        }

        /// <summary>Escapes a value for inclusion inside a JSON string literal.</summary>
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < '\u0020')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        private static void AppendComposition(StringBuilder builder, CardComposition composition)
        {
            builder.Append("{");
            AppendNumber(builder, "total", composition.Total, true);
            builder.Append(",");
            AppendKey(builder, "byRarity");
            builder.Append("{");
            AppendNumber(builder, "common", composition.Common, true);
            AppendNumber(builder, "rare", composition.Rare, false);
            AppendNumber(builder, "epic", composition.Epic, false);
            AppendNumber(builder, "legendary", composition.Legendary, false);
            builder.Append("}");
            builder.Append(",");
            AppendKey(builder, "byFaction");
            builder.Append("{");
            AppendNumber(builder, "fsti", composition.Fsti, true);
            AppendNumber(builder, "frti", composition.Frti, false);
            AppendNumber(builder, "fpb", composition.Fpb, false);
            builder.Append("}");
            builder.Append("}");
        }
    }
}
