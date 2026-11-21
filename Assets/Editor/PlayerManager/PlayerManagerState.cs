using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Overlink.EditorTools
{
    /// <summary>Edits a JSON tree, never a reduced runtime DTO. Unknown fields/receipts survive.</summary>
    public sealed class PlayerManagerState
    {
        public static readonly string[] BalanceFields =
            { "SignalCredits", "Fragments", "StandardPacks", "LoyaltyPacks" };

        public string PlayerId { get; }
        public string WriteLock { get; }
        readonly JObject data;
        public JObject ReadData() => (JObject)data.DeepClone();

        PlayerManagerState(string playerId, string writeLock, JObject data)
        {
            PlayerId = playerId;
            WriteLock = writeLock;
            this.data = data;
        }

        public static JObject ParseObject(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                var result = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) throw new FormatException("Unexpected trailing JSON content.");
                return result;
            }
        }

        public static PlayerManagerState FromResponse(string playerId, string response)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.");
            var results = ParseObject(response)["results"] as JArray;
            var items = results?.OfType<JObject>()
                .Where(item => (string)item["key"] == TestingPlayerManagerClient.StateKey).ToArray();
            if (items == null || items.Length != 1)
                throw new InvalidOperationException("No unique progression record found. Sign in and claim a starter first; this tool does not create accounts.");
            JObject item = items[0];
            if (item["value"]?.Type != JTokenType.String)
                throw new FormatException("Expected progression stored as a serialized JSON string. Refusing to rewrite a different format.");
            return new PlayerManagerState(playerId, (string)item["writeLock"], ParseObject((string)item["value"]));
        }

        public int Balance(int index)
        {
            JToken value = data[BalanceFields[index]];
            if (value == null) return 0; // Older schemas may not have the new entitlement balances.
            if (value.Type != JTokenType.Integer || !int.TryParse(value.ToString(), out int amount) || amount < 0)
                throw new FormatException(BalanceFields[index] + " is not a non-negative Int32 balance.");
            return amount;
        }

        internal static int AddToBalance(int current, int amount)
        {
            if (current < 0 || amount <= 0 || (long)current + amount > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(amount), "Add a positive amount without exceeding the maximum balance.");
            return current + amount;
        }

        public JObject BuildSaveRequest(int[] balances)
        {
            if (string.IsNullOrWhiteSpace(WriteLock))
                throw new InvalidOperationException("No write lock was returned. Refresh; unconditional writes are not allowed.");
            JToken schema = data["SchemaVersion"];
            if (schema?.Type != JTokenType.Integer || (int)schema < 1 || (int)schema > 3)
                throw new InvalidOperationException("Unsupported progression schema. Update the tool before editing.");
            if (balances == null || balances.Length != BalanceFields.Length || balances.Any(value => value < 0))
                throw new ArgumentException("All four balances must be non-negative Int32 values.");
            var edited = (JObject)data.DeepClone();
            for (int i = 0; i < balances.Length; i++)
                if (balances[i] != Balance(i)) edited[BalanceFields[i]] = balances[i];
            return new JObject
            {
                ["key"] = TestingPlayerManagerClient.StateKey,
                ["value"] = edited.ToString(Formatting.None),
                ["writeLock"] = WriteLock
            };
        }
    }
}
