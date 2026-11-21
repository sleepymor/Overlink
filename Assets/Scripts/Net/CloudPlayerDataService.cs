using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Overlink.Core;
using Overlink.Persistence;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Overlink.Net
{
    /// <summary>
    /// Account-scoped progression gateway. The client never writes collection or progression directly to
    /// Cloud Save; every mutation goes through the server-authoritative Cloud Code module. This component
    /// only caches the last server response so the existing synchronous binder/deck UI can render it.
    /// </summary>
    public sealed class CloudPlayerDataService : MonoBehaviour, ICloudProgressionBackend
    {
        public static CloudPlayerDataService Instance { get; private set; }
        public const string ModuleName = "OverlinkProgression";
        public const int SinglePackCost = 100;
        public const int TenPackCost = 900;

        public CloudPlayerState State { get; private set; }
        public bool IsReady { get; private set; }
        bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (isBusy == value) return;
                isBusy = value;
                BusyChanged?.Invoke(value);
            }
        }
        public string Status { get; private set; } = "Cloud progression is not loaded.";
        public event Action<CloudPlayerState> StateChanged;
        public event Action<string> StatusChanged;
        public event Action<bool> BusyChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateBeforeScene() => EnsureInstance();

        public static CloudPlayerDataService EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CloudPlayerDataService");
            DontDestroyOnLoad(go);
            return go.AddComponent<CloudPlayerDataService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ReferenceEquals(DeckLibrary.Backend, this)) DeckLibrary.SetBackend(null);
        }

        public Task<ClientVersionResponse> CheckClientVersionAsync(string version)
        {
            RequireSignedIn();
            return CallAsync<ClientVersionResponse>("CheckClientVersion",
                new Dictionary<string, object> { { "clientVersion", version ?? "" } });
        }

        public async Task<CloudPlayerState> LoadAsync()
        {
            RequireSignedIn();
            float startedAt = Time.realtimeSinceStartup;
            IsBusy = true;
            SetStatus("Loading collection and progression…");
            try
            {
                CloudStateResponse response = await CallAsync<CloudStateResponse>("GetState", null);
                Apply(response?.state);
                SetStatus("Cloud progression loaded.");
                return State;
            }
            finally
            {
                IsBusy = false;
                Debug.Log($"[CloudProgression] GetState finished in {Time.realtimeSinceStartup - startedAt:F2}s; ready={IsReady}");
            }
        }

        public async Task<CloudPlayerState> GrantStarterAsync(Faction faction)
        {
            RequireSignedIn();
            IsBusy = true;
            SetStatus("Granting your starter collection…");
            try
            {
                var response = await CallAsync<CloudStateResponse>("GrantStarter",
                    new Dictionary<string, object> { { "faction", CardJson.FactionToken(faction) } });
                Apply(response?.state);
                SetStatus("Starter collection saved.");
                return State;
            }
            finally { IsBusy = false; }
        }

        public Task<CloudPlayerState> EnsureStarterAsync(Faction faction)
        {
            if (State != null && State.starterGranted) return Task.FromResult(State);
            return GrantStarterAsync(faction);
        }

        public async Task SaveDeckAsync(string id, string name, DeckData data)
        {
            RequireReady();
            IsBusy = true;
            SetStatus("Saving deck…");
            try
            {
                CloudStateResponse response = await CallAsync<CloudStateResponse>("SaveDeck", new Dictionary<string, object>
                {
                    { "deck", CloudDeck.FromDeck(id, name, data) }
                });
                Apply(response?.state);
                SetStatus("Deck saved.");
            }
            finally { IsBusy = false; }
        }

        public async Task SetDeckSelectionAsync(string activeDeckId, string battleDeckId)
        {
            RequireReady();
            CloudStateResponse response = await CallAsync<CloudStateResponse>("SetDeckSelection",
                new Dictionary<string, object>
                {
                    { "activeDeckId", activeDeckId ?? "" },
                    { "battleDeckId", battleDeckId ?? "" }
                });
            Apply(response?.state);
        }

        /// <summary>Loads the caller's selected, server-validated deck for unranked play.</summary>
        public async Task<DeckData> GetOwnBattleDeckAsync()
        {
            RequireReady();
            CloudDeck deck = await CallAsync<CloudDeck>("GetOwnBattleDeck", null);
            if (deck == null) throw new InvalidOperationException("Cloud Code returned no battle deck.");
            return deck.ToDeckData();
        }

        public async Task<CloudPlayerState> ReportMatchAsync(string matchId, string result, bool versusAi)
        {
            return await ReportMatchAsync(matchId, result, versusAi, null);
        }

        public async Task<CloudPlayerState> ReportMatchAsync(string matchId, string result, bool versusAi,
            string opponentPlayerId)
        {
            RequireReady();
            IsBusy = true;
            SetStatus("Saving match result…");
            try
            {
                var response = await CallAsync<CloudStateResponse>("ReportMatch", new Dictionary<string, object>
                {
                    { "matchId", matchId ?? "" }, { "result", result ?? "" }, { "versusAi", versusAi },
                    { "opponentPlayerId", opponentPlayerId ?? "" }
                });
                Apply(response?.state);
                SetStatus("Match result saved.");
                return State;
            }
            finally { IsBusy = false; }
        }

        public Task<CloudStateResponse> OpenPackAsync(bool tenPack)
        {
            return OpenPackAsync(tenPack, Guid.NewGuid().ToString("N"));
        }

        public async Task<CloudStateResponse> OpenPackAsync(bool tenPack, string requestId)
        {
            RequireReady();
            IsBusy = true;
            SetStatus("Opening pack…");
            try
            {
                CloudStateResponse response = await CallAsync<CloudStateResponse>("OpenPack",
                    new Dictionary<string, object>
                    {
                        { "packCount", tenPack ? 10 : 1 },
                        { "requestId", requestId ?? "" }
                    });
                Apply(response?.state);
                SetStatus("Pack saved to your collection.");
                return response;
            }
            finally { IsBusy = false; }
        }

        public async Task<CloudStateResponse> OpenLoyaltyPackAsync(string requestId)
        {
            RequireReady();
            IsBusy = true;
            SetStatus("Opening loyalty pack…");
            try
            {
                CloudStateResponse response = await CallAsync<CloudStateResponse>("OpenLoyaltyPack",
                    new Dictionary<string, object> { { "requestId", requestId ?? "" } });
                Apply(response?.state);
                SetStatus("Loyalty pack saved to your collection.");
                return response;
            }
            finally { IsBusy = false; }
        }

        public Task<CloudStateResponse> PurchasePackAsync(bool tenPack)
        {
            return PurchasePackAsync(tenPack, Guid.NewGuid().ToString("N"));
        }

        public async Task<CloudStateResponse> PurchasePackAsync(bool tenPack, string requestId)
        {
            RequireReady();
            IsBusy = true;
            SetStatus(tenPack ? "Buying ten packs…" : "Buying one pack…");
            try
            {
                CloudStateResponse response = await CallAsync<CloudStateResponse>("PurchasePack",
                    new Dictionary<string, object>
                    {
                        { "packCount", tenPack ? 10 : 1 },
                        { "requestId", requestId ?? "" }
                    });
                Apply(response?.state);
                SetStatus(tenPack ? "Ten packs purchased." : "Pack purchased.");
                return response;
            }
            finally { IsBusy = false; }
        }

        async Task<T> CallAsync<T>(string function, Dictionary<string, object> args)
        {
            if (CloudCodeService.Instance == null)
                throw new InvalidOperationException("Cloud Code is not initialized.");
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(ModuleName, function, args);
        }

        void Apply(CloudPlayerState state)
        {
            if (state == null) throw new InvalidOperationException("Cloud Code returned no player state.");
            State = state;
            IsReady = true;
            // Do not call PlayerPrefs.DeleteAll here: Unity Authentication caches its session there and
            // settings are intentionally device-local. Legacy collection/deck keys are ignored by the
            // cloud backend and are cleared by the explicit migration command below.
            PurgeLegacyProgressKeys();
            DeckLibrary.SetBackend(this);
            StateChanged?.Invoke(State);
        }

        static void PurgeLegacyProgressKeys()
        {
            // Delete per-deck keys before deleting the index that identifies them. PlayerPrefs has no
            // key enumeration API, so all dynamic legacy ids are recovered from the saved index and
            // the canonical 150-card id space is swept explicitly below.
            string deckIndex = PlayerPrefs.GetString(DeckLibrary.IndexKey, string.Empty);
            if (!string.IsNullOrEmpty(deckIndex))
            {
                string[] deckIds = deckIndex.Split(';');
                for (int i = 0; i < deckIds.Length; i++)
                {
                    string id = deckIds[i]?.Trim();
                    if (string.IsNullOrEmpty(id)) continue;
                    PlayerPrefs.DeleteKey(DeckLibrary.NameKeyPrefix + id);
                    PlayerPrefs.DeleteKey(DeckLibrary.DataKeyPrefix + id);
                }
            }

            string[] exact =
            {
                DeckLibrary.IndexKey, DeckLibrary.ActiveKey, DeckLibrary.BattleKey,
                DeckLibrary.SequenceKey, DeckLibrary.SavedDeckMigratedKey, DeckLibrary.CountsMigratedKey,
                DeckLibrary.LegacyDeckKey, "Overlink.OfflinePack.Resolved", "Overlink.OfflinePack.Revealed",
                "Overlink.OfflinePack.Fragments", "Overlink.OfflinePack.Bundle.Resolved",
                "Overlink.OfflinePack.Bundle.Revealed", "Overlink.OfflinePack.Bundle.LoyaltyPack",
                "Overlink.OfflinePack.Single.LoyaltyPack", "Overlink.OfflinePack.PurchasedPacks",
                "Overlink.OfflinePack.LoyaltyGranted", "Overlink.OfflinePack.Single.Draw",
                "Overlink.OfflinePack.Bundle.Draw", "Overlink.OfflinePack.Loyalty.Draw",
                "Overlink.OfflinePack.Mode"
            };
            for (int i = 0; i < exact.Length; i++) PlayerPrefs.DeleteKey(exact[i]);

            string[] factions = { "fsti", "frti", "fpb" };
            for (int f = 0; f < factions.Length; f++)
            {
                for (int number = 1; number <= 50; number++)
                {
                    string id = "card_" + factions[f] + "_" + number.ToString("000");
                    PlayerPrefs.DeleteKey("Overlink.OfflinePack.Owned." + id);
                    PlayerPrefs.DeleteKey("Overlink.OfflineDeck." + id);
                }
            }
            PlayerPrefs.Save();
        }

        void RequireSignedIn()
        {
            if (AccountManager.Instance == null || !AccountManager.Instance.IsSignedIn)
                throw new InvalidOperationException("Sign in before loading cloud progression.");
        }

        void RequireReady()
        {
            RequireSignedIn();
            if (!IsReady || State == null) throw new InvalidOperationException("Cloud progression is not loaded.");
        }

        void SetStatus(string value)
        {
            Status = value;
            StatusChanged?.Invoke(value);
            Debug.Log("[CloudProgression] " + value);
        }

        // ---- ICloudProgressionBackend: synchronous read cache for existing UI -----------------
        public IReadOnlyList<DeckSummary> AllDecks() => State?.DeckSummaries() ?? new List<DeckSummary>();
        public string ActiveDeckId => State?.activeDeckId ?? "";
        public string BattleDeckId => State?.battleDeckId ?? "";
        // For pre-snapshot server data FindBattleDeck falls back to the selected saved deck;
        // the match loader still performs full validation.
        public DeckData LoadBattleDeck() => State?.FindBattleDeck();
        public DeckData LoadDeck(string id) => State?.FindDeck(id) ?? new DeckData();
        public string NameOf(string id) => State?.DeckName(id) ?? "";

        public string CreateDeck(string name, Faction faction)
        {
            RequireReady();
            int sequence = ++State.nextDeckSequence;
            string id = "deck_" + sequence.ToString("000");
            var decks = new List<CloudDeck>(State.decks ?? Array.Empty<CloudDeck>())
            {
                CloudDeck.FromDeck(id, string.IsNullOrWhiteSpace(name) ? "Deck " + sequence : name,
                    new DeckData { Faction = faction })
            };
            State.decks = decks.ToArray();
            State.activeDeckId = id;
            QueueCloudMutation(async () =>
            {
                await SaveDeckAsync(id, NameOf(id), LoadDeck(id));
                // SaveDeck only auto-selects when the server has no active deck. A newly created
                // deck is immediately active in the client projection, so persist that selection
                // explicitly rather than leaving the UI and battle selection split across accounts.
                await SetDeckSelectionAsync(id, State.battleDeckId);
            });
            return id;
        }

        public void SaveDeck(string id, string name, DeckData data)
        {
            RequireReady();
            ReplaceDeck(CloudDeck.FromDeck(id, name, data));
            QueueCloudMutation(() => SaveDeckAsync(id, name, data));
        }

        void ReplaceDeck(CloudDeck replacement)
        {
            var decks = new List<CloudDeck>(State.decks ?? Array.Empty<CloudDeck>());
            for (int i = 0; i < decks.Count; i++)
                if (decks[i] != null && decks[i].id == replacement.id) { decks[i] = replacement; State.decks = decks.ToArray(); return; }
            decks.Add(replacement);
            State.decks = decks.ToArray();
        }

        public void SetActiveDeck(string id)
        {
            if (FindDeck(id) == null) return;
            State.activeDeckId = id;
            QueueCloudMutation(() => SetDeckSelectionAsync(State.activeDeckId, State.battleDeckId));
        }

        public void SetBattleDeck(string id)
        {
            if (FindDeck(id) == null) return;
            State.battleDeckId = id;
            QueueCloudMutation(() => SetDeckSelectionAsync(State.activeDeckId, State.battleDeckId));
        }
        public void DeleteDeck(string id)
        {
            if (string.IsNullOrEmpty(id) || State == null) return;
            var decks = new List<CloudDeck>(State?.decks ?? Array.Empty<CloudDeck>());
            bool existed = decks.Exists(d => d != null && d.id == id);
            if (!existed) return;
            decks.RemoveAll(d => d != null && d.id == id);
            State.decks = decks.ToArray();
            if (State.activeDeckId == id) State.activeDeckId = decks.Count > 0 ? decks[0].id : "";
            if (State.battleDeckId == id) State.battleDeckId = "";
            QueueCloudMutation(() => DeleteDeckAsync(id));
        }

        public void DeleteAllDecks()
        {
            if (State == null) return;
            State.decks = Array.Empty<CloudDeck>();
            State.activeDeckId = State.battleDeckId = "";
            QueueCloudMutation(DeleteAllDecksAsync);
        }

        async Task DeleteDeckAsync(string id)
        {
            RequireReady();
            CloudStateResponse response = await CallAsync<CloudStateResponse>("DeleteDeck",
                new Dictionary<string, object> { { "deckId", id ?? "" } });
            Apply(response?.state);
            SetStatus("Deck deletion confirmed.");
        }

        async Task DeleteAllDecksAsync()
        {
            RequireReady();
            CloudStateResponse response = await CallAsync<CloudStateResponse>("DeleteAllDecks", null);
            Apply(response?.state);
            SetStatus("Deck deletion confirmed.");
        }
        public int CopiesInActive(string cardId) => LoadDeck(ActiveDeckId).Cards.TryGetValue(cardId, out int n) ? Math.Max(0, n) : 0;
        public bool SetActiveCopies(string cardId, int copies)
        {
            if (State == null || State.OwnedCopies(cardId) < copies) return false;
            DeckData data = LoadDeck(ActiveDeckId);
            if (copies <= 0) data.Cards.Remove(cardId); else data.Cards[cardId] = Math.Min(DeckValidator.MaxCopiesPerCard, copies);
            SaveDeck(ActiveDeckId, NameOf(ActiveDeckId), data);
            return true;
        }

        readonly SerializedMutationQueue mutationQueue = new SerializedMutationQueue();

        /// <summary>
        /// Queues a detached draft and completes only after Cloud Code confirms its save.
        /// A failure propagates to the caller; callers must remain on the current screen and retry.
        /// Call from Unity's main thread.
        /// </summary>
        public Task SaveDraftConfirmedAsync(string id, string name, DeckData data)
        {
            RequireReady();
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Deck id is required.", nameof(id));
            if (data?.Cards == null) throw new ArgumentNullException(nameof(data));
            var copy = new DeckData
            {
                Version = data.Version,
                Faction = data.Faction,
                Cards = new Dictionary<string, int>(data.Cards)
            };
            return mutationQueue.Enqueue(() => SaveDeckAsync(id, name, copy));
        }

        void QueueCloudMutation(Func<Task> mutation)
        {
            ObserveMutation(mutationQueue.Enqueue(mutation));
        }

        async void ObserveMutation(Task mutation)
        {
            try { await mutation; }
            catch (Exception e)
            {
                SetStatus("Cloud save failed. Your last change was not confirmed by the server.");
                Debug.LogException(e);
            }
        }
        CloudDeck FindDeck(string id)
        {
            if (State?.decks == null) return null;
            for (int i = 0; i < State.decks.Length; i++) if (State.decks[i]?.id == id) return State.decks[i];
            return null;
        }
    }
}
