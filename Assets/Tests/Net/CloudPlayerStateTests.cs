using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using Overlink.Persistence;
using UnityEngine;

namespace Overlink.Net.Tests
{
    public sealed class CloudPlayerStateTests
    {
        [Test]
        public void FindBattleDeck_PrefersDetachedSnapshotOverDraft()
        {
            var state = new CloudPlayerState
            {
                battleDeckId = "deck_001",
                decks = new[] { new CloudDeck { id = "deck_001", cards = new Dictionary<string, int>() } },
                battleDeckSnapshot = new CloudDeck
                {
                    id = "deck_001",
                    cards = new Dictionary<string, int> { { "card_fsti_001", 3 } }
                }
            };
            DeckData first = state.FindBattleDeck();
            Assert.AreEqual(3, first.Cards["card_fsti_001"]);
            first.Cards.Clear();
            Assert.AreEqual(3, state.FindBattleDeck().Cards["card_fsti_001"]);
            state.battleDeckId = "deck_002";
            Assert.IsNull(state.FindBattleDeck());
        }

        [Test]
        public void BusyChanged_FiresOnlyWhenCloudBusyStateChanges()
        {
            var root = new GameObject("Cloud busy state test");
            try
            {
                var service = root.AddComponent<CloudPlayerDataService>();
                var changes = new List<bool>();
                service.BusyChanged += changes.Add;
                var property = typeof(CloudPlayerDataService).GetProperty("IsBusy");
                var setter = property.GetSetMethod(true);
                setter.Invoke(service, new object[] { true });
                setter.Invoke(service, new object[] { true });
                setter.Invoke(service, new object[] { false });
                CollectionAssert.AreEqual(new[] { true, false }, changes);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OwnedCopies_ReadsTheAuthoritativeObjectShape()
        {
            var state = new CloudPlayerState
            {
                owned = new Dictionary<string, int>
                {
                    { "card_fsti_001", 3 },
                    { "card_fsti_002", -2 },
                }
            };

            Assert.AreEqual(3, state.OwnedCopies("card_fsti_001"));
            Assert.AreEqual(0, state.OwnedCopies("card_fsti_002"));
            Assert.AreEqual(0, state.OwnedCopies("card_fsti_999"));
        }

        [Test]
        public void CloudDeck_RoundTripsDictionaryCardsIntoDeckData()
        {
            var source = new DeckData { Faction = Faction.Fpb };
            source.Cards["card_fpb_001"] = 2;
            source.Cards["card_fpb_002"] = 1;

            CloudDeck cloud = CloudDeck.FromDeck("deck_007", "Node Hold", source);
            DeckData roundTrip = cloud.ToDeckData();

            Assert.AreEqual("deck_007", cloud.id);
            Assert.AreEqual("Node Hold", cloud.name);
            Assert.AreEqual(Faction.Fpb, roundTrip.Faction);
            Assert.AreEqual(2, roundTrip.Cards["card_fpb_001"]);
            Assert.AreEqual(1, roundTrip.Cards["card_fpb_002"]);
        }

        [Test]
        public void DeckSummaries_UsesCloudSelectionAndCardCounts()
        {
            var state = new CloudPlayerState
            {
                activeDeckId = "deck_001",
                decks = new[]
                {
                    new CloudDeck
                    {
                        id = "deck_001",
                        name = "Starter Deck",
                        faction = "fsti",
                        cards = new Dictionary<string, int>
                        {
                            { "card_fsti_001", 28 },
                            { "card_fsti_026", 2 },
                        }
                    }
                }
            };

            IReadOnlyList<DeckSummary> summaries = state.DeckSummaries();

            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual(30, summaries[0].CardCount);
            Assert.IsTrue(summaries[0].IsActive);
            Assert.IsTrue(summaries[0].IsLegal);
        }
    }
}
