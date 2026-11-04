using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>Turns the main-menu mockup into editable scene UI without changing controller paths.</summary>
    public static class AuthorMainMenuLayout
    {
        static readonly Color Background = new Color32(8, 14, 24, 255);
        static readonly Color Plate = new Color32(23, 40, 51, 255);
        static readonly Color Accent = new Color32(37, 209, 222, 255);
        static readonly Color Bright = new Color32(225, 237, 243, 255);
        static readonly Color Dim = new Color32(135, 157, 171, 255);

        [MenuItem("Overlink/Scenes/Author Main Menu Layout")]
        public static void Author()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/MainMenu.unity")
            {
                Debug.LogError("Open MainMenu.unity before authoring its layout.");
                return;
            }
            var root = GameObject.Find("UI Root");
            var canvas = root != null ? root.transform.Find("Canvas") : null;
            if (canvas == null) { Debug.LogError("Main menu canvas missing."); return; }
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var backdrop = canvas.Find("Background").GetComponent<Image>();
            backdrop.sprite = null;
            backdrop.color = Background;
            backdrop.raycastTarget = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(backdrop);

            var commands = canvas.Find("Title and commands");
            commands.GetComponent<CanvasGroup>().alpha = 1;
            Rect(commands, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-120, 0), new Vector2(530, 680));
            Text(commands.Find("Game title"), "OVERLINK", 76, Bright, new Vector2(0, -4), new Vector2(530, 100));
            Text(commands.Find("Tactical subtitle"), "TACTICAL NETWORK  //  LINK ESTABLISHED", 15, Accent, new Vector2(0, -110), new Vector2(530, 30));
            var separator = commands.Find("Title separator");
            Rect(separator, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -145), new Vector2(530, 2));
            separator.GetComponent<Image>().color = new Color32(45, 65, 78, 255);
            var play = commands.Find("Play commands");
            Rect(play, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -165), new Vector2(530, 400));
            string[] nodes = { "Matchmaking command", "VS AI command", "Deckbuilder command", "Gacha command" };
            string[] titles = { "MATCHMAKING", "VS AI", "DECKBUILDER", "GACHA" };
            string[] details = { "ESTABLISH A COMPETITIVE LINK", "LOCAL COMBAT SIMULATION", "CARD BINDER  //  SIGNAL ARCHIVE", "OPEN A NEW SIGNAL" };
            for (int i = 0; i < nodes.Length; i++) StyleCommand(play.Find(nodes[i]), titles[i], details[i], i);
            var system = commands.Find("System commands");
            Rect(system, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -575), new Vector2(530, 60));
            StyleSystem(system.Find("Settings command"), "SETTINGS", 0);
            StyleSystem(system.Find("Quit command"), "QUIT", 210);

            Group(canvas, "Link signature", 1);
            Group(canvas, "Upper signal rule", 1);
            Group(canvas, "Lower signal rule", 1);
            Group(canvas, "Scene designation", 0);
            Group(canvas, "Lobby footer", 1);
            Group(canvas, "Account overlay", 1);
            // Existing placeholder insignia has no sprite and would render as a solid square.
            var signature = canvas.Find("Link signature");
            var insignia = signature.Find("Overlink insignia");
            if (insignia != null) insignia.gameObject.SetActive(false);
            var link = signature.Find("Link text");
            if (link != null) { var t = link.GetComponent<TextMeshProUGUI>(); t.text = "TACTICAL NETWORK  //  ONLINE"; t.enableWordWrapping = false; }
            var feature = Get(canvas, "Featured presentation");
            Rect(feature, new Vector2(0, 0), new Vector2(0, 0), new Vector2(58, 130), new Vector2(600, 160));
            Label(feature, "Eyebrow", "SIGNAL ARCHIVE  /  UNIT 001", 16, Accent, new Vector2(0, 0), new Vector2(600, 28));
            Label(feature, "Title", "SIGNAL SCOUT", 42, Bright, new Vector2(0, -36), new Vector2(600, 55));
            Label(feature, "Caption", "A reliable first link.", 18, Dim, new Vector2(0, -102), new Vector2(600, 30));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Overlink] Authored main-menu hierarchy.");
        }

        static void Group(Transform root, string name, float alpha)
        {
            var t = root.Find(name); if (t != null && t.TryGetComponent(out CanvasGroup group)) group.alpha = alpha;
        }
        static Transform Get(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) { t.gameObject.SetActive(true); return t; }
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false); return go.transform;
        }
        static void Rect(Transform t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = (RectTransform)t; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size; r.localScale = Vector3.one;
        }
        static void Text(Transform t, string text, float size, Color color, Vector2 pos, Vector2 bounds)
        {
            Rect(t, new Vector2(0, 1), new Vector2(0, 1), pos, bounds);
            var label = t.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
            label.color = color; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false; label.raycastTarget = false;
        }
        static void Label(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 bounds)
        {
            var t = Get(parent, name); if (!t.TryGetComponent<TextMeshProUGUI>(out _)) t.gameObject.AddComponent<TextMeshProUGUI>();
            Text(t, text, size, color, pos, bounds);
        }
        static void StyleCommand(Transform t, string title, string detail, int index)
        {
            Rect(t, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -index * 98), new Vector2(530, 86));
            foreach (Transform child in t) if (child.name != "Authored title" && child.name != "Authored detail" && child.name != "Accent") child.gameObject.SetActive(false);
            var image = t.GetComponent<Image>(); image.sprite = null; image.color = index == 0 ? Accent : Plate;
            var button = t.GetComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1); colors.pressedColor = new Color(.8f, .8f, .8f, 1); colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1); button.colors = colors;
            Label(t, "Authored title", title, 25, index == 0 ? Background : Bright, new Vector2(24, -12), new Vector2(470, 32));
            Label(t, "Authored detail", detail, 13, index == 0 ? Plate : Dim, new Vector2(24, -49), new Vector2(470, 22));
        }
        static void StyleSystem(Transform t, string text, float x)
        {
            Rect(t, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, 0), new Vector2(185, 48));
            foreach (Transform child in t) if (child.name != "Authored label") child.gameObject.SetActive(false);
            var image = t.GetComponent<Image>(); image.sprite = null; image.color = Plate;
            var button = t.GetComponent<Button>(); button.targetGraphic = image;
            Label(t, "Authored label", text, 17, Bright, new Vector2(18, -6), new Vector2(160, 36));
        }
    }
}
