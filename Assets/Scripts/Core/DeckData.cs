// Assets/Scripts/Core/DeckData.cs
using System;
using System.Collections.Generic;
using System.Text;

namespace Overlink.Core
{
    /// <summary>
    /// Serializable deck representation for persistence with versioning and faction tracking.
    /// </summary>
    [Serializable]
    public class DeckData
    {
        /// <summary>Format version for forward compatibility.</summary>
        public int Version { get; set; }

        /// <summary>The deck's faction (single-faction for MVP).</summary>
        public Faction Faction { get; set; }

        /// <summary>Card counts: cardId -> count (1-3).</summary>
        public Dictionary<string, int> Cards { get; set; }

        public DeckData()
        {
            Version = 1;
            Faction = Faction.Fsti;
            Cards = new Dictionary<string, int>();
        }

        /// <summary>
        /// Serialize to JSON manually (Unity's JsonUtility doesn't handle Dictionary).
        /// </summary>
        public static string ToJson(DeckData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"version\":{data.Version},");
            sb.Append($"\"faction\":\"{CardJson.FactionToken(data.Faction)}\",");
            sb.Append("\"cards\":{");

            bool first = true;
            foreach (var kvp in data.Cards)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kvp.Key}\":{kvp.Value}");
                first = false;
            }

            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>
        /// Deserialize from JSON (manual parser for Dictionary).
        /// </summary>
        public static DeckData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var data = new DeckData();

                // Simple manual parser (sufficient for our controlled format)
                json = json.Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                    return null;

                // Validate balanced braces and quotes
                if (!IsValidJsonStructure(json))
                    return null;

                string inner = json.Substring(1, json.Length - 2).Trim(); // Remove outer braces

                // Reject a doubly-wrapped document such as {{"version":1}}. Once the outer braces are
                // removed the payload must be a flat list of members, so if the first brace it opens
                // closes on its last character then the whole inner content is one nested object and
                // there is nothing beside it - which this format never produces.
                if (inner.StartsWith("{"))
                {
                    int matchingClose = FindMatchingBrace(inner, 0);
                    if (matchingClose == inner.Length - 1)
                        return null;
                }
                
                json = inner;

                // Parse version
                int versionIdx = json.IndexOf("\"version\"");
                if (versionIdx >= 0)
                {
                    int colonIdx = json.IndexOf(':', versionIdx);
                    if (colonIdx < 0)
                        return null;
                    
                    int versionStart = colonIdx + 1;
                    int versionEnd = json.IndexOf(',', versionStart);
                    if (versionEnd < 0) versionEnd = json.Length;
                    string versionStr = json.Substring(versionStart, versionEnd - versionStart).Trim();
                    if (int.TryParse(versionStr, out int version))
                        data.Version = version;
                }

                // Parse faction
                int factionIdx = json.IndexOf("\"faction\"");
                if (factionIdx >= 0)
                {
                    int colonIdx = json.IndexOf(':', factionIdx);
                    if (colonIdx < 0)
                        return null;
                    
                    int quoteStart = json.IndexOf('"', colonIdx);
                    if (quoteStart < 0)
                        return null;
                    
                    int factionStart = quoteStart + 1;
                    int factionEnd = json.IndexOf('"', factionStart);
                    if (factionEnd < 0 || factionEnd <= factionStart)
                        return null;
                    
                    string factionToken = json.Substring(factionStart, factionEnd - factionStart).Trim();
                    if (CardJson.TryParseFactionToken(factionToken, out Faction faction))
                        data.Faction = faction;
                }

                // Parse cards dictionary
                int cardsIdx = json.IndexOf("\"cards\"");
                if (cardsIdx >= 0)
                {
                    int colonIdx = json.IndexOf(':', cardsIdx);
                    if (colonIdx < 0)
                        return null;
                    
                    int braceStart = json.IndexOf('{', colonIdx);
                    if (braceStart < 0)
                        return null;
                    
                    int cardsStart = braceStart + 1;
                    int cardsEnd = FindMatchingBrace(json, braceStart);
                    // cardsEnd == cardsStart is the legitimate empty case ("cards":{}), so only an
                    // unclosed brace (cardsEnd < 0) is an error here. The empty span falls through to
                    // the IsNullOrEmpty guard below and leaves the constructor's empty dictionary.
                    if (cardsEnd < 0)
                        return null;
                    
                    string cardsJson = json.Substring(cardsStart, cardsEnd - cardsStart).Trim();
                    if (!string.IsNullOrEmpty(cardsJson))
                    {
                        // Split by comma (simple approach, doesn't handle escaped commas but our IDs don't have them)
                        var pairs = cardsJson.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var pair in pairs)
                        {
                            int pairColonIdx = pair.IndexOf(':');
                            if (pairColonIdx > 0)
                            {
                                string keyPart = pair.Substring(0, pairColonIdx).Trim();
                                // Remove quotes from key
                                if (keyPart.StartsWith("\"") && keyPart.EndsWith("\"") && keyPart.Length >= 2)
                                {
                                    keyPart = keyPart.Substring(1, keyPart.Length - 2);
                                }
                                else
                                {
                                    // Malformed key
                                    return null;
                                }
                                
                                string valuePart = pair.Substring(pairColonIdx + 1).Trim();
                                if (int.TryParse(valuePart, out int count))
                                {
                                    data.Cards[keyPart] = count;
                                }
                                else
                                {
                                    // Malformed value
                                    return null;
                                }
                            }
                        }
                    }
                }

                return data;
            }
            catch
            {
                // Malformed JSON — return null without logging (Core layer has noEngineReferences)
                return null;
            }
        }

        private static bool IsValidJsonStructure(string json)
        {
            int braceCount = 0;
            int quoteCount = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    quoteCount++;
                }
                else if (!inString)
                {
                    if (c == '{')
                        braceCount++;
                    else if (c == '}')
                        braceCount--;
                }

                if (braceCount < 0)
                    return false;
            }

            return braceCount == 0 && !inString;
        }

        private static int FindMatchingBrace(string json, int openBraceIndex)
        {
            int braceCount = 1;
            bool inString = false;
            bool escaped = false;

            for (int i = openBraceIndex + 1; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == '{')
                        braceCount++;
                    else if (c == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                            return i;
                    }
                }
            }

            return -1; // No matching brace found
        }
    }
}
