using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.Net
{
    /// <summary>
    /// In-game log overlay so a standalone build can be diagnosed without the editor.
    /// Self-installs, so scenes need no setup. Toggle with F1 or the LOG button.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        const int MaxLines = 300;
        const float PanelHeight = 320f;
        const float LineHeight = 14f;
        static readonly int VisibleLines = Mathf.FloorToInt(PanelHeight / LineHeight) - 2;

        static DebugConsole instance;

        // Application.logMessageReceived can fire off the main thread, so both buffers
        // are guarded and drained once per frame in Update.
        readonly object gate = new object();
        readonly Queue<string> lines = new Queue<string>();
        readonly List<string> pending = new List<string>();

        TextMeshProUGUI output;
        GameObject panel;
        bool visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (instance != null) return;
            var go = new GameObject("DebugConsole");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugConsole>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            Application.logMessageReceived += OnLog;
            Enqueue("<color=#9ad>Console ready — F1 or the LOG button toggles this.</color>");
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (instance == this) instance = null;
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            string prefix;
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    prefix = "<color=#ff6b6b>[ERR]</color> ";
                    break;
                case LogType.Warning:
                    prefix = "<color=#ffd166>[WRN]</color> ";
                    break;
                default:
                    prefix = "";
                    break;
            }

            int newline = message.IndexOf('\n');
            string firstLine = newline >= 0 ? message.Substring(0, newline) : message;
            Enqueue(prefix + Escape(firstLine));
        }

        void Enqueue(string line)
        {
            lock (gate) pending.Add(line);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) SetVisible(!visible);

            lock (gate)
            {
                if (pending.Count == 0) return;
                foreach (string line in pending)
                {
                    lines.Enqueue(line);
                    while (lines.Count > MaxLines) lines.Dequeue();
                }
                pending.Clear();
            }
            RefreshText();
        }

        void RefreshText()
        {
            if (output == null) return;
            var buffer = new StringBuilder();
            int skip = Mathf.Max(0, lines.Count - VisibleLines);
            int index = 0;
            foreach (string line in lines)
            {
                if (index++ < skip) continue;
                if (buffer.Length > 0) buffer.Append('\n');
                buffer.Append(line);
            }
            output.text = buffer.ToString();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (panel != null) panel.SetActive(value);
        }

        public void Clear()
        {
            lock (gate) lines.Clear();
            RefreshText();
        }

        /// <summary>
        /// Log text is rendered as rich text, so angle brackets in a message would
        /// otherwise be swallowed as a formatting tag.
        /// </summary>
        static string Escape(string text)
        {
            return text.Replace("<", "‹").Replace(">", "›");
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("ConsoleCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGo.AddComponent<GraphicRaycaster>();

            panel = new GameObject("ConsolePanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.sizeDelta = new Vector2(0f, PanelHeight);

            var textGo = new GameObject("ConsoleText");
            textGo.transform.SetParent(panel.transform, false);
            output = textGo.AddComponent<TextMeshProUGUI>();
            output.fontSize = 13f;
            output.lineSpacing = LineHeight;
            output.alignment = TextAlignmentOptions.BottomLeft;
            output.color = Color.white;
            output.richText = true;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 44f);
            textRt.offsetMax = new Vector2(-12f, -8f);

            var clearGo = MakeButton(panel.transform, "ConsoleClear", "Clear", 14);
            var clearRt = clearGo.GetComponent<RectTransform>();
            clearRt.anchorMin = new Vector2(1f, 0f);
            clearRt.anchorMax = new Vector2(1f, 0f);
            clearRt.pivot = new Vector2(1f, 0f);
            clearRt.sizeDelta = new Vector2(90f, 30f);
            clearRt.anchoredPosition = new Vector2(-10f, 7f);
            clearGo.GetComponent<Button>().onClick.AddListener(Clear);

            var toggleGo = MakeButton(canvasGo.transform, "ConsoleToggle", "LOG", 16);
            var toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(1f, 1f);
            toggleRt.anchorMax = new Vector2(1f, 1f);
            toggleRt.pivot = new Vector2(1f, 1f);
            toggleRt.sizeDelta = new Vector2(90f, 34f);
            toggleRt.anchoredPosition = new Vector2(-10f, -10f);
            toggleGo.GetComponent<Button>().onClick.AddListener(() => SetVisible(!visible));

            panel.SetActive(false);
        }

        static GameObject MakeButton(Transform parent, string name, string label, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.18f);
            go.AddComponent<Button>();
            MakeLabel(go.transform, label, fontSize);
            return go;
        }

        static void MakeLabel(Transform parent, string text, int fontSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
