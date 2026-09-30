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
        Task servicesTask;
        Coroutine watchdog;
        bool ready;

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

        public async Task HostGame()
        {
            if (!ready || !CanStart("Host")) return;

            try
            {
                SetStatus("Signing in…");
                await EnsureServicesAsync();
                SetStatus("Creating Relay allocation…");

                // maxPlayers counts the host, Relay counts only the clients that may join.
                Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

                transport.SetRelayServerData(new RelayServerData(alloc, RelayProtocol));
                if (!mgr.StartHost())
                {
                    SetStatus("Host failed to start — see console.");
                    return;
                }

                LastJoinCode = code;
                OnJoinCodeChanged?.Invoke(code);
                SetStatus($"Hosting — share code: {code}");
            }
            catch (Exception e)
            {
                SetStatus($"Host failed: {e.Message}");
            }
        }

        public async Task JoinGame(string rawCode)
        {
            if (!ready || !CanStart("Join")) return;

            string code = (rawCode ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                SetStatus("Enter a join code first.");
                return;
            }
            OnJoinCodeChanged?.Invoke("");

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
                    return;
                }

                SetStatus($"Joining {code}… (waiting for host)");
                watchdog = StartCoroutine(JoinWatchdog(code));
            }
            catch (Exception e)
            {
                SetStatus($"Join failed: {e.Message}");
            }
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
            if (IsListening)
            {
                SetStatus($"Already hosting or connected — Leave before trying to {action}.");
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

        void ResetSeats()
        {
            freeSeats.Clear();
            clientSeats.Clear();
            for (int seat = maxPlayers - 1; seat >= 0; seat--) freeSeats.Push(seat);
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
        /// </summary>
        Task EnsureServicesAsync()
        {
            if (servicesTask == null)
            {
                servicesTask = SignInAsync();
            }
            return servicesTask;

            static async Task SignInAsync()
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}
