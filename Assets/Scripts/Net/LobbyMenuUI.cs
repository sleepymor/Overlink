using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.Net
{
    /// <summary>
    /// Matchmaking menu: Quick Play, a custom lobby, and the in-lobby panel. Elements are
    /// found by name once in <see cref="Awake"/> so the scene needs no serialized wiring,
    /// matching how <see cref="NetMenuUI"/> is built. Start is host-only and lights up at
    /// two of two — there is no per-player ready toggle.
    /// </summary>
    public class LobbyMenuUI : MonoBehaviour
    {
        LobbyManager lobby;
        NetBootstrap net;

        Button quickPlayButton, createButton, joinButton, cancelButton, startButton, leaveButton;
        TMP_InputField lobbyCodeInput;
        TextMeshProUGUI lobbyCodeText, playerCountText, statusText, roleText;

        // Seeded so the first refresh always applies, whatever the current state is.
        bool wasBusy = true;
        bool wasListening = true;

        void Awake()
        {
            lobby = LobbyManager.Instance != null ? LobbyManager.Instance : FindObjectOfType<LobbyManager>();
            net = NetBootstrap.Instance != null ? NetBootstrap.Instance : FindObjectOfType<NetBootstrap>();

            if (lobby == null)
            {
                Debug.LogError("[Lobby] LobbyMenuUI found no LobbyManager in the scene.");
                enabled = false;
                return;
            }

            quickPlayButton = FindButton("LobbyQuickPlayButton");
            createButton = FindButton("LobbyCreateButton");
            joinButton = FindButton("LobbyJoinButton");
            cancelButton = FindButton("LobbyCancelButton");
            startButton = FindButton("LobbyStartButton");
            leaveButton = FindButton("LobbyLeaveButton");
            lobbyCodeInput = FindComponent<TMP_InputField>("LobbyCodeInput");
            lobbyCodeText = FindComponent<TextMeshProUGUI>("LobbyCodeText");
            playerCountText = FindComponent<TextMeshProUGUI>("LobbyPlayerCountText");
            statusText = FindComponent<TextMeshProUGUI>("LobbyStatusText");
            roleText = FindComponent<TextMeshProUGUI>("LobbyRoleText");

            if (quickPlayButton != null) quickPlayButton.onClick.AddListener(lobby.QuickPlay);
            if (createButton != null) createButton.onClick.AddListener(lobby.CreateLobby);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (lobbyCodeInput != null) lobbyCodeInput.onValueChanged.AddListener(_ => Refresh());
            if (cancelButton != null) cancelButton.onClick.AddListener(lobby.CancelSearch);
            if (startButton != null) startButton.onClick.AddListener(lobby.StartMatch);
            if (leaveButton != null) leaveButton.onClick.AddListener(lobby.LeaveLobby);

            lobby.StateChanged += Refresh;
            lobby.LobbyChanged += Refresh;
            lobby.StatusChanged += SetStatus;

            Refresh();
            SetStatus("");
        }

        void OnDestroy()
        {
            if (lobby == null) return;
            lobby.StateChanged -= Refresh;
            lobby.LobbyChanged -= Refresh;
            lobby.StatusChanged -= SetStatus;
        }

        void Update()
        {
            bool busy = lobby.IsBusy;
            bool listening = net != null && net.IsListening;
            if (busy == wasBusy && listening == wasListening) return;
            wasBusy = busy;
            wasListening = listening;
            Refresh();
        }

        void OnJoinClicked()
        {
            if (lobbyCodeInput != null) lobby.JoinLobbyByCode(lobbyCodeInput.text);
        }

        /// <summary>
        /// Mirrors the lobby state onto the menu: which panel is showing, whether the actions
        /// are available, and how many of the two seats are taken.
        /// </summary>
        void Refresh()
        {
            bool inLobby = lobby.CurrentLobby != null;
            bool idle = lobby.State == LobbyState.Idle;
            bool matched = lobby.State == LobbyState.InGame || (net != null && net.IsListening);

            SetActive(lobbyCodeText, inLobby);
            SetActive(startButton, lobby.IsHost);
            SetActive(roleText, inLobby);

            if (playerCountText != null)
            {
                playerCountText.text = inLobby ? $"{lobby.MemberCount}/{LobbyManager.MaxPlayers}" : "";
            }
            if (roleText != null)
            {
                roleText.text = lobby.IsHost ? "Host" : "Guest";
            }

            SetInteractable(quickPlayButton, idle);
            SetInteractable(createButton, idle);
            SetInteractable(joinButton, idle && !string.IsNullOrEmpty(lobbyCodeInput?.text));
            SetInteractable(cancelButton, !idle && !inLobby);
            SetInteractable(leaveButton, inLobby && !lobby.IsBusy);
            // Host-gated and only once both seats are filled.
            SetInteractable(startButton, lobby.CanStart && lobby.State == LobbyState.InLobby);
            if (lobbyCodeInput != null) lobbyCodeInput.interactable = idle && !matched;

            if (lobbyCodeText != null && inLobby)
            {
                lobbyCodeText.text = lobby.IsHost ? $"Lobby code: {lobby.LobbyCode}" : "In lobby";
            }
        }

        void SetStatus(string status)
        {
            if (statusText != null) statusText.text = status;
        }

        static void SetActive(Component component, bool value)
        {
            if (component != null) component.gameObject.SetActive(value);
        }

        static void SetInteractable(Selectable selectable, bool value)
        {
            if (selectable != null) selectable.interactable = value;
        }

        static Button FindButton(string objectName)
        {
            return FindComponent<Button>(objectName);
        }

        static T FindComponent<T>(string objectName) where T : Component
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogError($"[Lobby] UI object missing: {objectName}");
                return null;
            }
            var component = go.GetComponent<T>();
            if (component == null) Debug.LogError($"[Lobby] {objectName} has no {typeof(T).Name} component.");
            return component;
        }
    }
}
