#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Overlink.Editor
{
    /// <summary>One-shot editor utility for the runtime-loadable Overlink display font.</summary>
    public static class OverlinkFontAssetGenerator
    {
        const string FontPath = "Assets/TextMesh Pro/Fonts/Rajdhani-SemiBold.ttf";
        const string Folder = "Assets/Resources/Fonts";
        const string AssetPath = Folder + "/Rajdhani-SemiBold.asset";

        [MenuItem("Overlink/Visuals/Generate Rajdhani TMP Font")]
        public static void Generate()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError("[Overlink] Rajdhani font not found at " + FontPath);
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

            AssetDatabase.DeleteAsset(AssetPath);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, 48, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogError("[Overlink] Could not create the Rajdhani TMP font asset.");
                return;
            }

            asset.name = "Rajdhani-SemiBold";
            AssetDatabase.CreateAsset(asset, AssetPath);

            // CreateFontAsset returns the atlas and material as in-memory objects. They must be
            // registered as sub-assets before saving; otherwise the YAML asset contains {fileID: 0}
            // for m_AtlasTextures and TMP throws when it tries to resolve a character at runtime.
            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = "Rajdhani-SemiBold Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);

            Material material = asset.material;
            material.name = "Rajdhani-SemiBold Atlas Material";
            AssetDatabase.AddObjectToAsset(material, asset);

            // Populate the complete printable ASCII set up front. This produces a self-contained
            // atlas for the UI and avoids relying on dynamic atlas mutation during scene startup.
            asset.TryAddCharacters(
                " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~",
                true);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Overlink] Generated " + AssetPath);
        }
    }
}
#endif
