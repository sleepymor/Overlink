using System;
using System.Collections.Generic;
using Overlink.Core;
using Overlink.Persistence;

namespace Overlink.Net
{
    [Serializable]
    public sealed class ClientVersionResponse
    {
        public bool supported;
        public string minimumVersion = "";
        public string updateUrl = "";
        public string message = "";
    }

    /// <summary>JSON-safe DTO returned by the OverlinkProgression Cloud Code module.</summary>
    [Serializable]
    public sealed class CloudPlayerState
    {
        public int schemaVersion = 3;
        public bool starterGranted;
        public string starterFaction = "";
        public int fragments;
        public int purchasedPacks;
        public int loyaltyGranted;
        public int xp;
        public int level = 1;
        public int wins;
        public int losses;
        public int draws;
        public string activeDeckId = "";
        public string battleDeckId = "";
        public CloudDeck battleDeckSnapshot;
        public int nextDeckSequence;
        // Cloud Code persists Owned as a JSON object (card id -> copies), not an array. Keep the
        // wire shape identical so the SDK can deserialize the authoritative response without a
        // client-side migration layer.
        public Dictionary<string, int> owned = new Dictionary<string, int>();
        public CloudDeck[] decks = Array.Empty<CloudDeck>();
        public CloudMatchRecord[] matchHistory = Array.Empty<CloudMatchRecord>();
        public string pendingMatchId = "";
        public string pendingResult = "";
        public string pendingOpponentId = "";
        public string dailyXpDate = "";
        public int dailyXp;
        public int standardPacks;
        public int loyaltyPacks;
        public int signalCredits = 2400;
        // The server keeps the most recent pack results in the authoritative state as well as in the
        // top-level response fields. Keep both transient arrays on the client DTO so Cloud Code's
        // complete state payload can be deserialized without rejecting unknown members.
        public CloudPackCard[] lastPack = Array.Empty<CloudPackCard>();
        public CloudPackCard[] lastLoyaltyPack = Array.Empty<CloudPackCard>();
        public Dictionary<string, CloudPurchaseReceipt> purchaseReceipts = new Dictionary<string, CloudPurchaseReceipt>();
        public Dictionary<string, CloudPackOpenReceipt> packOpenReceipts = new Dictionary<string, CloudPackOpenReceipt>();

        public int OwnedCopies(string cardId)
        {
            if (owned == null || string.IsNullOrEmpty(cardId)) return 0;
            return owned.TryGetValue(cardId, out int count) ? Math.Max(0, count) : 0;
        }

        public DeckData FindDeck(string id)
        {
            if (decks == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < decks.Length; i++)
                if (decks[i] != null && decks[i].id == id) return decks[i].ToDeckData();
            return null;
        }

        public DeckData FindBattleDeck()
        {
            if (string.IsNullOrEmpty(battleDeckId)) return null;
            if (battleDeckSnapshot != null && battleDeckSnapshot.id == battleDeckId)
                return battleDeckSnapshot.ToDeckData();
            return FindDeck(battleDeckId);
        }

        public string DeckName(string id)
        {
            if (decks == null || string.IsNullOrEmpty(id)) return string.Empty;
            for (int i = 0; i < decks.Length; i++)
                if (decks[i] != null && decks[i].id == id) return decks[i].name ?? id;
            return string.Empty;
        }

        public IReadOnlyList<DeckSummary> DeckSummaries()
        {
            var result = new List<DeckSummary>();
            if (decks == null) return result;
            for (int i = 0; i < decks.Length; i++)
            {
                CloudDeck deck = decks[i];
                if (deck == null || string.IsNullOrEmpty(deck.id)) continue;
                DeckData data = deck.ToDeckData();
                result.Add(new DeckSummary(deck.id, deck.name ?? deck.id, data.Faction,
                    DeckCardCount(data), deck.id == activeDeckId));
            }
            return result;
        }

        static int DeckCardCount(DeckData data)
        {
            if (data?.Cards == null) return 0;
            int total = 0;
            foreach (int count in data.Cards.Values) total += Math.Max(0, count);
            return total;
        }
    }

    [Serializable]
    public sealed class CloudDeck
    {
        public string id = "";
        public string name = "";
        public string faction = "fsti";
        public Dictionary<string, int> cards = new Dictionary<string, int>();

        public DeckData ToDeckData()
        {
            CardJson.TryParseFactionToken(faction, out Faction parsedFaction);
            var data = new DeckData { Version = 1, Faction = parsedFaction };
            if (cards != null)
                foreach (KeyValuePair<string, int> entry in cards)
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value > 0)
                        data.Cards[entry.Key] = entry.Value;
            return data;
        }

        public static CloudDeck FromDeck(string id, string name, DeckData data)
        {
            var entries = new Dictionary<string, int>();
            if (data?.Cards != null)
                foreach (KeyValuePair<string, int> entry in data.Cards)
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value > 0)
                        entries[entry.Key] = entry.Value;
            return new CloudDeck
            {
                id = id ?? "",
                name = name ?? id ?? "",
                faction = CardJson.FactionToken(data?.Faction ?? Faction.Fsti),
                cards = entries
            };
        }
    }

    [Serializable]
    public sealed class CloudMatchRecord
    {
        public string matchId = "";
        public string result = "";
        public int xp;
        public long timestamp;
    }

    [Serializable]
    public sealed class CloudStateResponse
    {
        public CloudPlayerState state;
        public string error = "";
        public CloudPackCard[] pack = Array.Empty<CloudPackCard>();
        public CloudPackCard[] loyaltyPack = Array.Empty<CloudPackCard>();
        public int fragmentsAwarded;
    }

    [Serializable]
    public sealed class EmailVerificationState
    {
        public bool verified;
        public string email = "";
    }

    [Serializable]
    public sealed class CloudPackCard
    {
        public string id = "";
        public string rarity = "Common";
    }

    [Serializable]
    public sealed class CloudPurchaseReceipt
    {
        public int packCount;
        public int cost;
        public int loyaltyRewards;
    }

    [Serializable]
    public sealed class CloudPackOpenReceipt
    {
        public int packCount;
        public CloudPackCard[] pack = Array.Empty<CloudPackCard>();
        public CloudPackCard[] loyaltyPack = Array.Empty<CloudPackCard>();
        public int fragmentsAwarded;
    }
}
