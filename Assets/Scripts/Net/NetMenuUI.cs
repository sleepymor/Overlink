using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.Net
{
    /// <summary>
    /// Binds the NetPresenceDemo menu to <see cref="NetBootstrap"/>. Elements are looked
    /// up once by name so the scene needs no serialized wiring, and the cached references
    /// survive the JoinCodeText label being toggled off (GameObject.Find skips inactive
    /// objects, so a per-click lookup would silently fail).
    /// </summary>
    public class NetMenuUI : MonoBehaviour
    {
        NetBootstrap net;

        Button hostButton, joinButton, pingButton, leaveButton;
        TMP_InputField joinInput;
        TextMeshProUGUI statusText, joinCodeText;

        Action<string> statusHandler;
        Action<string> codeHandler;

        // Seeded so the first Update always applies, whatever the current state is.
        bool wasListening = true;
        bool wasPingable = true;

        void Awake()
        {
            net = NetBootstrap.Instance != null ? NetBootstrap.Instance : FindObjectOfType<NetBootstrap>();
            if (net == null)
            {
                Debug.LogError("[Net] NetMenuUI found no NetBootstrap in the scene.");
                enabled = false;
                return;
            }

            hostButton = FindButton("HostButton");
            joinButton = FindButton("JoinButton");
            pingButton = FindButton("PingButton");
            leaveButton = FindButton("LeaveButton");
            joinInput = FindComponent<TMP_InputField>("JoinCodeInput");
            statusText = FindComponent<TextMeshProUGUI>("StatusText");
            joinCodeText = FindComponent<TextMeshProUGUI>("JoinCodeText");

            if (hostButton != null) hostButton.onClick.AddListener(() => Forget(net.HostGame()));
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (pingButton != null) pingButton.onClick.AddListener(OnPingClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(net.LeaveGame);

            statusHandler = SetStatus;
            codeHandler = SetJoinCode;
            net.OnStatusChanged += statusHandler;
            net.OnJoinCodeChanged += codeHandler;

            SetStatus(net.Status);
            SetJoinCode(net.LastJoinCode);
        }

        void OnDestroy()
        {
            if (net == null) return;
            if (statusHandler != null) net.OnStatusChanged -= statusHandler;
            if (codeHandler != null) net.OnJoinCodeChanged -= codeHandler;
        }

        void Update()
        {
            bool listening = net.IsListening;
            bool canPing = PresencePlayer.Local != null;
            if (listening == wasListening && canPing == wasPingable) return;
            wasListening = listening;
            wasPingable = canPing;

            SetInteractable(hostButton, !listening);
            SetInteractable(joinButton, !listening);
            SetInteractable(leaveButton, listening);
            SetInteractable(pingButton, canPing);
            if (joinInput != null) joinInput.interactable = !listening;
        }

        void OnJoinClicked()
        {
            Forget(net.JoinGame(joinInput != null ? joinInput.text : ""));
        }

        static void OnPingClicked()
        {
            if (PresencePlayer.Local != null) PresencePlayer.Local.PingFromOwner();
        }

        void SetStatus(string status)
        {
            if (statusText != null) statusText.text = status;
        }

        void SetJoinCode(string code)
        {
            if (joinCodeText == null) return;
            bool hasCode = !string.IsNullOrEmpty(code);
            joinCodeText.gameObject.SetActive(hasCode);
            if (hasCode) joinCodeText.text = $"Code: {code}";
        }

        static void SetInteractable(Selectable selectable, bool value)
        {
            if (selectable != null) selectable.interactable = value;
        }

        /// <summary>
        /// HostGame and JoinGame report every failure through OnStatusChanged instead of
        /// throwing, so discarding the returned Task cannot hide an exception.
        /// </summary>
        static void Forget(Task task)
        {
            _ = task;
        }

        static Button FindButton(string objectName)
        {
            return FindComponent<Button>(objectName);
        }

        /// <summary>
        /// Named lookup keeps the scene free of serialized references. Logging a missing
        /// object as an error makes scene wiring mistakes obvious instead of silent.
        /// </summary>
        static T FindComponent<T>(string objectName) where T : Component
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogError($"[Net] UI object missing: {objectName}");
                return null;
            }
            var component = go.GetComponent<T>();
            if (component == null) Debug.LogError($"[Net] {objectName} has no {typeof(T).Name} component.");
            return component;
        }
    }
}
