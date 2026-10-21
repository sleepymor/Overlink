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

        [Header("Controls")]
        [SerializeField] Slider masterVolume;
        [SerializeField] TMP_Text volumeValue;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] Button btnBack;

        void Awake()
        {
            if (!ValidateReferences())
                return;

            ConfigureQualityOptions();
            LoadSavedValues();

            masterVolume.onValueChanged.AddListener(ApplyMasterVolume);
            fullscreenToggle.onValueChanged.AddListener(ApplyFullscreen);
            qualityDropdown.onValueChanged.AddListener(ApplyQuality);
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

        bool ValidateReferences()
        {
            bool valid = true;
            valid &= Check(masterVolume, nameof(masterVolume));
            valid &= Check(volumeValue, nameof(volumeValue));
            valid &= Check(fullscreenToggle, nameof(fullscreenToggle));
            valid &= Check(qualityDropdown, nameof(qualityDropdown));
            valid &= Check(btnBack, nameof(btnBack));
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
