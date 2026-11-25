using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Owns the player-facing settings screen. Values are applied immediately and
    /// persisted locally so the screen remains useful between launches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsController : MonoBehaviour
    {
        const string MasterVolumeKey = "Overlink.Settings.MasterVolume";
        const string FullscreenKey = "Overlink.Settings.Fullscreen";
        const string QualityKey = "Overlink.Settings.Quality";
        const string PostFxKey = "Overlink.Settings.PostFX";
        const string ReduceShakeKey = "Overlink.Settings.ReduceShake";
        const string ReduceFlashKey = "Overlink.Settings.ReduceFlash";
        // Shared with the pack reveal screen, which reads this preference in its Awake; the
        // toggle moved here from that screen so all motion options live in one place.
        const string ReducedMotionKey = "Overlink.OfflinePack.ReducedMotion";
        const string ResolutionKey = "Overlink.Settings.Resolution";
        const string NativeResolutionKey = "Overlink.Settings.NativeResolution";
        const string VSyncKey = "Overlink.Settings.VSync";
        const string AntiAliasingKey = "Overlink.Settings.AntiAliasing";
        const string RenderScaleKey = "Overlink.Settings.RenderScale";
        const string ShakeIntensityKey = "Overlink.Settings.ShakeIntensity";
        const string FlashIntensityKey = "Overlink.Settings.FlashIntensity";

        [Header("Controls")]
        [SerializeField] Slider masterVolume;
        [SerializeField] TMP_Text volumeValue;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] Button btnBack;
        [SerializeField] Button btnReplayTutorial;

        [Header("Display")]
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle nativeResolutionToggle;
        [SerializeField] Toggle vSyncToggle;
        [SerializeField] TMP_Dropdown antiAliasingDropdown;
        [SerializeField] Slider renderScaleSlider;
        [SerializeField] TMP_Text renderScaleValue;

        [Header("Visual")]
        [SerializeField] Toggle postFxToggle;
        [SerializeField] Toggle reduceShakeToggle;
        [SerializeField] Toggle reduceFlashToggle;
        [SerializeField] Toggle reducedMotionToggle;
        [SerializeField] Slider shakeIntensitySlider;
        [SerializeField] TMP_Text shakeIntensityValue;
        [SerializeField] Slider flashIntensitySlider;
        [SerializeField] TMP_Text flashIntensityValue;

        public static bool PostFxEnabled { get; private set; } = true;
        public static bool ReduceShake { get; private set; }
        public static bool ReduceFlash { get; private set; }
        public static bool ReducedMotion { get; private set; }

        /// <summary>
        /// Applies every saved preference before the first scene loads, so the game starts with the
        /// player's settings. Before this, the values only took effect once the Settings screen opened.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedSettingsAtStartup()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));

            if (PlayerPrefs.HasKey(FullscreenKey))
                Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, 0) != 0;

            if (PlayerPrefs.HasKey(QualityKey) && QualitySettings.names.Length > 0)
            {
                int level = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 0), 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, true);
            }

            PostFxEnabled = PlayerPrefs.GetInt(PostFxKey, 1) != 0;
            ReduceShake = PlayerPrefs.GetInt(ReduceShakeKey, 0) != 0;
            ReduceFlash = PlayerPrefs.GetInt(ReduceFlashKey, 0) != 0;
            ReducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;

            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey, 1) != 0 ? 1 : 0;
            ApplyRenderPipelineSettings(
                PlayerPrefs.GetInt(AntiAliasingKey, 0),
                PlayerPrefs.GetFloat(RenderScaleKey, 1f));
            ApplySavedResolution();

            // Post FX lives on a per-scene volume, so it has to be applied to each scene as it loads.
            SceneManager.sceneLoaded -= OnSceneLoadedApplyPostFx;
            SceneManager.sceneLoaded += OnSceneLoadedApplyPostFx;
        }

        static void OnSceneLoadedApplyPostFx(Scene scene, LoadSceneMode mode)
        {
            SetGlobalVolumeActive(PostFxEnabled);
        }

        /// <summary>
        /// Anti-aliasing and render scale live on the URP asset, not on QualitySettings. The URP
        /// types are reached by reflection, as SignalBayController does, so this file needs no
        /// assembly reference to the render pipeline package.
        /// </summary>
        static void ApplyRenderPipelineSettings(int antiAliasingIndex, float renderScale)
        {
            object asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                return;

            var type = asset.GetType();
            int[] msaaLevels = { 0, 2, 4, 8 };
            int msaa = msaaLevels[Mathf.Clamp(antiAliasingIndex, 0, msaaLevels.Length - 1)];
            var msaaField = type.GetField("m_MSAA", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (msaaField != null)
                msaaField.SetValue(asset, msaa == 0 ? 1 : msaa);

            var scaleField = type.GetField("m_RenderScale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (scaleField != null)
                scaleField.SetValue(asset, Mathf.Clamp(renderScale, 0.5f, 1f));
        }

        static void ApplySavedResolution()
        {
            if (!PlayerPrefs.HasKey(ResolutionKey))
                return;

            int width = PlayerPrefs.GetInt(ResolutionKey + ".W", Screen.width);
            int height = PlayerPrefs.GetInt(ResolutionKey + ".H", Screen.height);
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            Screen.SetResolution(width, height, fullscreen);
        }

        static void SetGlobalVolumeActive(bool enabled)
        {
            // GameObject.Find skips inactive objects, so a volume switched off could never be switched
            // back on. Scanning loaded transforms finds it in either state.
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name == "GlobalVolume" && candidate.gameObject.scene.isLoaded)
                    candidate.gameObject.SetActive(enabled);
            }
        }

        void Awake()
        {
            if (!ValidateReferences())
                return;

            ConfigureQualityOptions();
            ConfigureDisplayOptions();
            LoadSavedValues();

            masterVolume.onValueChanged.AddListener(ApplyMasterVolume);
            fullscreenToggle.onValueChanged.AddListener(ApplyFullscreen);
            qualityDropdown.onValueChanged.AddListener(ApplyQuality);

            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
            if (nativeResolutionToggle != null)
                nativeResolutionToggle.onValueChanged.AddListener(ApplyNativeResolution);
            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(ApplyVSync);
            if (antiAliasingDropdown != null)
                antiAliasingDropdown.onValueChanged.AddListener(ApplyAntiAliasing);
            if (renderScaleSlider != null)
                renderScaleSlider.onValueChanged.AddListener(ApplyRenderScale);
            if (shakeIntensitySlider != null)
                shakeIntensitySlider.onValueChanged.AddListener(ApplyShakeIntensity);
            if (flashIntensitySlider != null)
                flashIntensitySlider.onValueChanged.AddListener(ApplyFlashIntensity);

            if (postFxToggle != null)
                postFxToggle.onValueChanged.AddListener(ApplyPostFx);
            if (reduceShakeToggle != null)
                reduceShakeToggle.onValueChanged.AddListener(ApplyReduceShake);
            if (reduceFlashToggle != null)
                reduceFlashToggle.onValueChanged.AddListener(ApplyReduceFlash);
            if (reducedMotionToggle != null)
                reducedMotionToggle.onValueChanged.AddListener(ApplyReducedMotion);

            btnBack.onClick.AddListener(SceneLoader.LoadMainMenu);
            if (btnReplayTutorial != null)
                btnReplayTutorial.onClick.AddListener(OnReplayTutorialClicked);
        }

        // Opens the walkthrough without touching the saved flag. Finishing it again still records completion.
        void OnReplayTutorialClicked() => SceneLoader.Load(SceneIndex.Tutorial);

        void OnDestroy()
        {
            if (masterVolume != null)
                masterVolume.onValueChanged.RemoveListener(ApplyMasterVolume);
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.RemoveListener(ApplyFullscreen);
            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.RemoveListener(ApplyQuality);
            if (postFxToggle != null)
                postFxToggle.onValueChanged.RemoveListener(ApplyPostFx);
            if (reduceShakeToggle != null)
                reduceShakeToggle.onValueChanged.RemoveListener(ApplyReduceShake);
            if (reduceFlashToggle != null)
                reduceFlashToggle.onValueChanged.RemoveListener(ApplyReduceFlash);
            if (reducedMotionToggle != null)
                reducedMotionToggle.onValueChanged.RemoveListener(ApplyReducedMotion);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveListener(ApplyResolution);
            if (nativeResolutionToggle != null)
                nativeResolutionToggle.onValueChanged.RemoveListener(ApplyNativeResolution);
            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.RemoveListener(ApplyVSync);
            if (antiAliasingDropdown != null)
                antiAliasingDropdown.onValueChanged.RemoveListener(ApplyAntiAliasing);
            if (renderScaleSlider != null)
                renderScaleSlider.onValueChanged.RemoveListener(ApplyRenderScale);
            if (shakeIntensitySlider != null)
                shakeIntensitySlider.onValueChanged.RemoveListener(ApplyShakeIntensity);
            if (flashIntensitySlider != null)
                flashIntensitySlider.onValueChanged.RemoveListener(ApplyFlashIntensity);
            if (btnBack != null)
                btnBack.onClick.RemoveListener(SceneLoader.LoadMainMenu);
            if (btnReplayTutorial != null)
                btnReplayTutorial.onClick.RemoveListener(OnReplayTutorialClicked);
        }

        void ConfigureQualityOptions()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
        }

        void ConfigureDisplayOptions()
        {
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var labels = new System.Collections.Generic.List<string>();
                foreach (Resolution res in Screen.resolutions)
                    labels.Add($"{res.width} x {res.height}");
                resolutionDropdown.AddOptions(labels);
            }

            if (antiAliasingDropdown != null)
            {
                antiAliasingDropdown.ClearOptions();
                antiAliasingDropdown.AddOptions(new System.Collections.Generic.List<string> { "OFF", "2X", "4X", "8X" });
            }
        }

        static int FindResolutionIndex(int width, int height)
        {
            Resolution[] options = Screen.resolutions;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].width == width && options[i].height == height)
                    return i;
            }
            return Mathf.Max(0, options.Length - 1);
        }

        void RefreshIntensityLabels()
        {
            if (renderScaleValue != null && renderScaleSlider != null)
                renderScaleValue.text = $"{Mathf.RoundToInt(renderScaleSlider.value * 100f)}%";
            if (shakeIntensityValue != null && shakeIntensitySlider != null)
                shakeIntensityValue.text = $"{Mathf.RoundToInt(shakeIntensitySlider.value * 100f)}%";
            if (flashIntensityValue != null && flashIntensitySlider != null)
                flashIntensityValue.text = $"{Mathf.RoundToInt(flashIntensitySlider.value * 100f)}%";
        }

        void LoadSavedValues()
        {
            float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            int savedQuality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()),
                0, Mathf.Max(0, qualityDropdown.options.Count - 1));

            masterVolume.SetValueWithoutNotify(savedVolume);
            fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
            qualityDropdown.SetValueWithoutNotify(savedQuality);

            ApplyMasterVolume(savedVolume);
            ApplyFullscreen(savedFullscreen);
            ApplyQuality(savedQuality);

            bool savedPostFx = PlayerPrefs.GetInt(PostFxKey, 1) != 0;
            bool savedReduceShake = PlayerPrefs.GetInt(ReduceShakeKey, 0) != 0;
            bool savedReduceFlash = PlayerPrefs.GetInt(ReduceFlashKey, 0) != 0;
            bool savedReducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;

            if (postFxToggle != null)
                postFxToggle.SetIsOnWithoutNotify(savedPostFx);
            if (reduceShakeToggle != null)
                reduceShakeToggle.SetIsOnWithoutNotify(savedReduceShake);
            if (reduceFlashToggle != null)
                reduceFlashToggle.SetIsOnWithoutNotify(savedReduceFlash);
            if (reducedMotionToggle != null)
                reducedMotionToggle.SetIsOnWithoutNotify(savedReducedMotion);

            ApplyPostFx(savedPostFx);
            ApplyReduceShake(savedReduceShake);
            ApplyReduceFlash(savedReduceFlash);
            ApplyReducedMotion(savedReducedMotion);

            LoadDisplaySavedValues();
            LoadEffectSavedValues();
        }

        void LoadDisplaySavedValues()
        {
            int currentWidth = Screen.width;
            int currentHeight = Screen.height;
            int savedWidth = PlayerPrefs.GetInt(ResolutionKey + ".W", currentWidth);
            int savedHeight = PlayerPrefs.GetInt(ResolutionKey + ".H", currentHeight);

            if (resolutionDropdown != null)
            {
                int index = FindResolutionIndex(savedWidth, savedHeight);
                resolutionDropdown.SetValueWithoutNotify(index);
                resolutionDropdown.RefreshShownValue();
            }

            if (nativeResolutionToggle != null)
                nativeResolutionToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(NativeResolutionKey, 0) != 0);

            bool vSync = PlayerPrefs.GetInt(VSyncKey, 1) != 0;
            if (vSyncToggle != null)
                vSyncToggle.SetIsOnWithoutNotify(vSync);
            ApplyVSync(vSync);

            int antiAliasing = PlayerPrefs.GetInt(AntiAliasingKey, 0);
            if (antiAliasingDropdown != null)
            {
                antiAliasingDropdown.SetValueWithoutNotify(antiAliasing);
                antiAliasingDropdown.RefreshShownValue();
            }
            ApplyAntiAliasing(antiAliasing);

            float renderScale = PlayerPrefs.GetFloat(RenderScaleKey, 1f);
            if (renderScaleSlider != null)
                renderScaleSlider.SetValueWithoutNotify(renderScale);
            ApplyRenderScale(renderScale);
        }

        void LoadEffectSavedValues()
        {
            float shake = PlayerPrefs.GetFloat(ShakeIntensityKey, 1f);
            float flash = PlayerPrefs.GetFloat(FlashIntensityKey, 1f);

            if (shakeIntensitySlider != null)
                shakeIntensitySlider.SetValueWithoutNotify(shake);
            if (flashIntensitySlider != null)
                flashIntensitySlider.SetValueWithoutNotify(flash);

            RefreshIntensityLabels();
        }

        void ApplyMasterVolume(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            if (volumeValue != null)
                volumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
            PlayerPrefs.Save();
        }

        void ApplyFullscreen(bool enabled)
        {
            Screen.fullScreen = enabled;
            PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyQuality(int index)
        {
            if (QualitySettings.names.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(index, true);
            PlayerPrefs.SetInt(QualityKey, index);
            PlayerPrefs.Save();
        }

        void ApplyResolution(int index)
        {
            Resolution[] options = Screen.resolutions;
            if (options.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, options.Length - 1);
            if (nativeResolutionToggle != null && nativeResolutionToggle.isOn)
            {
                nativeResolutionToggle.SetIsOnWithoutNotify(false);
                PlayerPrefs.SetInt(NativeResolutionKey, 0);
            }
            SaveResolution(options[index].width, options[index].height);
        }

        void ApplyNativeResolution(bool useNative)
        {
            PlayerPrefs.SetInt(NativeResolutionKey, useNative ? 1 : 0);
            PlayerPrefs.Save();

            if (useNative)
            {
                Resolution native = Screen.currentResolution;
                if (resolutionDropdown != null)
                {
                    resolutionDropdown.SetValueWithoutNotify(FindResolutionIndex(native.width, native.height));
                    resolutionDropdown.RefreshShownValue();
                }
                SaveResolution(native.width, native.height);
            }
            else if (resolutionDropdown != null)
            {
                ApplyResolution(resolutionDropdown.value);
            }
        }

        void SaveResolution(int width, int height)
        {
            PlayerPrefs.SetInt(ResolutionKey + ".W", width);
            PlayerPrefs.SetInt(ResolutionKey + ".H", height);
            PlayerPrefs.SetInt(ResolutionKey, 1);
            PlayerPrefs.Save();

            bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;
            Screen.SetResolution(width, height, fullscreen);
        }

        void ApplyVSync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyAntiAliasing(int index)
        {
            PlayerPrefs.SetInt(AntiAliasingKey, index);
            PlayerPrefs.Save();
            ApplyRenderPipelineSettings(index, PlayerPrefs.GetFloat(RenderScaleKey, 1f));
        }

        void ApplyRenderScale(float value)
        {
            value = Mathf.Clamp(value, 0.5f, 1f);
            PlayerPrefs.SetFloat(RenderScaleKey, value);
            PlayerPrefs.Save();
            ApplyRenderPipelineSettings(PlayerPrefs.GetInt(AntiAliasingKey, 0), value);
            RefreshIntensityLabels();
        }

        void ApplyShakeIntensity(float value)
        {
            PlayerPrefs.SetFloat(ShakeIntensityKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            RefreshIntensityLabels();
        }

        void ApplyFlashIntensity(float value)
        {
            PlayerPrefs.SetFloat(FlashIntensityKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            RefreshIntensityLabels();
        }

        void ApplyPostFx(bool enabled)
        {
            PostFxEnabled = enabled;
            PlayerPrefs.SetInt(PostFxKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            SetGlobalVolumeActive(enabled);
        }

        void ApplyReduceShake(bool reduced)
        {
            ReduceShake = reduced;
            PlayerPrefs.SetInt(ReduceShakeKey, reduced ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyReduceFlash(bool reduced)
        {
            ReduceFlash = reduced;
            PlayerPrefs.SetInt(ReduceFlashKey, reduced ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyReducedMotion(bool reduced)
        {
            ReducedMotion = reduced;
            PlayerPrefs.SetInt(ReducedMotionKey, reduced ? 1 : 0);
            PlayerPrefs.Save();
        }

        bool ValidateReferences()
        {
            bool valid = true;
            valid &= Check(masterVolume, nameof(masterVolume));
            valid &= Check(volumeValue, nameof(volumeValue));
            valid &= Check(fullscreenToggle, nameof(fullscreenToggle));
            valid &= Check(qualityDropdown, nameof(qualityDropdown));
            valid &= Check(btnBack, nameof(btnBack));
            valid &= Check(postFxToggle, nameof(postFxToggle));
            valid &= Check(reduceShakeToggle, nameof(reduceShakeToggle));
            valid &= Check(reduceFlashToggle, nameof(reduceFlashToggle));
            valid &= Check(reducedMotionToggle, nameof(reducedMotionToggle));
            return valid;
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            ValidateReferences();
        }

        static bool Check(Object value, string fieldName)
        {
            if (value != null)
                return true;

            Debug.LogError($"[{nameof(SettingsController)}] Assign {fieldName} in the Inspector.");
            return false;
        }
    }


}
