using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Collections;
using Overlink.Core;
using System.Text;

namespace Overlink.Net
{
    /// <summary>
    /// Owns the Relay session: signs in, allocates a Relay server, starts the NGO host or
    /// client, and spawns one avatar per connected player. Lives on the same GameObject
    /// as the NetworkManager.
    /// </summary>
    public class NetBootstrap : MonoBehaviour
    {
        // This machine's Burst toolchain is broken (every CompileFunctionPointer throws,
        // including Unity's own packages) and the editor ignores the Jobs-menu toggle on
        // this path, so the managed fallback is forced instead. No gameplay code uses
        // Burst, so there is no measurable cost to the project.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ForceManagedTransportFallback()
        {
            Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false;
        }

        public static NetBootstrap Instance { get; private set; }

        const string AvatarResourceName = "NetAvatar";
        const string RelayProtocol = "dtls";
        const float SeatRadius = 2.5f;
        // Relay/lobby verification can legitimately take longer while Unity Services is
        // recovering from a rate limit. This is only a diagnostic warning, but 15 seconds
        // made a normal two-client connection look like it had timed out.
        const float WatchdogSeconds = 60f;
        const float SeatAnnouncementSeconds = 10f;
        const float SeatVerificationInitialRetrySeconds = 5f;
        const float SeatVerificationMaxRetrySeconds = 60f;
        const string SeatReadyMessage = "overlink.seat.ready.v1";
        const string SeatChallengeMessage = "overlink.seat.challenge.v1";
        const string SeatProofMessage = "overlink.seat.proof.v1";
        const string SeatAcceptedMessage = "overlink.seat.accepted.v1";
        const string HostChallengeMessage = "overlink.host.challenge.v1";
        const string HostProofMessage = "overlink.host.proof.v1";
        const string HostAcceptedMessage = "overlink.host.accepted.v1";
        const string BattleDeckMessage = "overlink.match.deck.v1";
        const string MatchCommandMessage = "overlink.match.command.v1";
        const string MatchCommandResponseMessage = "overlink.match.command.response.v1";
        const string MatchViewMessage = "overlink.match.view.v1";
        const int MaxBattleDeckPayloadBytes = 4096;

        [Header("Relay")]
        [Min(2)] public int maxPlayers = 4;

        [Header("Presence")]
        [Tooltip("Avatar prefab registered with Netcode for GameObjects. " +
                 "Falls back to Resources/" + AvatarResourceName + " when left empty.")]
        public GameObject avatarPrefab;

        public string LastJoinCode { get; private set; } = "";
        public string Status { get; private set; } = "Offline";
        public bool IsListening => mgr != null && mgr.IsListening;
        public ulong LocalPeerId => mgr != null ? mgr.LocalClientId : ulong.MaxValue;
        public bool IsConnected => mgr != null && mgr.IsConnectedClient;
        public bool IsHost => mgr != null && mgr.IsHost;
        public bool HasRemotePeer => mgr != null && mgr.IsServer && mgr.ConnectedClientsIds.Count > 1;
        public bool IsSeatAuthenticated { get; private set; }
        public DeckData RemoteBattleDeck { get; private set; }
        public event Action OnSeatAuthenticated;
        public event Action<DeckData> OnRemoteBattleDeck;
        public event Action<long, long, CommandResult> OnMatchCommandResponse;
        public event Action<GameCommand, long> OnRemoteCommandAccepted;
        public event Action<MatchViewSnapshot> OnGuestViewChanged;
        public MatchViewSnapshot GuestView => guestViewReceiver?.Current;

        /// <summary>Publish a redacted full view at the bound host boundary's current revision.</summary>
        public void SendGuestView()
        {
            if (mgr == null || !mgr.IsServer || !IsSeatAuthenticated || commandBoundary == null || boundEngine == null)
                throw new InvalidOperationException("The authenticated host match channel is not ready.");
            byte[] payload = MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(boundEngine.State, PlayerIndex.Opponent, commandBoundary.Revision));
            SendSeatMessage(MatchViewMessage, AuthenticatedRemotePeer(), payload);
        }

        /// <summary>One in-flight request at a time; the caller must reconcile state before issuing another.</summary>
        public void SendMatchCommand(long requestId, long revision, GameCommand command)
        {
            if (mgr == null || mgr.IsServer || !mgr.IsConnectedClient || !IsSeatAuthenticated || pendingCommandRequest != 0 ||
                GuestView == null || GuestView.Revision < requiredGuestRevision)
                throw new InvalidOperationException("The authenticated guest command channel is not ready.");
            byte[] payload = MatchCommandTransport.CreateRequest(requestId, revision, command);
            pendingCommandRequest = requestId;
            try { SendSeatMessage(MatchCommandMessage, NetworkManager.ServerClientId, payload); }
            catch { pendingCommandRequest = 0; throw; }
        }

        /// <summary>Unranked only: the guest shares its deck with the authenticated host.</summary>
        public void SendBattleDeck(DeckData deck)
        {
            if (mgr == null || mgr.IsServer || !mgr.IsConnectedClient || !IsSeatAuthenticated || deck == null)
                throw new InvalidOperationException("The authenticated guest deck channel is not ready.");
            byte[] payload = Encoding.UTF8.GetBytes(DeckData.ToJson(deck));
            if (payload.Length == 0 || payload.Length > MaxBattleDeckPayloadBytes)
                throw new InvalidOperationException("Battle deck payload is too large.");
            SendSeatMessage(BattleDeckMessage, NetworkManager.ServerClientId, payload);
        }

        /// <summary>
        /// Reject ambiguous or oversized unranked deck envelopes before they reach the match.
        /// This does not prove ownership or protect against a malicious match host.
        /// </summary>
        public static bool TryReadBattleDeck(byte[] payload, out DeckData deck)
        {
            deck = null;
            if (payload == null || payload.Length == 0 || payload.Length > MaxBattleDeckPayloadBytes) return false;
            try
            {
                string json = new UTF8Encoding(false, true).GetString(payload);
                DeckData parsed = DeckData.FromJson(json);
                // The wire format is generated by ToJson. Requiring the exact representation
                // rejects duplicate fields, ignored fields and parser normalization ambiguities.
                if (parsed == null || parsed.Version != 1 || parsed.Cards == null ||
                    !string.Equals(json, DeckData.ToJson(parsed), StringComparison.Ordinal)) return false;
                int total = 0;
                foreach (var entry in parsed.Cards)
                {
                    if (string.IsNullOrEmpty(entry.Key) || entry.Value < 1 ||
                        entry.Value > DeckValidator.MaxCopiesPerCard) return false;
                    total += entry.Value;
                    if (total > DeckValidator.MaxDeckSize) return false;
                }
                if (total < DeckValidator.MinDeckSize) return false;
                deck = parsed;
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                return false;
            }
        }

        /// <summary>Bind the authoritative engine once the real match has been initialized.</summary>
        public AuthoritativeCommandBoundary BindAuthenticatedMatch(MatchEngine engine)
        {
            if (mgr == null || !mgr.IsServer || !IsSeatAuthenticated || seatHandshake == null)
                throw new InvalidOperationException("No authenticated remote seat is available.");
            var boundary = seatHandshake.CreateCommandBoundary(engine);
            commandBoundary = boundary;
            boundEngine = engine;
            return boundary;
        }

        public event Action<string> OnStatusChanged;
        public event Action<string> OnJoinCodeChanged;

        static readonly Color[] Palette =
        {
            new Color(0.85f, 0.30f, 0.25f),
            new Color(0.25f, 0.50f, 0.90f),
            new Color(0.30f, 0.70f, 0.40f),
            new Color(0.95f, 0.75f, 0.25f),
        };

        NetworkManager mgr;
        UnityTransport transport;
        Coroutine watchdog;
        bool ready;
        bool relayOperation;
        int seatSessionVersion;
        bool seatVerificationPending;
        bool seatProofSent;
        bool guestProofAccepted;
        bool guestSeatAccepted;
        bool hostProofSent;
        bool hostProofAcknowledged;
        bool hostProofVerified;
        float nextSeatAnnouncementAt;
        float nextSeatVerificationAt;
        int seatAnnouncementFailures;
        int seatVerificationFailures;
        bool seatAnnouncementPending;
        Coroutine seatAnnouncementRoutine;
        RelaySeatHandshake seatHandshake;
        RelayHostChallenge hostChallenge;
        AuthoritativeCommandBoundary commandBoundary;
        long requiredGuestRevision;
        MatchEngine boundEngine;
        long pendingCommandRequest;
        MatchViewReceiver guestViewReceiver;

        public event Action OnNetworkConnected;
        public event Action OnNetworkDisconnected;

        readonly Stack<int> freeSeats = new Stack<int>();
        readonly Dictionary<ulong, int> clientSeats = new Dictionary<ulong, int>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // The Relay/NGO session is created in the matchmaking scene and consumed by Gameplay.
            // Keep this owner alive across that scene transition; otherwise the network manager is
            // destroyed exactly when the match is ready to begin.
            DontDestroyOnLoad(gameObject);

            mgr = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            if (mgr == null) Debug.LogError("[Net] NetBootstrap needs a NetworkManager on the same object.");
            if (transport == null) Debug.LogError("[Net] NetBootstrap needs a UnityTransport on the same object.");

            if (mgr != null && transport != null)
            {
                // A NetworkManager authored in a scene has serialized configuration, but
                // AddComponent<NetworkManager>() on the production session root may not.
                if (mgr.NetworkConfig == null)
                    mgr.NetworkConfig = new NetworkConfig();
                if (mgr.NetworkConfig.NetworkTransport == null)
                    mgr.NetworkConfig.NetworkTransport = transport;
                RegisterAvatarPrefab();
                mgr.OnClientConnectedCallback += OnClientConnected;
                mgr.OnClientDisconnectCallback += OnClientDisconnected;
                ResetSeats();
                ready = true;
            }

            SetStatus("Offline — Host or Join to begin.");
        }

        void OnDestroy()
        {
            if (mgr != null)
            {
                mgr.OnClientConnectedCallback -= OnClientConnected;
                mgr.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        // --- Session lifecycle -------------------------------------------------

        public Task HostGame()
        {
            if (!CanStart("Host")) return Task.CompletedTask;
            return CreateRelayAndHostAsync(maxPlayers);
        }

        public Task JoinGame(string rawCode)
        {
            if (!CanStart("Join")) return Task.CompletedTask;

            string code = NormalizeCode(rawCode);
            if (code.Length == 0)
            {
                SetStatus("Enter a join code first.");
                return Task.CompletedTask;
            }

            OnJoinCodeChanged?.Invoke("");
            return JoinRelayAsync(code);
        }

        /// <summary>
        /// Allocates a Relay server for <paramref name="playerCount"/> total players (the
        /// host included) and starts the NGO host. Returns the join code to hand out, or
        /// null if the attempt failed. LobbyManager uses this directly so it never has to
        /// surface a Relay code to the player.
        /// </summary>
        public async Task<string> CreateRelayAndHostAsync(int playerCount)
        {
            if (!ready)
            {
                SetStatus("Session layer not ready — see console.");
                return null;
            }
            if (mgr.IsListening)
            {
                SetStatus("Already hosting or connected — Leave before starting again.");
                return null;
            }
            if (relayOperation)
            {
                SetStatus("A session operation is already in progress.");
                return null;
            }

            relayOperation = true;
            try
            {
                SetStatus("Signing in…");
                await EnsureServicesAsync();
                SetStatus("Creating Relay allocation…");

                // playerCount counts the host, Relay counts only the clients that may join.
                Allocation alloc = await RelayService.Instance.CreateAllocationAsync(Mathf.Max(0, playerCount - 1));
                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

                transport.SetRelayServerData(new RelayServerData(alloc, RelayProtocol));
                // StartHost invokes the local connected callback synchronously. Reset before
                // starting so the host consumes seat zero and it cannot be reused by a guest.
                ResetSeats(playerCount);
                if (!mgr.StartHost())
                {
                    ResetSeats(playerCount);
                    SetStatus("Host failed to start — see console.");
                    return null;
                }

                RegisterSeatMessages();

                LastJoinCode = code;
                OnJoinCodeChanged?.Invoke(code);
                SetStatus($"Hosting — share code: {code}");
                return code;
            }
            catch (Exception e)
            {
                SetStatus($"Host failed: {e.Message}");
                return null;
            }
            finally
            {
                relayOperation = false;
            }
        }

        /// <summary>
        /// Joins a Relay allocation by join code and starts the NGO client. Returns false
        /// if the attempt failed.
        /// </summary>
        public async Task<bool> JoinRelayAsync(string rawCode)
        {
            if (!ready)
            {
                SetStatus("Session layer not ready — see console.");
                return false;
            }
            if (mgr.IsListening)
            {
                SetStatus("Already hosting or connected — Leave before joining.");
                return false;
            }
            if (relayOperation)
            {
                SetStatus("A session operation is already in progress.");
                return false;
            }

            string code = NormalizeCode(rawCode);
            if (code.Length == 0)
            {
                SetStatus("Enter a join code first.");
                return false;
            }

            relayOperation = true;
            try
            {
                SetStatus("Signing in…");
                await EnsureServicesAsync();
                SetStatus($"Joining {code}…");

                JoinAllocation alloc = await RelayService.Instance.JoinAllocationAsync(code);
                transport.SetRelayServerData(new RelayServerData(alloc, RelayProtocol));
                if (!mgr.StartClient())
                {
                    SetStatus("Client failed to start — see console.");
                    return false;
                }

                RegisterSeatMessages();

                LastJoinCode = "";
                SetStatus($"Joining {code}… (waiting for host)");
                if (watchdog != null) StopCoroutine(watchdog);
                watchdog = StartCoroutine(JoinWatchdog(code));
                return true;
            }
            catch (Exception e)
            {
                SetStatus($"Join failed: {e.Message}");
                return false;
            }
            finally
            {
                relayOperation = false;
            }
        }

        static string NormalizeCode(string rawCode)
        {
            return (rawCode ?? "").Trim().ToUpperInvariant();
        }

        public void LeaveGame()
        {
            if (!IsListening) return;
            ResetSeatHandshake();
            mgr.Shutdown();
            ResetSeats();
            LastJoinCode = "";
            OnJoinCodeChanged?.Invoke("");
            SetStatus("Disconnected.");
        }

        bool CanStart(string action)
        {
            if (!ready)
            {
                SetStatus("Session layer not ready — see console.");
                return false;
            }
            if (IsListening)
            {
                SetStatus($"Already hosting or connected — Leave before trying to {action}.");
                return false;
            }
            if (relayOperation)
            {
                SetStatus($"Already trying to {action.ToLowerInvariant()} — please wait.");
                return false;
            }
            return true;
        }

        IEnumerator JoinWatchdog(string code)
        {
            yield return new WaitForSeconds(WatchdogSeconds);
            watchdog = null;
            if (mgr != null && mgr.IsClient && !mgr.IsConnectedClient)
                SetStatus($"Still waiting on {code} — is the host still running?");
        }

        // --- Presence ----------------------------------------------------------

        void OnClientConnected(ulong clientId)
        {
            if (mgr == null) return;

            if (!mgr.IsServer)
            {
                if (clientId != mgr.LocalClientId) return;
                if (watchdog != null)
                {
                    StopCoroutine(watchdog);
                    watchdog = null;
                }
                SetStatus("Connected!");
                OnNetworkConnected?.Invoke();
                if (seatAnnouncementRoutine != null) StopCoroutine(seatAnnouncementRoutine);
                seatAnnouncementRoutine = StartCoroutine(AnnounceSeatUntilAuthenticated());
                return;
            }

            SpawnAvatar(clientId);
            if (clientId != mgr.LocalClientId) OnNetworkConnected?.Invoke();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (mgr == null) return;
            if ((!mgr.IsServer && clientId == mgr.LocalClientId) ||
                (mgr.IsServer && clientId != mgr.LocalClientId)) ResetSeatHandshake();

            if (!mgr.IsServer)
            {
                if (clientId == mgr.LocalClientId) SetStatus("Disconnected from host.");
                if (clientId == mgr.LocalClientId) OnNetworkDisconnected?.Invoke();
                return;
            }

            // Netcode despawns the avatar itself when its owner leaves, because
            // SpawnAvatar leaves DontDestroyWithOwner off. All this does is release the
            // seat and log it — no NetworkVariable writes, which is what used to throw
            // inside NGO's disconnect callback and abort the network tick.
            if (clientSeats.Remove(clientId, out int seat))
            {
                freeSeats.Push(seat);
                Debug.Log($"[Net] Client {clientId} left, seat {seat} released.");
            }
            if (clientId != mgr.LocalClientId) OnNetworkDisconnected?.Invoke();
        }

        void SpawnAvatar(ulong clientId)
        {
            if (clientSeats.ContainsKey(clientId)) return;
            if (freeSeats.Count == 0)
            {
                Debug.LogWarning($"[Net] No free seat for client {clientId}; lobby is full.");
                return;
            }

            int seat = freeSeats.Pop();
            var go = Instantiate(avatarPrefab, SeatPosition(seat), Quaternion.identity);
            go.name = $"Avatar_{clientId}";

            var netObj = go.GetComponent<NetworkObject>();
            netObj.DontDestroyWithOwner = false;
            netObj.SpawnWithOwnership(clientId);

            // NetworkVariables only accept writes once the object is spawned.
            go.GetComponent<PresencePlayer>()
              .ServerSetProfile($"Player {clientId}", Palette[seat % Palette.Length]);

            clientSeats[clientId] = seat;
            Debug.Log($"[Net] Spawned avatar for client {clientId} on seat {seat}.");
        }

        void RegisterSeatMessages()
        {
            var messages = mgr.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(SeatReadyMessage, OnSeatReady);
            messages.RegisterNamedMessageHandler(SeatChallengeMessage, OnSeatChallenge);
            messages.RegisterNamedMessageHandler(SeatProofMessage, OnSeatProof);
            messages.RegisterNamedMessageHandler(SeatAcceptedMessage, OnSeatAccepted);
            messages.RegisterNamedMessageHandler(HostChallengeMessage, OnHostChallenge);
            messages.RegisterNamedMessageHandler(HostProofMessage, OnHostProof);
            messages.RegisterNamedMessageHandler(HostAcceptedMessage, OnHostAccepted);
            messages.RegisterNamedMessageHandler(BattleDeckMessage, OnBattleDeck);
            messages.RegisterNamedMessageHandler(MatchCommandMessage, OnMatchCommand);
            messages.RegisterNamedMessageHandler(MatchCommandResponseMessage, OnMatchCommandResponseMessage);
            messages.RegisterNamedMessageHandler(MatchViewMessage, OnMatchView);
        }

        void ResetSeatHandshake()
        {
            seatSessionVersion++;
            IsSeatAuthenticated = false;
            seatVerificationPending = false;
            seatProofSent = false;
            guestProofAccepted = false;
            guestSeatAccepted = false;
            hostProofSent = false;
            hostProofAcknowledged = false;
            hostProofVerified = false;
            nextSeatAnnouncementAt = 0f;
            nextSeatVerificationAt = 0f;
            seatAnnouncementFailures = 0;
            seatVerificationFailures = 0;
            seatAnnouncementPending = false;
            if (seatAnnouncementRoutine != null) StopCoroutine(seatAnnouncementRoutine);
            seatAnnouncementRoutine = null;
            seatHandshake = null;
            hostChallenge = null;
            RemoteBattleDeck = null;
            commandBoundary = null;
            boundEngine = null;
            pendingCommandRequest = 0;
            requiredGuestRevision = 0;
            guestViewReceiver = null;
        }

        IEnumerator AnnounceSeatUntilAuthenticated()
        {
            int version = seatSessionVersion;
            while (version == seatSessionVersion && mgr != null && mgr.IsConnectedClient && !mgr.IsServer && !IsSeatAuthenticated)
            {
                if (!seatAnnouncementPending && Time.realtimeSinceStartup >= nextSeatAnnouncementAt)
                    _ = AnnounceSeatAsync();
                yield return new WaitForSecondsRealtime(1f);
            }
            seatAnnouncementRoutine = null;
        }

        async Task AnnounceSeatAsync()
        {
            int version = seatSessionVersion;
            seatAnnouncementPending = true;
            try
            {
                var lobby = LobbyManager.Instance;
                if (lobby == null) return;
                await lobby.PublishSeatProofAsync();
                if (this == null || version != seatSessionVersion || mgr == null || !mgr.IsConnectedClient || mgr.IsServer) return;
                seatAnnouncementFailures = 0;
                nextSeatAnnouncementAt = Time.realtimeSinceStartup + SeatAnnouncementSeconds;
                SendSeatMessage(SeatReadyMessage, NetworkManager.ServerClientId, Array.Empty<byte>());
            }
            catch (Exception error)
            {
                if (this != null && version == seatSessionVersion)
                {
                    seatAnnouncementFailures++;
                    nextSeatAnnouncementAt = Time.realtimeSinceStartup + RetryDelay(seatAnnouncementFailures);
                    SetStatus(IsRateLimited(error)
                        ? "Lobby verification is rate limited — retrying shortly…"
                        : "Could not authenticate the lobby connection: " + error.Message);
                }
            }
            finally { if (this != null && version == seatSessionVersion) seatAnnouncementPending = false; }
        }

        void OnSeatReady(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || !mgr.IsServer || sender == mgr.LocalClientId || IsSeatAuthenticated ||
                seatVerificationPending || Time.realtimeSinceStartup < nextSeatVerificationAt ||
                reader.Length - reader.Position != 0) return;
            _ = ChallengePeerAsync(sender);
        }

        async Task ChallengePeerAsync(ulong sender)
        {
            seatVerificationPending = true;
            int version = seatSessionVersion;
            try
            {
                var lobby = LobbyManager.Instance;
                if (lobby == null) return;
                LobbySeatIdentity identity = await lobby.FetchOpponentSeatIdentityAsync();
                if (this == null || version != seatSessionVersion || mgr == null || !mgr.IsServer ||
                    !mgr.ConnectedClients.ContainsKey(sender)) return;
                seatVerificationFailures = 0;
                nextSeatVerificationAt = 0f;
                seatHandshake = new RelaySeatHandshake(identity, mgr.LocalClientId, sender);
                SendSeatMessage(SeatChallengeMessage, sender, seatHandshake.Challenge);
            }
            catch (Exception error)
            {
                if (this != null && version == seatSessionVersion)
                {
                    seatVerificationFailures++;
                    nextSeatVerificationAt = Time.realtimeSinceStartup + RetryDelay(seatVerificationFailures);
                    SetStatus(IsRateLimited(error)
                        ? "Lobby verification is rate limited — retrying shortly…"
                        : "Could not verify the lobby connection: " + error.Message);
                }
            }
            finally { if (this != null && version == seatSessionVersion) seatVerificationPending = false; }
        }

        static float RetryDelay(int failures)
        {
            int exponent = Mathf.Clamp(failures - 1, 0, 4);
            return Mathf.Min(SeatVerificationMaxRetrySeconds,
                SeatVerificationInitialRetrySeconds * Mathf.Pow(2f, exponent));
        }

        static bool IsRateLimited(Exception error)
        {
            string message = error?.Message ?? string.Empty;
            return message.IndexOf("request_rate_limited", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("rate limited", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void OnSeatChallenge(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || mgr.IsServer || sender != NetworkManager.ServerClientId || IsSeatAuthenticated || reader.Length - reader.Position != LobbySeatProof.ChallengeBytes) return;
            try
            {
                var challenge = new byte[LobbySeatProof.ChallengeBytes];
                reader.ReadBytesSafe(ref challenge, challenge.Length);
                byte[] proof = LobbyManager.Instance.SignSeatChallenge(mgr.LocalClientId, challenge);
                SendSeatMessage(SeatProofMessage, NetworkManager.ServerClientId, proof);
                seatProofSent = true;
            }
            catch (Exception error) { SetStatus("Could not sign the lobby challenge: " + error.Message); }
        }

        void OnSeatProof(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || !mgr.IsServer || seatHandshake == null || reader.Length - reader.Position != 256) return;
            var proof = new byte[256];
            reader.ReadBytesSafe(ref proof, proof.Length);
            if (!seatHandshake.Accept(sender, proof)) return;
            SendSeatMessage(SeatAcceptedMessage, sender, Array.Empty<byte>());
            guestProofAccepted = true;
            TryCompleteAuthentication();
        }

        void OnSeatAccepted(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || mgr.IsServer || sender != NetworkManager.ServerClientId || reader.Length - reader.Position != 0 || guestSeatAccepted || !seatProofSent) return;
            guestSeatAccepted = true;
            TryCompleteAuthentication();
            _ = BeginHostAuthenticationAsync();
        }

        async Task BeginHostAuthenticationAsync()
        {
            int version = seatSessionVersion;
            int failures = 0;
            while (version == seatSessionVersion && mgr != null && !mgr.IsServer &&
                   mgr.IsConnectedClient && guestSeatAccepted && !hostProofSent)
            {
                try
                {
                    LobbyManager lobby = LobbyManager.Instance;
                    LobbySeatIdentity identity = await lobby.FetchHostSeatIdentityAsync();
                    if (this == null || version != seatSessionVersion || mgr == null || mgr.IsServer ||
                        !mgr.IsConnectedClient || !guestSeatAccepted) return;
                    hostChallenge = new RelayHostChallenge(identity, NetworkManager.ServerClientId);
                    SendSeatMessage(HostChallengeMessage, NetworkManager.ServerClientId, hostChallenge.Challenge);
                    hostProofSent = true;
                    return;
                }
                catch (Exception error)
                {
                    if (this == null || version != seatSessionVersion) return;
                    failures++;
                    SetStatus(IsRateLimited(error)
                        ? "Lobby verification is rate limited — retrying shortly…"
                        : "Waiting for fresh lobby data — retrying shortly…");
                    await Task.Delay(TimeSpan.FromSeconds(RetryDelay(failures)));
                }
            }
        }

        void OnHostChallenge(ulong sender, FastBufferReader reader)
        {
            int remaining = reader.Length - reader.Position;
            if (mgr == null || !mgr.IsServer || sender == mgr.LocalClientId ||
                !guestProofAccepted || seatHandshake == null || !seatHandshake.IsAuthenticatedPeer(sender) ||
                remaining != LobbySeatProof.ChallengeBytes) return;
            var challenge = new byte[remaining];
            reader.ReadBytesSafe(ref challenge, challenge.Length);
            try
            {
                byte[] proof = LobbyManager.Instance.SignSeatChallenge(mgr.LocalClientId, challenge);
                SendSeatMessage(HostProofMessage, sender, proof);
            }
            catch (Exception error) { SetStatus("Could not prove the host identity: " + error.Message); }
        }

        void OnHostProof(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || mgr.IsServer || sender != NetworkManager.ServerClientId ||
                !hostProofSent || hostChallenge == null || reader.Length - reader.Position != 256) return;
            var proof = new byte[256];
            reader.ReadBytesSafe(ref proof, proof.Length);
            if (!hostChallenge.Accept(sender, proof)) return;
            hostProofVerified = true;
            SendSeatMessage(HostAcceptedMessage, NetworkManager.ServerClientId, Array.Empty<byte>());
            TryCompleteAuthentication();
        }

        void OnHostAccepted(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || !mgr.IsServer || sender == mgr.LocalClientId ||
                !guestProofAccepted || seatHandshake == null || !seatHandshake.IsAuthenticatedPeer(sender) ||
                reader.Length - reader.Position != 0) return;
            hostProofAcknowledged = true;
            TryCompleteAuthentication();
        }

        void TryCompleteAuthentication()
        {
            bool complete = mgr != null && (mgr.IsServer
                ? guestProofAccepted && hostProofAcknowledged
                : guestSeatAccepted && hostProofVerified);
            if (!complete || IsSeatAuthenticated) return;
            IsSeatAuthenticated = true;
            OnSeatAuthenticated?.Invoke();
        }

        void OnBattleDeck(ulong sender, FastBufferReader reader)
        {
            int remaining = reader.Length - reader.Position;
            if (mgr == null || !mgr.IsServer || !IsSeatAuthenticated || remaining <= 0 ||
                remaining > MaxBattleDeckPayloadBytes || RemoteBattleDeck != null) return;
            if (seatHandshake == null || !seatHandshake.IsAuthenticatedPeer(sender)) return;

            byte[] payload = new byte[remaining];
            reader.ReadBytesSafe(ref payload, payload.Length);
            if (!TryReadBattleDeck(payload, out DeckData deck)) return;
            RemoteBattleDeck = deck;
            OnRemoteBattleDeck?.Invoke(deck);
        }

        ulong AuthenticatedRemotePeer()
        {
            foreach (ulong peer in mgr.ConnectedClientsIds)
                if (seatHandshake != null && seatHandshake.IsAuthenticatedPeer(peer)) return peer;
            throw new InvalidOperationException("No authenticated remote player is connected.");
        }

        void OnMatchCommand(ulong sender, FastBufferReader reader)
        {
            int length = reader.Length - reader.Position;
            if (mgr == null || !mgr.IsServer || !IsSeatAuthenticated || seatHandshake == null ||
                !seatHandshake.IsAuthenticatedPeer(sender) || commandBoundary == null ||
                length < 18 || length > MatchCommandTransport.MaxRequestBytes) return;
            var payload = new byte[length];
            reader.ReadBytesSafe(ref payload, length);
            if (!MatchCommandTransport.TryExecute(payload, sender, commandBoundary, out byte[] response, out GameCommand accepted)) return;
            SendSeatMessage(MatchCommandResponseMessage, sender, response);
            if (accepted != null) OnRemoteCommandAccepted?.Invoke(accepted, commandBoundary.Revision);
            else SendGuestView(); // Rejected stale commands need the current authoritative view too.
        }

        void OnMatchCommandResponseMessage(ulong sender, FastBufferReader reader)
        {
            if (mgr == null || mgr.IsServer || !IsSeatAuthenticated || sender != NetworkManager.ServerClientId ||
                pendingCommandRequest == 0 || reader.Length - reader.Position != MatchCommandTransport.ResponseBytes) return;
            var payload = new byte[MatchCommandTransport.ResponseBytes];
            reader.ReadBytesSafe(ref payload, payload.Length);
            if (!MatchCommandTransport.TryReadResponse(payload, out long requestId, out long revision, out CommandResult result) ||
                requestId != pendingCommandRequest) return;
            pendingCommandRequest = 0;
            requiredGuestRevision = Math.Max(requiredGuestRevision, revision);
            OnMatchCommandResponse?.Invoke(requestId, revision, result);
        }

        void OnMatchView(ulong sender, FastBufferReader reader)
        {
            int length = reader.Length - reader.Position;
            if (mgr == null || mgr.IsServer || !IsSeatAuthenticated || sender != NetworkManager.ServerClientId ||
                length <= 0 || length > MatchViewDecoder.MaxPayloadBytes) return;
            var payload = new byte[length];
            reader.ReadBytesSafe(ref payload, length);
            if (!MatchViewDecoder.TryDecode(payload, out var snapshot)) return;
            if (guestViewReceiver == null) guestViewReceiver = new MatchViewReceiver(PlayerIndex.Opponent);
            if (guestViewReceiver.TryApply(snapshot)) OnGuestViewChanged?.Invoke(snapshot);
        }

        void SendSeatMessage(string name, ulong recipient, byte[] bytes)
        {
            using (var writer = new FastBufferWriter(Math.Max(1, bytes.Length), Allocator.Temp))
            {
                if (bytes.Length > 0) writer.WriteBytesSafe(bytes, bytes.Length);
                mgr.CustomMessagingManager.SendNamedMessage(name, recipient, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        void ResetSeats() => ResetSeats(maxPlayers);

        /// <summary>
        /// Seat 0 always belongs to the host, so positions and colours stay put across
        /// join/leave cycles. <paramref name="count"/> is clamped to at least one.
        /// </summary>
        void ResetSeats(int count)
        {
            int seats = Mathf.Max(1, count);
            freeSeats.Clear();
            clientSeats.Clear();
            for (int seat = seats - 1; seat >= 0; seat--) freeSeats.Push(seat);
        }

        static Vector3 SeatPosition(int seat)
        {
            float angle = seat * Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(angle) * SeatRadius, 0f, Mathf.Sin(angle) * SeatRadius);
        }

        // --- Setup -------------------------------------------------------------

        /// <summary>
        /// Avatars are spawned dynamically rather than placed in the scene: NGO can only
        /// replicate a dynamically spawned object if its prefab is registered, and that
        /// registration has to happen on every peer before the session starts.
        /// </summary>
        void RegisterAvatarPrefab()
        {
            if (avatarPrefab == null) avatarPrefab = Resources.Load<GameObject>(AvatarResourceName);
            if (avatarPrefab == null)
            {
                Debug.LogError($"[Net] Avatar prefab missing — assign one, or add one at Resources/{AvatarResourceName}.");
                return;
            }
            if (avatarPrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError($"[Net] Avatar prefab '{avatarPrefab.name}' has no NetworkObject.");
                return;
            }
            // Re-registering the same prefab is a no-op inside NGO's prefab handler, so
            // this stays correct if the prefab is later wired up in the Inspector too.
            mgr.AddNetworkPrefab(avatarPrefab);
        }

        void SetStatus(string status)
        {
            Status = status;
            OnStatusChanged?.Invoke(status);
            Debug.Log($"[Net] {status}");
        }

        /// <summary>
        /// Single-flight Unity Services startup: caching the Task means a second click
        /// during sign-in waits on the same work instead of initialising the SDK twice.
        /// LobbyManager reuses this so the Lobby and Relay paths share one signed-in player.
        /// </summary>
        public async Task EnsureServicesAsync()
        {
            // AccountManager owns the single-flight initialization task and resets it after
            // failures, so a transient outage can be retried instead of poisoning this layer.
            AccountManager account = AccountManager.EnsureInstance();
            if (!await account.EnsureSignedInAsync())
                throw new InvalidOperationException(account.Status);
        }
    }
}
