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
        const float WatchdogSeconds = 15f;

        [Header("Relay")]
        [Min(2)] public int maxPlayers = 4;

        [Header("Presence")]
        [Tooltip("Avatar prefab registered with Netcode for GameObjects. " +
                 "Falls back to Resources/" + AvatarResourceName + " when left empty.")]
        public GameObject avatarPrefab;

        public string LastJoinCode { get; private set; } = "";
        public string Status { get; private set; } = "Offline";
        public bool IsListening => mgr != null && mgr.IsListening;
        public bool IsHost => mgr != null && mgr.IsHost;

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

            mgr = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            if (mgr == null) Debug.LogError("[Net] NetBootstrap needs a NetworkManager on the same object.");
            if (transport == null) Debug.LogError("[Net] NetBootstrap needs a UnityTransport on the same object.");

            if (mgr != null && transport != null)
            {
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
                return;
            }

            SpawnAvatar(clientId);
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (mgr == null) return;

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
