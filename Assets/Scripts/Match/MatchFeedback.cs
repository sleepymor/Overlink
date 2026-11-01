using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.Match
{
    /// <summary>
    /// View-only feedback director for the match. It never changes MatchState or time scale:
    /// it layers a short camera trauma response and a screen flash over already-resolved events.
    ///
    /// The director is intentionally small and event-shaped. BattlefieldPresenter calls the public
    /// methods at the same seams it already uses for card and battle feedback, which keeps this pass
    /// additive and makes the intensity tiers easy to tune without burying simulation code.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class MatchFeedback : MonoBehaviour
    {
        const string ReduceShakeKey = "Overlink.Settings.ReduceShake";
        const string ReduceFlashKey = "Overlink.Settings.ReduceFlash";
        const string PostFxKey = "Overlink.Settings.PostFX";

        static MatchFeedback instance;

        [Header("Camera trauma")]
        [SerializeField] Camera targetCamera;
        [SerializeField] float traumaDecay = 2.8f;
        [SerializeField] Vector2 maxOffset = new Vector2(0.075f, 0.055f);
        [SerializeField] float maxRoll = 0.012f;

        [Header("Impact flash")]
        [SerializeField] Color flashColor = new Color(0.55f, 0.86f, 1f, 1f);
        [SerializeField] float flashFade = 7.5f;

        Canvas flashCanvas;
        Image flashImage;
        Transform cameraTransform;
        float trauma;
        float flash;
        float noiseTime;

        public static MatchFeedback Instance => instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ApplyPostFxPreference();
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
            {
                cameraTransform = targetCamera.transform;
                // The camera controller sets the base pose each LateUpdate. Without it, applying
                // offsets here would accumulate, so leave the camera untouched.
                if (targetCamera.GetComponent<BattlefieldCameraController>() == null)
                    cameraTransform = null;
            }

            BuildFlashOverlay();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            trauma = Mathf.MoveTowards(trauma, 0f, traumaDecay * dt);
            flash = Mathf.MoveTowards(flash, 0f, flashFade * dt);
            noiseTime += dt * 28f;

            if (cameraTransform != null)
            {
                float shake = trauma * trauma;
                Vector3 offset = new Vector3(
                    Mathf.Sin(noiseTime * 1.71f) * maxOffset.x * shake,
                    Mathf.Sin(noiseTime * 2.43f) * maxOffset.y * shake,
                    0f);
                float roll = Mathf.Sin(noiseTime * 1.13f) * maxRoll * shake;
                cameraTransform.localPosition += offset;
                cameraTransform.localRotation *= Quaternion.Euler(0f, 0f, roll * Mathf.Rad2Deg);
            }

            if (flashImage != null)
            {
                Color color = flashColor;
                color.a = flash * (ReduceFlashEnabled() ? 0.35f : 1f);
                flashImage.color = color;
            }
        }

        public void PlayDeploy()
        {
            AddTrauma(0.16f);
        }

        public void PlayAttack()
        {
            AddTrauma(0.30f);
            AddFlash(0.18f);
        }

        public void PlayBlock()
        {
            AddTrauma(0.12f);
            AddFlash(0.08f);
        }

        public void PlayDamage()
        {
            AddTrauma(0.25f);
            AddFlash(0.14f);
        }

        public void PlayDestroyed()
        {
            AddTrauma(0.48f);
            AddFlash(0.28f);
        }

        public void PlayCoreImpact()
        {
            AddTrauma(0.62f);
            AddFlash(0.38f);
        }

        /// <summary>Short, non-interactive board-space ring; it never changes a card or its slot.</summary>
        public void PlaySlotBurst(Transform slot, Vector2 size, Color color, float duration = 0.35f)
        {
            if (slot == null || !isActiveAndEnabled) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            StartCoroutine(SlotBurst(slot, size, color, duration, shader));
        }

        IEnumerator SlotBurst(Transform slot, Vector2 size, Color color, float duration, Shader shader)
        {
            var effect = new GameObject("Slot feedback");
            effect.transform.SetParent(slot, false);
            effect.transform.localPosition = new Vector3(0f, 0f, -0.12f);
            var line = effect.AddComponent<LineRenderer>();
            var material = new Material(shader);
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.widthMultiplier = 0.035f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * size.x * 0.5f,
                    Mathf.Sin(angle) * size.y * 0.5f, 0f));
            }

            float elapsed = 0f;
            while (elapsed < duration && effect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                effect.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.3f, 1f - (1f - t) * (1f - t));
                Color faded = color;
                faded.a *= (1f - t) * (ReduceFlashEnabled() ? 0.4f : 1f);
                line.startColor = faded;
                line.endColor = faded;
                yield return null;
            }
            if (effect != null) Destroy(effect);
            Destroy(material);
        }

        void AddTrauma(float amount)
        {
            if (ReduceShakeEnabled())
                amount *= 0.35f;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        void AddFlash(float amount)
        {
            flash = Mathf.Clamp01(Mathf.Max(flash, amount));
        }

        bool ReduceShakeEnabled() => PlayerPrefs.GetInt(ReduceShakeKey, 0) != 0;
        bool ReduceFlashEnabled() => PlayerPrefs.GetInt(ReduceFlashKey, 0) != 0;

        static void ApplyPostFxPreference()
        {
            GameObject volume = GameObject.Find("GlobalVolume");
            if (volume != null)
                volume.SetActive(PlayerPrefs.GetInt(PostFxKey, 1) != 0);
        }

        void BuildFlashOverlay()
        {
            GameObject canvasObject = new GameObject("Match Feedback Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            flashCanvas = canvasObject.AddComponent<Canvas>();
            flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            flashCanvas.sortingOrder = 250;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>().enabled = false;
            GameObject imageObject = new GameObject("Impact Flash", typeof(RectTransform));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            flashImage = imageObject.AddComponent<Image>();
            flashImage.raycastTarget = false;
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }
    }
}
