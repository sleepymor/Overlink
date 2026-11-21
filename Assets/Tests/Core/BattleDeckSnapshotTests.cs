using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;

namespace Overlink.Core.Tests
{
    public class BattleDeckSnapshotTests
    {
        static DeckData LegalDeck(out Dictionary<string, CardDefinition> cards)
        {
            cards = new Dictionary<string, CardDefinition>();
            var data = new DeckData { Faction = Faction.Fsti };
            for (int i = 1; i <= 10; i++)
            {
                string id = "card_fsti_" + i.ToString("000");
                cards.Add(id, new CardDefinition { Id = id, Faction = Faction.Fsti });
                data.Cards.Add(id, 3);
            }
            return data;
        }

        [Test]
        public void SnapshotIsIndependentOfDraftAndReturnedCopies()
        {
            DeckData draft = LegalDeck(out var cards);
            Assert.IsTrue(BattleDeckSnapshot.TryCreate(draft, id => cards[id], out var snapshot));
            draft.Cards.Clear();
            DeckData copy = snapshot.ToDeckData();
            Assert.AreEqual(10, copy.Cards.Count);
            copy.Cards.Clear();
            Assert.AreEqual(10, snapshot.ToDeckData().Cards.Count);
        }

        [Test]
        public void RejectsIncompleteOrInvalidDrafts()
        {
            DeckData draft = LegalDeck(out var cards);
            draft.Cards.Remove("card_fsti_001");
            Assert.IsFalse(BattleDeckSnapshot.TryCreate(draft, id => cards[id], out _));
            draft.Cards["card_fsti_001"] = 4;
            Assert.IsFalse(BattleDeckSnapshot.TryCreate(draft, id => cards[id], out _));
            draft.Cards["card_fsti_001"] = 0;
            Assert.IsFalse(BattleDeckSnapshot.TryCreate(draft, id => cards[id], out _));
        }

        [Test]
        public void RejectsUnknownOrWrongFactionCards()
        {
            DeckData draft = LegalDeck(out var cards);
            Assert.IsFalse(BattleDeckSnapshot.TryCreate(draft, id => null, out _));
            cards["card_fsti_001"].Faction = Faction.Frti;
            Assert.IsFalse(BattleDeckSnapshot.TryCreate(draft, id => cards[id], out _));
        }
    }
}
