// Assets/Scripts/UI/PlaceholderSceneController.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    [DisallowMultipleComponent]
    public sealed class PlaceholderSceneController : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Button   btnBack;

        void Awake()
        {
            if (label == null || btnBack == null)
            {
                Debug.LogError($"[{nameof(PlaceholderSceneController)}] Label and Back button must be wired.", this);
                return;
            }

            label.text = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().name + "\n<size=60%>(placeholder)</size>";
            btnBack.onClick.AddListener(SceneLoader.LoadMainMenu);
        }

        void OnValidate()
        {
            if (label == null)
                Debug.LogError($"[{nameof(PlaceholderSceneController)}] Assign label in the Inspector.", this);
            if (btnBack == null)
                Debug.LogError($"[{nameof(PlaceholderSceneController)}] Assign btnBack in the Inspector.", this);
        }
    }
}
