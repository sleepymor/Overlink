using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Applies the Signal Bay reference art: the bay background, the Gacha icon set, and the single
    /// Signal Archive pack tile. Positions use the reference's 1600x900 layout as normalised anchors,
    /// so they scale with the canvas. Every object it creates is named "Reference ..." and is rebuilt
    /// on each run, so the menu is safe to repeat.
    /// </summary>
    public static class AuthorSignalBayReferenceArt
    {
        const string IconRoot = "Assets/UI/Icons/Gacha/";
        const string BackgroundPath = "Assets/UI/Background/Signal bay.png";
        const string ButtonSprite = "Assets/UI/Sprites/ButtonBevel.png";
        const int BevelSize = 64;
        const int BevelCut = 18;
        const int BevelBorder = 20;
        static readonly Color ButtonSlate = new Color32(27, 40, 55, 255);
        static readonly Color ButtonInk = new Color32(6, 15, 24, 255);
        const string Prefix = "Reference ";
        static readonly Vector2 Reference = new Vector2(1600f, 900f);
        static readonly Color Cyan = new Color32(41, 223, 242, 255);
        static readonly Color Muted = new Color32(135, 157, 171, 255);
        static readonly Color Bright = new Color32(220, 236, 241, 255);
        static readonly Color Amber = new Color32(255, 180, 59, 255);
        static readonly Color TileFill = new Color32(14, 52, 62, 255);

        const string RegularFont = "Assets/UI/Fonts/ChakraPetch-Regular SDF.asset";
        const string BoldFont = "Assets/UI/Fonts/ChakraPetch-Bold SDF.asset";

        static TMP_FontAsset font;
        static TMP_FontAsset boldFont;

        /// <summary>Sets every bay label to Chakra Petch; headings and titles use the Bold face.</summary>
        static void ApplyFonts(Transform bay)
        {
            foreach (TMP_Text label in bay.GetComponentsInChildren<TMP_Text>(true))
            {
                if (font != null) label.font = font;
                if (boldFont != null && IsHeading(label.name)) label.font = boldFont;
            }
        }

        static bool IsHeading(string name)
        {
            return name == "Header Title" || name == "Info Title" || name == "Reference Tile Title"
                || name == "Milestone Progress" || name == "Credit Balance";
        }

        [MenuItem("Overlink/Scenes/Apply Signal Bay Reference Art")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") as RectTransform : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || bay == null)
            {
                Debug.LogError("[Overlink] Open Gacha.unity in edit mode before applying the Signal Bay reference art.");
                return;
            }

            EnsureBevelSprite();
            Undo.RegisterFullObjectHierarchyUndo(bay.gameObject, "Apply Signal Bay reference art");
            Clear(screen.transform);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFont);
            boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFont);
            if (font == null) Debug.LogWarning("[Overlink] Chakra Petch Regular SDF font is missing.");
            ApplyFonts(bay);

            ApplyBackground(bay);
            ApplyHeader(bay);
            ApplySidebar(bay);
            ApplyRightPanel(bay);
            ApplyCreditReadout(screen.transform);
            ApplyFooter(bay);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Overlink] Signal Bay reference art applied and Gacha.unity saved.");
        }

        static void ApplyBackground(Transform bay)
        {
            var background = bay.Find("Background")?.GetComponent<Image>();
            if (background == null) return;
            background.sprite = LoadSprite(BackgroundPath);
            background.color = Color.white;
            background.preserveAspect = false;
            background.raycastTarget = false;
        }

        static void ApplyHeader(Transform bay)
        {
            Place(bay.Find("Header Eyebrow"), 98, 32, 420, 16);
            Place(bay.Find("Header Title"), 98, 50, 640, 48);
            Place(bay.Find("Header Status"), 1100, 116, 456, 18, TextAlignmentOptions.Right);
            Icon(bay, "Reference Header Icon", "box.png", 44, 44, 36, 36, Cyan);
        }

        static void ApplySidebar(Transform bay)
        {
            Place(bay.Find("Pack Selection Label"), 44, 155, 220, 16);
            Place(bay.Find("Pack Count"), 180, 155, 144, 16, TextAlignmentOptions.Right);
            var count = bay.Find("Pack Count")?.GetComponent<TMP_Text>();
            if (count != null) count.fontSize = 11;
            Place(bay.Find("Pool Note"), 44, 612, 320, 36);

            Fill(bay, "Reference Tile", 44, 187, 280, 132, TileFill);
            Fill(bay, "Reference Tile Accent", 44, 187, 3, 132, Cyan);
            Icon(bay, "Reference Tile Icon", "radio-tower.png", 62, 204, 40, 40, Cyan);
            Text(bay, "Reference Tile Title", "Signal Archive", 22, Bright, 116, 204, 190, 30);
            Text(bay, "Reference Tile Price", "100 credits / pack", 12, Muted, 116, 238, 190, 18);
            Text(bay, "Reference Tile Meta", "CORE SET / MIXED FACTIONS", 9, Muted, 60, 282, 170, 14);
            Text(bay, "Reference Tile Active", "ACTIVE", 9, Cyan, 216, 282, 88, 14, TextAlignmentOptions.Right);
            Icon(bay, "Reference Tile Check", "check.png", 292, 226, 14, 14, Cyan);
        }

        static void ApplyRightPanel(Transform bay)
        {
            var contents = bay.Find("Contents Rates");
            if (contents != null)
            {
                Place(contents, 1072, 398, 320, 20);
                Icon(bay, "Reference Contents Icon", "layers.png", 1048, 400, 16, 16, Muted);
            }

            StyleOpenButton(bay, "Figma Open One", ButtonSlate, "box.png", Cyan);
            StyleOpenButton(bay, "Figma Open Ten", Cyan, "chevrons-right.png", ButtonInk);

            var returnButton = bay.Find("Figma Return") as RectTransform;
            if (returnButton != null)
            {
                var returnLabel = returnButton.GetComponentInChildren<TMP_Text>(true);
                if (returnLabel != null)
                {
                    returnLabel.text = "CARD BINDER";
                    var labelRect = (RectTransform)returnLabel.transform;
                    labelRect.offsetMin = new Vector2(34f, labelRect.offsetMin.y);
                }

                var rect = Make(returnButton, "Reference Return Icon");
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(14f, 0f);
                rect.sizeDelta = new Vector2(18f, 18f);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = LoadSprite(IconRoot + "corner-down-left.png");
                image.preserveAspect = true;
                image.color = Color.white;
                image.raycastTarget = false;
            }
        }

        /// <summary>
        /// The live credit counter sits beside the bay, not inside it. Give it a diamond and a
        /// "SIGNAL CREDITS" label, moving the number right to make room for the icon.
        /// </summary>
        static void ApplyCreditReadout(Transform screen)
        {
            var counter = screen.Find("Credit Live") as RectTransform;
            if (counter == null) return;

            Undo.RecordObject(counter, "Move credit readout");
            counter.anchoredPosition = new Vector2(462f, 456f);

            var icon = Make(counter.parent, "Reference Credit Icon");
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(432f, 456f);
            icon.sizeDelta = new Vector2(20f, 20f);
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(IconRoot + "credit-diamond.png");
            image.preserveAspect = true;
            image.color = Bright;
            image.raycastTarget = false;

            var label = Make(counter.parent, "Reference Credit Label");
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
            label.pivot = new Vector2(0f, 0.5f);
            label.anchoredPosition = new Vector2(432f, 474f);
            label.sizeDelta = new Vector2(200f, 14f);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = "SIGNAL CREDITS";
            text.fontSize = 10;
            text.color = Muted;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        static void ApplyFooter(Transform bay)
        {
            Place(bay.Find("Milestone Label"), 384, 748, 480, 24);
            SetLabel(bay.Find("Next Reward"), "NEXT REWARD", 10, Muted, false);
            Place(bay.Find("Next Reward"), 1380, 746, 170, 14);
            SetLabel(bay.Find("Reward Detail"), "1 FREE PACK", 18, Amber, true);
            Place(bay.Find("Reward Detail"), 1380, 762, 170, 24);
            Icon(bay, "Reference Reward Icon", "package.png", 1334, 752, 34, 34, Amber);
        }

        static void SetLabel(Transform target, string value, float size, Color color, bool bold)
        {
            var label = target != null ? target.GetComponent<TMP_Text>() : null;
            if (label == null) return;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Left;
            if (bold && boldFont != null) label.font = boldFont;
        }

        /// <summary>
        /// Writes a 9-sliced sprite with a bottom-right diagonal cut, the same bevel the binder's
        /// ApplyChamferedCorner builds at runtime. It is saved as an asset so the cut survives scene saves.
        /// </summary>
        static void EnsureBevelSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSprite) != null) return;

            var texture = new Texture2D(BevelSize, BevelSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[BevelSize * BevelSize];
            for (int y = 0; y < BevelSize; y++)
            {
                for (int x = 0; x < BevelSize; x++)
                {
                    bool cut = (BevelSize - 1 - x) + y < BevelCut;
                    pixels[y * BevelSize + x] = cut ? new Color32(255, 255, 255, 0) : new Color32(255, 255, 255, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string full = System.IO.Path.Combine(Application.dataPath, "UI/Sprites/ButtonBevel.png");
            System.IO.File.WriteAllBytes(full, png);
            AssetDatabase.ImportAsset(ButtonSprite, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(ButtonSprite) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(BevelBorder, BevelBorder, BevelBorder, BevelBorder);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static void StyleOpenButton(Transform bay, string buttonName, Color fill, string file, Color iconColor)
        {
            var button = bay.Find(buttonName) as RectTransform;
            if (button == null) return;
            var face = button.GetComponent<Image>();
            if (face != null)
            {
                face.sprite = LoadSprite(ButtonSprite);
                face.type = Image.Type.Sliced;
                face.color = fill;
                face.raycastTarget = true;
            }

            var rect = Make(button, "Reference Button Icon");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-28f, 0f);
            rect.sizeDelta = new Vector2(20f, 20f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(IconRoot + file);
            image.preserveAspect = true;
            image.color = iconColor;
            image.raycastTarget = false;
        }

        static void Clear(Transform root)
        {
            var stale = new List<GameObject>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name.StartsWith(Prefix)) stale.Add(child.gameObject);
            }

            foreach (GameObject go in stale) Undo.DestroyObjectImmediate(go);
        }

        static void Place(Transform target, float x, float y, float w, float h, TextAlignmentOptions? alignment = null)
        {
            var rect = target as RectTransform;
            if (rect == null) return;
            rect.anchorMin = new Vector2(x / Reference.x, 1f - (y + h) / Reference.y);
            rect.anchorMax = new Vector2((x + w) / Reference.x, 1f - y / Reference.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            var tmp = target.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                if (alignment.HasValue) tmp.alignment = alignment.Value;
            }
        }

        static RectTransform Make(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Fill(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            RectTransform rect = Make(parent, name);
            Place(rect, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void Icon(Transform parent, string name, string file, float x, float y, float w, float h, Color color)
        {
            RectTransform rect = Make(parent, name);
            Place(rect, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(IconRoot + file);
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
        }

        static void Text(Transform parent, string name, string value, float size, Color color,
            float x, float y, float w, float h, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            RectTransform rect = Make(parent, name);
            Place(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = IsHeading(name) && boldFont != null ? boldFont : font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("[Overlink] Signal Bay art is not a sprite: " + path);
            return sprite;
        }
    }
}
