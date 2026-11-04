using TMPro;
using UnityEngine;
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
        // Shared with the offline pack reveal screen, which reads this preference in its Awake; the
        // toggle moved here from that screen so all motion options live in one place.
        const string ReducedMotionKey = "Overlink.OfflinePack.ReducedMotion";

        [Header("Controls")]
        [SerializeField] Slider masterVolume;
        [SerializeField] TMP_Text volumeValue;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] Button btnBack;

        [Header("Visual")]
        [SerializeField] Toggle postFxToggle;
        [SerializeField] Toggle reduceShakeToggle;
        [SerializeField] Toggle reduceFlashToggle;
        [SerializeField] Toggle reducedMotionToggle;

        public static bool PostFxEnabled { get; private set; } = true;
        public static bool ReduceShake { get; private set; }
        public static bool ReduceFlash { get; private set; }
        public static bool ReducedMotion { get; private set; }

        void Awake()
        {
            if (!ValidateReferences())
                return;

            ConfigureQualityOptions();
            LoadSavedValues();

            masterVolume.onValueChanged.AddListener(ApplyMasterVolume);
            fullscreenToggle.onValueChanged.AddListener(ApplyFullscreen);
            qualityDropdown.onValueChanged.AddListener(ApplyQuality);

            if (postFxToggle != null)
                postFxToggle.onValueChanged.AddListener(ApplyPostFx);
            if (reduceShakeToggle != null)
                reduceShakeToggle.onValueChanged.AddListener(ApplyReduceShake);
            if (reduceFlashToggle != null)
                reduceFlashToggle.onValueChanged.AddListener(ApplyReduceFlash);
            if (reducedMotionToggle != null)
                reducedMotionToggle.onValueChanged.AddListener(ApplyReducedMotion);

            btnBack.onClick.AddListener(SceneLoader.LoadMainMenu);
        }

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
            if (btnBack != null)
                btnBack.onClick.RemoveListener(SceneLoader.LoadMainMenu);
        }

        void ConfigureQualityOptions()
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
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

        void ApplyPostFx(bool enabled)
        {
            PostFxEnabled = enabled;
            PlayerPrefs.SetInt(PostFxKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            var volumeGo = GameObject.Find("GlobalVolume");
            if (volumeGo != null) volumeGo.SetActive(enabled);
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
