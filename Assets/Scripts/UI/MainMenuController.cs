// Assets/Scripts/UI/MainMenuController.cs
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button btnPlay;
        [SerializeField] Button btnDeckbuilder;
        [SerializeField] Button btnCollection;
        [SerializeField] Button btnSettings;
        [SerializeField] Button btnNetDemo;
        [SerializeField] Button btnQuit;

        void Awake()
        {
            AddListener(btnPlay,        () => SceneLoader.Load(SceneIndex.Gameplay));
            AddListener(btnDeckbuilder, () => SceneLoader.Load(SceneIndex.Deckbuilder));
            AddListener(btnCollection,  () => SceneLoader.Load(SceneIndex.Collection));
            AddListener(btnSettings,    () => SceneLoader.Load(SceneIndex.Settings));
            AddListener(btnNetDemo,     () => SceneLoader.Load(SceneIndex.NetDemo));
            AddListener(btnQuit,        SceneLoader.Quit);
        }

        void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(MainMenuController)}] A menu button is not wired.", this);
                return;
            }

            button.onClick.AddListener(action);
        }

        void OnValidate()
        {
            ValidateButton(btnPlay, nameof(btnPlay));
            ValidateButton(btnDeckbuilder, nameof(btnDeckbuilder));
            ValidateButton(btnCollection, nameof(btnCollection));
            ValidateButton(btnSettings, nameof(btnSettings));
            ValidateButton(btnNetDemo, nameof(btnNetDemo));
            ValidateButton(btnQuit, nameof(btnQuit));
        }

        void ValidateButton(Button button, string fieldName)
        {
            if (button == null)
                Debug.LogError($"[{nameof(MainMenuController)}] Assign {fieldName} in the Inspector.", this);
        }
    }
}
