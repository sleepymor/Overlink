using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Overlink.Match;
using Overlink.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// Completes the serialized Gameplay HUD contract from the existing authored scene hierarchy.
    /// This is an editor authoring operation: runtime code only binds the resulting references.
    /// </summary>
    public static class AuthorGameplayHud
    {
        const string ScenePath = "Assets/Scenes/Gameplay.unity";

        static readonly string[] RailNames =
        {
            "EndPhaseButton", "DoneBlockingButton", "SwingCoreButton", "SwingNode1Button",
            "SwingNode2Button", "SwingNode3Button", "SwingNode4Button", "ActivateButton",
            "EndTurnButton", "ConcedeButton", "StateButton", "PilePrevButton", "PileNextButton",
            "RerollLineupButton", "ConfirmLineupButton"
        };

        [MenuItem("Overlink/UI/Author Gameplay HUD")]
        public static void Author()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = FindRoot(BattlefieldHud.RootName);
            if (root == null)
            {
                Debug.LogError("[AuthorGameplayHud] Missing root: " + BattlefieldHud.RootName);
                return;
            }

            BattlefieldHud hud = root.GetComponent<BattlefieldHud>();
            if (hud == null)
            {
                Debug.LogError("[AuthorGameplayHud] Root has no BattlefieldHud component.");
                return;
            }

            EnsureEventSystem();

            Transform phase = Required(root.transform, "PhaseControls");
            var buttons = new Button[RailNames.Length];
            var labels = new TextMeshProUGUI[RailNames.Length];
            for (int i = 0; i < RailNames.Length; i++)
            {
                Transform button = Required(phase, RailNames[i]);
                buttons[i] = button.GetComponent<Button>();
                labels[i] = Required(button, "Label").GetComponent<TextMeshProUGUI>();
                if (buttons[i] == null || labels[i] == null)
                    throw new InvalidOperationException("Rail control is missing Button or Label: " + RailNames[i]);
            }

            Transform banner = Required(root.transform, "Banner");
            TextMeshProUGUI bannerText = Required(banner, "BannerText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI bannerTagLeft = EnsureText(banner, "TagLeft", TextAlignmentOptions.MidlineRight);
            TextMeshProUGUI bannerTagRight = EnsureText(banner, "TagRight", TextAlignmentOptions.MidlineLeft);
            ConfigureTag(bannerTagLeft, true);
            ConfigureTag(bannerTagRight, false);

            Transform hint = Required(root.transform, "ControlsHint");
            TextMeshProUGUI hintText = Required(hint, "ControlsHintText").GetComponent<TextMeshProUGUI>();

            Transform opponentChip = Required(root.transform, "OpponentChip");
            TextMeshProUGUI opponentTitle = Required(opponentChip, "OpponentChipText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI opponentBody = Required(opponentChip, "Body").GetComponent<TextMeshProUGUI>();
            Transform playerChip = Required(root.transform, "PlayerChip");
            TextMeshProUGUI playerTitle = Required(playerChip, "PlayerChipText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI playerBody = Required(playerChip, "Body").GetComponent<TextMeshProUGUI>();
            UiBar opponentCore = EnsureMeter(opponentChip, "CoreMeter", new Color(0.89f, 0.57f, 0.36f), true);
            UiBar opponentNp = EnsureMeter(opponentChip, "NpMeter", new Color(0.16f, 0.85f, 0.96f), false);
            UiBar playerCore = EnsureMeter(playerChip, "CoreMeter", new Color(0.16f, 0.85f, 0.96f), true);
            UiBar playerNp = EnsureMeter(playerChip, "NpMeter", new Color(0.16f, 0.85f, 0.96f), false);

            Transform nodeList = Required(root.transform, "NodeList");
            var nodeRows = new TextMeshProUGUI[4];
            for (int i = 0; i < nodeRows.Length; i++)
                nodeRows[i] = Required(nodeList, "NodeRow" + i).GetComponent<TextMeshProUGUI>();

            Transform notice = Required(root.transform, "Notice");
            TextMeshProUGUI noticeText = Required(notice, "NoticeText").GetComponent<TextMeshProUGUI>();
            Transform result = Required(root.transform, "Result");
            TextMeshProUGUI resultText = Required(result, "ResultText").GetComponent<TextMeshProUGUI>();

            Transform panel = Required(root.transform, "LogPanel");
            TextMeshProUGUI header = Required(panel, "LogHeader").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI body = Required(panel, "LogText").GetComponent<TextMeshProUGUI>();

            Transform reader = Required(root.transform, "ReaderPanel");
            Transform readerCard = Required(reader, "CardFace");
            TextMeshProUGUI readerTitle = Required(readerCard, "ReaderTitle").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI readerBody = Required(readerCard, "ReaderBody").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI readerFoot = Required(readerCard, "ReaderFoot").GetComponent<TextMeshProUGUI>();
            Required(readerCard, "ReaderHint").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI inspectionDetails = EnsureInspectionDetails(reader);

            SerializedObject so = new SerializedObject(hud);
            Set(so, "phaseControls", phase);
            SetArray(so, "railButtonRefs", buttons);
            SetArray(so, "railLabelRefs", labels);
            Set(so, "banner", banner);
            Set(so, "bannerText", bannerText);
            Set(so, "bannerTagLeft", bannerTagLeft);
            Set(so, "bannerTagRight", bannerTagRight);
            Set(so, "hint", hint);
            Set(so, "hintText", hintText);
            Set(so, "opponentChip", opponentChip);
            Set(so, "opponentTitle", opponentTitle);
            Set(so, "opponentBody", opponentBody);
            Set(so, "coreMeterOpponent", opponentCore);
            Set(so, "npMeterOpponent", opponentNp);
            Set(so, "playerChip", playerChip);
            Set(so, "playerTitle", playerTitle);
            Set(so, "playerBody", playerBody);
            Set(so, "coreMeterPlayer", playerCore);
            Set(so, "npMeterPlayer", playerNp);
            Set(so, "nodeList", nodeList);
            SetArray(so, "nodeRows", nodeRows);
            Set(so, "notice", notice);
            Set(so, "noticeText", noticeText);
            Set(so, "result", result);
            Set(so, "resultText", resultText);
            Set(so, "panel", panel);
            Set(so, "header", header);
            Set(so, "body", body);
            Set(so, "reader", reader);
            Set(so, "readerCard", readerCard);
            Set(so, "readerTitle", readerTitle);
            Set(so, "readerBody", readerBody);
            Set(so, "readerFoot", readerFoot);
            Set(so, "inspectionDetails", inspectionDetails);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AuthorGameplayHud] Serialized Gameplay HUD authored and saved.");
        }

        static GameObject FindRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static Transform Required(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found == null) throw new InvalidOperationException("Missing authored Gameplay HUD object: " + parent.name + "/" + name);
            return found;
        }

        static TextMeshProUGUI EnsureText(Transform parent, string name, TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static void ConfigureTag(TextMeshProUGUI tag, bool left)
        {
            RectTransform rect = tag.rectTransform;
            rect.anchorMin = new Vector2(left ? 0f : 0.70f, 0f);
            rect.anchorMax = new Vector2(left ? 0.30f : 1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            tag.fontSize = 13f;
            tag.fontStyle = FontStyles.Bold;
            tag.color = new Color(0.16f, 0.85f, 0.96f, 1f);
        }

        static UiBar EnsureMeter(Transform chip, string name, Color accent, bool ghost)
        {
            Transform existing = chip.Find(name);
            GameObject root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) root.transform.SetParent(chip, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            if (existing == null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(-24f, 4f);
                rect.anchoredPosition = new Vector2(0f, ghost ? -6f : -12f);
            }
            Image track = EnsureImage(root.transform, "Track", new Color(0.16f, 0.20f, 0.28f, 0.92f), false);
            Image ghostImage = ghost ? EnsureImage(root.transform, "Ghost", Color.Lerp(track.color, accent, 0.45f), true) : null;
            Image fill = EnsureImage(root.transform, "Fill", accent, true);
            UiBar bar = root.GetComponent<UiBar>() ?? root.AddComponent<UiBar>();
            bar.Bind(fill, ghostImage);
            return bar;
        }

        static Image EnsureImage(Transform parent, string name, Color color, bool filled)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            if (existing == null) go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            if (existing == null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.sprite = Resources.Load<Sprite>("VFX/UiWhite");
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
            }
            return image;
        }

        static TextMeshProUGUI EnsureInspectionDetails(Transform reader)
        {
            Transform panel = reader.Find("InspectionDetails");
            GameObject panelGo = panel != null ? panel.gameObject : new GameObject("InspectionDetails", typeof(RectTransform), typeof(Image));
            if (panel == null) panelGo.transform.SetParent(reader, false);
            RectTransform rect = panelGo.GetComponent<RectTransform>();
            if (panel == null)
            {
                rect.anchorMin = new Vector2(0.73f, 0.25f);
                rect.anchorMax = new Vector2(0.98f, 0.75f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            Image image = panelGo.GetComponent<Image>();
            image.color = new Color(0.07f, 0.09f, 0.15f, 0.98f);
            image.raycastTarget = false;
            Transform textTransform = panelGo.transform.Find("Text");
            GameObject textGo = textTransform != null ? textTransform.gameObject : new GameObject("Text", typeof(RectTransform));
            if (textTransform == null) textGo.transform.SetParent(panelGo.transform, false);
            TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>() ?? textGo.AddComponent<TextMeshProUGUI>();
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 18f);
            textRect.offsetMax = new Vector2(-18f, -18f);
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 22f;
            panelGo.SetActive(false);
            return text;
        }

        static void Set(SerializedObject so, string property, UnityEngine.Object value)
        {
            so.FindProperty(property).objectReferenceValue = value;
        }

        static void SetArray<T>(SerializedObject so, string property, T[] values) where T : UnityEngine.Object
        {
            SerializedProperty array = so.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>(true) != null) return;
            GameObject go = new GameObject("Gameplay EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Author Gameplay EventSystem");
        }
    }
}
