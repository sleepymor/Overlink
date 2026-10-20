// Assets/Editor/SceneRegistrar.cs
using System.Collections.Generic;
using UnityEditor;

namespace Overlink.Editor
{
    public static class SceneRegistrar
    {
        // Desired build order — edit here if the list changes.
        private static readonly (string path, bool enabled)[] Scenes =
        {
            ("Assets/Scenes/MainMenu.unity",       true),
            ("Assets/Scenes/Gameplay.unity",       true),
            ("Assets/Scenes/Deckbuilder.unity",    true),
            ("Assets/Scenes/Collection.unity",     true),
            ("Assets/Scenes/Settings.unity",       true),
            ("Assets/Scenes/NetPresenceDemo.unity",true),
            ("Assets/Scenes/SampleScene.unity",    false),
        };

        [MenuItem("Overlink/Register Scenes in Build Settings")]
        public static void Register()
        {
            var missing = new List<string>();
            foreach (var (path, _) in Scenes)
            {
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                    missing.Add(path);
            }

            if (missing.Count > 0)
            {
                UnityEngine.Debug.LogError(
                    "[SceneRegistrar] Registration aborted; missing scene assets:\n" +
                    string.Join("\n", missing));
                return;
            }

            var list = new List<EditorBuildSettingsScene>();
            foreach (var (path, enabled) in Scenes)
                list.Add(new EditorBuildSettingsScene(path, enabled));

            EditorBuildSettings.scenes = list.ToArray();

            var actual = EditorBuildSettings.scenes;
            if (actual.Length != list.Count)
            {
                UnityEngine.Debug.LogError(
                    $"[SceneRegistrar] Verification failed: expected {list.Count} scenes, got {actual.Length}.");
                return;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (actual[i].path != Scenes[i].path || actual[i].enabled != Scenes[i].enabled)
                {
                    UnityEngine.Debug.LogError(
                        $"[SceneRegistrar] Verification failed at index {i}: " +
                        $"expected {Scenes[i].path} (enabled={Scenes[i].enabled}), " +
                        $"got {actual[i].path} (enabled={actual[i].enabled}).");
                    return;
                }
            }

            UnityEngine.Debug.Log("[SceneRegistrar] Build settings updated and verified.");
        }
    }
}
