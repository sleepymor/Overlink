// Assets/Scripts/UI/SceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.UI
{
    public static class SceneLoader
    {
        public static void Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not enabled or registered in Build Settings.");
                return;
            }

            SceneManager.LoadScene(sceneName);
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
}
