// Assets/Tests/Persistence/DeckLibraryTests.cs
using System.Collections.Generic;
using NUnit.Framework;
using Overlink.Core;
using Overlink.Persistence;
using UnityEngine;

namespace Overlink.Persistence.Tests
{
    /// <summary>
    /// EditMode coverage for the shared deck store. These tests touch PlayerPrefs, so every case starts
    /// with a wiped library and no legacy keys — otherwise a previous run's "Deck 1" would change the
    /// auto-name the next case expects. The editor's PlayerPrefs are the player's real saved decks, so
    /// each case captures every key it can write and puts them back afterwards.
    /// </summary>
    public class DeckLibraryTests
    {
        static readonly string[] LegacyCardIds =
        {
            "card_fsti_001", "card_fsti_002", "card_frti_001", "card_fpb_001"
        };

        // Deck slots the library can allocate during a test run. Create() hands out deck_001 onward, so a
        // test run after DeleteAll() reuses those ids and would overwrite a real deck stored under them.
        const int SnapshotDeckSlots = 64;

        Dictionary<string, string> savedStrings;
        Dictionary<string, int> savedInts;

        [SetUp]
        public void SetUp()
        {
            Capture();
            Wipe();
        }

        [TearDown]
        public void TearDown()
        {
            Wipe();
            Restore();
        }

        static IEnumerable<string> OwnedStringKeys()
        {
            yield return DeckLibrary.IndexKey;
            yield return DeckLibrary.ActiveKey;
            yield return DeckLibrary.BattleKey;
            yield return DeckLibrary.LegacyDeckKey;
            for (int slot = 1; slot <= SnapshotDeckSlots; slot++)
            {
                string id = "deck_" + slot.ToString("000");
                yield return DeckLibrary.NameKeyPrefix + id;
                yield return DeckLibrary.DataKeyPrefix + id;
            }

            // Decks already in the index, whatever their id, are wiped by DeleteAll and must come back too.
            string index = PlayerPrefs.GetString(DeckLibrary.IndexKey, string.Empty);
            foreach (string raw in index.Split(';'))
            {
                string id = raw.Trim();
                if (id.Length == 0) continue;
                yield return DeckLibrary.NameKeyPrefix + id;
                yield return DeckLibrary.DataKeyPrefix + id;
            }
        }

        static IEnumerable<string> OwnedIntKeys()
        {
            yield return DeckLibrary.SequenceKey;
            yield return DeckLibrary.SavedDeckMigratedKey;
            yield return DeckLibrary.CountsMigratedKey;
            for (int i = 0; i < LegacyCardIds.Length; i++)
                yield return DeckLibrary.LegacyCountKeyPrefix + LegacyCardIds[i];
        }

        void Capture()
        {
            savedStrings = new Dictionary<string, string>();
            savedInts = new Dictionary<string, int>();
            foreach (string key in OwnedStringKeys())
            {
                if (PlayerPrefs.HasKey(key)) savedStrings[key] = PlayerPrefs.GetString(key);
            }

            foreach (string key in OwnedIntKeys())
            {
                if (PlayerPrefs.HasKey(key)) savedInts[key] = PlayerPrefs.GetInt(key);
            }
        }

        void Restore()
        {
            foreach (string key in OwnedStringKeys()) PlayerPrefs.DeleteKey(key);
            foreach (string key in OwnedIntKeys()) PlayerPrefs.DeleteKey(key);
            foreach (var entry in savedStrings) PlayerPrefs.SetString(entry.Key, entry.Value);
            foreach (var entry in savedInts) PlayerPrefs.SetInt(entry.Key, entry.Value);
            PlayerPrefs.Save();
        }

        static void Wipe()
        {
            DeckLibrary.DeleteAll();
            PlayerPrefs.DeleteKey(DeckLibrary.LegacyDeckKey);
            for (int i = 0; i < LegacyCardIds.Length; i++)
                PlayerPrefs.DeleteKey(DeckLibrary.LegacyCountKeyPrefix + LegacyCardIds[i]);
            PlayerPrefs.Save();
        }

        static DeckData DeckWith(params (string id, int count)[] entries)
        {
            var data = new DeckData { Version = 1, Faction = Faction.Fsti };
            for (int i = 0; i < entries.Length; i++)
                data.Cards[entries[i].id] = entries[i].count;
            return data;
        }

        // ── Naming ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void Create_WithBlankName_AutoNamesInOrder()
        {
            string first = DeckLibrary.Create(string.Empty, Faction.Fsti);
            string second = DeckLibrary.Create("   ", Faction.Frti);

            Assert.AreEqual("Deck 1", DeckLibrary.NameOf(first));
            Assert.AreEqual("Deck 2", DeckLibrary.NameOf(second));
        }

        [Test]
        public void Create_WithDuplicateName_AppendsSuffix()
        {
            DeckLibrary.Create("Aggro", Faction.Fsti);
            string second = DeckLibrary.Create("Aggro", Faction.Frti);

            Assert.AreEqual("Aggro (2)", DeckLibrary.NameOf(second));
        }

        [Test]
        public void Save_RenamingToAnExistingName_AppendsSuffix()
        {
            string first = DeckLibrary.Create("Control", Faction.Fsti);
            string second = DeckLibrary.Create("Aggro", Faction.Fsti);

            DeckLibrary.Save(second, "Control", DeckLibrary.Load(second));

            Assert.AreEqual("Control", DeckLibrary.NameOf(first));
            Assert.AreEqual("Control (2)", DeckLibrary.NameOf(second));
        }

        // ── Round trip and active deck ────────────────────────────────────────────────────

        [Test]
        public void Save_ThenLoad_PreservesCardsAndFaction()
        {
            string id = DeckLibrary.Create("Round Trip", Faction.Fpb);
            DeckData data = DeckWith(("card_fpb_021", 3), ("card_fpb_022", 2));
            data.Faction = Faction.Fpb;
            DeckLibrary.Save(id, "Round Trip", data);

            DeckData loaded = DeckLibrary.Load(id);

            Assert.AreEqual(Faction.Fpb, loaded.Faction);
            Assert.AreEqual(3, loaded.Cards["card_fpb_021"]);
            Assert.AreEqual(2, loaded.Cards["card_fpb_022"]);
            Assert.AreEqual(5, DeckLibrary.CardCount(loaded));
        }

        [Test]
        public void Create_MakesTheDeckActive()
        {
            DeckLibrary.Create("First", Faction.Fsti);
            string second = DeckLibrary.Create("Second", Faction.Fsti);

            Assert.AreEqual(second, DeckLibrary.ActiveId);

            DeckLibrary.SetActive(DeckLibrary.All()[0].Id);
            Assert.AreEqual("First", DeckLibrary.Active.Name);
        }

        [Test]
        public void All_ReportsCountAndLegality()
        {
            string draft = DeckLibrary.Create("Draft", Faction.Fsti);
            DeckLibrary.Save(draft, "Draft", DeckWith(("card_fsti_021", 2)));

            DeckSummary summary = DeckLibrary.All()[0];

            Assert.AreEqual(2, summary.CardCount);
            Assert.IsFalse(summary.IsLegal);
            Assert.IsTrue(summary.IsActive);
        }

        [Test]
        public void Save_AcceptsAnIllegalDraft()
        {
            string id = DeckLibrary.Create("Draft", Faction.Fsti);

            Assert.DoesNotThrow(() => DeckLibrary.Save(id, "Draft", DeckWith(("card_fsti_021", 1))));
            Assert.AreEqual(1, DeckLibrary.All()[0].CardCount);
        }

        // ── Copies ────────────────────────────────────────────────────────────────────────

        [Test]
        public void SetActiveCopies_ClampsToTheCopyLimit()
        {
            string id = DeckLibrary.Create("Clamp", Faction.Fsti);

            DeckLibrary.SetActiveCopies("card_fsti_021", 9);
            Assert.AreEqual(DeckValidator.MaxCopiesPerCard, DeckLibrary.CopiesInActive("card_fsti_021"));

            DeckLibrary.SetActiveCopies("card_fsti_021", 0);
            Assert.AreEqual(0, DeckLibrary.CopiesInActive("card_fsti_021"));
            Assert.AreEqual(0, DeckLibrary.Load(id).Cards.Count);
        }

        [Test]
        public void SetActiveCopies_OnAnEmptyLibrary_CreatesADeckWithTheCardsFaction()
        {
            Assert.IsFalse(DeckLibrary.HasActive);

            DeckLibrary.SetActiveCopies("card_frti_025", 2);

            Assert.IsTrue(DeckLibrary.HasActive);
            Assert.AreEqual(Faction.Frti, DeckLibrary.Active.Faction);
            Assert.AreEqual(2, DeckLibrary.CopiesInActive("card_frti_025"));
            Assert.AreEqual("Deck 1", DeckLibrary.Active.Name);
        }

        // ── Deletion ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Delete_RemovesTheDeckAndRepointsTheActivePointer()
        {
            string first = DeckLibrary.Create("First", Faction.Fsti);
            string second = DeckLibrary.Create("Second", Faction.Fsti);
            DeckLibrary.SetActive(second);

            DeckLibrary.Delete(second);

            Assert.AreEqual(1, DeckLibrary.All().Count);
            Assert.AreEqual(first, DeckLibrary.ActiveId);
        }

        [Test]
        public void DeleteAll_LeavesNoDecksOrMigrationFlags()
        {
            DeckLibrary.Create("First", Faction.Fsti);
            DeckLibrary.DeleteAll();

            Assert.AreEqual(0, DeckLibrary.All().Count);
            Assert.IsFalse(DeckLibrary.HasActive);
            Assert.AreEqual(0, PlayerPrefs.GetInt(DeckLibrary.SavedDeckMigratedKey, 0));
        }

        // ── Migration ─────────────────────────────────────────────────────────────────────

        [Test]
        public void MigrateLegacySavedDeck_ImportsTheOldSingleSlot()
        {
            PlayerPrefs.SetString(DeckLibrary.LegacyDeckKey,
                DeckData.ToJson(DeckWith(("card_fsti_021", 3), ("card_fsti_022", 1))));
            PlayerPrefs.Save();

            DeckLibrary.MigrateLegacySavedDeck();

            Assert.AreEqual(1, DeckLibrary.All().Count);
            Assert.AreEqual(4, DeckLibrary.Active.CardCount);
            Assert.AreEqual(3, DeckLibrary.CopiesInActive("card_fsti_021"));
            Assert.IsFalse(PlayerPrefs.HasKey(DeckLibrary.LegacyDeckKey));
        }

        [Test]
        public void MigrateLegacySavedDeck_RunsOnlyOnce()
        {
            PlayerPrefs.SetString(DeckLibrary.LegacyDeckKey,
                DeckData.ToJson(DeckWith(("card_fsti_021", 3))));
            PlayerPrefs.Save();

            DeckLibrary.MigrateLegacySavedDeck();
            PlayerPrefs.SetString(DeckLibrary.LegacyDeckKey,
                DeckData.ToJson(DeckWith(("card_fsti_021", 3))));
            DeckLibrary.MigrateLegacySavedDeck();

            Assert.AreEqual(1, DeckLibrary.All().Count);
        }

        [Test]
        public void MigrateLegacySavedDeck_DoesNotDuplicateAnExistingLibrary()
        {
            DeckLibrary.Create("Existing", Faction.Fsti);
            PlayerPrefs.SetString(DeckLibrary.LegacyDeckKey,
                DeckData.ToJson(DeckWith(("card_fsti_021", 3))));
            PlayerPrefs.Save();

            DeckLibrary.MigrateLegacySavedDeck();

            Assert.AreEqual(1, DeckLibrary.All().Count);
            Assert.AreEqual("Existing", DeckLibrary.Active.Name);
        }

        [Test]
        public void MigrateLegacyCounts_MergesAndDeletesTheOldKeys()
        {
            PlayerPrefs.SetInt(DeckLibrary.LegacyCountKeyPrefix + "card_fsti_001", 2);
            PlayerPrefs.SetInt(DeckLibrary.LegacyCountKeyPrefix + "card_frti_001", 1);
            PlayerPrefs.Save();

            DeckLibrary.MigrateLegacyCounts(LegacyCardIds);

            Assert.AreEqual(1, DeckLibrary.All().Count);
            Assert.AreEqual(3, DeckLibrary.Active.CardCount);
            Assert.AreEqual(Faction.Fsti, DeckLibrary.Active.Faction);
            Assert.IsFalse(PlayerPrefs.HasKey(DeckLibrary.LegacyCountKeyPrefix + "card_fsti_001"));
            Assert.IsFalse(PlayerPrefs.HasKey(DeckLibrary.LegacyCountKeyPrefix + "card_frti_001"));
        }

        [Test]
        public void MigrateLegacyCounts_ClampsMergedCopies()
        {
            DeckLibrary.Create("Existing", Faction.Fsti);
            DeckLibrary.SetActiveCopies("card_fsti_001", 3);
            PlayerPrefs.SetInt(DeckLibrary.LegacyCountKeyPrefix + "card_fsti_001", 3);
            PlayerPrefs.Save();

            DeckLibrary.MigrateLegacyCounts(LegacyCardIds);

            Assert.AreEqual(DeckValidator.MaxCopiesPerCard, DeckLibrary.CopiesInActive("card_fsti_001"));
        }
    }
}