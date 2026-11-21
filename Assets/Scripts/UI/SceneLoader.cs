// Assets/Scripts/UI/SceneLoader.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.UI
{
    public static class SceneLoader
    {
        const float ActivationBeat = 0.30f;

        static SceneTransitionRunner transition;
        /// <summary>
        /// Where a screen's Back control should go when it was opened from another screen rather than
        /// from the menu. Static state survives a scene load (there is no domain reload between scenes),
        /// so the binder can send the player to the deck builder and get them back.
        /// </summary>
        public static string ReturnScene { get; private set; }

        public static void Load(string sceneName)
        {
            ReturnScene = null;
            LoadInternal(sceneName);
        }

        /// <summary>Loads a scene and remembers which screen its Back control returns to.</summary>
        public static void LoadFrom(string sceneName, string returnScene)
        {
            ReturnScene = returnScene;
            LoadInternal(sceneName);
        }

        /// <summary>Backs out to the launching screen, or to the main menu when nothing launched us.</summary>
        public static void Return() => Return(SceneIndex.MainMenu);

        /// <summary>
        /// Backs out to the launching screen, or to <paramref name="fallback"/> when nothing launched us.
        /// Screens that own a parent (the deck builder belongs to the binder) pass their parent, so Back
        /// is right even when the scene is played directly from the Editor instead of through the menu.
        /// </summary>
        public static void Return(string fallback)
        {
            string target = ReturnScene;
            ReturnScene = null;
            LoadInternal(string.IsNullOrEmpty(target) ? fallback : target);
        }

        static void LoadInternal(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not enabled or registered in Build Settings.");
                return;
            }

            if (transition == null)
            {
                GameObject host = new GameObject("Scene Transition");
                Object.DontDestroyOnLoad(host);
                transition = host.AddComponent<SceneTransitionRunner>();
            }

            // The short beat keeps the outgoing button's activation VFX visible before the fade
            // begins. This also handles pointer-event ordering: Button.onClick may run before or
            // after UiFxButton receives its pointer callback, but the old scene remains on screen.
            transition.Begin(sceneName, ActivationBeat);
        }

        public static void LoadMainMenu() => Load(SceneIndex.MainMenu);

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// Persistent screen-space fade used for all regular scene navigation. It lives outside loaded
    /// scenes so the outgoing scene can fade to the same dark tactical blue that the next scene fades
    /// up from, instead of hard-cutting between two cameras/canvases.
    /// </summary>
    sealed class SceneTransitionRunner : MonoBehaviour
    {
        static readonly Color FadeColor = new Color(0.008f, 0.024f, 0.043f, 1f);
        const float FadeOutDuration = 0.24f;
        const float FadeInDuration = 0.34f;

        CanvasGroup group;
        bool busy;

        void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject veil = new GameObject("Fade Veil", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)veil.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = veil.GetComponent<Image>();
            image.color = FadeColor;
            image.raycastTarget = false;

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Begin(string sceneName, float activationBeat)
        {
            if (busy) return;
            Debug.Log($"[SceneLoader] Begin '{sceneName}' at {Time.realtimeSinceStartup:F3}s");
            StartCoroutine(Run(sceneName, activationBeat));
        }

        IEnumerator Run(string sceneName, float activationBeat)
        {
            busy = true;
            yield return new WaitForSecondsRealtime(activationBeat);
            yield return Fade(0f, 1f, FadeOutDuration);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log($"[SceneLoader] LoadScene('{sceneName}') at {Time.realtimeSinceStartup:F3}s");
            SceneManager.LoadScene(sceneName);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Debug.Log($"[SceneLoader] SceneLoaded '{scene.name}' at {Time.realtimeSinceStartup:F3}s");
            StartCoroutine(FadeIntoScene());
        }

        IEnumerator FadeIntoScene()
        {
            // Let the new scene finish its first layout/presentation pass under the veil.
            yield return null;
            yield return Fade(1f, 0f, FadeInDuration);
            Debug.Log($"[SceneLoader] Scene ready after fade at {Time.realtimeSinceStartup:F3}s");
            busy = false;
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
