using System.Collections.Generic;
using UnityEngine;
using Overlink.Core;
using Overlink.Net;
using Overlink.Persistence;

namespace Overlink.UI
{
    /// <summary>
    /// Shared collection projection for the Gacha and Collection screens.
    /// Signed-in gameplay reads ownership/progression from <see cref="CloudPlayerDataService"/>. The
    /// PlayerPrefs implementation remains only as an isolated legacy/editor fallback and is never used
    /// after the cloud account state is loaded.
    /// <para>
    /// Decks are no longer stored here. Both this binder and the deck builder read and write the same
    /// <see cref="DeckLibrary"/> records, so a saved deck is the same object in both screens; the
    /// deck helpers below are adapters that keep the binder's card-focused call sites unchanged.
    /// </para>
    /// </summary>
    public static class CollectionData
    {
        public const string ResolvedKey = "Overlink.OfflinePack.Resolved";
        public const string RevealedKey = "Overlink.OfflinePack.Revealed";
        public const string OwnedKey = "Overlink.OfflinePack.Owned.";
        public const string FragmentsKey = "Overlink.OfflinePack.Fragments";
        public const string BundleResolvedKey = "Overlink.OfflinePack.Bundle.Resolved";
        public const string BundleRevealedKey = "Overlink.OfflinePack.Bundle.Revealed";
        public const string BundleLoyaltyPackKey = "Overlink.OfflinePack.Bundle.LoyaltyPack";
        public const string SingleLoyaltyPackKey = "Overlink.OfflinePack.Single.LoyaltyPack";
        public const string PurchasedPacksKey = "Overlink.OfflinePack.PurchasedPacks";
        public const string LoyaltyGrantedKey = "Overlink.OfflinePack.LoyaltyGranted";
        public const string PendingSinglePurchaseKey = "Overlink.CloudPack.Pending.Single";
        public const string PendingBundlePurchaseKey = "Overlink.CloudPack.Pending.Bundle";

        /// <summary>Authoritative free-pack entitlements; loyalty rewards are not standard purchases.</summary>
        public static int LoyaltyPacks()
        {
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            return cloud != null && cloud.IsReady && cloud.State != null
                ? Mathf.Max(0, cloud.State.loyaltyPacks) : 0;
        }

        public const string DeckKey = "Overlink.OfflineDeck.";
        public const string SingleDrawKey = "Overlink.OfflinePack.Single.Draw";
        public const string BundleDrawKey = "Overlink.OfflinePack.Bundle.Draw";
        public const string LoyaltyDrawKey = "Overlink.OfflinePack.Loyalty.Draw";

        /// <summary>The prototype ships one set; keep it as data so a second set stays a data-only change.</summary>
        public const string SetName = "SET 001";

        // Deck legality has exactly one source of truth: DeckValidator. The binder therefore stops at
        // the constructed maximum (40) and the copy cap (3), so the two screens can never disagree.
        public const int DeckLimit = DeckValidator.MaxDeckSize;
        public const int CopyLimit = DeckValidator.MaxCopiesPerCard;
        public const int LoyaltyThreshold = 100;

        public static readonly CollectionCard[] Catalog =
        {
            new CollectionCard("card_fsti_001", "Signal Scout", CardType.Unit, Faction.Fsti, Rarity.Common,
                1, 2, 2, 0, "A reliable first link.", 5),
            new CollectionCard("card_fsti_002", "Relay Spark", CardType.Relay, Faction.Fsti, Rarity.Common,
                2, 0, 0, 0, "Connects the next play.", 5),
            new CollectionCard("card_frti_001", "Repair Pulse", CardType.Ability, Faction.Frti, Rarity.Rare,
                3, 0, 0, 0, "Restore a damaged Core.", 12),
            new CollectionCard("card_fpb_001", "Bulwark Node", CardType.Unit, Faction.Fpb, Rarity.Epic,
                4, 4, 6, 0, "Hold the line while the network stabilizes.", 30),
            new CollectionCard("card_fpb_002", "Mesh Courier", CardType.Relay, Faction.Fpb, Rarity.Rare,
                2, 0, 0, 0, "Relay the line beyond the veil.", 12),
        };

        /// <summary>The three legendary units shown on the Signal Bay banner. They are featured only; packs draw from the whole library.</summary>
        public static readonly string[] FeaturedIds =
        {
            "card_fsti_049", "card_frti_048", "card_fpb_048",
        };

        public const int SinglePackSize = 5;
        public const int BundlePackCount = 10;
        public const int BundleSize = SinglePackSize * BundlePackCount;

        static bool HasLibrary => MvpCardLibrary.IsRegistered;

        static bool CloudReady
        {
            get
            {
                CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
                return cloud != null && cloud.IsReady;
            }
        }

        /// <summary>
        /// Draws <paramref name="count"/> cards uniformly from the whole library and persists their ids under
        /// <paramref name="key"/>. Odds are deliberately flat for now; this is the one place weights would go.
        /// </summary>
        static PackCard[] RandomCards(int count)
        {
            if (!HasLibrary) return new PackCard[0];
            IReadOnlyList<CardDefinition> library = MvpCardLibrary.All();
            if (library.Count == 0) return new PackCard[0];

            var cards = new PackCard[count];
            for (int i = 0; i < count; i++)
            {
                cards[i] = ToPackCard(library[UnityEngine.Random.Range(0, library.Count)]);
            }

            return cards;
        }

        /// <summary>Rolls a new pack and stores its ids under <paramref name="key"/> so reopening shows the same cards.</summary>
        static PackCard[] DrawAndStore(string key, int count)
        {
            if (CloudReady) return new PackCard[0];
            PackCard[] cards = RandomCards(count);
            var ids = new string[cards.Length];
            for (int i = 0; i < cards.Length; i++) ids[i] = cards[i].Id;
            PlayerPrefs.SetString(key, string.Join(",", ids));
            return cards;
        }

        /// <summary>The ids persisted for an opened pack, or null when none are stored or one no longer exists.</summary>
        static PackCard[] StoredDraw(string key)
        {
            string raw = PlayerPrefs.GetString(key, string.Empty);
            if (raw.Length == 0) return null;

            string[] ids = raw.Split(',');
            var cards = new PackCard[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CardDefinition definition = LibraryCard(ids[i]);
                if (definition == null) return null;
                cards[i] = ToPackCard(definition);
            }

            return cards;
        }

        static PackCard[] CurrentDraw(string key, int count)
        {
            return StoredDraw(key) ?? DrawAndStore(key, count);
        }

        static PackCard ToPackCard(CardDefinition definition)
        {
            return new PackCard(definition.Id, definition.Name, definition.Type, definition.Faction, definition.Rarity,
                definition.Cost, definition.Atk, definition.Hp, definition.AbilityText ?? string.Empty,
                FragmentValueFor(definition.Rarity));
        }

        /// <summary>The cards of the last purchased single pack, as persisted. Rolls only if none are stored.</summary>
        public static PackCard[] SinglePack() => CloudReady ? new PackCard[0] : CurrentDraw(SingleDrawKey, SinglePackSize);

        /// <summary>The fifty cards of the last purchased bundle, as persisted. Rolls only if none are stored.</summary>
        public static PackCard[] BundlePack() => CloudReady ? new PackCard[0] : CurrentDraw(BundleDrawKey, BundleSize);

        /// <summary>Every bonus card granted by the last purchase's loyalty thresholds, as persisted. Empty when none.</summary>
        public static PackCard[] LoyaltyPack() => CloudReady ? new PackCard[0] : StoredDraw(LoyaltyDrawKey) ?? new PackCard[0];

        /// <summary>Projects an authoritative Cloud Code pack manifest into the existing reveal view model.</summary>
        public static PackCard FromCloudPackCard(CloudPackCard cloudCard)
        {
            if (cloudCard == null || string.IsNullOrEmpty(cloudCard.id)) return null;
            CardDefinition definition = LibraryCard(cloudCard.id);
            if (definition != null) return ToPackCard(definition);
            CollectionCard authored = Find(cloudCard.id);
            if (authored == null) return null;
            return new PackCard(authored.Id, authored.Name, authored.Type, authored.Faction, authored.Rarity,
                authored.Cost, authored.Atk, authored.Hp, authored.Rules, authored.FragmentValue);
        }

        /// <summary>Rolls the bonus packs a purchase earned (five cards each) and stores them together.</summary>
        static PackCard[] RollLoyalty(int rewards)
        {
            return DrawAndStore(LoyaltyDrawKey, rewards * SinglePackSize);
        }

        /// <summary>
        /// Resolves the ten-pack bundle once: commits ownership and fragments for all fifty cards plus
        /// any loyalty pack the purchase crossed into existence, advances the purchased-pack counter by
        /// exactly ten, and records how many free bonus packs the threshold crossings granted.
        /// </summary>
        public static void EnsureBundleResolved()
        {
            if (CloudReady) return;
            if (PlayerPrefs.GetInt(BundleResolvedKey, 0) == 1)
            {
                return;
            }

            PackCard[] cards = DrawAndStore(BundleDrawKey, BundleSize);
            int fragments = CommitCards(cards, new HashSet<string>());

            // §5: a purchase crossing 100 purchased packs grants one free bonus pack per threshold.
            int rewards = CommitPurchase(BundlePackCount);
            PlayerPrefs.SetInt(BundleLoyaltyPackKey, rewards);
            // The loyalty packs are standard packs resolved with the same ownership rules (§10.7).
            fragments += CommitCards(RollLoyalty(rewards), new HashSet<string>());

            PlayerPrefs.SetInt(FragmentsKey, Fragments() + fragments);
            PlayerPrefs.SetInt(BundleResolvedKey, 1);
            PlayerPrefs.SetInt(BundleRevealedKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>Loyalty packs granted with the resolved bundle (0 unless a 100-threshold was crossed).</summary>
        public static int BundleLoyaltyPacks()
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(BundleLoyaltyPackKey, 0));
        }

        /// <summary>
        /// Duplicate flag for a bundle card at reveal time: an earlier copy inside the bundle, or any
        /// ownership that predates the bundle commit (recovered from the committed counts minus the
        /// bundle's own copies, so the flag stays honest after relaunches).
        /// </summary>
        public static bool IsBundleDuplicate(int index)
        {
            PackCard[] manifest = BundlePack();
            if (index < 0 || index >= manifest.Length) return false;
            string id = manifest[index].Id;
            for (int i = 0; i < index; i++)
            {
                if (manifest[i].Id == id) return true;
            }

            int copiesInBundle = 0;
            for (int i = 0; i < manifest.Length; i++)
            {
                if (manifest[i].Id == id) copiesInBundle++;
            }

            return PulledCopies(id) - copiesInBundle > 0;
        }

        // ── Loyalty counter (§5): cumulative purchased packs, thresholds grant free bonus packs ──

        public static int PurchasedPacks()
        {
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud != null && cloud.IsReady) return Mathf.Max(0, cloud.State.purchasedPacks);
            return Mathf.Max(0, PlayerPrefs.GetInt(PurchasedPacksKey, 0));
        }

        public static int LoyaltyGranted()
        {
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud != null && cloud.IsReady) return Mathf.Max(0, cloud.State.loyaltyGranted);
            return Mathf.Max(0, PlayerPrefs.GetInt(LoyaltyGrantedKey, 0));
        }

        public static int LoyaltyProgress()
        {
            return PurchasedPacks() % LoyaltyThreshold;
        }

        public static int UnclaimedLoyaltyRewards()
        {
            return Mathf.Max(0, PurchasedPacks() / LoyaltyThreshold - LoyaltyGranted());
        }

        /// <summary>Commits a purchase of <paramref name="packs"/> purchased packs and grants one free
        /// bonus pack per 100-threshold crossed. Returns the number of rewards granted.</summary>
        public static int CommitPurchase(int packs)
        {
            if (CloudReady) return 0;
            int before = PurchasedPacks();
            int after = before + Mathf.Max(0, packs);
            PlayerPrefs.SetInt(PurchasedPacksKey, after);
            int granted = after / LoyaltyThreshold - before / LoyaltyThreshold;
            if (granted > 0)
            {
                PlayerPrefs.SetInt(LoyaltyGrantedKey, LoyaltyGranted() + granted);
            }

            PlayerPrefs.Save();
            return Mathf.Max(0, granted);
        }

        // ── Deck seam: adapters over DeckLibrary, the same store the deck builder writes ────────

        public static int DeckCopies(string id)
        {
            EnsureDeckMigration();
            return DeckLibrary.CopiesInActive(id);
        }

        /// <summary>Size of the active saved deck. Counts every card, not only catalog cards.</summary>
        public static int DeckSize()
        {
            EnsureDeckMigration();
            string active = DeckLibrary.ActiveId;
            return active.Length == 0 ? 0 : DeckLibrary.CardCount(DeckLibrary.Load(active));
        }

        /// <summary>Adds one copy to the active deck if ownership, copy limit and deck size allow.</summary>
        public static bool AddToDeck(string id)
        {
            EnsureDeckMigration();
            int copies = DeckLibrary.CopiesInActive(id);
            if (copies >= CopyLimit || copies >= Owned(id) || DeckSize() >= DeckLimit) return false;
            return DeckLibrary.SetActiveCopies(id, copies + 1);
        }

        /// <summary>Removes one copy from the active deck; used by the binder's deck view.</summary>
        public static bool RemoveFromDeck(string id)
        {
            EnsureDeckMigration();
            int copies = DeckLibrary.CopiesInActive(id);
            if (copies <= 0) return false;
            return DeckLibrary.SetActiveCopies(id, copies - 1);
        }

        /// <summary>
        /// Imports the pre-unification stores once: the deck builder's single <c>SavedDeck</c> slot and
        /// this binder's per-card counts. Both imports are idempotent, so the guard only spares the
        /// PlayerPrefs reads per call. The dictionary screen is the entry point because it owns the card
        /// ids the counts are keyed by; the deck builder calls it too so a player who never opens the
        /// binder still gets a single, fully migrated library.
        /// </summary>
        public static void EnsureDeckMigration()
        {
            if (CloudReady) return;
            if (s_deckMigrated) return;
            s_deckMigrated = true;
            DeckLibrary.MigrateLegacySavedDeck();
            DeckLibrary.MigrateLegacyCounts(CatalogIds);
        }

        static bool s_deckMigrated;

        public static void EnsureResolved()
        {
            if (CloudReady) return;
            if (PlayerPrefs.GetInt(ResolvedKey, 0) == 1)
            {
                return;
            }

            PackCard[] cards = DrawAndStore(SingleDrawKey, SinglePackSize);
            int fragments = CommitCards(cards, new HashSet<string>());
            PlayerPrefs.SetInt(FragmentsKey, Fragments() + fragments);
            PlayerPrefs.SetInt(ResolvedKey, 1);
            PlayerPrefs.SetInt(RevealedKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Adds each card to ownership and returns the duplicate-fragment value. A card is a duplicate when an
        /// earlier card in <paramref name="seen"/> or the stored count already has it.
        /// </summary>
        static int CommitCards(PackCard[] cards, HashSet<string> seen)
        {
            int fragments = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                PackCard card = cards[i];
                int stored = Mathf.Max(0, PlayerPrefs.GetInt(OwnedKey + card.Id, 0));
                bool duplicate = !seen.Add(card.Id) || stored > 0;
                PlayerPrefs.SetInt(OwnedKey + card.Id, stored + 1);
                if (duplicate) fragments += card.FragmentValue;
            }

            return fragments;
        }

        /// <summary>Resolve a purchased single pack once, including any earned loyalty pack.
        /// Collection seeding uses EnsureResolved directly and never records a purchase.</summary>
        public static void EnsurePurchasedSingleResolved()
        {
            if (CloudReady) return;
            if (PlayerPrefs.GetInt(ResolvedKey, 0) == 1) return;
            PackCard[] cards = DrawAndStore(SingleDrawKey, SinglePackSize);
            int fragments = CommitCards(cards, new HashSet<string>());
            PlayerPrefs.SetInt(ResolvedKey, 1);
            int rewards = CommitPurchase(1);
            PlayerPrefs.SetInt(SingleLoyaltyPackKey, rewards);
            fragments += CommitCards(RollLoyalty(rewards), new HashSet<string>());

            PlayerPrefs.SetInt(FragmentsKey, Fragments() + fragments);
            PlayerPrefs.Save();
        }

        public static int SingleLoyaltyPacks() => Mathf.Max(0, PlayerPrefs.GetInt(SingleLoyaltyPackKey, 0));

        /// <summary>Copies the player has actually pulled, without the local library fallback.</summary>
        public static int PulledCopies(string id)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(OwnedKey + id, 0));
        }

        public static int Owned(string id)
        {
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud != null && cloud.IsReady)
                return cloud.State.OwnedCopies(id);

            int stored = PulledCopies(id);
            return stored;
        }

        public static int Fragments()
        {
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud != null && cloud.IsReady) return Mathf.Max(0, cloud.State.fragments);
            return Mathf.Max(0, PlayerPrefs.GetInt(FragmentsKey, 0));
        }

        public static CollectionCard Find(string id)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].Id == id) return Catalog[i];
            }

            return null;
        }

        // ── Card lookup across the gacha catalog and the MVP library ─────────────────────────

        /// <summary>
        /// Resolves any card the binder can show: the authored gacha catalog first (its names and
        /// copy are the ones the packs awarded), then the 100-card MVP library the deck builder uses.
        /// </summary>
        public static CollectionCard Resolve(string id)
        {
            CardDefinition definition = LibraryCard(id);
            if (definition != null) return FromDefinition(definition);
            return Find(id);
        }

        public static CardDefinition LibraryCard(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLibraryIndex();
            return s_libraryById.TryGetValue(id, out CardDefinition card) ? card : null;
        }

        public static bool IsLibraryCard(string id) => LibraryCard(id) != null;

        /// <summary>Projects a library card into the tile/detail model the binder already renders.</summary>
        public static CollectionCard FromDefinition(CardDefinition card)
        {
            if (card == null) return null;
            return new CollectionCard(card.Id, card.Name, card.Type, card.Faction, card.Rarity,
                card.Cost, card.Atk, card.Hp, card.Shield, card.AbilityText ?? string.Empty,
                FragmentValueFor(card.Rarity));
        }

        /// <summary>Duplicate-conversion value per rarity, matching the authored pack manifests.</summary>
        public static int FragmentValueFor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return 12;
                case Rarity.Epic: return 30;
                case Rarity.Legendary: return 60;
                default: return 5;
            }
        }

        /// <summary>
        /// Every card a deck may contain for one faction: the 100-card MVP library plus the authored
        /// gacha catalog. The catalog predates the library and uses its own low ids (001/002), so the
        /// deck builder would otherwise refuse — and discard — a card the binder just added. One shared
        /// pool is what makes "saved here, editable there" true in both directions.
        /// </summary>
        /// <summary>Expands a saved deck's counts into one definition per copy, or an empty list for a null deck.</summary>
        public static List<CardDefinition> ExpandDeck(DeckData data)
        {
            var deck = new List<CardDefinition>();
            if (data == null || data.Cards == null) return deck;
            var pool = DeckPool(data.Faction);
            foreach (var entry in data.Cards)
            {
                CardDefinition card = pool.Find(c => c != null && c.Id == entry.Key);
                if (card == null) continue;
                for (int i = 0; i < entry.Value; i++) deck.Add(card);
            }
            return deck;
        }

        public static List<CardDefinition> DeckPool(Faction faction)
        {
            var pool = new List<CardDefinition>(MvpCardLibrary.ByFaction(faction));
            for (int i = 0; i < Catalog.Length; i++)
            {
                CollectionCard card = Catalog[i];
                if (card.Faction != faction) continue;

                // If the ids ever collide the library entry wins, otherwise the deck would list two
                // rows for one id and count it twice.
                if (IsLibraryCard(card.Id)) continue;

                pool.Add(new CardDefinition
                {
                    Id = card.Id,
                    Name = card.Name,
                    Faction = card.Faction,
                    Rarity = card.Rarity,
                    Type = card.Type,
                    Cost = card.Cost,
                    Atk = card.Atk,
                    Hp = card.Hp,
                    Shield = card.Shield,
                    AbilityText = card.Rules
                });
            }

            return pool;
        }

        static void EnsureLibraryIndex()
        {
            if (s_libraryById != null) return;

            s_libraryById = new Dictionary<string, CardDefinition>(System.StringComparer.Ordinal);
            IReadOnlyList<CardDefinition> all = MvpCardLibrary.All();
            for (int i = 0; i < all.Count; i++)
            {
                CardDefinition card = all[i];
                if (card != null && !string.IsNullOrEmpty(card.Id)) s_libraryById[card.Id] = card;
            }
        }

        static Dictionary<string, CardDefinition> s_libraryById;

        static string[] CatalogIds
        {
            get
            {
                if (s_catalogIds == null)
                {
                    s_catalogIds = new string[Catalog.Length];
                    for (int i = 0; i < Catalog.Length; i++) s_catalogIds[i] = Catalog[i].Id;
                }

                return s_catalogIds;
            }
        }

        static string[] s_catalogIds;

        /// <summary>Set membership (§4): the whole prototype catalog ships as SET 001, exposed as data rather
        /// than as a hard-coded label.</summary>
        public static string SetFor(CollectionCard card)
        {
            return card == null ? string.Empty : SetName;
        }

        /// <summary>
        /// Shared rarity accent colour. The scene builder bakes it into authored outlines and text, and
        /// the runtime screens use it for anything tinted dynamically, so both stay on one palette.
        /// </summary>
        public static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return new Color(0.32f, 0.68f, 1f);
                case Rarity.Epic: return new Color(0.75f, 0.38f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.65f, 0.22f);
                default: return new Color(0.65f, 0.75f, 0.84f);
            }
        }

        /// <summary>
        /// Every card the binder shows: the 150-card MVP library, plus any authored catalog entry the library
        /// does not carry. The library wins on id collisions, so each card appears once.
        /// </summary>
        public static List<CollectionCard> BinderCards()
        {
            var cards = new List<CollectionCard>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            IReadOnlyList<CardDefinition> library = MvpCardLibrary.All();
            for (int i = 0; i < library.Count; i++)
            {
                CollectionCard card = FromDefinition(library[i]);
                if (card != null && seen.Add(card.Id)) cards.Add(card);
            }

            for (int i = 0; i < Catalog.Length; i++)
            {
                if (seen.Add(Catalog[i].Id)) cards.Add(Catalog[i]);
            }

            return cards;
        }

        public static void Reset()
        {
            // A signed-in account is server-owned. This legacy reset is intentionally local-only and
            // must never masquerade as a cloud collection wipe.
            if (CloudReady) return;
            foreach (CollectionCard card in BinderCards())
            {
                PlayerPrefs.DeleteKey(OwnedKey + card.Id);
                PlayerPrefs.DeleteKey(DeckKey + card.Id);
            }

            for (int i = 0; i < Catalog.Length; i++)
            {
                PlayerPrefs.DeleteKey(OwnedKey + Catalog[i].Id);
                PlayerPrefs.DeleteKey(DeckKey + Catalog[i].Id);
            }

            PlayerPrefs.DeleteKey(ResolvedKey);
            PlayerPrefs.DeleteKey(RevealedKey);
            PlayerPrefs.DeleteKey(FragmentsKey);
            PlayerPrefs.DeleteKey(BundleResolvedKey);
            PlayerPrefs.DeleteKey(BundleRevealedKey);
            PlayerPrefs.DeleteKey(BundleLoyaltyPackKey);
            PlayerPrefs.DeleteKey(SingleLoyaltyPackKey);
            PlayerPrefs.DeleteKey(PurchasedPacksKey);
            PlayerPrefs.DeleteKey(LoyaltyGrantedKey);
            PlayerPrefs.DeleteKey(SingleDrawKey);
            PlayerPrefs.DeleteKey(BundleDrawKey);
            PlayerPrefs.DeleteKey(LoyaltyDrawKey);

            // The saved decks now live in DeckLibrary. Reset wipes them too, and drops the old
            // single-slot payload so the next migration cannot resurrect a deck the player cleared.
            DeckLibrary.DeleteAll();
            PlayerPrefs.DeleteKey(DeckLibrary.LegacyDeckKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears only the transient pack-opening cursor. The cards, fragments, loyalty ledger, and
        /// deck are collection progress and must survive leaving the Gacha screen; the next visit should
        /// nevertheless start with a fresh row of face-down cards instead of restoring an opened slot.
        /// </summary>
        public static void ClearPackRevealProgress()
        {
            if (CloudReady) return;
            PlayerPrefs.DeleteKey(RevealedKey);
            PlayerPrefs.DeleteKey(BundleRevealedKey);
            PlayerPrefs.Save();
        }

        /// <summary>Start a new resolution without discarding owned cards or the loyalty ledger.</summary>
        public static void BeginFreshPack(bool bundle)
        {
            if (CloudReady) return;
            PlayerPrefs.DeleteKey(bundle ? BundleResolvedKey : ResolvedKey);
            ClearPackRevealProgress();
        }

        public sealed class CollectionCard
        {
            public readonly string Id;
            public readonly string Name;
            public readonly CardType Type;
            public readonly Faction Faction;
            public readonly Rarity Rarity;
            public readonly int Cost;
            public readonly int Atk;
            public readonly int Hp;
            public readonly int Shield;
            public readonly string Rules;
            public readonly int FragmentValue;

            public CollectionCard(string id, string name, CardType type, Faction faction, Rarity rarity,
                int cost, int atk, int hp, int shield, string rules, int fragmentValue)
            {
                Id = id;
                Name = name;
                Type = type;
                Faction = faction;
                Rarity = rarity;
                Cost = cost;
                Atk = atk;
                Hp = hp;
                Shield = shield;
                Rules = rules;
                FragmentValue = fragmentValue;
            }
        }

        public sealed class PackCard
        {
            public readonly string Id;
            public readonly string Name;
            public readonly CardType Type;
            public readonly Faction Faction;
            public readonly Rarity Rarity;
            public readonly int Cost;
            public readonly int Atk;
            public readonly int Hp;
            public readonly string Rules;
            public readonly int FragmentValue;

            public PackCard(string id, string name, CardType type, Faction faction, Rarity rarity,
                int cost, int atk, int hp, string rules, int fragmentValue)
            {
                Id = id;
                Name = name;
                Type = type;
                Faction = faction;
                Rarity = rarity;
                Cost = cost;
                Atk = atk;
                Hp = hp;
                Rules = rules;
                FragmentValue = fragmentValue;
            }
        }
    }
}
