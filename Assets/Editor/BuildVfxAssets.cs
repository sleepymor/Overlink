using System.IO;
using UnityEditor;
using UnityEngine;

namespace Overlink.EditorTools
{
    /// <summary>
    /// One-time bootstrap for the authored VFX asset set. Shaders are checked in as source; the
    /// materials that reference them are generated here so their GUIDs stay stable across every
    /// machine and the widget code can load them by path without hand-authored .mat YAML.
    ///
    /// Menu: <c>Overlink/VFX/Ensure VFX Assets</c>. Idempotent — existing materials are left alone.
    /// </summary>
    public static class BuildVfxAssets
    {
        const string VfxRoot = "Assets/VFX";
        // Materials live under Resources so runtime widgets resolve them by name in builds too;
        // shaders stay beside them in Assets/VFX/Shaders as plain source.
        const string MaterialRoot = "Assets/Resources/VFX";
        const string WhiteSpritePath = MaterialRoot + "/UiWhite.png";

        [MenuItem("Overlink/VFX/Ensure VFX Assets")]
        public static void Ensure()
        {
            Directory.CreateDirectory(MaterialRoot);
            int created = 0;
            created += EnsureWhiteSprite();
            created += EnsureMaterial(MaterialRoot + "/UiSheen.mat", "Overlink/UI/Sheen", mat =>
            {
                mat.SetColor("_Color", new Color(0.161f, 0.875f, 0.949f, 1f));
                mat.SetFloat("_Speed", 0.45f);
                mat.SetFloat("_Width", 0.16f);
                mat.SetFloat("_Intensity", 0.7f);
                mat.SetFloat("_Angle", 0.35f);
            });
            created += EnsureMaterial(MaterialRoot + "/UiSheenWarm.mat", "Overlink/UI/Sheen", mat =>
            {
                mat.SetColor("_Color", new Color(1f, 0.72f, 0.28f, 1f));
                mat.SetFloat("_Speed", 0.32f);
                mat.SetFloat("_Width", 0.22f);
                mat.SetFloat("_Intensity", 0.55f);
                mat.SetFloat("_Angle", 0.25f);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Overlink] VFX assets ensured at " + VfxRoot + " (" + created + " created).");
        }

        static int EnsureMaterial(string path, string shaderName, System.Action<Material> configure)
        {
            if (File.Exists(path)) return 0;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[Overlink] Shader missing for " + path + ": " + shaderName);
                return 0;
            }

            Material material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(path);
            configure?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return 1;
        }

        /// <summary>
        /// The shared solid sprite every authored bar layer rides. A <c>Filled</c> Image with no sprite
        /// falls back to simple-quad rendering and silently ignores <c>fillAmount</c> — the milestone
        /// track shipped that way and painted a full-brightness bar at zero progress — so bar layers
        /// must always carry a real sprite, and this 4x4 white one is the neutral choice.
        /// </summary>
        public static Sprite WhiteSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (sprite == null)
            {
                Ensure();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            }

            return sprite;
        }

        static int EnsureWhiteSprite()
        {
            if (File.Exists(WhiteSpritePath)) return 0;
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(WhiteSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(WhiteSpritePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return 1;
        }
    }
}
