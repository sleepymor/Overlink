using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Authors the first-run tutorial inside Tutorial.unity. Runtime TutorialController only binds the
    /// objects built here; it never creates the screen itself.
    /// </summary>
    public static class AuthorTutorialScreen
    {
        const string ScenePath = "Assets/Scenes/Tutorial.unity";
        const string ArtPath = "Assets/UI/Background/Signal bay.png";
        const string FontPath = "Assets/Resources/Fonts/ChakraPetch-Bold.asset";

        static readonly Color Base = new Color(0.039f, 0.055f, 0.102f, 1f);
        static readonly Color Card = new Color(0.106f, 0.149f, 0.204f, 1f);
        static readonly Color Input = new Color(0.039f, 0.055f, 0.102f, 1f);
        static readonly Color Primary = new Color(0.161f, 0.875f, 0.949f, 1f);
        static readonly Color TextBright = new Color(0.871f, 0.910f, 0.933f, 1f);
        static readonly Color TextDim = new Color(0.510f, 0.588f, 0.663f, 1f);
        static readonly Color Border = new Color(0.149f, 0.227f, 0.290f, 0.35f);
        static readonly Color NodeYours = new Color(0.063f, 0.200f, 0.235f, 1f);
        static readonly Color NodeOpponent = new Color(0.235f, 0.094f, 0.094f, 1f);
        static readonly Color NodeContested = new Color(0.227f, 0.204f, 0.082f, 1f);

        const float PanelWidth = 1000f;
        const float PanelHeight = 560f;

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        [MenuItem("Overlink/UI/Author Tutorial Screen")]
        public static void AuthorAndSave()
        {
            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                scene = CreateScene();
            }
            else
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Author(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TutorialScreen] Tutorial screen authored and saved.");
        }

        // Builds the scene skeleton the authoring pass expects: a canvas, an EventSystem, and the
        // controller plus the login guard. Protected like the other UI scenes, so opening it directly
        // from the Editor without signing in sends the player to Auth.
        static Scene CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasObject = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var controllerObject = new GameObject("TutorialController", typeof(Overlink.UI.TutorialController),
                typeof(Overlink.UI.ProtectedSceneGuard));

            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        public static void Author(Scene scene)
        {
            Transform canvas = FindSceneTransform(scene, "TutorialCanvas");
            if (canvas == null)
            {
                Debug.LogError("[TutorialScreen] TutorialCanvas is missing.");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            ClearCanvas(canvas);

            EnsureCamera(scene);
            BuildBackground(canvas);
            Transform panel = BuildPanel(canvas);
            BuildBoardDiagram(panel);

            TextMeshProUGUI eyebrow = Text(Place(Stretched("Eyebrow", panel), Center, Center, new Vector2(-200f, 220f), new Vector2(500f, 24f)),
                "FIRST RUN  //  TUTORIAL", 13f, Primary, TextAlignmentOptions.Left, FontStyles.Normal);
            TextMeshProUGUI title = Text(Place(Stretched("Page Title", panel), Center, Center, new Vector2(-200f, 180f), new Vector2(500f, 52f)),
                "WELCOME TO OVERLINK", 30f, TextBright, TextAlignmentOptions.Left, FontStyles.Bold);
            TextMeshProUGUI body = Text(Place(Stretched("Page Body", panel), Center, Center, new Vector2(-200f, -20f), new Vector2(500f, 330f)),
                string.Empty, 17f, TextBright, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;

            TextMeshProUGUI counter = Text(Place(Stretched("Page Counter", panel), Center, Center, new Vector2(0f, -230f), new Vector2(300f, 24f)),
                "1 / 1", 13f, TextDim, TextAlignmentOptions.Center, FontStyles.Normal);

            Button back = CreateButton(panel, "Back Button", "BACK", new Vector2(-300f, -230f), new Vector2(220f, 46f), Card);
            Button next = CreateButton(panel, "Next Button", "NEXT", new Vector2(0f, -230f), new Vector2(220f, 46f), Card);
            Button skip = CreateButton(panel, "Skip Button", "SKIP TUTORIAL", new Vector2(300f, -230f), new Vector2(220f, 46f), Card);

            if (font != null)
            {
                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            }

            BindController(scene, title, body, counter, back, next, skip,
                FindOutlines(canvas, PlayerActiveSlots), FindOutlines(canvas, NodePlates));
        }

        // The Tutorial scene needs a camera for the board diagram to draw and for Unity's Game view to
        // render. The overlay canvas itself does not need one.
        static void EnsureCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera existing in root.GetComponentsInChildren<Camera>(true))
            {
                if (existing != null) return;
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Base;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        // Draws the battlefield as the game lays it out (BattlefieldPresenter): a Core battlefield and four
        // Node battlefields, each with an Active rank and a Bench rank per player. Mirrored about the centre
        // line, with the player's side at the bottom.
        static void BuildBoardDiagram(Transform panel)
        {
            GameObject board = NewChild("Board Diagram", panel);
            RectTransform boardRect = board.GetComponent<RectTransform>();
            Place(boardRect, Center, Center, new Vector2(250f, 40f), new Vector2(380f, 400f));

            Color plate = new Color(0.075f, 0.110f, 0.157f, 1f);
            Color activeSlot = new Color(0.114f, 0.165f, 0.231f, 1f);
            Color benchSlot = new Color(0.086f, 0.125f, 0.176f, 1f);
            Color ownSlot = new Color(0.161f, 0.875f, 0.949f, 0.85f);
            Color enemySlot = new Color(0.949f, 0.384f, 0.353f, 0.85f);

            // Core battlefield: centre column.
            BoardPlate(board.transform, "Core Plate Player", new Vector2(0f, -100f), new Vector2(180f, 150f), plate);
            BoardPlate(board.transform, "Core Plate Opponent", new Vector2(0f, 100f), new Vector2(180f, 150f), plate);
            BoardSlot(board.transform, "Core Active Player", new Vector2(-30f, -92f), new Vector2(44f, 60f), ownSlot);
            BoardSlot(board.transform, "Core Protocol Player", new Vector2(30f, -92f), new Vector2(30f, 44f), activeSlot);
            BoardSlot(board.transform, "Core Active Opponent", new Vector2(-30f, 92f), new Vector2(44f, 60f), enemySlot);
            BoardSlot(board.transform, "Core Protocol Opponent", new Vector2(30f, 92f), new Vector2(30f, 44f), activeSlot);
            for (int i = 0; i < 4; i++)
            {
                float x = -66f + i * 44f;
                BoardSlot(board.transform, "Core Bench Player " + i, new Vector2(x, -150f), new Vector2(34f, 40f), benchSlot);
                BoardSlot(board.transform, "Core Bench Opponent " + i, new Vector2(x, 150f), new Vector2(34f, 40f), benchSlot);
            }

            // Four Node battlefields at the corners. Each plate is split into a player half (bottom) and an
            // opponent half (top), each with one Active slot nearest the centre and two Bench slots outside it.
            // Sample states: Node 1 is yours, Node 2 and Node 4 are contested, Node 3 is the opponent's.
            Vector2[] corners = { new Vector2(-140f, -140f), new Vector2(140f, -140f), new Vector2(140f, 140f), new Vector2(-140f, 140f) };
            bool[] playerActive = { true, true, false, true };
            bool[] opponentActive = { false, true, true, true };
            for (int n = 0; n < corners.Length; n++)
            {
                Vector2 c = corners[n];
                string node = "Node " + (n + 1);
                bool mine = playerActive[n];
                bool theirs = opponentActive[n];
                Color tint = mine && !theirs ? NodeYours : theirs && !mine ? NodeOpponent : NodeContested;

                BoardPlate(board.transform, "Node Plate " + (n + 1), c, new Vector2(84f, 130f), tint);
                BoardSlot(board.transform, node + " Player Active", new Vector2(c.x, c.y - 16f), new Vector2(30f, 36f),
                    mine ? ownSlot : activeSlot);
                BoardSlot(board.transform, node + " Player Bench A", new Vector2(c.x - 18f, c.y - 46f), new Vector2(24f, 26f), benchSlot);
                BoardSlot(board.transform, node + " Player Bench B", new Vector2(c.x + 18f, c.y - 46f), new Vector2(24f, 26f), benchSlot);
                BoardSlot(board.transform, node + " Opponent Active", new Vector2(c.x, c.y + 16f), new Vector2(30f, 36f),
                    theirs ? enemySlot : activeSlot);
                BoardSlot(board.transform, node + " Opponent Bench A", new Vector2(c.x - 18f, c.y + 46f), new Vector2(24f, 26f), benchSlot);
                BoardSlot(board.transform, node + " Opponent Bench B", new Vector2(c.x + 18f, c.y + 46f), new Vector2(24f, 26f), benchSlot);
            }

            BoardLabel(board.transform, "Label Opponent", new Vector2(0f, 190f), "OPPONENT");
            BoardLabel(board.transform, "Label You", new Vector2(0f, -190f), "YOU");
            Text(Place(Stretched("Board Legend", board.transform), Center, Center, new Vector2(0f, -215f), new Vector2(380f, 18f)),
                "<color=#29DFF2>YOURS</color>     <color=#F2625A>OPPONENT</color>     <color=#F2D35A>CONTESTED</color>",
                11f, TextDim, TextAlignmentOptions.Center, FontStyles.Bold);

            foreach (string slot in PlayerActiveSlots) AddHighlight(FindChild(board.transform, slot));
            foreach (string plateName in NodePlates) AddHighlight(FindChild(board.transform, plateName));
        }

        // The Opening Lineup page highlights the player's five Active slots: the Core Active and one Active per Node.
        static readonly string[] PlayerActiveSlots =
        {
            "Core Active Player", "Node 1 Player Active", "Node 2 Player Active", "Node 3 Player Active", "Node 4 Player Active",
        };

        static readonly string[] NodePlates = { "Node Plate 1", "Node Plate 2", "Node Plate 3", "Node Plate 4" };

        // Outlines start disabled. TutorialController switches them on per page.
        static void AddHighlight(Transform target)
        {
            if (target == null) return;
            Outline outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = Primary;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;
        }

        static Outline[] FindOutlines(Transform root, string[] names)
        {
            var outlines = new Outline[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Transform target = FindChild(root, names[i]);
                outlines[i] = target != null ? target.GetComponent<Outline>() : null;
            }
            return outlines;
        }

        static void BoardPlate(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = NewChild(name, parent);
            Place(go.GetComponent<RectTransform>(), Center, Center, position, size);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void BoardSlot(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            BoardPlate(parent, name, position, size, color);
        }

        static void BoardLabel(Transform parent, string name, Vector2 position, string value)
        {
            GameObject go = NewChild(name, parent);
            Place(go.GetComponent<RectTransform>(), Center, Center, position, new Vector2(200f, 16f));
            Text(go.GetComponent<RectTransform>(), value, 11f, TextDim, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        static void ClearCanvas(Transform canvas)
        {
            var stale = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in canvas) stale.Add(child.gameObject);
            foreach (GameObject child in stale) Object.DestroyImmediate(child);
        }

        static void BuildBackground(Transform canvas)
        {
            GameObject bg = NewChild("Background", canvas);
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

        static Transform BuildPanel(Transform canvas)
        {
            GameObject go = NewChild("TutorialPanel", canvas);
            Place(go.GetComponent<RectTransform>(), Center, Center, Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            Image image = go.AddComponent<Image>();
            image.color = new Color(Card.r, Card.g, Card.b, 0.92f);
            image.raycastTarget = false;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return go.transform;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = NewChild(name, parent);
            Place(go.GetComponent<RectTransform>(), Center, Center, position, size);

            Image image = go.AddComponent<Image>();
            image.color = color;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = Colors(color);

            Text(Stretched("Label", go.transform), label, 15f, TextBright, TextAlignmentOptions.Center, FontStyles.Bold);
            return button;
        }

        static void BindController(Scene scene, TMP_Text title, TMP_Text body, TMP_Text counter,
            Button back, Button next, Button skip, Outline[] playerActive, Outline[] nodes)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (var controller in root.GetComponentsInChildren<Overlink.UI.TutorialController>(true))
            {
                SerializedObject serialized = new SerializedObject(controller);
                SetRef(serialized, "pageTitle", title);
                SetRef(serialized, "pageBody", body);
                SetRef(serialized, "pageCounter", counter);
                SetRef(serialized, "btnBack", back);
                SetRef(serialized, "btnNext", next);
                SetRef(serialized, "btnSkip", skip);
                SetRefArray(serialized, "playerActiveHighlights", playerActive);
                SetRefArray(serialized, "nodeHighlights", nodes);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Debug.LogError("[TutorialScreen] TutorialController not found in Tutorial.unity.");
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────────

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

        static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
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

        static void SetRefArray(SerializedObject serialized, string name, Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
