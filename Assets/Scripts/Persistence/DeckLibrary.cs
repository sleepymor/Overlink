// Assets/Scripts/Persistence/DeckLibrary.cs
using System;
using System.Collections.Generic;
using Overlink.Core;
using UnityEngine;

namespace Overlink.Persistence
{
    /// <summary>
    /// One saved deck, as the deck builder and the binder both see it.
    /// </summary>
    public sealed class DeckSummary
    {
        public DeckSummary(string id, string name, Faction faction, int cardCount, bool isActive)
        {
            Id = id;
            Name = name;
            Faction = faction;
            CardCount = cardCount;
            IsActive = isActive;
        }

        public string Id { get; }
        public string Name { get; }
        public Faction Faction { get; }
        public int CardCount { get; }
        public bool IsActive { get; }

        /// <summary>A deck inside the constructed-size window is legal; anything else is a draft.</summary>
        public bool IsLegal => CardCount >= DeckValidator.MinDeckSize && CardCount <= DeckValidator.MaxDeckSize;
    }

    /// <summary>
    /// The single PlayerPrefs-backed store for saved decks.
    /// <para>
    /// Both the deck builder and the binder read and write through this class, so a deck edited in one
    /// screen is the same deck the other screen shows. The store deliberately does not validate: the
    /// deck builder gates <em>saving</em> on <see cref="DeckValidator"/>, while the binder must be able
    /// to grow a partial deck one card at a time. Legality is a property of the summary
    /// (<see cref="DeckSummary.IsLegal"/>), not a write condition.
    /// </para>
    /// </summary>
    public static class DeckLibrary
    {
        /// <summary>
        /// Account-backed runtime implementation. Left null for persistence EditMode tests and for the
        /// legacy migration reader; protected gameplay installs it after Cloud Code state is loaded.
        /// </summary>
        public static ICloudProgressionBackend Backend { get; private set; }

        public static void SetBackend(ICloudProgressionBackend backend) => Backend = backend;

        public const string IndexKey = "Overlink.Decks.Index";
        public const string ActiveKey = "Overlink.Decks.Active";
        public const string BattleKey = "Overlink.Decks.Battle";
        public const string SequenceKey = "Overlink.Decks.Sequence";
        public const string SavedDeckMigratedKey = "Overlink.Decks.Migrated.SavedDeck";
        public const string CountsMigratedKey = "Overlink.Decks.Migrated.Counts";
        public const string NameKeyPrefix = "Overlink.Decks.Name.";
        public const string DataKeyPrefix = "Overlink.Decks.Data.";

        // Pre-unification stores. Read once by the two migration passes, then deleted.
        public const string LegacyDeckKey = "SavedDeck";
        public const string LegacyCountKeyPrefix = "Overlink.OfflineDeck.";

        // ── Reading ─────────────────────────────────────────────────────────────────────────

        /// <summary>Every saved deck, in creation order.</summary>
        public static IReadOnlyList<DeckSummary> All()
        {
            if (Backend != null) return Backend.AllDecks();
            List<string> ids = ReadIndex();
            var summaries = new List<DeckSummary>(ids.Count);
            string active = ActiveId;
            for (int i = 0; i < ids.Count; i++)
            {
                DeckData data = Load(ids[i]);
                summaries.Add(new DeckSummary(ids[i], NameOf(ids[i]), data.Faction, CardCount(data), ids[i] == active));
            }

            return summaries;
        }

        /// <summary>The id of the deck both screens operate on, or an empty string when none exists.</summary>
        public static string ActiveId
        {
            get
            {
                if (Backend != null) return Backend.ActiveDeckId ?? string.Empty;
                List<string> ids = ReadIndex();
                string active = PlayerPrefs.GetString(ActiveKey, string.Empty);
                if (!string.IsNullOrEmpty(active) && ids.Contains(active)) return active;
                return ids.Count > 0 ? ids[0] : string.Empty;
            }
        }

        public static bool HasActive => ActiveId.Length > 0;

        /// <summary>The deck the match uses. Separate from <see cref="ActiveId"/> so the player can edit one deck
        /// while another is set for battle. Empty when unset or when the saved deck no longer exists.</summary>
        public static string BattleId
        {
            get
            {
                if (Backend != null) return Backend.BattleDeckId ?? string.Empty;
                string id = PlayerPrefs.GetString(BattleKey, string.Empty);
                return !string.IsNullOrEmpty(id) && ReadIndex().Contains(id) ? id : string.Empty;
            }
        }

        public static bool IsBattle(string id) => !string.IsNullOrEmpty(id) && id == BattleId;

        /// <summary>Loads the selected battle snapshot from the account backend, when available.</summary>
        public static DeckData LoadBattleDeck()
        {
            if (Backend != null) return Backend.LoadBattleDeck();
            string id = BattleId;
            return id.Length == 0 ? null : Load(id);
        }

        public static DeckSummary Active
        {
            get
            {
                string id = ActiveId;
                if (id.Length == 0) return null;
                DeckData data = Load(id);
                return new DeckSummary(id, NameOf(id), data.Faction, CardCount(data), true);
            }
        }

        public static string NameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            if (Backend != null) return Backend.NameOf(id);
            return PlayerPrefs.GetString(NameKeyPrefix + id, id);
        }

        public static DeckData Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return new DeckData();
            if (Backend != null) return Backend.LoadDeck(id) ?? new DeckData();
            string json = PlayerPrefs.GetString(DataKeyPrefix + id, string.Empty);
            return DeckData.FromJson(json) ?? new DeckData();
        }

        public static int CardCount(DeckData data)
        {
            if (data == null || data.Cards == null) return 0;
            int total = 0;
            foreach (var count in data.Cards.Values)
            {
                if (count > 0) total += count;
            }

            return total;
        }

        public static int CopiesInActive(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return 0;
            if (Backend != null) return Backend.CopiesInActive(cardId);
            string id = ActiveId;
            if (id.Length == 0) return 0;
            DeckData data = Load(id);
            return data.Cards != null && data.Cards.TryGetValue(cardId, out int copies) ? Mathf.Max(0, copies) : 0;
        }

        // ── Writing ─────────────────────────────────────────────────────────────────────────

        /// <summary>Creates an empty deck and makes it active. A blank name becomes "Deck N".</summary>
        public static string Create(string name, Faction faction)
        {
            if (Backend != null) return Backend.CreateDeck(name, faction);
            int sequence = PlayerPrefs.GetInt(SequenceKey, 0) + 1;
            PlayerPrefs.SetInt(SequenceKey, sequence);

            string id = "deck_" + sequence.ToString("000");
            List<string> ids = ReadIndex();
            ids.Add(id);
            WriteIndex(ids);

            PlayerPrefs.SetString(NameKeyPrefix + id, ResolveName(name, id));
            PlayerPrefs.SetString(DataKeyPrefix + id, DeckData.ToJson(new DeckData { Version = 1, Faction = faction }));
            PlayerPrefs.SetString(ActiveKey, id);
            PlayerPrefs.Save();
            return id;
        }

        /// <summary>Writes a deck, creating the slot if it is unknown. Accepts drafts and illegal sizes.</summary>
        public static void Save(string id, string name, DeckData data)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Deck id is required.", nameof(id));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (Backend != null)
            {
                Backend.SaveDeck(id, name, data);
                return;
            }

            List<string> ids = ReadIndex();
            if (!ids.Contains(id))
            {
                ids.Add(id);
                WriteIndex(ids);
            }

            PlayerPrefs.SetString(NameKeyPrefix + id, ResolveName(name, id));
            PlayerPrefs.SetString(DataKeyPrefix + id, DeckData.ToJson(data));

            if (string.IsNullOrEmpty(PlayerPrefs.GetString(ActiveKey, string.Empty)))
                PlayerPrefs.SetString(ActiveKey, id);

            PlayerPrefs.Save();
        }

        public static void SetActive(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Backend != null) { Backend.SetActiveDeck(id); return; }
            if (!ReadIndex().Contains(id)) return;
            PlayerPrefs.SetString(ActiveKey, id);
            PlayerPrefs.Save();
        }

        /// <summary>Sets the deck used in battle. Ignored for unknown ids.</summary>
        public static void SetBattle(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Backend != null) { Backend.SetBattleDeck(id); return; }
            if (!ReadIndex().Contains(id)) return;
            PlayerPrefs.SetString(BattleKey, id);
            PlayerPrefs.Save();
        }

        public static void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Backend != null) { Backend.DeleteDeck(id); return; }
            List<string> ids = ReadIndex();
            if (!ids.Remove(id)) return;

            WriteIndex(ids);
            PlayerPrefs.DeleteKey(NameKeyPrefix + id);
            PlayerPrefs.DeleteKey(DataKeyPrefix + id);
            if (PlayerPrefs.GetString(ActiveKey, string.Empty) == id)
                PlayerPrefs.SetString(ActiveKey, ids.Count > 0 ? ids[0] : string.Empty);
            if (PlayerPrefs.GetString(BattleKey, string.Empty) == id)
                PlayerPrefs.DeleteKey(BattleKey);
            PlayerPrefs.Save();
        }

        /// <summary>Wipes every deck and the migration flags, but keeps legacy keys untouched.</summary>
        public static void DeleteAll()
        {
            if (Backend != null) { Backend.DeleteAllDecks(); return; }
            List<string> ids = ReadIndex();
            for (int i = 0; i < ids.Count; i++)
            {
                PlayerPrefs.DeleteKey(NameKeyPrefix + ids[i]);
                PlayerPrefs.DeleteKey(DataKeyPrefix + ids[i]);
            }

            PlayerPrefs.SetString(IndexKey, string.Empty);
            PlayerPrefs.SetString(ActiveKey, string.Empty);
            PlayerPrefs.DeleteKey(BattleKey);
            PlayerPrefs.DeleteKey(SequenceKey);
            PlayerPrefs.DeleteKey(SavedDeckMigratedKey);
            PlayerPrefs.DeleteKey(CountsMigratedKey);
            PlayerPrefs.Save();
        }

        /// <summary>Adds, replaces or removes copies of one card on the active deck.</summary>
        public static bool SetActiveCopies(string cardId, int copies)
        {
            if (string.IsNullOrEmpty(cardId)) return false;

            if (Backend != null)
            {
                // The cloud gateway is the final client-side cache gate. Cloud Code repeats this check
                // before accepting the mutation, so a modified client cannot mint deck copies.
                return Backend.SetActiveCopies(cardId, copies);
            }

            string id = ActiveId;
            if (id.Length == 0)
            {
                id = Create(string.Empty, FactionOf(cardId));
            }

            DeckData data = Load(id);
            int clamped = Mathf.Clamp(copies, 0, DeckValidator.MaxCopiesPerCard);
            if (clamped <= 0)
            {
                data.Cards.Remove(cardId);
            }
            else
            {
                data.Cards[cardId] = clamped;
            }

            Save(id, NameOf(id), data);
            return true;
        }

        /// <summary>Faction encoded in a card id ("card_fsti_021" → Fsti); Fsti when it cannot be read.</summary>
        public static Faction FactionOf(string cardId)
        {
            if (!string.IsNullOrEmpty(cardId))
            {
                string[] parts = cardId.Split('_');
                if (parts.Length >= 3 && CardJson.TryParseFactionToken(parts[1], out Faction faction))
                    return faction;
            }

            return Faction.Fsti;
        }

        // ── Migration from the two pre-unification stores ───────────────────────────────────

        /// <summary>
        /// Absorbs the deck builder's old single-slot <c>SavedDeck</c> JSON. Runs at most once and only
        /// when no deck exists yet, so a player who already has decks never gets a duplicate import.
        /// </summary>
        public static void MigrateLegacySavedDeck()
        {
            if (PlayerPrefs.GetInt(SavedDeckMigratedKey, 0) == 1) return;
            PlayerPrefs.SetInt(SavedDeckMigratedKey, 1);

            if (PlayerPrefs.HasKey(LegacyDeckKey) && ReadIndex().Count == 0)
            {
                DeckData legacy = DeckData.FromJson(PlayerPrefs.GetString(LegacyDeckKey, string.Empty));
                if (legacy != null && legacy.Cards != null && legacy.Cards.Count > 0)
                {
                    string id = Create(string.Empty, legacy.Faction);
                    // Store the original payload so its version and faction survive the import.
                    Save(id, NameOf(id), legacy);
                }
            }

            PlayerPrefs.DeleteKey(LegacyDeckKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Absorbs the binder's old per-card counts (<c>Overlink.OfflineDeck.&lt;id&gt;</c>). The binder
        /// owns the card catalog, so it passes the ids it knows about. Runs at most once.
        /// </summary>
        public static void MigrateLegacyCounts(IEnumerable<string> cardIds)
        {
            if (PlayerPrefs.GetInt(CountsMigratedKey, 0) == 1) return;

            // The JSON slot only imports while the library is empty, so it has to be read before these
            // counts create a deck; otherwise a player who opens the binder first would never get their
            // saved deck back.
            MigrateLegacySavedDeck();

            PlayerPrefs.SetInt(CountsMigratedKey, 1);

            var counts = new Dictionary<string, int>();
            if (cardIds != null)
            {
                foreach (string raw in cardIds)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    int copies = PlayerPrefs.GetInt(LegacyCountKeyPrefix + raw, 0);
                    if (copies > 0) counts[raw] = copies;
                    PlayerPrefs.DeleteKey(LegacyCountKeyPrefix + raw);
                }
            }

            if (counts.Count == 0)
            {
                PlayerPrefs.Save();
                return;
            }

            string id = ActiveId;
            if (id.Length == 0)
            {
                string first = null;
                foreach (string key in counts.Keys)
                {
                    first = key;
                    break;
                }

                id = Create(string.Empty, FactionOf(first));
            }

            DeckData data = Load(id);
            foreach (var entry in counts)
            {
                int existing = data.Cards.TryGetValue(entry.Key, out int current) ? current : 0;
                data.Cards[entry.Key] = Mathf.Min(existing + entry.Value, DeckValidator.MaxCopiesPerCard);
            }

            Save(id, NameOf(id), data);
            PlayerPrefs.Save();
        }

        // ── Internals ───────────────────────────────────────────────────────────────────────

        static List<string> ReadIndex()
        {
            var ids = new List<string>();
            string raw = PlayerPrefs.GetString(IndexKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return ids;

            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (id.Length == 0 || ids.Contains(id)) continue;
                ids.Add(id);
            }

            return ids;
        }

        static void WriteIndex(List<string> ids)
        {
            PlayerPrefs.SetString(IndexKey, string.Join(";", ids));
        }

        /// <summary>Keeps names human and unique: blank names become "Deck N", duplicates get " (2)".</summary>
        static string ResolveName(string name, string excludingId)
        {
            List<string> ids = ReadIndex();
            var taken = new List<string>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == excludingId) continue;
                taken.Add(NameOf(ids[i]));
            }

            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                int number = 1;
                while (taken.Exists(t => string.Equals(t, "Deck " + number, StringComparison.OrdinalIgnoreCase)))
                    number++;
                return "Deck " + number;
            }

            string candidate = trimmed;
            int suffix = 2;
            while (taken.Exists(t => string.Equals(t, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = trimmed + " (" + suffix + ")";
                suffix++;
            }

            return candidate;
        }
    }
}
