using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.Net
{
    public enum LobbyState
    {
        Idle,
        Searching,
        Creating,
        Joining,
        InLobby,
        Starting,
        InGame,
    }

    /// <summary>
    /// Matchmaking on top of Unity Lobby + Relay.
    ///
    /// Flow: Quick Play keeps searching for an open matchmaking lobby; Create
    /// makes a private lobby whose code is read out to the other player. When the host is
    /// ready and the lobby is full, it allocates a Relay server and publishes the join code
    /// into the lobby's member-visible data; clients pick that up on their poll and join
    /// without ever seeing or typing a Relay code.
    ///
    /// Only the lobby code is shared between players. Avatars stay named "Player {clientId}"
    /// — no display names come out of the lobby.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        /// <summary>
        /// Gets the persistent production session root. NetPresenceDemo still contains the same
        /// components as an authored fixture; duplicate scene instances yield to this one in Awake.
        /// </summary>
        public static LobbyManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var root = new GameObject("Online Session");
            DontDestroyOnLoad(root);
            root.AddComponent<Unity.Netcode.NetworkManager>();
            root.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            root.AddComponent<NetBootstrap>();
            return root.AddComponent<LobbyManager>();
        }

        /// <summary>Exactly two players per match, per the game design.</summary>
        public const int MaxPlayers = 2;

        const string KeyRelayCode = "RelayCode";
        const string KeyReady = "Ready";
        const string KeyStatus = "Status";
        const string StatusWaiting = "Waiting";
        const string StatusInGame = "InGame";

        // Lobby heartbeats keep the lobby alive; polling is what carries the host's
        // Relay code across to clients. Both are well inside the service's limits.
        const float HeartbeatSeconds = 12f;
        const float PollSeconds = 3f;
        const float MatchmakingRetrySeconds = 5f;

        [Header("Lobby")]
        [Tooltip("Name shown in the service dashboard. The lobby code is what players use.")]
        public string lobbyName = "Overlink Match";

        public LobbyState State { get; private set; } = LobbyState.Idle;
        public string Status { get; private set; } = "";
        public Lobby CurrentLobby { get; private set; }
        public bool HasActiveLobby => CurrentLobby != null;
        public bool IsOnlineMatch => CurrentLobby != null && State == LobbyState.InGame;
        public string LobbyCode => CurrentLobby?.LobbyCode ?? "";
        public bool IsHost { get; private set; }
        public bool IsQuickPlay => autoStartWhenFull;
        public int MemberCount => CurrentLobby?.Players?.Count ?? 0;
        public string OpponentPlayerId
        {
            get
            {
                if (CurrentLobby?.Players == null) return "";
                for (int i = 0; i < CurrentLobby.Players.Count; i++)
                {
                    string id = CurrentLobby.Players[i]?.Id;
                    if (!string.IsNullOrEmpty(id) && id != playerId) return id;
                }
                return "";
            }
        }
        public bool IsBusy => State == LobbyState.Searching
                           || State == LobbyState.Creating
                           || State == LobbyState.Joining
                           || State == LobbyState.Starting;

        /// <summary>Host-gated: the match only begins once both seats are filled.</summary>
        public bool LocalReady => IsPlayerReady(FindPlayer(playerId));
        public bool OpponentReady => IsPlayerReady(FindPlayer(OpponentPlayerId));
        public bool AllPlayersReady => CurrentLobby?.Players != null
            && MemberCount == MaxPlayers && CurrentLobby.Players.TrueForAll(IsPlayerReady);
        public bool CanStart => IsHost && CurrentLobby != null && MemberCount >= MaxPlayers
            && (autoStartWhenFull || AllPlayersReady);

        public event Action StateChanged;
        public event Action LobbyChanged;
        public event Action<string> StatusChanged;

        /// <summary>Installed by the active deck editor to confirm its draft before scene loading.</summary>
        public static Func<Task> PrepareForGameplayAsync { get; set; }
        public bool GameplayPreparationFailed { get; private set; }
        readonly GameplayTransitionGate gameplayTransition = new GameplayTransitionGate();

        Player FindPlayer(string id) => CurrentLobby?.Players?.Find(player => player.Id == id);
        static bool IsPlayerReady(Player player) => player?.Data != null
            && player.Data.TryGetValue(KeyReady, out var value) && value.Value == "1";

        NetBootstrap net;
        string playerId;
        Coroutine heartbeatRoutine;
        Coroutine pollRoutine;
        bool leaving;
        bool polling;
        bool updatingReady;
        bool autoStartWhenFull;
        LobbySeatProof localSeatProof;
        readonly SerializedMutationQueue seatProofPublications = new SerializedMutationQueue();
        int operationVersion;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Lobby membership and the opponent identity must survive the NetDemo -> Gameplay load.
            DontDestroyOnLoad(gameObject);

            net = FindObjectOfType<NetBootstrap>();
            if (net == null)
                Debug.LogWarning("[Lobby] No NetBootstrap in the scene; starting a match will fail.");
            else
            {
                net.OnNetworkConnected += OnNetworkConnected;
                net.OnNetworkDisconnected += OnNetworkDisconnected;
                net.OnSeatAuthenticated += OnNetworkConnected;
                net.OnStatusChanged += OnNetworkStatusChanged;
            }
        }

        void OnDestroy()
        {
            StopRoutines();
            localSeatProof?.Dispose();
            localSeatProof = null;
            if (net != null)
            {
                net.OnNetworkConnected -= OnNetworkConnected;
                net.OnNetworkDisconnected -= OnNetworkDisconnected;
                net.OnSeatAuthenticated -= OnNetworkConnected;
                net.OnStatusChanged -= OnNetworkStatusChanged;
            }
            if (Instance == this) Instance = null;
        }

        // --- Public actions -----------------------------------------------------

        /// <summary>
        /// Publishes this account's ephemeral public key to its own Lobby player record.
        /// This does not authenticate Relay or start gameplay; callers must perform the
        /// challenge handshake before trusting any connection.
        /// </summary>
        public Task PublishSeatProofAsync()
        {
            if (CurrentLobby == null || string.IsNullOrEmpty(playerId)) throw new InvalidOperationException("No active lobby.");
            string lobbyId = CurrentLobby.Id;
            int version = operationVersion;
            return seatProofPublications.Enqueue(() => PublishSeatProofCoreAsync(lobbyId, version));
        }

        async Task PublishSeatProofCoreAsync(string lobbyId, int version)
        {
            if (this == null || version != operationVersion || CurrentLobby == null || CurrentLobby.Id != lobbyId)
                throw new InvalidOperationException("Lobby changed before seat proof publication.");
            if (localSeatProof != null) return;
            LobbySeatProof candidate = null;
            try
            {
                candidate = new LobbySeatProof();
                await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId,
                    new UpdatePlayerOptions { Data = new Dictionary<string, PlayerDataObject>
                    {
                        { LobbySeatIdentity.ModulusKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, candidate.PublicModulus) },
                        { LobbySeatIdentity.ExponentKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, candidate.PublicExponent) }
                    }});
                if (this == null || version != operationVersion || CurrentLobby == null || CurrentLobby.Id != lobbyId)
                    throw new InvalidOperationException("Lobby changed during seat proof publication.");
                localSeatProof = candidate;
                candidate = null;
                Notify();
            }
            finally
            {
                candidate?.Dispose();
            }
        }

        /// <summary>Reads the opponent key from a new service response and rechecks membership after the await.</summary>
        public async Task<LobbySeatIdentity> FetchOpponentSeatIdentityAsync()
        {
            if (!IsHost || CurrentLobby == null || string.IsNullOrEmpty(OpponentPlayerId))
                throw new InvalidOperationException("A host and opponent are required.");
            string lobbyId = CurrentLobby.Id;
            string opponentId = OpponentPlayerId;
            string hostId = playerId;
            int version = operationVersion;
            Lobby fresh = await LobbyService.Instance.GetLobbyAsync(lobbyId);
            if (this == null || version != operationVersion || !IsHost || CurrentLobby == null ||
                CurrentLobby.Id != lobbyId || OpponentPlayerId != opponentId || playerId != hostId ||
                !LobbySeatIdentity.TryRead(fresh, lobbyId, hostId, opponentId, out LobbySeatIdentity identity))
                throw new InvalidOperationException("Lobby membership or opponent seat proof is not valid.");
            return identity;
        }

        /// <summary>Guest reads the expected host key from fresh Lobby data, never from Relay.</summary>
        public async Task<LobbySeatIdentity> FetchHostSeatIdentityAsync()
        {
            if (IsHost || CurrentLobby == null || string.IsNullOrEmpty(playerId) ||
                string.IsNullOrEmpty(CurrentLobby.HostId))
                throw new InvalidOperationException("A guest and host are required.");
            string lobbyId = CurrentLobby.Id, hostId = CurrentLobby.HostId, guestId = playerId;
            int version = operationVersion;
            Lobby fresh = await LobbyService.Instance.GetLobbyAsync(lobbyId);
            if (this == null || version != operationVersion || IsHost || CurrentLobby == null ||
                CurrentLobby.Id != lobbyId || CurrentLobby.HostId != hostId || playerId != guestId ||
                !LobbySeatIdentity.TryReadHost(fresh, lobbyId, hostId, guestId, out LobbySeatIdentity identity))
                throw new InvalidOperationException("Lobby membership or host seat proof is not valid.");
            return identity;
        }

        /// <summary>Signs only for the current lobby and local authenticated player.</summary>
        public byte[] SignSeatChallenge(ulong localRelayPeer, byte[] challenge)
        {
            if (localSeatProof == null || CurrentLobby == null || string.IsNullOrEmpty(playerId))
                throw new InvalidOperationException("Seat proof is not ready.");
            return localSeatProof.Sign(CurrentLobby.Id, playerId, localRelayPeer, challenge);
        }

        /// <summary>Keep searching for an open matchmaking lobby until one is found or the search is cancelled.</summary>
        public async void QuickPlay()
        {
            if (IsBusy || CurrentLobby != null) return;

            int version = ++operationVersion;
            autoStartWhenFull = true;
            SetState(LobbyState.Searching);
            try
            {
                if (!await EnsureSignedInAsync(version)) return;
                if (version != operationVersion) return;

                Lobby lobby = null;
                while (version == operationVersion && lobby == null)
                {
                    try
                    {
                        SetStatus("Searching for an open match...");
                        lobby = await LobbyService.Instance.QuickJoinLobbyAsync(
                            new QuickJoinLobbyOptions { Player = new Player(playerId) });
                        SetStatus("Joined a waiting match.");
                    }
                    catch (LobbyServiceException e)
                    {
                        if (version != operationVersion) return;
                        if (e.Reason != LobbyExceptionReason.NoOpenLobbies) throw;
                        Debug.Log("[Lobby] No open matchmaking lobby yet; continuing search.");
                        SetStatus("No open match yet — continuing to search...");
                        await Task.Delay(TimeSpan.FromSeconds(MatchmakingRetrySeconds));
                    }
                }

                if (lobby == null) return;
                if (version != operationVersion)
                {
                    await CleanupUnadoptedLobbyAsync(lobby);
                    return;
                }
                AdoptLobby(lobby);
            }
            catch (Exception e)
            {
                if (version != operationVersion) return;
                FailMatchmaking(e, "Quick play");
            }
        }

        /// <summary>Host a private lobby. Its code is shown in the UI for the other player.</summary>
        public async void CreateLobby()
        {
            if (IsBusy || CurrentLobby != null) return;

            int version = ++operationVersion;
            autoStartWhenFull = false;
            SetState(LobbyState.Creating);
            try
            {
                if (!await EnsureSignedInAsync(version)) return;
                Lobby lobby = await CreatePrivateLobbyAsync(MaxPlayers, true);
                if (version != operationVersion)
                {
                    await CleanupUnadoptedLobbyAsync(lobby);
                    return;
                }
                AdoptLobby(lobby);
            }
            catch (Exception e)
            {
                if (version != operationVersion) return;
                FailMatchmaking(e, "Create lobby");
            }
        }

        /// <summary>Join a friend's lobby by its lobby code. Relay codes never appear here.</summary>
        public async void JoinLobbyByCode(string rawCode)
        {
            if (IsBusy || CurrentLobby != null) return;

            string code = (rawCode ?? "").Trim();
            if (code.Length == 0)
            {
                SetStatus("Enter a lobby code first.");
                return;
            }

            autoStartWhenFull = false;
            SetState(LobbyState.Joining);
            int version = ++operationVersion;
            try
            {
                if (!await EnsureSignedInAsync(version)) return;

                Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(
                    code, new JoinLobbyByCodeOptions { Player = new Player(playerId) });
                if (version != operationVersion)
                {
                    await CleanupUnadoptedLobbyAsync(lobby);
                    return;
                }
                AdoptLobby(lobby);
            }
            catch (LobbyServiceException e)
            {
                if (version != operationVersion) return;
                // A full or locked lobby is the common user mistake, so name it directly.
                SetStatus(e.Reason == LobbyExceptionReason.LobbyFull
                    ? "That match is already full."
                    : $"Could not join lobby: {e.Message}");
                SetState(LobbyState.Idle);
            }
            catch (Exception e)
            {
                if (version != operationVersion) return;
                FailMatchmaking(e, "Join lobby");
            }
        }

        /// <summary>Changes this player's private-lobby ready state. Missing data means not ready.</summary>
        public async void SetReady(bool ready)
        {
            if (updatingReady || CurrentLobby == null || autoStartWhenFull || State != LobbyState.InLobby) return;
            string lobbyId = CurrentLobby.Id;
            int version = operationVersion;
            updatingReady = true;
            try
            {
                Lobby updated = await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId,
                    new UpdatePlayerOptions
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            { KeyReady, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "1" : "0") }
                        }
                    });
                if (version != operationVersion || CurrentLobby == null || CurrentLobby.Id != lobbyId) return;
                CurrentLobby = updated;
                Notify();
            }
            catch (Exception e)
            {
                if (version == operationVersion) SetStatus($"Could not change ready status: {e.Message}");
            }
            finally { updatingReady = false; }
        }

        /// <summary>
        /// Host-only. Allocates Relay, starts the host, and publishes the join code to the
        /// lobby so the client can pick it up without it ever being shown to a player.
        /// </summary>
        public async void StartMatch()
        {
            if (!CanStart || IsBusy) return;
            if (net == null)
            {
                SetStatus("No session layer available — see console.");
                return;
            }

            SetState(LobbyState.Starting);
            int version = operationVersion;
            string lobbyId = CurrentLobby.Id;
            try
            {
                string relayCode = await net.CreateRelayAndHostAsync(MaxPlayers);
                if (version != operationVersion || CurrentLobby == null || CurrentLobby.Id != lobbyId)
                {
                    if (net.IsListening) net.LeaveGame();
                    return;
                }
                if (string.IsNullOrEmpty(relayCode))
                {
                    SetState(LobbyState.InLobby);
                    return;
                }

                Lobby updated = await LobbyService.Instance.UpdateLobbyAsync(lobbyId,
                    new UpdateLobbyOptions
                    {
                        Data = new Dictionary<string, DataObject>
                        {
                            { KeyRelayCode, new DataObject(DataObject.VisibilityOptions.Member, relayCode) },
                            { KeyStatus, new DataObject(DataObject.VisibilityOptions.Member, StatusInGame) },
                        },
                    });

                if (version != operationVersion || CurrentLobby == null || CurrentLobby.Id != lobbyId)
                {
                    if (net.IsListening) net.LeaveGame();
                    return;
                }

                CurrentLobby = updated;
                SetState(LobbyState.InGame);
                SetStatus("Match started — waiting for the other player to connect.");
                Notify();
                TryEnterGameplay();
            }
            catch (Exception e)
            {
                if (net != null && net.IsListening) net.LeaveGame();
                if (version == operationVersion) FailMatchmaking(e, "Start match");
            }
        }

        public void CancelSearch()
        {
            if (CurrentLobby != null)
            {
                LeaveLobby();
                return;
            }

            operationVersion++;
            autoStartWhenFull = false;
            SetStatus("Cancelled.");
            SetState(LobbyState.Idle);
        }

        /// <summary>
        /// Leaves the lobby and drops the session. The host deletes the lobby so the code is
        /// freed immediately; a guest asks to be removed so the seat reopens for someone else.
        /// </summary>
        public async void LeaveLobby()
        {
            if (leaving) return;
            leaving = true;
            operationVersion++;
            StopRoutines();

            Lobby leavingLobby = CurrentLobby;
            bool wasHost = IsHost;
            ClearLobby();

            if (leavingLobby != null && net != null && net.IsListening)
            {
                net.LeaveGame();
                SetStatus("Left the match.");
            }
            else
            {
                SetStatus("Left the lobby.");
            }

            try
            {
                if (leavingLobby != null && !string.IsNullOrEmpty(playerId))
                {
                    if (wasHost) await LobbyService.Instance.DeleteLobbyAsync(leavingLobby.Id);
                    else await LobbyService.Instance.RemovePlayerAsync(leavingLobby.Id, playerId);
                }
            }
            catch (Exception e)
            {
                // The lobby expires on its own after ~30s without a heartbeat, so a failed
                // cleanup is not worth surfacing to the player.
                Debug.LogWarning($"[Lobby] Leave cleanup failed: {e.Message}");
            }
            finally
            {
                leaving = false;
                SetState(LobbyState.Idle);
            }
        }

        // --- Lobby plumbing -----------------------------------------------------

        async Task CleanupUnadoptedLobbyAsync(Lobby lobby)
        {
            if (lobby == null || string.IsNullOrEmpty(playerId)) return;
            try
            {
                if (lobby.HostId == playerId) await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
                else await LobbyService.Instance.RemovePlayerAsync(lobby.Id, playerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Cancelled lobby cleanup failed: {e.Message}");
            }
        }

        async Task<Lobby> CreatePrivateLobbyAsync(int maxPlayers, bool isPrivate)
        {
            SetStatus("Creating a lobby…");
            return await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayers,
                new CreateLobbyOptions
                {
                    IsPrivate = isPrivate,
                    Player = new Player(playerId),
                    Data = new Dictionary<string, DataObject>
                    {
                        { KeyStatus, new DataObject(DataObject.VisibilityOptions.Member, StatusWaiting) },
                        // Empty until the host starts the match; clients wait for this to fill.
                        { KeyRelayCode, new DataObject(DataObject.VisibilityOptions.Member, "") },
                    },
                });
        }

        void AdoptLobby(Lobby lobby)
        {
            CurrentLobby = lobby;
            IsHost = lobby.HostId == playerId;

            SetState(LobbyState.InLobby);
            SetStatus(IsHost
                ? $"Lobby created — share code: {LobbyCode}"
                : $"Joined lobby {LobbyCode}. Waiting for the host to start.");

            StartRoutines();
            Notify();
            _ = PublishSeatProofForLobbyAsync(lobby.Id, operationVersion);
        }

        async Task PublishSeatProofForLobbyAsync(string lobbyId, int version)
        {
            try { await PublishSeatProofAsync(); }
            catch (Exception error)
            {
                if (this != null && version == operationVersion && CurrentLobby?.Id == lobbyId)
                    SetStatus("Could not prepare the secure lobby connection: " + error.Message + ". Leave and rejoin to retry.");
            }
        }

        void ClearLobby()
        {
            localSeatProof?.Dispose();
            localSeatProof = null;
            GameplayPreparationFailed = false;
            CurrentLobby = null;
            IsHost = false;
            autoStartWhenFull = false;
            SetState(LobbyState.Idle);
            Notify();
        }

        /// <summary>
        /// Starts the heartbeat and poll loops, replacing any pair left over from a previous
        /// lobby. Both loops bail out on their own if <see cref="CurrentLobby"/> is replaced,
        /// so this is only ever called on adopting a fresh lobby.
        /// </summary>
        void StartRoutines()
        {
            StopRoutines();
            heartbeatRoutine = StartCoroutine(HeartbeatRoutine());
            pollRoutine = StartCoroutine(PollRoutine());
        }

        void StopRoutines()
        {
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            if (pollRoutine != null) StopCoroutine(pollRoutine);
            heartbeatRoutine = null;
            pollRoutine = null;
        }

        /// <summary>
        /// Pings every <see cref="HeartbeatSeconds"/> so the service does not expire the lobby
        /// while the players are still deciding. The id captured before each yield is what makes
        /// a lobby switch abort the loop instead of pinging a lobby we already left.
        /// </summary>
        IEnumerator HeartbeatRoutine()
        {
            var wait = new WaitForSeconds(HeartbeatSeconds);
            while (CurrentLobby != null && !leaving)
            {
                string lobbyId = CurrentLobby.Id;
                yield return wait;
                if (CurrentLobby == null || leaving || CurrentLobby.Id != lobbyId) yield break;

                // Fire-and-forget: a dropped ping only costs a member list refresh, and the
                // poll loop is what actually drives the UI.
                if (IsHost) _ = PingAsync(lobbyId);
            }
        }

        /// <summary>
        /// Pulls the lobby every <see cref="PollSeconds"/>. This is the only channel that carries
        /// the host's Relay code over to clients, and the only thing that keeps the member count
        /// and Start eligibility current.
        /// </summary>
        IEnumerator PollRoutine()
        {
            var wait = new WaitForSeconds(PollSeconds);
            while (CurrentLobby != null && !leaving)
            {
                string lobbyId = CurrentLobby.Id;
                yield return wait;
                if (CurrentLobby == null || leaving || CurrentLobby.Id != lobbyId) yield break;

                // The coroutine only paces the polls; PollOnceAsync runs the actual request
                // and updates the UI when it completes. The guard keeps a slow request from
                // overlapping the next tick.
                if (!polling) _ = PollOnceAsync(lobbyId);
            }
        }

        async Task PingAsync(string lobbyId)
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}");
            }
        }

        /// <summary>
        /// Refreshes the lobby and, for guests, starts joining the Relay allocation as soon
        /// as the host has published one. Mirrors <see cref="GetLobbyAsync"/>'s async work
        /// without blocking the main thread.
        /// </summary>
        async Task PollOnceAsync(string lobbyId)
        {
            polling = true;
            Lobby refreshed;
            try
            {
                refreshed = await LobbyService.Instance.GetLobbyAsync(lobbyId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] Poll failed: {e.Message}");
                return;
            }
            finally
            {
                polling = false;
            }

            // The player may have left or switched lobbies while the request was in flight.
            if (CurrentLobby == null || CurrentLobby.Id != lobbyId || leaving) return;
            CurrentLobby = refreshed;

            // Quick Play starts when full; private lobbies start only after both players are ready.
            if (IsHost && State == LobbyState.InLobby && CanStart)
            {
                StartMatch();
                Notify();
                return;
            }

            if (!IsHost && refreshed.Players.Count < refreshed.MaxPlayers)
            {
                // The other player dropped out of the lobby before the match started.
                SetStatus("The other player left the lobby.");
            }
            else if (!IsHost)
            {
                // The host publishes the Relay code and the InGame flag in the same write, so
                // both have to be present before a client moves off the lobby.
                string relayCode = GetData(refreshed, KeyRelayCode);
                bool started = GetData(refreshed, KeyStatus) == StatusInGame;
                if (started && !string.IsNullOrEmpty(relayCode) && net != null && !net.IsListening)
                {
                    SetStatus("Match found — connecting…");
                    if (await net.JoinRelayAsync(relayCode))
                    {
                        // NET-01: Revalidate lobby state after the async join. If the player left while
                        // the join was in flight, disconnect rather than putting the client in InGame.
                        if (CurrentLobby == null || CurrentLobby.Id != lobbyId || leaving)
                        {
                            if (net != null) net.LeaveGame();
                            return;
                        }
                        SetState(LobbyState.InGame);
                        SetStatus("Connecting to the match…");
                        TryEnterGameplay();
                    }
                }
            }

            Notify();
        }

        static string GetData(Lobby lobby, string key)
        {
            if (lobby?.Data == null || !lobby.Data.TryGetValue(key, out var data)) return "";
            return data.Value ?? "";
        }

        void OnNetworkConnected()
        {
            if (State == LobbyState.InGame)
            {
                SetStatus("Connected to the match.");
                TryEnterGameplay();
            }
        }

        /// <summary>
        /// The lobby owns the transition because it is the one object that knows both halves of the
        /// online handshake: the lobby is marked InGame and NGO has a live Relay connection. This
        /// keeps MainMenu from loading Gameplay before an opponent ID is available and keeps the
        /// persistent lobby/network objects alive for GameCoordinator to inspect.
        /// </summary>
        public void RetryGameplayTransition() => TryEnterGameplay();

        async void TryEnterGameplay()
        {
            if (CurrentLobby == null || State != LobbyState.InGame || net == null || !net.IsConnected)
                return;
            if (!net.IsSeatAuthenticated) { SetStatus("Verifying the lobby connection…"); return; }
            if (net.IsHost && !net.HasRemotePeer) return;
            if (SceneManager.GetActiveScene().name == "Gameplay") return;
            if (gameplayTransition.IsRunning) return;

            string lobbyId = CurrentLobby.Id;
            int version = operationVersion;
            GameplayPreparationFailed = false;
            SetStatus("Saving your draft before the match…");
            try
            {
                await gameplayTransition.RunAsync(PrepareForGameplayAsync,
                    () => this != null && version == operationVersion && CurrentLobby != null
                        && CurrentLobby.Id == lobbyId && State == LobbyState.InGame && net != null
                        && net.IsConnected && net.IsSeatAuthenticated && (!net.IsHost || net.HasRemotePeer)
                        && SceneManager.GetActiveScene().name != "Gameplay",
                    () => SceneManager.LoadScene("Gameplay"));
            }
            catch (Exception error)
            {
                if (this == null || version != operationVersion) return;
                GameplayPreparationFailed = true;
                SetStatus("Draft save failed. Your edits are still open; retry or leave the lobby. " + error.Message);
            }
        }

        void OnNetworkDisconnected()
        {
            if (State == LobbyState.InGame) SetStatus("Disconnected from the match.");
        }

        void OnNetworkStatusChanged(string message)
        {
            if (State == LobbyState.InGame && net != null && !net.IsSeatAuthenticated &&
                (message.StartsWith("Could not authenticate", StringComparison.Ordinal) ||
                 message.StartsWith("Could not verify", StringComparison.Ordinal) ||
                 message.StartsWith("Could not sign", StringComparison.Ordinal)))
                SetStatus(message + " Retrying…");
        }

        async Task<bool> EnsureSignedInAsync(int version)
        {
            AccountManager account = AccountManager.EnsureInstance();
            bool signedIn = await account.EnsureSignedInAsync();
            // A cancelled or superseded operation must not update the current UI or
            // create/join a lobby after the asynchronous sign-in completes.
            if (version != operationVersion) return false;
            if (!signedIn)
            {
                SetStatus(account.Status);
                SetState(LobbyState.Idle);
                return false;
            }

            IAuthenticationService auth = AuthenticationService.Instance;
            playerId = auth.PlayerId;
            if (!string.IsNullOrEmpty(playerId)) return true;

            SetStatus("Unity Services returned no player id.");
            SetState(LobbyState.Idle);
            return false;
        }

        void FailMatchmaking(Exception e, string what)
        {
            Debug.LogError($"[Lobby] {what} failed: {e}");
            SetStatus($"{what} failed: {e.Message}");
            SetState(LobbyState.Idle);
        }

        void SetState(LobbyState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke();
        }

        void SetStatus(string status)
        {
            Status = status ?? "";
            StatusChanged?.Invoke(Status);
        }

        void Notify() => LobbyChanged?.Invoke();
    }
}
