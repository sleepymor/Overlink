using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>Authors a draft editable pack inside the hidden Signal Bay hierarchy.</summary>
    public static class AuthorSignalBayPack
    {
        static readonly Color Cyan = new Color32(41, 223, 242, 255);
        static readonly Color Ink = new Color32(6, 15, 24, 255);
        static readonly Color Bright = new Color32(220, 236, 241, 255);

        [MenuItem("Overlink/Scenes/Refine Signal Bay Loyalty Accents")]
        public static void RefineLoyaltyAccents()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            var milestone = bay != null ? bay.Find("Milestone Label")?.GetComponent<TMP_Text>() : null;
            var reward = bay != null ? bay.Find("Next Reward")?.GetComponent<TMP_Text>() : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || milestone == null || reward == null)
            {
                Debug.LogError("Open Gacha.unity in edit mode with the Signal Bay draft before refining loyalty accents.");
                return;
            }
            Undo.RecordObjects(new Object[] { milestone, reward }, "Refine Signal Bay loyalty accents");
            milestone.text = "EVERY 100 PULLS <color=#FFB43B>→ 1 FREE PACK</color>";
            reward.text = "NEXT REWARD  //  <color=#FFB43B>FREE SIGNAL PACK</color>";
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable loyalty reward accents updated; save the scene to keep them.");
        }

        [MenuItem("Overlink/Scenes/Author Signal Bay Ambient Glow")]
        public static void AuthorAmbientGlow()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            var showcase = bay != null ? bay.Find("Selected Pack Showcase") : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || showcase == null)
            {
                Debug.LogError("Open Gacha.unity in edit mode before authoring the ambient glow.");
                return;
            }
            if (showcase.Find("Editable ambient glow") != null)
            {
                Debug.Log("[Overlink] Ambient glow already exists; leaving edits intact.");
                return;
            }
            var glow = Box(showcase, "Editable ambient glow", Vector2.zero, new Vector2(660, 532));
            Undo.RegisterCreatedObjectUndo(glow.gameObject, "Author Signal Bay ambient glow");
            var graphic = glow.gameObject.AddComponent<Overlink.UI.SignalBayGlow>();
            graphic.color = new Color32(35, 161, 174, 100);
            graphic.raycastTarget = false;
            glow.SetAsFirstSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable non-interactive ambient glow added; save the scene to keep it.");
        }

        [MenuItem("Overlink/Scenes/Refine Signal Bay Purchase Labels")]
        public static void RefinePurchaseLabels()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || bay == null)
            {
                Debug.LogError("Open Gacha.unity in edit mode before refining purchase labels.");
                return;
            }
            var one = bay.Find("Figma Open One/Text")?.GetComponent<TMP_Text>();
            var ten = bay.Find("Figma Open Ten/Text")?.GetComponent<TMP_Text>();
            var stale = new[] { bay.Find("Single Cost"), bay.Find("Ten Cost"), bay.Find("Single Detail"), bay.Find("Ten Detail") };
            if (one == null || ten == null || System.Array.Exists(stale, item => item == null))
            {
                Debug.LogError("[Overlink] Purchase label elements missing; no changes made.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(bay.gameObject, "Refine Signal Bay purchase labels");
            one.text = "OPEN 1 PACK\n<size=10>100 SIGNAL CREDITS  /  5 CARDS</size>";
            ten.text = "OPEN 10 PACKS\n<size=10>1,000 SIGNAL CREDITS  /  50 CARDS</size>";
            one.fontSize = ten.fontSize = 17;
            one.raycastTarget = ten.raycastTarget = false;
            foreach (var item in stale) item.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Purchase labels now show offline demo prices and card counts; save the scene.");
        }

        [MenuItem("Overlink/Scenes/Author Signal Bay Pack Draft")]
        public static void Author()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Gacha.unity")
            {
                Debug.LogError("Open Gacha.unity before authoring the pack draft.");
                return;
            }
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            if (bay == null) { Debug.LogError("Signal Bay hierarchy missing."); return; }
            if (bay.Find("Editable Pack Draft") != null)
            {
                Debug.Log("[Overlink] Editable pack draft already exists; leaving edits intact.");
                return;
            }

            foreach (string name in new[] { "Pack Foil Sleeve", "Pack Aperture", "Pack Sample", "Pack Identifier", "Pack Brand" })
            {
                var old = bay.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }

            var root = Box(bay, "Editable Pack Draft", new Vector2(-72, 62), new Vector2(280, 420));
            root.localRotation = Quaternion.Euler(0, 0, 5);
            var insertBefore = bay.Find("Selected Label");
            if (insertBefore != null) root.SetSiblingIndex(insertBefore.GetSiblingIndex());
            Panel(root, "Foil rim", Vector2.zero, new Vector2(280, 420), new Color32(107, 188, 201, 255));
            Panel(root, "Foil face", Vector2.zero, new Vector2(266, 405), new Color32(30, 67, 79, 255));
            AddFoilDetail(root);
            Panel(root, "Left sheen", new Vector2(-126, 0), new Vector2(8, 395), new Color32(37, 132, 148, 255));
            Panel(root, "Right sheen", new Vector2(123, 0), new Vector2(14, 395), new Color32(73, 132, 144, 255));
            Panel(root, "Top crimp", new Vector2(0, 190), new Vector2(258, 18), Ink);
            Panel(root, "Bottom crimp", new Vector2(0, -190), new Vector2(258, 18), Ink);
            for (int i = 0; i < 30; i++)
            {
                float x = -120 + i * 8.25f;
                Panel(root, "Top ridge " + i.ToString("00"), new Vector2(x, 190), new Vector2(1, 14), new Color32(51, 86, 95, 255));
                Panel(root, "Bottom ridge " + i.ToString("00"), new Vector2(x, -190), new Vector2(1, 14), new Color32(51, 86, 95, 255));
            }
            Label(root, "Brand", "OVERLINK", 28, Bright, new Vector2(0, 150), new Vector2(232, 34));
            Label(root, "Network", "TACTICAL NETWORK // SA–01", 9, Cyan, new Vector2(0, 123), new Vector2(232, 16));
            Panel(root, "Illustration aperture", new Vector2(0, 18), new Vector2(232, 180), Ink);
            AddApertureCorners(root.Find("Illustration aperture"));
            Label(root, "Illustration placeholder", "[IMAGE SAMPLE]", 16, new Color32(144, 207, 219, 255), new Vector2(0, 18), new Vector2(210, 28), TextAlignmentOptions.Center);
            Label(root, "Pack title", "SIGNAL\nARCHIVE", 28, Bright, new Vector2(0, -109), new Vector2(232, 62));
            Label(root, "Contents", "5 CARDS / CORE COLLECTION", 9, Cyan, new Vector2(0, -155), new Vector2(232, 18));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Overlink] Editable Signal Bay pack draft authored; approved backdrop retained.");
        }

        [MenuItem("Overlink/Scenes/Detail Signal Bay Pack Aperture")]
        public static void DetailAperture()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity")
            {
                Debug.LogError("Open Gacha.unity in edit mode before detailing the pack aperture.");
                return;
            }
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var aperture = screen != null ? screen.transform.Find("Figma Signal Bay/Editable Pack Draft/Illustration aperture") : null;
            if (aperture == null)
            {
                Debug.LogError("Signal Bay pack aperture missing.");
                return;
            }
            if (aperture.Find("Aperture corner markers") != null)
            {
                Debug.Log("[Overlink] Aperture corner markers already exist; leaving edits intact.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(aperture.gameObject, "Detail Signal Bay pack aperture");
            AddApertureCorners(aperture);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable aperture corners added; save the scene to keep them.");
        }

        static void AddApertureCorners(Transform aperture)
        {
            var markers = Box(aperture, "Aperture corner markers", Vector2.zero, new Vector2(232, 180));
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                string corner = (y > 0 ? "Top " : "Bottom ") + (x < 0 ? "left" : "right");
                Panel(markers, corner + " horizontal", new Vector2(x * 108, y * 89.25f), new Vector2(16, 1.5f), Cyan);
                Panel(markers, corner + " vertical", new Vector2(x * 115.25f, y * 82), new Vector2(1.5f, 16), Cyan);
            }
        }

        [MenuItem("Overlink/Scenes/Detail Signal Bay Display Frame")]
        public static void DetailDisplayFrame()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity")
            {
                Debug.LogError("Open Gacha.unity in edit mode before detailing the display frame.");
                return;
            }
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            var pack = bay != null ? bay.Find("Editable Pack Draft") as RectTransform : null;
            if (pack == null) { Debug.LogError("Signal Bay pack draft missing."); return; }
            if (bay.Find("Editable display frame") != null)
            {
                Debug.Log("[Overlink] Editable display frame already exists; leaving edits intact.");
                return;
            }

            var frame = Box(bay, "Editable display frame", pack.anchoredPosition, new Vector2(600, 600));
            Undo.RegisterCreatedObjectUndo(frame.gameObject, "Create Signal Bay display frame");
            frame.SetSiblingIndex(pack.GetSiblingIndex());
            Color wire = new Color32(70, 135, 151, 65);
            Vector2 leftTop = new Vector2(-294, 55), top = new Vector2(0, 170), rightTop = new Vector2(294, 55);
            Vector2 leftBottom = new Vector2(-294, -165), bottom = new Vector2(0, -280), rightBottom = new Vector2(294, -165);
            Segment(frame, "Upper left", leftTop, top, wire);
            Segment(frame, "Upper right", top, rightTop, wire);
            Segment(frame, "Left edge", leftTop, leftBottom, wire);
            Segment(frame, "Right edge", rightTop, rightBottom, wire);
            Segment(frame, "Lower left", leftBottom, bottom, wire);
            Segment(frame, "Lower right", bottom, rightBottom, wire);
            Segment(frame, "Left diagonal", leftTop, bottom, wire);
            Segment(frame, "Right diagonal", rightTop, bottom, wire);
            Segment(frame, "Center axis", top, bottom, wire);
            Ellipse(frame, "Outer platform", new Vector2(0, -246), new Vector2(224, 30), new Color32(21, 154, 171, 125));
            Ellipse(frame, "Inner platform", new Vector2(0, -240), new Vector2(174, 16), new Color32(21, 154, 171, 160));
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable display frame added behind pack; save the scene to keep it.");
        }

        static void Segment(Transform parent, string name, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            var rect = Box(parent, name, (from + to) * .5f, new Vector2(delta.magnitude, 1f));
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void Ellipse(Transform parent, string name, Vector2 center, Vector2 radius, Color color)
        {
            const int count = 48;
            for (int i = 0; i < count; i++)
            {
                float a = i * 2f * Mathf.PI / count, b = (i + 1) * 2f * Mathf.PI / count;
                Segment(parent, name + " " + i.ToString("00"),
                    center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y),
                    center + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), color);
            }
        }

        [MenuItem("Overlink/Scenes/Refine Signal Bay Identity")]
        public static void RefineIdentity()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var account = screen != null ? screen.transform.Find("Figma Signal Bay/Account") : null;
            var text = account != null ? account.GetComponent<TMP_Text>() : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || text == null)
            {
                Debug.LogError("Open Gacha.unity in edit mode with its editable Account node present.");
                return;
            }
            Undo.RecordObjects(new Object[] { account.gameObject, account, text }, "Refine Signal Bay identity");
            text.text = "Player";
            text.richText = false;
            text.raycastTarget = false;
            text.color = Bright;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Right;
            var rect = (RectTransform)account;
            rect.anchoredPosition = new Vector2(648, 396);
            rect.sizeDelta = new Vector2(180, 24);
            account.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable bay identity uses a safe fallback; save the scene.");
        }

        [MenuItem("Overlink/Scenes/Mark Unavailable Signal Bay Packs")]
        public static void MarkUnavailablePacks()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || bay == null)
            {
                Debug.LogError("Open Gacha.unity in edit mode before marking unavailable packs.");
                return;
            }
            var count = bay.Find("Pack Count")?.GetComponent<TMP_Text>();
            var second = bay.Find("Pack Choice 2 Price")?.GetComponent<TMP_Text>();
            var third = bay.Find("Pack Choice 3 Price")?.GetComponent<TMP_Text>();
            if (count == null || second == null || third == null)
            {
                Debug.LogError("[Overlink] Pack choice text missing; no changes made.");
                return;
            }
            Undo.RecordObjects(new Object[] { count, second, third }, "Mark unavailable packs");
            count.text = "1 SIGNAL AVAILABLE";
            second.text = third.text = "NOT AVAILABLE";
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Unimplemented pack choices marked unavailable; save the scene to keep the change.");
        }

        [MenuItem("Overlink/Scenes/Refine Signal Bay Pack Information")]
        public static void RefinePackInformation()
        {
            var scene = SceneManager.GetActiveScene();
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity" || bay == null)
            {
                Debug.LogError("Open Gacha.unity in edit mode before refining pack information.");
                return;
            }
            if (bay.Find("Editable information accents") != null)
            {
                Debug.Log("[Overlink] Information accents already exist; leaving edits intact.");
                return;
            }
            var panel = bay.Find("Pack Info Panel")?.GetComponent<Image>();
            var title = bay.Find("Info Title")?.GetComponent<TMP_Text>();
            var description = bay.Find("Info Description")?.GetComponent<TMP_Text>();
            var composition = bay.Find("Composition")?.GetComponent<TMP_Text>();
            var body = bay.Find("Composition Body")?.GetComponent<TMP_Text>();
            var contents = bay.Find("Contents Rates")?.GetComponent<TMP_Text>();
            if (panel == null || title == null || description == null || composition == null || body == null || contents == null)
            {
                Debug.LogError("[Overlink] Pack information element missing; no changes made.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(bay.gameObject, "Refine Signal Bay pack information");
            panel.color = new Color32(7, 18, 28, 60);
            ((RectTransform)title.transform).sizeDelta = new Vector2(460, 46);
            title.fontSize = 38;
            ((RectTransform)description.transform).sizeDelta = new Vector2(460, 44);
            description.color = Bright;
            composition.text = "5 CARDS";
            composition.color = Cyan;
            composition.fontSize = 21;
            var compositionRect = (RectTransform)composition.transform;
            compositionRect.anchoredPosition = new Vector2(336, 156);
            compositionRect.sizeDelta = new Vector2(132, 28);
            body.text = "UNITS / RELAYS / ABILITIES\nDETERMINISTIC OFFLINE PACKS";
            body.fontSize = 12;
            var bodyRect = (RectTransform)body.transform;
            bodyRect.anchoredPosition = new Vector2(568, 156);
            bodyRect.sizeDelta = new Vector2(300, 44);
            contents.text = "PACK CONTENTS  /  OFFLINE DEMO";
            contents.color = Bright;
            var contentsRect = (RectTransform)contents.transform;
            contentsRect.anchoredPosition = new Vector2(442, 92);
            contentsRect.sizeDelta = new Vector2(344, 20);
            var accents = Box(bay, "Editable information accents", Vector2.zero, new Vector2(1600, 900));
            Undo.RegisterCreatedObjectUndo(accents.gameObject, "Create Signal Bay information accents");
            accents.SetSiblingIndex(composition.transform.GetSiblingIndex());
            var muted = new Color32(135, 157, 171, 255);
            Label(accents, "Cards caption", "IN EVERY PACK", 9, muted, new Vector2(336, 132), new Vector2(132, 14));
            Panel(accents, "Composition separator", new Vector2(412, 155), new Vector2(1, 36), new Color32(39, 64, 77, 255));
            Panel(accents, "Composition lower rule", new Vector2(500, 120), new Vector2(440, 1), new Color32(39, 64, 77, 255));
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable pack information refined; save the scene to keep the change.");
        }

        [MenuItem("Overlink/Scenes/Refine Signal Bay Pack Choices")]
        public static void RefinePackChoices()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity")
            {
                Debug.LogError("Open Gacha.unity in edit mode before refining pack choices.");
                return;
            }
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var bay = screen != null ? screen.transform.Find("Figma Signal Bay") : null;
            if (bay == null || bay.Find("Pack Choice 1/Editable choice accents") != null)
            {
                Debug.LogWarning("[Overlink] Pack choices missing or already refined; leaving existing edits intact.");
                return;
            }
            var panel = bay.Find("Pack Selection Panel")?.GetComponent<Image>();
            var choices = new Image[3];
            var labels = new TMP_Text[3];
            var prices = new TMP_Text[3];
            var meta = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                string prefix = "Pack Choice " + (i + 1);
                choices[i] = bay.Find(prefix)?.GetComponent<Image>();
                labels[i] = bay.Find(prefix + " Label")?.GetComponent<TMP_Text>();
                prices[i] = bay.Find(prefix + " Price")?.GetComponent<TMP_Text>();
                meta[i] = bay.Find(prefix + " Meta")?.GetComponent<TMP_Text>();
                if (choices[i] == null || labels[i] == null || prices[i] == null || meta[i] == null)
                {
                    Debug.LogError("[Overlink] Pack choice element missing; no changes made.");
                    return;
                }
            }
            if (panel == null) { Debug.LogError("[Overlink] Pack selection panel missing; no changes made."); return; }
            Undo.RegisterFullObjectHierarchyUndo(bay.gameObject, "Refine Signal Bay pack choices");
            panel.color = new Color32(7, 18, 28, 60);
            Color muted = new Color32(135, 157, 171, 255);
            for (int i = 0; i < 3; i++)
            {
                bool selected = i == 0;
                choices[i].color = selected ? new Color32(13, 51, 61, 240) : new Color32(8, 20, 30, 220);
                labels[i].color = selected ? Cyan : Bright;
                labels[i].fontSize = 16;
                prices[i].color = muted;
                meta[i].color = muted;
                foreach (var text in new[] { labels[i], prices[i] })
                {
                    var rect = (RectTransform)text.transform;
                    rect.anchoredPosition += new Vector2(46, 0);
                    rect.sizeDelta = new Vector2(145, rect.sizeDelta.y);
                }
                var accents = Box(choices[i].transform, "Editable choice accents", Vector2.zero, new Vector2(232, 92));
                Undo.RegisterCreatedObjectUndo(accents.gameObject, "Create Signal Bay choice accents");
                Color edge = selected ? Cyan : new Color32(95, 130, 145, 255);
                Panel(accents, "Bottom rule", new Vector2(0, -45.5f), new Vector2(232, 1), edge);
                if (selected) Panel(accents, "Selected edge", new Vector2(-115, 0), new Vector2(2, 92), Cyan);
                Vector2 icon = new Vector2(-86, 12);
                Panel(accents, "Icon backing", icon, new Vector2(36, 50), new Color32(27, 53, 64, 255));
                Panel(accents, "Icon top", icon + new Vector2(0, 24), new Vector2(36, 1), edge);
                Panel(accents, "Icon bottom", icon + new Vector2(0, -24), new Vector2(36, 1), edge);
                Panel(accents, "Icon left", icon + new Vector2(-17.5f, 0), new Vector2(1, 50), edge);
                Panel(accents, "Icon right", icon + new Vector2(17.5f, 0), new Vector2(1, 50), edge);
                Label(accents, "Icon code", i == 0 ? "SA / 01" : i == 1 ? "FP / 01" : "RN / 01", 7, edge,
                    icon + new Vector2(0, -15), new Vector2(34, 10), TextAlignmentOptions.Center);
                Label(accents, "Icon mark", i == 0 ? "+" : i == 1 ? "O" : "#", 17, edge,
                    icon + new Vector2(0, 4), new Vector2(30, 25), TextAlignmentOptions.Center);
                if (selected) Label(accents, "Active status", "ACTIVE", 8, Cyan,
                    new Vector2(88, -27), new Vector2(48, 12), TextAlignmentOptions.Right);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable pack-choice accents and typography refined; save the scene to keep them.");
        }

        [MenuItem("Overlink/Scenes/Detail Signal Bay Pack Draft")]
        public static void DetailDraft()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gacha.unity")
            {
                Debug.LogError("Open Gacha.unity in edit mode before detailing the pack draft.");
                return;
            }
            var screen = GameObject.Find("Offline Pack Canvas/PackScreen");
            var root = screen != null ? screen.transform.Find("Figma Signal Bay/Editable Pack Draft") : null;
            if (root == null || root.Find("Foil face") == null)
            {
                Debug.LogError("Signal Bay pack draft or foil face missing.");
                return;
            }
            if (root.Find("Foil face/Editable foil shading") != null)
            {
                Debug.Log("[Overlink] Foil detail already exists; leaving edits intact.");
                return;
            }
            AddFoilDetail(root);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Overlink] Editable foil shading added; save the scene to keep it.");
        }

        // Separate narrow image strips approximate the reference's horizontal metallic falloff.
        // They remain editable uGUI elements and never participate in hit testing.
        static void AddFoilDetail(Transform root)
        {
            var face = root.Find("Foil face");
            var shading = Box(face, "Editable foil shading", Vector2.zero, new Vector2(266, 405));
            const int strips = 28;
            for (int i = 0; i < strips; i++)
            {
                float t = (i + .5f) / strips;
                Color dark = new Color32(14, 39, 51, 255);
                Color edge = new Color32(101, 161, 171, 255);
                float shine = Mathf.Pow(Mathf.Abs(2f * t - 1f), 2.4f);
                Panel(shading, "Foil band " + i.ToString("00"),
                    new Vector2(-133f + (i + .5f) * 266f / strips, 0),
                    new Vector2(266f / strips + .2f, 405), Color.Lerp(dark, edge, shine));
            }
        }

        static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = Box(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void Label(Transform parent, string name, string text, float size, Color color,
            Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Box(parent, name, position, dimensions);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
        }
    }
}
