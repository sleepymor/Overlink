using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace OverlinkProgression;

public sealed class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton<IGameApiClient>(GameApiClient.Create());
    }
}

public sealed class ProgressionModule
{
    private const string StateKey = "overlink_progression_v1";
    // Ownership remains part of the existing progression system, but is mirrored in
    // Protected Player Data so a modified client cannot rewrite the entitlement source
    // used by competitive deck validation. The default record is retained for the existing
    // collection UI and as a migration source only. Protected data is readable by its
    // owner, but writable only by server code; use Private data for hidden match state.
    private const string ProtectedOwnershipKey = "overlink_owned_v1";
    // The deck-builder draft remains in the default record, but this selected snapshot is
    // server-written. PvP must never trust BattleDeckSnapshot from default player data.
    private const string ProtectedBattleDeckKey = "overlink_battle_deck_v1";
    // Update this server-side policy when a release becomes mandatory. Deploy the module
    // before raising the minimum.
    private const string MinimumClientVersion = "2026.11.9";
    private const string UpdateUrl = "https://github.com/sleepymor/Overlink/releases/latest";
    private const int CurrentSchema = 3;
    private const int PackSize = 5;
    private const int DailyXpCap = 300;
    private const int SinglePackCost = 100;
    private const int TenPackCost = 900;
    private const int StartingSignalCredits = 2400;

    [CloudCodeFunction("CheckClientVersion")]
    public ClientVersionResponse CheckClientVersion(string clientVersion)
    {
        bool supported = ClientVersionPolicy.IsSupported(clientVersion, MinimumClientVersion);
        return new ClientVersionResponse
        {
            Supported = supported,
            MinimumVersion = MinimumClientVersion,
            UpdateUrl = UpdateUrl,
            Message = supported ? "Version supported." : "Please update Overlink to continue."
        };
    }

    [CloudCodeFunction("GetState")]
    public async Task<CloudStateResponse> GetState(IExecutionContext context, IGameApiClient api)
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        if (BackfillStarterCredits(state))
            await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    // Only returns the caller's own selected deck. The unranked Relay prototype may
    // forward this to its host; it is not proof that a modified client sent this result.
    [CloudCodeFunction("GetOwnBattleDeck")]
    public async Task<DeckDto> GetOwnBattleDeck(IExecutionContext context, IGameApiClient api)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerId) || string.IsNullOrWhiteSpace(context.AccessToken))
            throw new InvalidOperationException("An authenticated player is required.");
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        DeckDto? deck = state.BattleDeckSnapshot;
        if (deck == null || string.IsNullOrEmpty(state.BattleDeckId) || deck.Id != state.BattleDeckId)
            throw new InvalidOperationException("Select a valid battle deck before matchmaking.");
        ValidateDeck(state, deck);
        if (!IsLegalDeck(deck)) throw new InvalidOperationException("Battle deck must contain 30 to 40 cards.");
        return NormalizeDeck(deck);
    }

    [CloudCodeFunction("GrantStarter")]
    public async Task<CloudStateResponse> GrantStarter(IExecutionContext context, IGameApiClient api, string faction)
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        if (state.StarterGranted)
        {
            if (BackfillStarterCredits(state))
                await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
            return Response(state);
        }

        FactionValue selected = ParseFaction(faction);
        AddStarterCollection(state, selected);
        state.StarterGranted = true;
        state.StarterFaction = selected.Token;
        state.SignalCredits = Math.Max(state.SignalCredits, StartingSignalCredits);
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    [CloudCodeFunction("SaveDeck")]
    public async Task<CloudStateResponse> SaveDeck(IExecutionContext context, IGameApiClient api, DeckDto deck)
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        if (!state.StarterGranted) throw new InvalidOperationException("Starter collection is not granted.");
        ValidateDeck(state, deck);

        DeckDto normalized = NormalizeDeck(deck);
        int index = state.Decks.FindIndex(d => d.Id == normalized.Id);
        if (index < 0) state.Decks.Add(normalized);
        else state.Decks[index] = normalized;
        if (string.IsNullOrWhiteSpace(state.ActiveDeckId)) state.ActiveDeckId = normalized.Id;
        if (string.IsNullOrWhiteSpace(state.BattleDeckId) && IsLegalDeck(normalized))
            state.BattleDeckId = normalized.Id;
        if (state.BattleDeckId == normalized.Id && IsLegalDeck(normalized))
            state.BattleDeckSnapshot = NormalizeDeck(normalized);

        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    [CloudCodeFunction("SetDeckSelection")]
    public async Task<CloudStateResponse> SetDeckSelection(IExecutionContext context, IGameApiClient api,
        string activeDeckId, string battleDeckId)
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        if (!string.IsNullOrEmpty(activeDeckId) && state.Decks.All(d => d.Id != activeDeckId))
            throw new InvalidOperationException("Unknown active deck.");
        if (!string.IsNullOrEmpty(battleDeckId) && state.Decks.All(d => d.Id != battleDeckId))
            throw new InvalidOperationException("Unknown battle deck.");

        DeckDto? selectedBattleDeck = state.Decks.FirstOrDefault(d => d.Id == battleDeckId);
        // Changing only the active editor deck must not erase a snapshot when the battle
        // deck's editable record is currently an incomplete draft.
        if (selectedBattleDeck != null && battleDeckId != state.BattleDeckId)
        {
            ValidateDeck(state, selectedBattleDeck);
            if (!IsLegalDeck(selectedBattleDeck)) throw new InvalidOperationException("Battle deck must be legal.");
            state.BattleDeckSnapshot = NormalizeDeck(selectedBattleDeck);
        }
        else if (string.IsNullOrEmpty(battleDeckId)) state.BattleDeckSnapshot = null;

        state.ActiveDeckId = activeDeckId ?? "";
        state.BattleDeckId = battleDeckId ?? "";
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    [CloudCodeFunction("DeleteDeck")]
    public async Task<CloudStateResponse> DeleteDeck(IExecutionContext context, IGameApiClient api, string deckId)
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        if (string.IsNullOrWhiteSpace(deckId)) throw new InvalidOperationException("Deck id is required.");
        int removed = state.Decks.RemoveAll(deck => deck.Id == deckId);
        if (removed == 0) throw new InvalidOperationException("Unknown deck.");
        if (state.ActiveDeckId == deckId)
            state.ActiveDeckId = state.Decks.FirstOrDefault()?.Id ?? "";
        if (state.BattleDeckId == deckId)
        {
            DeckDto? replacement = state.Decks.FirstOrDefault(deck => IsLegalDeck(deck));
            state.BattleDeckId = replacement?.Id ?? "";
            state.BattleDeckSnapshot = replacement == null ? null : NormalizeDeck(replacement);
        }
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    [CloudCodeFunction("DeleteAllDecks")]
    public async Task<CloudStateResponse> DeleteAllDecks(IExecutionContext context, IGameApiClient api)
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        state.Decks.Clear();
        state.ActiveDeckId = "";
        state.BattleDeckId = "";
        state.BattleDeckSnapshot = null;
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    [CloudCodeFunction("OpenPack")]
    public async Task<CloudStateResponse> OpenPack(IExecutionContext context, IGameApiClient api,
        int packCount = 1, string requestId = "")
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        string operationId = NormalizeRequestId(requestId, "Pack opening");
        int count = packCount == 10 ? 10 : packCount == 1 ? 1 : 0;
        if (count == 0) throw new InvalidOperationException("Pack opening must be for one or ten packs.");
        if (state.PackOpenReceipts.TryGetValue(operationId, out PackOpenReceiptDto previous))
        {
            if (previous.PackCount != count)
                throw new InvalidOperationException("Pack opening request id was reused for a different pack size.");
            return Response(state, previous.Pack, previous.LoyaltyPack, previous.FragmentsAwarded);
        }

        if (state.StandardPacks < count) throw new InvalidOperationException("No standard pack reward is available.");

        state.StandardPacks -= count;
        var cards = new List<PackCardDto>(count * PackSize);
        int fragments = 0;
        for (int i = 0; i < count * PackSize; i++)
        {
            PackCardDto card = RollCard();
            cards.Add(card);
            fragments += AddCard(state, card.Id, 1);
        }
        state.Fragments += fragments;
        state.LastPack = cards.ToArray();
        state.LastLoyaltyPack = Array.Empty<PackCardDto>();
        state.PackOpenReceipts[operationId] = new PackOpenReceiptDto
        {
            PackCount = count,
            Pack = cards.ToArray(),
            LoyaltyPack = Array.Empty<PackCardDto>(),
            FragmentsAwarded = fragments
        };
        TrimReceipts(state.PackOpenReceipts);
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state, cards.ToArray(), Array.Empty<PackCardDto>(), fragments);
    }

    [CloudCodeFunction("OpenLoyaltyPack")]
    public async Task<CloudStateResponse> OpenLoyaltyPack(IExecutionContext context, IGameApiClient api,
        string requestId = "")
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        string operationId = NormalizeRequestId(requestId, "Loyalty pack opening");
        if (state.PackOpenReceipts.TryGetValue(operationId, out PackOpenReceiptDto previous))
            return Response(state, previous.Pack, previous.LoyaltyPack, previous.FragmentsAwarded);

        if (state.LoyaltyPacks < 1) throw new InvalidOperationException("No loyalty pack reward is available.");
        state.LoyaltyPacks--;
        var cards = new List<PackCardDto>(PackSize);
        int fragments = 0;
        for (int i = 0; i < PackSize; i++)
        {
            PackCardDto card = RollCard();
            cards.Add(card);
            fragments += AddCard(state, card.Id, 1);
        }

        state.Fragments += fragments;
        state.LastPack = Array.Empty<PackCardDto>();
        state.LastLoyaltyPack = cards.ToArray();
        state.PackOpenReceipts[operationId] = new PackOpenReceiptDto
        {
            PackCount = 0,
            Pack = Array.Empty<PackCardDto>(),
            LoyaltyPack = cards.ToArray(),
            FragmentsAwarded = fragments
        };
        TrimReceipts(state.PackOpenReceipts);
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state, Array.Empty<PackCardDto>(), cards.ToArray(), fragments);
    }

    [CloudCodeFunction("PurchasePack")]
    public async Task<CloudStateResponse> PurchasePack(IExecutionContext context, IGameApiClient api,
        int packCount = 1, string requestId = "")
    {
        PlayerState state = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        int count = packCount == 10 ? 10 : packCount == 1 ? 1 : 0;
        if (count == 0) throw new InvalidOperationException("Pack purchase must be for one or ten packs.");

        string purchaseId = NormalizeRequestId(requestId, "Pack purchase");
        if (state.PurchaseReceipts.TryGetValue(purchaseId, out PurchaseReceiptDto previous))
        {
            if (previous.PackCount != count)
                throw new InvalidOperationException("Pack purchase request id was reused for a different pack size.");
            return Response(state);
        }

        int cost = count == 10 ? TenPackCost : SinglePackCost;
        if (state.SignalCredits < cost)
            throw new InvalidOperationException("Not enough Signal Credits for this pack purchase.");

        int purchasedBefore = state.PurchasedPacks;
        state.SignalCredits -= cost;
        state.PurchasedPacks += count;
        state.StandardPacks += count;

        // Purchased packs advance loyalty; free packs do not. Keep threshold rewards as a separate
        // loyalty entitlement so they cannot be lost if the client closes during opening.
        int loyaltyRewards = state.PurchasedPacks / 100 - purchasedBefore / 100;
        if (loyaltyRewards > 0)
        {
            state.LoyaltyGranted += loyaltyRewards;
            state.LoyaltyPacks += loyaltyRewards;
        }

        state.PurchaseReceipts[purchaseId] = new PurchaseReceiptDto
        {
            PackCount = count,
            Cost = cost,
            LoyaltyRewards = loyaltyRewards
        };
        TrimReceipts(state.PurchaseReceipts);

        await SaveState(context, api, context.AccessToken!, context.PlayerId!, state);
        return Response(state);
    }

    [CloudCodeFunction("ReportMatch")]
    public async Task<CloudStateResponse> ReportMatch(IExecutionContext context, IGameApiClient api,
        string matchId, string result, bool versusAi, string opponentPlayerId = "")
    {
        if (string.IsNullOrWhiteSpace(matchId)) throw new InvalidOperationException("Match id is required.");
        string normalized = NormalizeResult(result);
        PlayerState current = await LoadState(context, api, context.AccessToken!, context.PlayerId!);
        if (current.MatchHistory.Any(m => m.MatchId == matchId)) return Response(current);

        if (versusAi)
        {
            int xp = ApplyResult(current, normalized);
            current.MatchHistory.Add(new MatchRecordDto
            {
                MatchId = matchId, Result = normalized, Xp = xp, Timestamp = UnixNow()
            });
            await SaveState(context, api, context.AccessToken!, context.PlayerId!, current);
            return Response(current);
        }

        if (string.IsNullOrWhiteSpace(opponentPlayerId) || opponentPlayerId == context.PlayerId)
            throw new InvalidOperationException("PvP results require the opponent player id.");

        // The first report is a pending claim. The second report is read from the other account and must
        // name this account as its opponent. Neither report can award XP by itself.
        if (current.PendingMatchId == matchId) return Response(current);
        PlayerState opponent = await LoadState(context, api, context.ServiceToken!, opponentPlayerId);
        bool agree = opponent.PendingMatchId == matchId
            && opponent.PendingOpponentId == context.PlayerId
            && ResultsAgree(normalized, opponent.PendingResult);

        if (!agree)
        {
            current.PendingMatchId = matchId;
            current.PendingResult = normalized;
            current.PendingOpponentId = opponentPlayerId;
            await SaveState(context, api, context.AccessToken!, context.PlayerId!, current);
            return Response(current);
        }

        int currentXp = ApplyResult(current, normalized);
        int opponentXp = ApplyResult(opponent, opponent.PendingResult);
        long timestamp = UnixNow();
        current.MatchHistory.Add(new MatchRecordDto { MatchId = matchId, Result = normalized, Xp = currentXp, Timestamp = timestamp });
        opponent.MatchHistory.Add(new MatchRecordDto { MatchId = matchId, Result = opponent.PendingResult, Xp = opponentXp, Timestamp = timestamp });
        ClearPending(current);
        ClearPending(opponent);
        await SaveState(context, api, context.ServiceToken!, opponentPlayerId, opponent);
        await SaveState(context, api, context.AccessToken!, context.PlayerId!, current);
        return Response(current);
    }

    private static async Task<PlayerState> LoadState(IExecutionContext context, IGameApiClient api,
        string token, string playerId)
    {
        var result = await api.CloudSaveData.GetItemsAsync(context, token, context.ProjectId!, playerId,
            new List<string> { StateKey });
        string json = result.Data.Results.FirstOrDefault(item => item.Key == StateKey)?.Value?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(json)) return new PlayerState();
        PlayerState state = JsonSerializer.Deserialize<PlayerState>(json);
        if (state == null) throw new InvalidOperationException("Stored Overlink progression data is empty.");
        await LoadTrustedOwnership(context, api, playerId, state);
        bool migrated = state.Normalize();
        // Existing accounts may predate snapshots. Backfill only a fully valid selected deck;
        // an already-invalid draft cannot be reconstructed as a previous legal deck.
        if (state.BattleDeckSnapshot == null && !string.IsNullOrEmpty(state.BattleDeckId))
        {
            DeckDto? selected = state.Decks.FirstOrDefault(d => d.Id == state.BattleDeckId);
            if (selected != null && IsLegalDeck(selected))
            {
                try
                {
                    ValidateDeck(state, selected);
                    state.BattleDeckSnapshot = NormalizeDeck(selected);
                    migrated = true;
                }
                catch (InvalidOperationException) { /* Leave invalid legacy data untouched. */ }
            }
        }
        if (migrated)
            await SaveState(context, api, token, playerId, state);
        return state;
    }

    /// <summary>
    /// Loads the existing collection's ownership counts from server-write-only Protected
    /// Player Data. Existing accounts are migrated once from the legacy default record;
    /// thereafter the default Owned field is never trusted for validation.
    /// </summary>
    private static async Task LoadTrustedOwnership(IExecutionContext context, IGameApiClient api,
        string playerId, PlayerState state)
    {
        if (string.IsNullOrWhiteSpace(context.ServiceToken))
            throw new InvalidOperationException("Cloud Code service token is required for trusted ownership.");

        var result = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken!,
            context.ProjectId!, playerId, new List<string> { ProtectedOwnershipKey, ProtectedBattleDeckKey });
        var item = result.Data.Results.FirstOrDefault(entry => entry.Key == ProtectedOwnershipKey);
        if (item == null)
        {
            // One-time migration of the existing authoritative progression record. This
            // does not create a second inventory: the protected copy becomes the protected
            // access class for the same Owned collection used by the UI and pack logic.
            await SaveTrustedOwnership(context, api, playerId, state.Owned);
        }
        else
        {
            string value = item.Value?.ToString() ?? "";
            Dictionary<string, int>? owned = JsonSerializer.Deserialize<Dictionary<string, int>>(value);
            state.Owned = owned ?? new Dictionary<string, int>();
        }

        var battleItem = result.Data.Results.FirstOrDefault(entry => entry.Key == ProtectedBattleDeckKey);
        if (battleItem != null)
        {
            string battleValue = battleItem.Value?.ToString() ?? "";
            state.BattleDeckSnapshot = JsonSerializer.Deserialize<DeckDto>(battleValue);
        }
        else if (state.BattleDeckSnapshot != null && !string.IsNullOrWhiteSpace(state.BattleDeckId))
        {
            // One-time migration only after the deck has been checked against the protected
            // ownership record. Future loads ignore the client-writable default snapshot.
            DeckDto legacy = state.BattleDeckSnapshot;
            ValidateDeck(state, legacy);
            if (legacy.Id != state.BattleDeckId || !IsLegalDeck(legacy))
                throw new InvalidOperationException("The selected battle deck is invalid.");
            await SaveTrustedBattleDeck(context, api, playerId, NormalizeDeck(legacy));
        }
        else
        {
            // Never return a client-writable snapshot as if it were trusted.
            state.BattleDeckSnapshot = null;
        }
    }

    private static async Task SaveTrustedOwnership(IExecutionContext context, IGameApiClient api,
        string playerId, Dictionary<string, int> owned)
    {
        if (string.IsNullOrWhiteSpace(context.ServiceToken))
            throw new InvalidOperationException("Cloud Code service token is required for trusted ownership.");

        var cloud = api.CloudSaveData;
        var current = await cloud.GetProtectedItemsAsync(context, context.ServiceToken!, context.ProjectId!,
            playerId, new List<string> { ProtectedOwnershipKey });
        var item = current.Data.Results.FirstOrDefault(entry => entry.Key == ProtectedOwnershipKey);
        string value = JsonSerializer.Serialize(owned ?? new Dictionary<string, int>());
        var body = item == null
            ? new SetItemBody(ProtectedOwnershipKey, value)
            : new SetItemBody(ProtectedOwnershipKey, value, item.WriteLock);
        await cloud.SetProtectedItemAsync(context, context.ServiceToken!, context.ProjectId!, playerId, body);
    }

    private static async Task SaveTrustedBattleDeck(IExecutionContext context, IGameApiClient api,
        string playerId, DeckDto? deck)
    {
        if (string.IsNullOrWhiteSpace(context.ServiceToken))
            throw new InvalidOperationException("Cloud Code service token is required for a protected battle deck.");

        var cloud = api.CloudSaveData;
        var current = await cloud.GetProtectedItemsAsync(context, context.ServiceToken!, context.ProjectId!,
            playerId, new List<string> { ProtectedBattleDeckKey });
        var item = current.Data.Results.FirstOrDefault(entry => entry.Key == ProtectedBattleDeckKey);
        string value = JsonSerializer.Serialize(deck);
        var body = item == null
            ? new SetItemBody(ProtectedBattleDeckKey, value)
            : new SetItemBody(ProtectedBattleDeckKey, value, item.WriteLock);
        await cloud.SetProtectedItemAsync(context, context.ServiceToken!, context.ProjectId!, playerId, body);
    }

    private static bool BackfillStarterCredits(PlayerState state)
    {
        // Signal Credits did not exist in schema 2. A completely untouched starter account is
        // therefore migrated once; accounts that have bought packs, earned packs, or progressed
        // XP are never silently granted currency again.
        if (!state.StarterGranted || state.SignalCredits != 0 || state.PurchasedPacks != 0
            || state.StandardPacks != 0 || state.LoyaltyGranted != 0 || state.Xp != 0
            || state.Level != 1 || state.Wins != 0 || state.Losses != 0 || state.Draws != 0)
            return false;
        state.SignalCredits = StartingSignalCredits;
        return true;
    }

    private static string NormalizeRequestId(string requestId, string operation)
    {
        string value = (requestId ?? "").Trim();
        if (value.Length < 8 || value.Length > 128)
            throw new InvalidOperationException(operation + " request id is required.");
        return value;
    }

    private static void TrimReceipts<T>(Dictionary<string, T> receipts)
    {
        const int maxReceipts = 128;
        while (receipts.Count > maxReceipts)
        {
            string oldest = receipts.Keys.First();
            receipts.Remove(oldest);
        }
    }

    private static async Task SaveState(IExecutionContext context, IGameApiClient api,
        string token, string playerId, PlayerState state)
    {
        state.SchemaVersion = CurrentSchema;
        await api.CloudSaveData.SetItemAsync(context, token, context.ProjectId!, playerId,
            new SetItemBody(StateKey, JsonSerializer.Serialize(state)));
        await SaveTrustedOwnership(context, api, playerId, state.Owned);
        await SaveTrustedBattleDeck(context, api, playerId,
            string.IsNullOrWhiteSpace(state.BattleDeckId) ? null : state.BattleDeckSnapshot);
    }

    private static CloudStateResponse Response(PlayerState state, PackCardDto[] pack = null,
        PackCardDto[] loyaltyPack = null, int fragmentsAwarded = 0)
    {
        return new CloudStateResponse
        {
            State = state,
            Pack = pack ?? Array.Empty<PackCardDto>(),
            LoyaltyPack = loyaltyPack ?? Array.Empty<PackCardDto>(),
            FragmentsAwarded = fragmentsAwarded
        };
    }

    private static void AddStarterCollection(PlayerState state, FactionValue faction)
    {
        List<string> commons = Enumerable.Range(1, 25).Select(i => CardId(faction, i)).ToList();
        foreach (string id in commons) AddCard(state, id, 1);

        List<string> extras = commons.OrderBy(_ => Random.Shared.Next()).Take(3).ToList();
        foreach (string id in extras) AddCard(state, id, 1);

        List<string> rares = Enumerable.Range(26, 15)
            .Select(i => CardId(faction, i)).OrderBy(_ => Random.Shared.Next()).Take(5).ToList();
        foreach (string id in rares) AddCard(state, id, 1);
        foreach (string id in EpicIds(faction).OrderBy(_ => Random.Shared.Next()).Take(2)) AddCard(state, id, 1);

        var starter = new DeckDto
        {
            Id = "deck_001", Name = "Starter Deck", Faction = faction.Token,
            Cards = commons.ToDictionary(id => id, _ => 1)
        };
        foreach (string id in extras) starter.Cards[id]++;
        foreach (string id in rares.Take(2)) starter.Cards[id] = 1;
        state.Decks.Add(starter);
        state.NextDeckSequence = 1;
        state.ActiveDeckId = starter.Id;
        state.BattleDeckId = starter.Id;
        state.BattleDeckSnapshot = NormalizeDeck(starter);
    }

    private static int AddCard(PlayerState state, string id, int amount)
    {
        int old = state.Owned.TryGetValue(id, out int count) ? count : 0;
        state.Owned[id] = old + amount;
        return old > 0 ? RarityValue(id) * amount : 0;
    }

    private static void ValidateDeck(PlayerState state, DeckDto deck)
    {
        if (deck == null || string.IsNullOrWhiteSpace(deck.Id)) throw new InvalidOperationException("Deck is required.");
        FactionValue faction = ParseFaction(deck.Faction);
        deck.Cards ??= new Dictionary<string, int>();
        int total = 0;
        foreach (var entry in deck.Cards)
        {
            if (!IsCardInFaction(entry.Key, faction)) throw new InvalidOperationException("Deck contains an unknown or off-faction card.");
            if (entry.Value < 1 || entry.Value > 3) throw new InvalidOperationException("A card may have at most three copies.");
            if (!state.Owned.TryGetValue(entry.Key, out int owned) || entry.Value > owned)
                throw new InvalidOperationException("Deck contains a card the player does not own.");
            total += entry.Value;
        }
        if (total > 40) throw new InvalidOperationException("Decks may contain at most 40 cards.");
        // An empty deck is a valid draft (including immediately after creating a new slot).
    }

    private static DeckDto NormalizeDeck(DeckDto deck) => new()
    {
        Id = deck.Id,
        Name = string.IsNullOrWhiteSpace(deck.Name) ? deck.Id : deck.Name.Trim(),
        Faction = ParseFaction(deck.Faction).Token,
        Cards = new Dictionary<string, int>(deck.Cards ?? new Dictionary<string, int>())
    };

    private static bool IsLegalDeck(DeckDto deck)
    {
        int count = deck.Cards?.Values.Sum() ?? 0;
        return count is >= 30 and <= 40;
    }

    private static int ApplyResult(PlayerState state, string result)
    {
        int baseXp = result == "win" ? 30 : result == "loss" ? 10 : 15;
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (state.DailyXpDate != today) { state.DailyXpDate = today; state.DailyXp = 0; }
        int awarded = Math.Max(0, Math.Min(baseXp, DailyXpCap - state.DailyXp));
        state.DailyXp += awarded;
        state.Xp += awarded;
        while (state.Xp >= state.Level * 100)
        {
            state.Xp -= state.Level * 100;
            state.Level++;
            state.StandardPacks++;
        }
        if (result == "win") state.Wins++;
        else if (result == "loss") state.Losses++;
        else state.Draws++;
        return awarded;
    }

    private static string NormalizeResult(string result)
    {
        string value = (result ?? "").Trim().ToLowerInvariant();
        if (value is "win" or "loss" or "draw") return value;
        throw new InvalidOperationException("Unknown match result.");
    }

    private static bool ResultsAgree(string a, string b) =>
        (a == "draw" && b == "draw") || (a == "win" && b == "loss") || (a == "loss" && b == "win");

    private static void ClearPending(PlayerState state)
    {
        state.PendingMatchId = "";
        state.PendingResult = "";
        state.PendingOpponentId = "";
    }

    private static PackCardDto RollCard()
    {
        int roll = Random.Shared.Next(150);
        FactionValue faction = new(new[] { "fsti", "frti", "fpb" }[Random.Shared.Next(3)]);
        int number = roll < 75 ? Random.Shared.Next(1, 26)
            : roll < 120 ? Random.Shared.Next(26, 41)
            : roll < 142 ? Random.Shared.Next(41, EpicEndExclusive(faction))
            : Random.Shared.Next(EpicEndExclusive(faction), 51);
        return new PackCardDto { Id = CardId(faction, number), Rarity = RarityFor(faction, number) };
    }

    private static int EpicEndExclusive(FactionValue faction) => faction.Token == "fsti" ? 49 : 48;

    private static FactionValue ParseFaction(string token) => (token ?? "").Trim().ToLowerInvariant() switch
    {
        "fsti" => new FactionValue("fsti"),
        "frti" => new FactionValue("frti"),
        "fpb" => new FactionValue("fpb"),
        _ => throw new InvalidOperationException("Unknown faction.")
    };

    private static string CardId(FactionValue faction, int number) => $"card_{faction.Token}_{number:000}";
    private static IEnumerable<string> EpicIds(FactionValue faction) =>
        Enumerable.Range(41, faction.Token == "fsti" ? 8 : 7).Select(i => CardId(faction, i));
    private static bool IsCardInFaction(string id, FactionValue faction) =>
        id?.StartsWith($"card_{faction.Token}_", StringComparison.Ordinal) == true
        && int.TryParse(id[^3..], out int number) && number is >= 1 and <= 50;
    private static string RarityFor(FactionValue faction, int number) => number <= 25 ? "Common" : number <= 40 ? "Rare"
        : number < EpicEndExclusive(faction) ? "Epic" : "Legendary";
    private static int RarityValue(string id)
    {
        string[] parts = (id ?? "").Split('_');
        FactionValue faction = ParseFaction(parts.Length > 1 ? parts[1] : "");
        return RarityFor(faction, int.Parse(id[^3..])) switch
    {
        "Rare" => 12, "Epic" => 30, "Legendary" => 60, _ => 5
    };
    }
    private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public static class ClientVersionPolicy
{
    public static bool IsSupported(string? clientVersion, string? minimumVersion)
    {
        return TryParse(clientVersion, out Version? client)
            && TryParse(minimumVersion, out Version? minimum)
            && client!.CompareTo(minimum) >= 0;
    }

    private static bool TryParse(string? value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(1);
        string[] parts = normalized.Split('.');
        if (parts.Length != 3 || parts.Any(part => part.Length == 0 || part.Any(c => c < '0' || c > '9')))
            return false;
        return Version.TryParse(normalized, out version);
    }
}

public sealed class ClientVersionResponse
{
    public bool Supported { get; set; }
    public string MinimumVersion { get; set; } = "";
    public string UpdateUrl { get; set; } = "";
    public string Message { get; set; } = "";
}

public readonly record struct FactionValue(string Token);

public sealed class PlayerState
{
    public int SchemaVersion { get; set; } = 3;
    public bool StarterGranted { get; set; }
    public string StarterFaction { get; set; } = "";
    public Dictionary<string, int> Owned { get; set; } = new();
    public List<DeckDto> Decks { get; set; } = new();
    public string ActiveDeckId { get; set; } = "";
    public string BattleDeckId { get; set; } = "";
    public DeckDto? BattleDeckSnapshot { get; set; }
    public int NextDeckSequence { get; set; }
    public int Fragments { get; set; }
    public int PurchasedPacks { get; set; }
    public int LoyaltyGranted { get; set; }
    public int Xp { get; set; }
    public int Level { get; set; } = 1;
    public int StandardPacks { get; set; }
    public int LoyaltyPacks { get; set; }
    public int SignalCredits { get; set; } = 2400;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public List<MatchRecordDto> MatchHistory { get; set; } = new();
    public string PendingMatchId { get; set; } = "";
    public string PendingResult { get; set; } = "";
    public string PendingOpponentId { get; set; } = "";
    public string DailyXpDate { get; set; } = "";
    public int DailyXp { get; set; }
    public PackCardDto[] LastPack { get; set; } = Array.Empty<PackCardDto>();
    public PackCardDto[] LastLoyaltyPack { get; set; } = Array.Empty<PackCardDto>();
    public Dictionary<string, PurchaseReceiptDto> PurchaseReceipts { get; set; } = new();
    public Dictionary<string, PackOpenReceiptDto> PackOpenReceipts { get; set; } = new();

    public bool Normalize()
    {
        bool migrated = SchemaVersion < 3;
        if (migrated)
        {
            // Schema 2 mixed progression, purchased, and loyalty entitlements in StandardPacks.
            // Preserve that balance rather than guessing which packs were consumed. New threshold
            // rewards are tracked separately in LoyaltyPacks from schema 3 onward.
            SchemaVersion = 3;
        }
        Owned ??= new();
        Decks ??= new();
        MatchHistory ??= new();
        LastPack ??= Array.Empty<PackCardDto>();
        LastLoyaltyPack ??= Array.Empty<PackCardDto>();
        PurchaseReceipts ??= new();
        PackOpenReceipts ??= new();
        foreach (DeckDto deck in Decks) deck.Cards ??= new();
        Level = Math.Max(1, Level);
        StandardPacks = Math.Max(0, StandardPacks);
        LoyaltyPacks = Math.Max(0, LoyaltyPacks);
        SignalCredits = Math.Max(0, SignalCredits);
        return migrated;
    }
}

public sealed class PurchaseReceiptDto
{
    public int PackCount { get; set; }
    public int Cost { get; set; }
    public int LoyaltyRewards { get; set; }
}

public sealed class PackOpenReceiptDto
{
    public int PackCount { get; set; }
    public PackCardDto[] Pack { get; set; } = Array.Empty<PackCardDto>();
    public PackCardDto[] LoyaltyPack { get; set; } = Array.Empty<PackCardDto>();
    public int FragmentsAwarded { get; set; }
}

public sealed class DeckDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Faction { get; set; } = "fsti";
    public Dictionary<string, int> Cards { get; set; } = new();
}

public sealed class MatchRecordDto
{
    public string MatchId { get; set; } = "";
    public string Result { get; set; } = "";
    public int Xp { get; set; }
    public long Timestamp { get; set; }
}

public sealed class PackCardDto
{
    public string Id { get; set; } = "";
    public string Rarity { get; set; } = "Common";
}

public sealed class CloudStateResponse
{
    public PlayerState State { get; set; } = new();
    public string Error { get; set; } = "";
    public PackCardDto[] Pack { get; set; } = Array.Empty<PackCardDto>();
    public PackCardDto[] LoyaltyPack { get; set; } = Array.Empty<PackCardDto>();
    public int FragmentsAwarded { get; set; }
}
