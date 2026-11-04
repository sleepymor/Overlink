using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>Temporarily selects a built-in Game view aspect for a manual bay preview.</summary>
    [InitializeOnLoad]
    static class SignalBayAspectPreview
    {
        const string PreviousKey = "Overlink.SignalBay.PreviousGameViewSize";
        internal const string PreviewOnPlayKey = "Overlink.SignalBay.Preview16OnPlay";
        internal const string PreviewFourOnPlayKey = "Overlink.SignalBay.Preview4OnPlay";

        static SignalBayAspectPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PreviewOnPlayKey, false))
                {
                    SessionState.SetBool(PreviewOnPlayKey, false);
                    PreviewSixteenByTen();
                }
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PreviewFourOnPlayKey, false))
                {
                    SessionState.SetBool(PreviewFourOnPlayKey, false);
                    PreviewFourByThree();
                }
                if (state == PlayModeStateChange.EnteredEditMode) Restore();
            };
        }

        [MenuItem("Tools/Offline Gacha/Preview Bay at 16 by 10")]
        static void PreviewSixteenByTen()
        {
            PreviewAspect(false);
        }

        [MenuItem("Tools/Offline Gacha/Preview Bay at 4 by 3")]
        static void PreviewFourByThree()
        {
            PreviewAspect(true);
        }

        [MenuItem("Tools/Offline Gacha/Remove Temporary Bay Aspect")]
        static void RemoveTemporaryAspect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[BayAspect] Stop Play mode before cleanup."); return; }
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var getGroup = sizesType.GetMethod("GetGroup");
            var group = getGroup.Invoke(sizes, new[] { Enum.Parse(getGroup.GetParameters()[0].ParameterType, "Standalone") });
            var labels = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
            int index = Array.FindIndex(labels, label => label.Contains("Overlink temporary 4:3"));
            if (index < 0) { Debug.Log("[BayAspect] No temporary size to remove."); return; }
            int builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            if (index < builtin) { Debug.LogError("[BayAspect] Refusing to remove a built-in size."); return; }
            group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
            Debug.Log("[BayAspect] Removed the temporary 4:3 size.");
        }

        static void PreviewAspect(bool fourByThree)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[BayAspect] Enter Play mode before changing the preview aspect.");
                return;
            }
            try
            {
                var assembly = typeof(EditorWindow).Assembly;
                var gameViewType = assembly.GetType("UnityEditor.GameView", true);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
                var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var getGroup = sizesType.GetMethod("GetGroup");
                var groupType = getGroup.GetParameters()[0].ParameterType;
                var group = getGroup.Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                var labels = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
                int index = Array.FindIndex(labels, label => label.StartsWith(fourByThree ? "4:3" : "16:10", StringComparison.Ordinal));
                if (index < 0 && fourByThree)
                {
                    var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
                    var kindType = assembly.GetType("UnityEditor.GameViewSizeType", true);
                    var ctor = sizeType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { kindType, typeof(int), typeof(int), typeof(string) }, null);
                    var size = ctor.Invoke(new object[] { Enum.Parse(kindType, "AspectRatio"), 4, 3, "Overlink temporary 4:3" });
                    index = labels.Length;
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    SessionState.SetInt(PreviousKey + ".customIndex", index);
                    SessionState.SetBool(PreviousKey + ".custom", true);
                    labels = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
                }
                var view = EditorWindow.GetWindow(gameViewType);
                var property = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (index < 0 || property == null) throw new InvalidOperationException("Game view size or selector not found. Sizes: " + string.Join(", ", labels));
                if (!SessionState.GetBool(PreviousKey + ".saved", false))
                {
                    SessionState.SetInt(PreviousKey, (int)property.GetValue(view));
                    SessionState.SetBool(PreviousKey + ".saved", true);
                }
                property.SetValue(view, index);
                Debug.Log("[BayAspect] Selected " + labels[index] + "; previous Game view size will be restored on leaving Play mode.");
            }
            catch (Exception e) { Debug.LogError("[BayAspect] Could not change Game view aspect: " + e); }
        }

        static void Restore()
        {
            if (!SessionState.GetBool(PreviousKey + ".saved", false)) return;
            try
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
                var property = type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                property.SetValue(EditorWindow.GetWindow(type), SessionState.GetInt(PreviousKey, 0));
                if (SessionState.GetBool(PreviousKey + ".custom", false))
                {
                    var assembly = typeof(EditorWindow).Assembly;
                    var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
                    var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                    var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    var getGroup = sizesType.GetMethod("GetGroup");
                    var group = getGroup.Invoke(sizes, new[] { Enum.Parse(getGroup.GetParameters()[0].ParameterType, "Standalone") });
                    var labels = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
                    int index = SessionState.GetInt(PreviousKey + ".customIndex", -1);
                    if (index < 0 || index >= labels.Length || !labels[index].Contains("Overlink temporary 4:3"))
                        throw new InvalidOperationException("Temporary size changed; leaving custom sizes intact.");
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                    SessionState.SetBool(PreviousKey + ".custom", false);
                }
                SessionState.SetBool(PreviousKey + ".saved", false);
                Debug.Log("[BayAspect] Restored previous Game view size.");
            }
            catch (Exception e) { Debug.LogError("[BayAspect] Could not restore Game view size: " + e); }
        }
    }
}
