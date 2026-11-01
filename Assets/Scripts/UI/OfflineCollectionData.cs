using System.Collections.Generic;
using UnityEngine;
using Overlink.Core;

namespace Overlink.UI
{
    /// <summary>
    /// Shared deterministic collection state for the offline Gacha and Collection prototypes.
    /// This is intentionally a local PlayerPrefs-backed seam; it is not an economy or server ledger.
    /// </summary>
    public static class OfflineCollectionData
    {
        public const string ResolvedKey = "Overlink.OfflinePack.Resolved";
        public const string RevealedKey = "Overlink.OfflinePack.Revealed";
        public const string OwnedKey = "Overlink.OfflinePack.Owned.";
        public const string FragmentsKey = "Overlink.OfflinePack.Fragments";
        public const string BundleResolvedKey = "Overlink.OfflinePack.Bundle.Resolved";
        public const string BundleRevealedKey = "Overlink.OfflinePack.Bundle.Revealed";
        public const string BundleLoyaltyPackKey = "Overlink.OfflinePack.Bundle.LoyaltyPack";
        public const string PurchasedPacksKey = "Overlink.OfflinePack.PurchasedPacks";
        public const string LoyaltyGrantedKey = "Overlink.OfflinePack.LoyaltyGranted";
        public const string DeckKey = "Overlink.OfflineDeck.";

        /// <summary>The prototype ships one set; the binder exposes it so the set filter is exercised.</summary>
        public const string SetName = "SET 001";
        public const int DeckLimit = 30;
        public const int CopyLimit = 3;
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
            new CollectionCard("card_frti_002", "Aurora Vanguard", CardType.Unit, Faction.Frti, Rarity.Legendary,
                5, 5, 5, 0, "Strike first; the dawn follows.", 60),
        };

        /// <summary>Five-card deterministic pack used by the offline reveal prototype; the final entry is a duplicate.</summary>
        public static readonly PackCard[] PackManifest =
        {
            new PackCard("card_fsti_001", "Signal Scout", CardType.Unit, Faction.Fsti, Rarity.Common, 1, 2, 2, "A reliable first link.", 5),
            new PackCard("card_fsti_002", "Relay Spark", CardType.Relay, Faction.Fsti, Rarity.Common, 2, 0, 0, "Connects the next play.", 5),
            new PackCard("card_frti_001", "Repair Pulse", CardType.Ability, Faction.Frti, Rarity.Rare, 3, 0, 0, "Restore a damaged Core.", 12),
            new PackCard("card_fpb_001", "Bulwark Node", CardType.Unit, Faction.Fpb, Rarity.Epic, 4, 4, 6, "Hold the line while the network stabilizes.", 30),
            new PackCard("card_fsti_001", "Signal Scout", CardType.Unit, Faction.Fsti, Rarity.Common, 1, 2, 2, "A duplicate copy becomes Data Fragments.", 5),
        };

        /// <summary>
        /// Ten deterministic standard packs (fifty cards) for the offline Milestone 3 prototype.
        /// Authored, not rolled: the bundle always contains the same cards so the queue, grouping,
        /// guarantee audit and loyalty simulation are reviewable frame by frame. Pack 2 slot 3 carries
        /// the Epic that satisfies the ten-pack guarantee; packs 3 and 6 carry Legendary hit slots.
        /// </summary>
        public static readonly PackCard[] TenPackManifest = BuildTenPackManifest();

        static PackCard[] BuildTenPackManifest()
        {
            string[][] packs =
            {
                new[] { "card_fsti_001", "card_fsti_002", "card_frti_001", "card_fsti_001", "card_fpb_002" },
                new[] { "card_fsti_002", "card_frti_001", "card_fpb_001", "card_fsti_001", "card_fpb_002" },
                new[] { "card_fsti_001", "card_fpb_002", "card_frti_002", "card_fsti_002", "card_frti_001" },
                new[] { "card_fpb_001", "card_fsti_001", "card_fsti_002", "card_frti_001", "card_fpb_002" },
                new[] { "card_fsti_002", "card_fpb_002", "card_fsti_001", "card_frti_001", "card_fpb_001" },
                new[] { "card_frti_001", "card_fsti_001", "card_fpb_002", "card_fsti_002", "card_frti_002" },
                new[] { "card_fsti_001", "card_fsti_002", "card_fpb_001", "card_frti_001", "card_fpb_002" },
                new[] { "card_fpb_002", "card_frti_001", "card_fsti_002", "card_fsti_001", "card_fpb_001" },
                new[] { "card_fsti_002", "card_fpb_002", "card_frti_001", "card_fsti_001", "card_fpb_001" },
                new[] { "card_fsti_001", "card_frti_001", "card_fpb_002", "card_fsti_002", "card_fpb_001" },
            };

            var cards = new List<PackCard>();
            for (int p = 0; p < packs.Length; p++)
            {
                for (int s = 0; s < packs[p].Length; s++)
                {
                    cards.Add(FromCatalog(packs[p][s]));
                }
            }

            return cards.ToArray();
        }

        /// <summary>Copies a catalog entry into pack-card form so manifest and binder never drift.</summary>
        static PackCard FromCatalog(string id)
        {
            CollectionCard card = Find(id);
            if (card == null)
            {
                return new PackCard(id, id, CardType.Unit, Faction.Fsti, Rarity.Common, 0, 0, 0, string.Empty, 0);
            }

            return new PackCard(card.Id, card.Name, card.Type, card.Faction, card.Rarity,
                card.Cost, card.Atk, card.Hp, card.Rules, card.FragmentValue);
        }

        public static int PacksInBundle => TenPackManifest.Length / 5;

        /// <summary>
        /// Resolves the ten-pack bundle once: commits ownership and fragments for all fifty cards plus
        /// any loyalty pack the purchase crossed into existence, advances the purchased-pack counter by
        /// exactly ten, and records how many free bonus packs the threshold crossings granted.
        /// </summary>
        public static void EnsureBundleResolved()
        {
            if (PlayerPrefs.GetInt(BundleResolvedKey, 0) == 1)
            {
                return;
            }

            var seen = new HashSet<string>();
            int fragments = 0;
            for (int i = 0; i < TenPackManifest.Length; i++)
            {
                PackCard card = TenPackManifest[i];
                int owned = PlayerPrefs.GetInt(OwnedKey + card.Id, 0);
                bool duplicate = !seen.Add(card.Id) || owned > 0;
                PlayerPrefs.SetInt(OwnedKey + card.Id, owned + 1);
                if (duplicate) fragments += card.FragmentValue;
            }

            // §5: a purchase crossing 100 purchased packs grants one free bonus pack per threshold.
            int rewards = CommitPurchase(10);
            PlayerPrefs.SetInt(BundleLoyaltyPackKey, rewards);
            if (rewards > 0)
            {
                // The loyalty pack is a standard pack resolved with the same ownership rules (§10.7).
                for (int i = 0; i < PackManifest.Length; i++)
                {
                    PackCard card = PackManifest[i];
                    int owned = PlayerPrefs.GetInt(OwnedKey + card.Id, 0);
                    bool duplicate = !seen.Add("loyalty:" + card.Id) || owned > 0;
                    PlayerPrefs.SetInt(OwnedKey + card.Id, owned + 1);
                    if (duplicate) fragments += card.FragmentValue;
                }
            }

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
            if (index < 0 || index >= TenPackManifest.Length) return false;
            string id = TenPackManifest[index].Id;
            for (int i = 0; i < index; i++)
            {
                if (TenPackManifest[i].Id == id) return true;
            }

            int copiesInBundle = 0;
            for (int i = 0; i < TenPackManifest.Length; i++)
            {
                if (TenPackManifest[i].Id == id) copiesInBundle++;
            }

            return Owned(id) - copiesInBundle > 0;
        }

        /// <summary>Index of the first Epic-or-better card in the bundle: the ten-pack guarantee audit.</summary>
        public static int BundleGuaranteeIndex()
        {
            for (int i = 0; i < TenPackManifest.Length; i++)
            {
                if (TenPackManifest[i].Rarity >= Rarity.Epic) return i;
            }

            return -1;
        }

        // ── Loyalty counter (§5): cumulative purchased packs, thresholds grant free bonus packs ──

        public static int PurchasedPacks()
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(PurchasedPacksKey, 0));
        }

        public static int LoyaltyGranted()
        {
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

        // ── Offline deck seam (Milestone 2 deck-builder integration) ─────────────────────────────

        public static int DeckCopies(string id)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(DeckKey + id, 0));
        }

        public static int DeckSize()
        {
            int total = 0;
            for (int i = 0; i < Catalog.Length; i++) total += DeckCopies(Catalog[i].Id);
            return total;
        }

        /// <summary>Adds one copy to the offline deck if ownership, copy limit and deck size allow.</summary>
        public static bool AddToDeck(string id)
        {
            int copies = DeckCopies(id);
            if (copies >= CopyLimit || copies >= Owned(id) || DeckSize() >= DeckLimit) return false;
            PlayerPrefs.SetInt(DeckKey + id, copies + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void EnsureResolved()
        {
            if (PlayerPrefs.GetInt(ResolvedKey, 0) == 1)
            {
                return;
            }

            var seen = new HashSet<string>();
            int fragments = 0;
            for (int i = 0; i < PackManifest.Length; i++)
            {
                PackCard card = PackManifest[i];
                int owned = PlayerPrefs.GetInt(OwnedKey + card.Id, 0);
                bool duplicate = !seen.Add(card.Id) || owned > 0;
                PlayerPrefs.SetInt(OwnedKey + card.Id, owned + 1);
                if (duplicate)
                {
                    fragments += card.FragmentValue;
                }
            }

            PlayerPrefs.SetInt(FragmentsKey, fragments);
            PlayerPrefs.SetInt(ResolvedKey, 1);
            PlayerPrefs.SetInt(RevealedKey, 0);
            PlayerPrefs.Save();
        }

        public static int Owned(string id)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(OwnedKey + id, 0));
        }

        public static int Fragments()
        {
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

        /// <summary>Set membership (§4): the whole prototype catalog ships as SET 001. The binder's set
        /// filter compares through this seam, so a second set is a data-only change.</summary>
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

        public static void Reset()
        {
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
            PlayerPrefs.DeleteKey(PurchasedPacksKey);
            PlayerPrefs.DeleteKey(LoyaltyGrantedKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears only the transient pack-opening cursor. The cards, fragments, loyalty ledger, and
        /// deck are collection progress and must survive leaving the Gacha screen; the next visit should
        /// nevertheless start with a fresh row of face-down cards instead of restoring an opened slot.
        /// </summary>
        public static void ClearPackRevealProgress()
        {
            PlayerPrefs.DeleteKey(RevealedKey);
            PlayerPrefs.DeleteKey(BundleRevealedKey);
            PlayerPrefs.Save();
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
