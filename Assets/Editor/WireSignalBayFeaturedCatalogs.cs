using Overlink.Cards;
using Overlink.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.EditorTools
{
    /// <summary>Sets the card catalogs the Signal Bay uses to render its featured legendaries.</summary>
    public static class WireSignalBayFeaturedCatalogs
    {
        const string PresentationRoot = "Assets/Cards/Presentation";

        [MenuItem("Overlink/Scenes/Wire Signal Bay Featured Catalogs")]
        public static void Wire()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity")
            {
                Debug.LogError("[Overlink] Open Gacha.unity in edit mode before wiring the Signal Bay catalogs.");
                return;
            }

            var bay = Object.FindObjectOfType<OfflineSignalBayController>();
            if (bay == null)
            {
                Debug.LogError("[Overlink] No OfflineSignalBayController in Gacha.unity.");
                return;
            }

            var so = new SerializedObject(bay);
            so.FindProperty("templateCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTemplateCatalog>(PresentationRoot + "/CardTemplateCatalog.asset");
            so.FindProperty("frameCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardFrameCatalog>(PresentationRoot + "/CardFrameCatalog.asset");
            so.FindProperty("tierMaterials").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CardTierMaterialSet>(PresentationRoot + "/CardTierMaterialSet.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bay);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Overlink] Signal Bay featured catalogs wired and Gacha.unity saved.");
        }
    }
}
