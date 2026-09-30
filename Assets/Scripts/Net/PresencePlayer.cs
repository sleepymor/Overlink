using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Overlink.Net
{
    /// <summary>
    /// One networked avatar. The visuals are built locally on every peer by
    /// <see cref="EnsureVisuals"/> — only the name and tint travel over the wire, so the
    /// prefab itself stays a bare NetworkObject.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PresencePlayer : NetworkBehaviour
    {
        const float PopSeconds = 0.35f;
        const float PopAmount = 0.3f;

        /// <summary>The avatar owned by this peer, or null while not connected.</summary>
        public static PresencePlayer Local { get; private set; }

        public readonly NetworkVariable<FixedString32Bytes> DisplayName = new NetworkVariable<FixedString32Bytes>();
        public readonly NetworkVariable<Color> Tint = new NetworkVariable<Color>(Color.white);

        Renderer bodyRenderer;
        Transform bodyTransform;
        TextMeshPro nameplate;
        Coroutine popRoutine;

        NetworkVariable<FixedString32Bytes>.OnValueChangedDelegate nameChanged;
        NetworkVariable<Color>.OnValueChangedDelegate tintChanged;

        void Awake()
        {
            EnsureVisuals();
        }

        void EnsureVisuals()
        {
            if (transform.Find("Body") == null)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
                body.name = "Body";
                body.SetParent(transform, false);
                body.localPosition = new Vector3(0f, 1f, 0f);

                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) Debug.LogWarning("[Net] URP Lit shader not found; avatar will render magenta.");
                else body.GetComponent<Renderer>().material = new Material(lit);
            }

            if (transform.Find("Nameplate") == null)
            {
                var plate = new GameObject("Nameplate").transform;
                plate.SetParent(transform, false);
                plate.localPosition = new Vector3(0f, 2.5f, 0f);
                plate.localScale = Vector3.one * 0.03f;

                var tmp = plate.gameObject.AddComponent<TextMeshPro>();
                tmp.text = "…";
                tmp.fontSize = 14;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 8;
                tmp.fontSizeMax = 20;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.rectTransform.sizeDelta = new Vector2(100f, 14f);
                tmp.color = Color.white;
            }
        }

        void CacheVisuals()
        {
            var body = transform.Find("Body");
            if (body != null)
            {
                bodyTransform = body;
                bodyRenderer = body.GetComponent<Renderer>();
            }
            var plate = transform.Find("Nameplate");
            if (plate != null) nameplate = plate.GetComponent<TextMeshPro>();
        }

        /// <summary>
        /// Server-only, and only valid once the object is spawned: NetworkVariables reject
        /// writes on a NetworkBehaviour that has not spawned yet.
        /// </summary>
        public void ServerSetProfile(string displayName, Color tint)
        {
            if (!IsSpawned) return;
            DisplayName.Value = new FixedString32Bytes(displayName);
            Tint.Value = tint;
        }

        public override void OnNetworkSpawn()
        {
            CacheVisuals();

            nameChanged = (_, __) => ApplyVisuals();
            tintChanged = (_, __) => ApplyVisuals();
            DisplayName.OnValueChanged += nameChanged;
            Tint.OnValueChanged += tintChanged;

            ApplyVisuals();
            if (IsOwner) Local = this;
        }

        public override void OnNetworkDespawn()
        {
            // An avatar is destroyed when its owner leaves, so the handlers have to come
            // back off or they would keep the NetworkBehaviour alive.
            if (nameChanged != null) DisplayName.OnValueChanged -= nameChanged;
            if (tintChanged != null) Tint.OnValueChanged -= tintChanged;
            if (popRoutine != null)
            {
                StopCoroutine(popRoutine);
                popRoutine = null;
            }
            if (Local == this) Local = null;
        }

        void ApplyVisuals()
        {
            if (bodyRenderer != null) bodyRenderer.material.color = Tint.Value;
            if (nameplate != null) nameplate.text = DisplayName.Value.ToString();
        }

        public void PingFromOwner()
        {
            if (IsOwner && IsSpawned) PingServerRpc();
        }

        [Rpc(SendTo.Server)]
        void PingServerRpc()
        {
            PingEveryoneRpc();
        }

        [Rpc(SendTo.Everyone)]
        void PingEveryoneRpc()
        {
            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(Pop());
        }

        IEnumerator Pop()
        {
            if (bodyTransform == null) yield break;

            float elapsed = 0f;
            while (elapsed < PopSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / PopSeconds);
                bodyTransform.localScale = Vector3.one * (1f + PopAmount * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            bodyTransform.localScale = Vector3.one;
        }
    }
}
