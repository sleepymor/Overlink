using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Sets the Gacha icon set to trilinear sampling with mipmaps and high-quality compression, so the
    /// icons stay sharp when the bay scales them. Re-running is safe: it only rewrites import settings.
    /// </summary>
    public static class ApplyGachaIconImportSettings
    {
        const string IconRoot = "Assets/UI/Icons/Gacha";
        static readonly string[] PlatformTargets =
        {
            "DefaultTexturePlatform", "Standalone", "WebGL", "Android", "Windows Store Apps"
        };

        [MenuItem("Overlink/UI/Apply Gacha Icon Import Settings")]
        public static void Apply()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconRoot });
            int changed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                importer.isReadable = false;

                foreach (string target in PlatformTargets)
                {
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(target);
                    settings.overridden = target != "DefaultTexturePlatform";
                    settings.maxTextureSize = Mathf.Max(settings.maxTextureSize, 2048);
                    settings.textureCompression = TextureImporterCompression.CompressedHQ;
                    settings.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(settings);
                }

                importer.SaveAndReimport();
                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Overlink] Gacha icon import settings applied to " + changed + " texture(s): trilinear, mipmaps on, high-quality compression.");
        }
    }
}
