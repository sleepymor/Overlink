using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Authors the Settings screen inside Settings.unity. Runtime SettingsController only binds the
    /// objects built here; it never creates the screen itself.
    /// </summary>
    public static class AuthorSettingsScreen
    {
        const string ScenePath = "Assets/Scenes/Settings.unity";
        const string ArtPath = "Assets/UI/Background/Signal bay.png";
        const string FontPath = "Assets/Resources/Fonts/ChakraPetch-Bold.asset";

        static readonly Color Base = new Color(0.039f, 0.055f, 0.102f, 1f);
        static readonly Color Card = new Color(0.106f, 0.149f, 0.204f, 1f);
        static readonly Color Input = new Color(0.039f, 0.055f, 0.102f, 1f);
        static readonly Color Primary = new Color(0.161f, 0.875f, 0.949f, 1f);
        static readonly Color TextBright = new Color(0.871f, 0.910f, 0.933f, 1f);
        static readonly Color TextDim = new Color(0.510f, 0.588f, 0.663f, 1f);
        static readonly Color Border = new Color(0.149f, 0.227f, 0.290f, 0.35f);

        const float PanelWidth = 1280f;
        const float PanelHeight = 620f;
        const float ColumnWidth = 560f;
        const float RowHeight = 44f;

        [MenuItem("Overlink/UI/Author Settings Screen")]
        public static void AuthorAndSave()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("[SettingsScreen] Missing scene: " + ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Author(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SettingsScreen] Settings screen authored and saved.");
        }

        public static void Author(Scene scene)
        {
            Transform panel = FindSceneTransform(scene, "SettingsPanel");
            if (panel == null)
            {
                Debug.LogError("[SettingsScreen] SettingsPanel is missing.");
                return;
            }

            Transform canvas = panel.parent;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            ClearCanvasExcept(canvas, panel);

            BuildBackground(canvas);
            BuildHeading(canvas);
            Button back = BuildBackButton(canvas);
            Button replay = BuildReplayTutorialButton(canvas);
            Controls controls = BuildPanel(panel);

            if (font != null)
            {
                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            }

            BindController(scene, controls, back, replay);
        }

        // Old heading and background objects from the previous layout would otherwise sit underneath
        // the rebuilt ones, which is what doubled the title.
        static void ClearCanvasExcept(Transform canvas, Transform keep)
        {
            var stale = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in canvas)
            {
                if (child != keep) stale.Add(child.gameObject);
            }
            foreach (GameObject child in stale) Object.DestroyImmediate(child);
        }

        static void BuildBackground(Transform canvas)
        {
            GameObject bg = Recreate("Background", canvas);
            bg.transform.SetAsFirstSibling();
            Stretch(bg.GetComponent<RectTransform>());
            Image art = bg.AddComponent<Image>();
            art.color = Base;
            art.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            art.preserveAspect = true;
            art.raycastTarget = false;

            GameObject scrim = NewChild("Scrim", bg.transform);
            Stretch(scrim.GetComponent<RectTransform>());
            Image scrimImage = scrim.AddComponent<Image>();
            scrimImage.color = new Color(0.02f, 0.03f, 0.05f, 0.6f);
            scrimImage.raycastTarget = false;
        }

        static void BuildHeading(Transform canvas)
        {
            Text(Place(Stretched("Eyebrow", canvas), Center, Center, new Vector2(0f, 455f), new Vector2(800f, 24f)),
                "SYSTEM / PREFERENCES", 13f, Primary, TextAlignmentOptions.Center, FontStyles.Normal);
            Text(Place(Stretched("Title", canvas), Center, Center, new Vector2(0f, 405f), new Vector2(800f, 60f)),
                "SETTINGS", 40f, TextBright, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        static Button BuildBackButton(Transform canvas)
        {
            GameObject go = Recreate("Btn_Back", canvas);
            Place(go.GetComponent<RectTransform>(), Center, Center, new Vector2(0f, -400f), new Vector2(300f, 48f));

            Image image = go.AddComponent<Image>();
            image.color = Card;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors(Card);

            GameObject edge = NewChild("AccentEdge", go.transform);
            RectTransform edgeRect = edge.GetComponent<RectTransform>();
            edgeRect.anchorMin = new Vector2(0f, 1f);
            edgeRect.anchorMax = Vector2.one;
            edgeRect.pivot = new Vector2(0.5f, 1f);
            edgeRect.sizeDelta = new Vector2(0f, 2f);
            Image edgeImage = edge.AddComponent<Image>();
            edgeImage.color = new Color(Primary.r, Primary.g, Primary.b, 0.3f);
            edgeImage.raycastTarget = false;

            Text(Stretched("Label", go.transform), "BACK TO MAIN MENU", 15f, TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        static Button BuildReplayTutorialButton(Transform canvas)
        {
            GameObject go = Recreate("Btn_ReplayTutorial", canvas);
            Place(go.GetComponent<RectTransform>(), Center, Center, new Vector2(330f, -400f), new Vector2(300f, 48f));

            Image image = go.AddComponent<Image>();
            image.color = Card;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors(Card);

            GameObject edge = NewChild("AccentEdge", go.transform);
            RectTransform edgeRect = edge.GetComponent<RectTransform>();
            edgeRect.anchorMin = new Vector2(0f, 1f);
            edgeRect.anchorMax = Vector2.one;
            edgeRect.pivot = new Vector2(0.5f, 1f);
            edgeRect.sizeDelta = new Vector2(0f, 2f);
            Image edgeImage = edge.AddComponent<Image>();
            edgeImage.color = new Color(Primary.r, Primary.g, Primary.b, 0.3f);
            edgeImage.raycastTarget = false;

            Text(Stretched("Label", go.transform), "REPLAY TUTORIAL", 15f, TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        sealed class Controls
        {
            public Slider Volume;
            public TMP_Text VolumeValue;
            public Toggle Fullscreen;
            public TMP_Dropdown Quality;
            public TMP_Dropdown Resolution;
            public Toggle NativeResolution;
            public Toggle VSync;
            public TMP_Dropdown AntiAliasing;
            public Slider RenderScale;
            public TMP_Text RenderScaleValue;
            public Toggle PostFx;
            public Toggle Shake;
            public Toggle Flash;
            public Toggle Motion;
            public Slider ShakeIntensity;
            public TMP_Text ShakeIntensityValue;
            public Slider FlashIntensity;
            public TMP_Text FlashIntensityValue;
        }

        static Controls BuildPanel(Transform panel)
        {
            var controls = new Controls();
            var stale = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in panel) stale.Add(child.gameObject);
            foreach (GameObject child in stale) Object.DestroyImmediate(child);
            foreach (var layout in panel.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
            foreach (var fitter in panel.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Place(panelRect, Center, Center, Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            Image panelImage = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(Card.r, Card.g, Card.b, 0.92f);
            panelImage.raycastTarget = false;
            Outline outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // Left column: audio, display, graphics. Right column: effects and motion.
            float top = 250f;
            float left = -PanelWidth * 0.5f + 60f;
            float right = 60f;
            Header(panel, "AUDIO & DISPLAY", left, top);
            controls.Volume = SliderRow(panel, "MasterVolume", "MASTER VOLUME", left, top - 52f, out controls.VolumeValue);
            controls.Fullscreen = ToggleRow(panel, "FullscreenToggle", "FULLSCREEN", left, top - 100f);
            controls.Quality = Dropdown(panel, "QualityDropdown", "QUALITY", left, top - 150f);
            controls.Resolution = Dropdown(panel, "ResolutionDropdown", "RESOLUTION", left, top - 200f);
            controls.NativeResolution = ToggleRow(panel, "NativeResolutionToggle", "NATIVE RESOLUTION", left, top - 250f);
            controls.VSync = ToggleRow(panel, "VSyncToggle", "VSYNC", left, top - 300f);
            controls.AntiAliasing = Dropdown(panel, "AntiAliasingDropdown", "ANTI-ALIASING", left, top - 350f);
            controls.RenderScale = SliderRow(panel, "RenderScaleSlider", "RENDER SCALE", left, top - 400f, out controls.RenderScaleValue);

            Header(panel, "EFFECTS & MOTION", right, top);
            controls.PostFx = ToggleRow(panel, "PostFxToggle", "POST FX", right, top - 52f);
            controls.Shake = ToggleRow(panel, "ReduceShakeToggle", "REDUCE SHAKE", right, top - 100f);
            controls.Flash = ToggleRow(panel, "ReduceFlashToggle", "REDUCE FLASH", right, top - 150f);
            controls.Motion = ToggleRow(panel, "ReducedMotionToggle", "REDUCED MOTION", right, top - 200f);
            controls.ShakeIntensity = SliderRow(panel, "ShakeIntensitySlider", "SHAKE INTENSITY", right, top - 250f, out controls.ShakeIntensityValue);
            controls.FlashIntensity = SliderRow(panel, "FlashIntensitySlider", "FLASH INTENSITY", right, top - 300f, out controls.FlashIntensityValue);

            return controls;
        }

        static void Header(Transform panel, string caption, float x, float y)
        {
            Text(ColumnRect("Header " + caption, panel, x, y, ColumnWidth, 20f),
                caption, 12f, Primary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        }

        // x is the column's left edge, measured from the panel centre. Centre-anchored, so the panel's
        // size never moves it.
        static RectTransform ColumnRect(string name, Transform parent, float x, float y, float width, float height)
        {
            return Place(Stretched(name, parent), Center, new Vector2(0f, 0.5f), new Vector2(x, y),
                new Vector2(width, height));
        }

        static Slider SliderRow(Transform panel, string name, string caption, float x, float y, out TMP_Text value)
        {
            Text(ColumnRect(caption + " Label", panel, x, y, 300f, 30f),
                caption, 15f, TextBright, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            Slider slider = BuildSlider(panel, name, x, y);

            value = Text(Place(Stretched(name + " Value", panel), Center, new Vector2(1f, 0.5f),
                    new Vector2(x + ColumnWidth, y), new Vector2(90f, 30f)),
                "100%", 15f, Primary, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            return slider;
        }

        static TMP_Dropdown Dropdown(Transform panel, string name, string caption, float x, float y)
        {
            Text(ColumnRect(caption + " Label", panel, x, y, 300f, 30f),
                caption, 15f, TextBright, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            GameObject go = NewChild(name, panel);
            Place(go.GetComponent<RectTransform>(), Center, new Vector2(0f, 0.5f),
                new Vector2(x + 300f, y), new Vector2(ColumnWidth - 300f, 40f));
            Image box = go.AddComponent<Image>();
            box.color = Input;
            TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = box;

            RectTransform labelRect = Stretched("Label", go.transform);
            labelRect.offsetMin = new Vector2(16f, 0f);
            labelRect.offsetMax = new Vector2(-40f, 0f);
            TextMeshProUGUI label = Text(labelRect, "Ultra", 15f, TextBright, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            dropdown.captionText = label;
            AddDropdownTemplate(go, dropdown);
            return dropdown;
        }

        // TMP_Dropdown opens a copy of its Template, so the template must exist before Play mode.
        // Layout: Template > Viewport > Content > Item (Toggle with Label), wired through ScrollRect.
        static void AddDropdownTemplate(GameObject owner, TMP_Dropdown dropdown)
        {
            RectTransform template = Place(NewChild("Template", owner.transform), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(0f, 180f));
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.sizeDelta = new Vector2(0f, 180f);
            template.gameObject.AddComponent<Image>().color = Card;
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            template.gameObject.SetActive(false);

            RectTransform viewport = Stretched("Viewport", template);
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform content = NewChild("Content", viewport).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 28f);

            RectTransform item = NewChild("Item", content).GetComponent<RectTransform>();
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.pivot = new Vector2(0.5f, 0.5f);
            item.sizeDelta = new Vector2(0f, 28f);
            Image itemImage = item.gameObject.AddComponent<Image>();
            itemImage.color = Card;
            Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemImage;
            itemToggle.colors = Colors(Card);

            RectTransform itemLabel = Stretched("Item Label", item);
            itemLabel.offsetMin = new Vector2(16f, 0f);
            itemLabel.offsetMax = new Vector2(-16f, 0f);
            TextMeshProUGUI itemText = Text(itemLabel, "Option", 14f, TextBright,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            scroll.viewport = viewport;
            scroll.content = content;
            dropdown.template = template;
            dropdown.itemText = itemText;
        }

        static Slider BuildSlider(Transform panel, string name, float x, float y)
        {
            GameObject go = NewChild(name, panel);
            Place(go.GetComponent<RectTransform>(), Center, new Vector2(0f, 0.5f),
                new Vector2(x + 300f, y), new Vector2(180f, 20f));

            RectTransform track = Stretched("Background", go.transform);
            track.gameObject.AddComponent<Image>().color = Input;

            RectTransform area = Stretched("Fill Area", go.transform);
            area.offsetMin = new Vector2(5f, 0f);
            area.offsetMax = new Vector2(-5f, 0f);
            RectTransform fill = Stretched("Fill", area);
            fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Primary;

            RectTransform handleArea = Stretched("Handle Slide Area", go.transform);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = Place(NewChild("Handle", handleArea.transform), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 28f));
            handle.gameObject.AddComponent<Image>().color = TextBright;

            Slider slider = go.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
        }

        static Toggle ToggleRow(Transform panel, string name, string caption, float x, float y)
        {
            GameObject row = NewChildNamed(name, panel);
            Place(row.GetComponent<RectTransform>(), Center, new Vector2(0f, 0.5f),
                new Vector2(x, y), new Vector2(ColumnWidth, RowHeight));

            Image rowImage = row.AddComponent<Image>();
            rowImage.color = new Color(Card.r, Card.g, Card.b, 0.55f);
            Toggle toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = rowImage;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = Colors(new Color(Card.r, Card.g, Card.b, 0.55f));

            GameObject box = NewChildNamed("Box", row.transform);
            Place(box.GetComponent<RectTransform>(), Right, Right, new Vector2(-20f, 0f), new Vector2(22f, 22f));
            Image boxImage = box.AddComponent<Image>();
            boxImage.color = Input;
            boxImage.raycastTarget = false;

            GameObject check = NewChildNamed("Checkmark", box.transform);
            Stretch(check.GetComponent<RectTransform>());
            RectTransform checkRect = check.GetComponent<RectTransform>();
            checkRect.offsetMin = new Vector2(5f, 5f);
            checkRect.offsetMax = new Vector2(-5f, -5f);
            Image checkImage = check.AddComponent<Image>();
            checkImage.color = Primary;
            checkImage.raycastTarget = false;
            toggle.graphic = checkImage;

            RectTransform labelRect = Stretched("Label", row.transform);
            labelRect.offsetMin = new Vector2(20f, 0f);
            labelRect.offsetMax = new Vector2(-70f, 0f);
            Text(labelRect, caption, 15f, TextBright, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            return toggle;
        }

        static void BindController(Scene scene, Controls c, Button back, Button replay)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (var controller in root.GetComponentsInChildren<Overlink.UI.SettingsController>(true))
            {
                SerializedObject serialized = new SerializedObject(controller);
                SetRef(serialized, "masterVolume", c.Volume);
                SetRef(serialized, "volumeValue", c.VolumeValue);
                SetRef(serialized, "fullscreenToggle", c.Fullscreen);
                SetRef(serialized, "qualityDropdown", c.Quality);
                SetRef(serialized, "btnBack", back);
                SetRef(serialized, "btnReplayTutorial", replay);
                SetRef(serialized, "resolutionDropdown", c.Resolution);
                SetRef(serialized, "nativeResolutionToggle", c.NativeResolution);
                SetRef(serialized, "vSyncToggle", c.VSync);
                SetRef(serialized, "antiAliasingDropdown", c.AntiAliasing);
                SetRef(serialized, "renderScaleSlider", c.RenderScale);
                SetRef(serialized, "renderScaleValue", c.RenderScaleValue);
                SetRef(serialized, "postFxToggle", c.PostFx);
                SetRef(serialized, "reduceShakeToggle", c.Shake);
                SetRef(serialized, "reduceFlashToggle", c.Flash);
                SetRef(serialized, "reducedMotionToggle", c.Motion);
                SetRef(serialized, "shakeIntensitySlider", c.ShakeIntensity);
                SetRef(serialized, "shakeIntensityValue", c.ShakeIntensityValue);
                SetRef(serialized, "flashIntensitySlider", c.FlashIntensity);
                SetRef(serialized, "flashIntensityValue", c.FlashIntensityValue);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Debug.LogError("[SettingsScreen] SettingsController not found in Settings.unity.");
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────────

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 Right = new Vector2(1f, 0.5f);

        static ColorBlock Colors(Color normal)
        {
            ColorBlock block = ColorBlock.defaultColorBlock;
            block.normalColor = normal;
            block.highlightedColor = new Color(0.16f, 0.22f, 0.29f, normal.a);
            block.pressedColor = new Color(0.10f, 0.32f, 0.48f, normal.a);
            block.selectedColor = block.highlightedColor;
            block.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.45f);
            return block;
        }

        static void Tint(Transform target, Color color)
        {
            if (target == null) return;
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.color = color;
        }

        static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject NewChildNamed(string name, Transform parent) => NewChild(name, parent);

        static GameObject Recreate(string name, Transform parent)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return NewChild(name, parent);
        }

        static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            return Place(go.GetComponent<RectTransform>(), anchor, pivot, position, size);
        }

        static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static RectTransform Stretched(string name, Transform parent)
        {
            RectTransform rect = NewChild(name, parent).GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        static TextMeshProUGUI Text(RectTransform rect, string value, float size, Color color,
            TextAlignmentOptions alignment, FontStyles style)
        {
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Transform FindSceneTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindChild(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void SetRef(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
