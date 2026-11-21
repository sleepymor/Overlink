using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Overlink.EditorTools.Tests
{
    public sealed class PlayerManagerStateTests
    {
        static PlayerManagerState Load(JObject data, string writeLock = "0123456789abcdef0123456789abcdef")
        {
            return PlayerManagerState.FromResponse("test-player", new JObject
            {
                ["results"] = new JArray(new JObject
                {
                    ["key"] = TestingPlayerManagerClient.StateKey,
                    ["writeLock"] = writeLock,
                    ["value"] = data.ToString(Formatting.None)
                })
            }.ToString(Formatting.None));
        }

        static JObject Example() => PlayerManagerState.ParseObject(@"{
            'SchemaVersion':3, 'SignalCredits':2400, 'Fragments':12,
            'StandardPacks':2, 'LoyaltyPacks':1,
            'PurchasedPacks':99, 'LoyaltyGranted':0,
            'Owned':{'card_fsti_001':3},
            'Decks':[{'Id':'d1','Cards':{'card_fsti_001':2}}],
            'PurchaseReceipts':{'request123':{'PackCount':10,'Cost':900}},
            'FutureField':{'timestamp':'2026-11-21T10:00:00Z','large':9223372036854775806}
        }");

        [Test]
        public void Save_ChangesOnlyRequestedBalancesAndPreservesUnknownFields()
        {
            JObject original = Example();
            PlayerManagerState state = Load(original);
            JObject request = state.BuildSaveRequest(new[] { 700, 12, 5, 1 });
            Assert.AreEqual(TestingPlayerManagerClient.StateKey, (string)request["key"]);
            Assert.AreEqual(state.WriteLock, (string)request["writeLock"]);
            Assert.AreEqual(JTokenType.String, request["value"].Type);
            JObject saved = PlayerManagerState.ParseObject((string)request["value"]);
            var expected = (JObject)original.DeepClone();
            expected["SignalCredits"] = 700;
            expected["StandardPacks"] = 5;
            Assert.IsTrue(JToken.DeepEquals(expected, saved));
            Assert.AreEqual(2400, state.Balance(0), "Snapshot must remain immutable.");
            Assert.AreEqual(JTokenType.String, saved["FutureField"]["timestamp"].Type);
        }

        [Test]
        public void ReadData_CannotMutateSnapshot()
        {
            PlayerManagerState state = Load(Example());
            state.ReadData()["SignalCredits"] = 0;
            Assert.AreEqual(2400, state.Balance(0));
        }

        [Test]
        public void OlderSchema_MissingLoyaltyDefaultsToZeroWithoutAddingUnchangedProperties()
        {
            JObject source = Example();
            source["SchemaVersion"] = 2;
            source.Remove("LoyaltyPacks");
            PlayerManagerState state = Load(source);
            Assert.AreEqual(0, state.Balance(3));
            JObject saved = PlayerManagerState.ParseObject((string)state.BuildSaveRequest(new[] { 2500, 12, 2, 0 })["value"]);
            Assert.IsNull(saved["LoyaltyPacks"]);
            Assert.AreEqual(2, (int)saved["SchemaVersion"]);
        }

        [Test]
        public void Save_RequiresWriteLock()
        {
            Assert.Throws<InvalidOperationException>(() => Load(Example(), null).BuildSaveRequest(new[] { 1, 2, 3, 4 }));
        }

        [TestCase(-1, 0, 0, 0)]
        [TestCase(0, -1, 0, 0)]
        [TestCase(0, 0, -1, 0)]
        [TestCase(0, 0, 0, -1)]
        public void Save_RejectsNegativeBalances(int credits, int fragments, int standard, int loyalty)
        {
            Assert.Throws<ArgumentException>(() => Load(Example()).BuildSaveRequest(new[] { credits, fragments, standard, loyalty }));
        }

        [Test]
        public void Save_RequiresAllFourBalances()
        {
            Assert.Throws<ArgumentException>(() => Load(Example()).BuildSaveRequest(new[] { 1, 2 }));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Save_RefusesUnsupportedSchemas(int version)
        {
            JObject source = Example();
            source["SchemaVersion"] = version;
            Assert.Throws<InvalidOperationException>(() => Load(source).BuildSaveRequest(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void Load_RefusesMissingRecordInsteadOfCreatingProgression()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerManagerState.FromResponse("test", "{\"results\":[]}"));
        }

        [Test]
        public void Load_RefusesObjectValueInsteadOfChangingStorageFormat()
        {
            string response = new JObject { ["results"] = new JArray(new JObject
            {
                ["key"] = TestingPlayerManagerClient.StateKey, ["value"] = Example()
            }) }.ToString();
            Assert.Throws<FormatException>(() => PlayerManagerState.FromResponse("test", response));
        }

        [TestCase("-1")]
        [TestCase("2147483648")]
        [TestCase("1.5")]
        [TestCase("'100'")]
        public void Balance_RefusesMalformedValues(string literal)
        {
            JObject source = Example();
            source["SignalCredits"] = JToken.Parse(literal);
            Assert.Throws<FormatException>(() => Load(source).Balance(0));
        }

        [Test]
        public void Parse_RejectsDuplicateProperties()
        {
            Assert.Throws<JsonReaderException>(() => PlayerManagerState.ParseObject("{\"SignalCredits\":1,\"SignalCredits\":2}"));
        }

        [Test]
        public void Environment_IsHardLockedToTesting()
        {
            Assert.AreEqual("ff807509-790a-4ab0-90aa-0b2a9aac70dc", TestingPlayerManagerClient.EnvironmentId);
            Assert.AreEqual("467bcece-28d8-4091-9b3c-8138c7a02dca", TestingPlayerManagerClient.ProjectId);
        }

        [TestCase(2400, 1000, 3400)]
        [TestCase(0, 1, 1)]
        [TestCase(2147483646, 1, 2147483647)]
        public void AddToBalance_IncreasesExistingTotal(int current, int amount, int expected)
        {
            Assert.AreEqual(expected, PlayerManagerState.AddToBalance(current, amount));
        }

        [TestCase(100, 0)]
        [TestCase(100, -1)]
        [TestCase(-1, 100)]
        [TestCase(2147483647, 1)]
        public void AddToBalance_RejectsInvalidAmountsAndOverflow(int current, int amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerManagerState.AddToBalance(current, amount));
        }
    }
}
