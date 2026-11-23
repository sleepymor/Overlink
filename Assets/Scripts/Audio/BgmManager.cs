// Assets/Scripts/Audio/BgmManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Overlink.UI;

namespace Overlink.Audio
{
    /// <summary>
    /// Persistent background-music player.
    /// <para>
    /// Lives on a <c>DontDestroyOnLoad</c> object so it survives every scene change. Each track
    /// plays with <see cref="AudioSource.loop"/> so it never stops mid-scene, and scene changes
    /// crossfade between two <see cref="AudioSource"/>s so the music never cuts, gaps, or restarts
    /// from the top when you return to a scene.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BgmManager : MonoBehaviour
    {
        static BgmManager _instance;
        /// <summary>Singleton instance, created on first use.</summary>
        public static BgmManager Instance => _instance;

        [Header("Crossfade")]
        [Tooltip("Seconds to fade from the current track to the next one on a scene change.")]
        [SerializeField, Min(0f)] float crossfadeSeconds = 0.7f;

        [Header("Volume")]
        [Tooltip("Music volume multiplier, applied on top of the master AudioListener volume.")]
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;

        const string VolumeKey = "Overlink.Bgm.Volume";

        // ── Tracks ──────────────────────────────────────────────────────────────
        // Loaded from Resources/Audio/BGM at runtime so no Inspector assignment is required.
        const string ResourceRoot = "Audio/BGM";
        const string ClipMainMenu    = "Twilight_Triangle_MainMenu";
        const string ClipGameplay    = "haze_Battle";
        const string ClipCollection  = "Dusk_Colors_Collection";
        const string ClipSignalBay   = "Residual_Light_SignalBay";

        AudioClip _clipMainMenu;
        AudioClip _clipGameplay;
        AudioClip _clipCollection;
        AudioClip _clipSignalBay;

        // ── Playback ────────────────────────────────────────────────────────────
        readonly AudioSource[] _sources = new AudioSource[2];
        int _active;                 // index of the source currently audible
        string _currentKey;          // resource key of the track that is / was playing
        Coroutine _fade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateInstance()
        {
            if (_instance == null)
            {
                new GameObject("BgmManager").AddComponent<BgmManager>();
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Load the player's saved BGM volume before sources are created.
            musicVolume = PlayerPrefs.GetFloat(VolumeKey, musicVolume);

            // Play the track for the scene that just loaded (covers the first scene, which
            // never fires sceneLoaded).
            Play(TrackFor(SceneManager.GetActiveScene().name));

            for (int i = 0; i < _sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.loop = true;               // each track loops forever
                source.playOnAwake = false;
                source.spatialBlend = 0f;         // 2D: no panning / attenuation
                source.volume = musicVolume;
                _sources[i] = source;
            }

            // Load all BGM clips once so scene changes never stall on disk reads.
            _clipMainMenu    = LoadClip(ClipMainMenu);
            _clipGameplay    = LoadClip(ClipGameplay);
            _clipCollection  = LoadClip(ClipCollection);
            _clipSignalBay   = LoadClip(ClipSignalBay);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Play(TrackFor(scene.name));
        }

        /// <summary>Pick the track key for a scene. null = keep playing the current track.</summary>
        static string TrackFor(string sceneName)
        {
            return sceneName switch
            {
                SceneIndex.MainMenu    => "MainMenu",
                SceneIndex.Gameplay    => "Gameplay",
                SceneIndex.Collection  => "Collection",
                SceneIndex.Deckbuilder => "Collection",
                SceneIndex.Gacha       => "SignalBay",
                SceneIndex.Settings    => "MainMenu", // settings screen plays menu music
                SceneIndex.Auth        => null,  // no music during auth
                _                      => null,
            };
        }

        /// <summary>
        /// Play (or crossfade to) the track for <paramref name="key"/>. Passing null keeps the
        /// current track playing (used by overlay scenes such as Settings).
        /// </summary>
        public void Play(string key)
        {
            if (key == _currentKey) return;
            _currentKey = key;

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossfadeTo(ClipFor(key), crossfadeSeconds));
        }

        /// <summary>Immediately snap to a track with no crossfade.</summary>
        public void PlayImmediate(string key)
        {
            if (key == _currentKey) return;
            _currentKey = key;

            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            var incoming = _sources[_active];
            incoming.Stop();
            incoming.clip = ClipFor(key);
            incoming.volume = musicVolume;
            if (incoming.clip != null) incoming.Play();
        }

        AudioClip ClipFor(string key)
        {
            return key switch
            {
                "MainMenu"  => _clipMainMenu,
                "Gameplay"  => _clipGameplay,
                "Collection" => _clipCollection,
                "SignalBay" => _clipSignalBay,
                null        => null,
                _           => null,
            };
        }

        AudioClip LoadClip(string nameWithoutExtension)
        {
            var path = $"{ResourceRoot}/{nameWithoutExtension}";
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning($"[BgmManager] Could not load BGM clip '{path}'. Make sure the file lives under Assets/Resources/Audio/BGM/.");
            return clip;
        }

        IEnumerator CrossfadeTo(AudioClip nextClip, float duration)
        {
            var outgoing = _sources[_active];
            var incoming = _sources[1 - _active];

            float outStartVol = outgoing.volume;

            incoming.clip = nextClip;
            incoming.volume = 0f;
            if (nextClip != null) incoming.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out so the fade-in starts soft and settles cleanly.
                float smooth = 1f - Mathf.Pow(1f - t, 3f);
                incoming.volume = musicVolume * smooth;
                outgoing.volume = outStartVol * (1f - smooth);
                yield return null;
            }

            incoming.volume = musicVolume;
            outgoing.volume = 0f;
            outgoing.Stop();
            if (nextClip == null) incoming.Stop();

            _active = 1 - _active;
            _fade = null;
        }

        /// <summary>Set the music volume multiplier (0 = silent, 1 = full).</summary>
        public void SetVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(VolumeKey, musicVolume);
            PlayerPrefs.Save();
            foreach (var source in _sources)
                source.volume = musicVolume;
        }
    }
}
