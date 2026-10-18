using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

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
    /// Flow: Quick Play tries to quick-join an open lobby and otherwise creates one; Create
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

        /// <summary>Exactly two players per match, per the game design.</summary>
        public const int MaxPlayers = 2;

        const string KeyRelayCode = "RelayCode";
        const string KeyStatus = "Status";
        const string StatusWaiting = "Waiting";
        const string StatusInGame = "InGame";

        // Lobby heartbeats keep the lobby alive; polling is what carries the host's
        // Relay code across to clients. Both are well inside the service's limits.
        const float HeartbeatSeconds = 12f;
        const float PollSeconds = 3f;

        [Header("Lobby")]
        [Tooltip("Name shown in the service dashboard. The lobby code is what players use.")]
        public string lobbyName = "Overlink Match";

        public LobbyState State { get; private set; } = LobbyState.Idle;
        public Lobby CurrentLobby { get; private set; }
        public string LobbyCode => CurrentLobby?.LobbyCode ?? "";
        public bool IsHost { get; private set; }
        public int MemberCount => CurrentLobby?.Players?.Count ?? 0;
        public bool IsBusy => State == LobbyState.Searching
                           || State == LobbyState.Creating
                           || State == LobbyState.Joining
                           || State == LobbyState.Starting;

        /// <summary>Host-gated: the match only begins once both seats are filled.</summary>
        public bool CanStart => IsHost && CurrentLobby != null && MemberCount >= MaxPlayers;

        public event Action StateChanged;
        public event Action LobbyChanged;
        public event Action<string> StatusChanged;

        NetBootstrap net;
        string playerId;
        Coroutine heartbeatRoutine;
        Coroutine pollRoutine;
        bool leaving;
        bool polling;
        int operationVersion;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            net = FindObjectOfType<NetBootstrap>();
            if (net == null)
                Debug.LogWarning("[Lobby] No NetBootstrap in the scene; starting a match will fail.");
            else
            {
                net.OnNetworkConnected += OnNetworkConnected;
                net.OnNetworkDisconnected += OnNetworkDisconnected;
            }
        }

        void OnDestroy()
        {
            StopRoutines();
            if (net != null)
            {
                net.OnNetworkConnected -= OnNetworkConnected;
                net.OnNetworkDisconnected -= OnNetworkDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        // --- Public actions -----------------------------------------------------

        /// <summary>Join any open lobby, or host a new one if there is nothing to join.</summary>
        public async void QuickPlay()
        {
            if (IsBusy || CurrentLobby != null) return;

            int version = ++operationVersion;
            SetState(LobbyState.Searching);
            try
            {
                if (!await EnsureSignedInAsync()) return;
                if (version != operationVersion) return;

                Lobby lobby = null;
                try
                {
                    lobby = await LobbyService.Instance.QuickJoinLobbyAsync(
                        new QuickJoinLobbyOptions { Player = new Player(playerId) });
                    SetStatus("Joined a waiting match.");
                }
                catch (LobbyServiceException e)
                {
                    // No open lobby is the normal case, not an error worth surfacing loudly.
                    Debug.Log($"[Lobby] Quick join found nothing ({e.Reason}); hosting instead.");
                    lobby = null;
                }

                if (lobby == null) lobby = await CreatePrivateLobbyAsync(MaxPlayers, false);
                if (version != operationVersion) return;
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
            SetState(LobbyState.Creating);
            try
            {
                if (!await EnsureSignedInAsync()) return;
                Lobby lobby = await CreatePrivateLobbyAsync(MaxPlayers, true);
                if (version != operationVersion) return;
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

            SetState(LobbyState.Joining);
            int version = ++operationVersion;
            try
            {
                if (!await EnsureSignedInAsync()) return;

                Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(
                    code, new JoinLobbyByCodeOptions { Player = new Player(playerId) });
                if (version != operationVersion) return;
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
            try
            {
                int version = operationVersion;
                string relayCode = await net.CreateRelayAndHostAsync(MaxPlayers);
                if (string.IsNullOrEmpty(relayCode))
                {
                    SetState(LobbyState.InLobby);
                    return;
                }

                Lobby updated = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id,
                    new UpdateLobbyOptions
                    {
                        Data = new Dictionary<string, DataObject>
                        {
                            { KeyRelayCode, new DataObject(DataObject.VisibilityOptions.Member, relayCode) },
                            { KeyStatus, new DataObject(DataObject.VisibilityOptions.Member, StatusInGame) },
                        },
                    });

                if (version != operationVersion || CurrentLobby == null)
                {
                    if (net.IsListening) net.LeaveGame();
                    return;
                }

                CurrentLobby = updated;
                SetState(LobbyState.InGame);
                SetStatus("Match started — waiting for the other player to connect.");
                Notify();
            }
            catch (Exception e)
            {
                if (net != null && net.IsListening) net.LeaveGame();
                FailMatchmaking(e, "Start match");
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
        }

        void ClearLobby()
        {
            CurrentLobby = null;
            IsHost = false;
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
                _ = PingAsync(lobbyId);
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
                        SetState(LobbyState.InGame);
                        SetStatus("Connecting to the match…");
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
            if (State == LobbyState.InGame) SetStatus("Connected to the match.");
        }

        void OnNetworkDisconnected()
        {
            if (State == LobbyState.InGame) SetStatus("Disconnected from the match.");
        }

        async Task<bool> EnsureSignedInAsync()
        {
            AccountManager account = AccountManager.EnsureInstance();
            if (!await account.EnsureSignedInAsync())
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

        void SetStatus(string status) => StatusChanged?.Invoke(status);

        void Notify() => LobbyChanged?.Invoke();
    }
}
