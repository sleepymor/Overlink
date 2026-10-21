using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// The handful of UGUI primitives the login and account screens are assembled from.
    /// <para>
    /// Both build their widgets in code rather than shipping a prefab, so the awkward parts of doing
    /// that are worth centralising: <c>TMP_InputField</c> only wires its caret and text callbacks in
    /// <c>OnEnable</c> (so a field must be constructed inactive and activated last), and a <c>Button</c>
    /// on the ColorTint transition replaces its graphic's colour outright instead of multiplying it.
    /// Getting either wrong produces a control that looks right and silently does nothing, which is
    /// exactly the kind of bug a shared helper prevents.
    /// </para>
    /// </summary>
    static class UiKit
    {
        public const float RowHeight = 44f;
        public const float ButtonHeight = 48f;

        public static readonly Color Background = new Color(0.035f, 0.043f, 0.070f);
        public static readonly Color Card = new Color(0.086f, 0.102f, 0.157f);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color InputBackground = new Color(0.043f, 0.051f, 0.082f);
        public static readonly Color Primary = new Color(0.180f, 0.478f, 0.831f);
        public static readonly Color Google = new Color(0.953f, 0.953f, 0.953f);
        public static readonly Color Ghost = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Danger = new Color(0.784f, 0.290f, 0.267f);
        public static readonly Color TextBright = new Color(0.925f, 0.941f, 0.965f);
        public static readonly Color TextDim = new Color(0.612f, 0.655f, 0.722f);
        public static readonly Color TextError = new Color(0.949f, 0.494f, 0.443f);
        public static readonly Color Accent = new Color(0.478f, 0.706f, 0.976f);

        public enum Style { Primary, Google, Ghost, Danger }

        /// <summary>A child RectTransform, positioned at the origin of its parent.</summary>
        public static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>A child that fills its parent, with an Image for a panel or a scrim.</summary>
        public static GameObject Filled(string name, Transform parent, Color color)
        {
            var go = Child(name, parent);
            Stretch(go.GetComponent<RectTransform>()).color = color;
            return go;
        }

        public static Image Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect.gameObject.AddComponent<Image>();
        }

        /// <summary>
        /// A container that fills its parent and stacks its children vertically. Rows size themselves
        /// from their <c>LayoutElement</c>, so nothing here force-expands.
        /// <para>
        /// The graphic is made explicitly transparent: <c>Image</c> defaults to opaque white, so a stack
        /// laid over a card would paint over it and the card's colour would never be seen.
        /// </para>
        /// </summary>
        public static GameObject Stack(string name, Transform parent, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = Child(name, parent);
            Stretch(go.GetComponent<RectTransform>()).color = new Color(0f, 0f, 0f, 0f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(26, 26, 24, 24);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go;
        }

        /// <summary>
        /// A text row. Pass a positive <paramref name="height"/> for copy that must wrap inside a
        /// budgeted box; without one the row sizes itself from the font, which keeps a vertical stack
        /// from leaving gaps under short labels.
        /// </summary>
        public static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color,
            TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal, float height = 0f)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            float resolved = height > 0f ? height : size + 10f;
            rect.sizeDelta = new Vector2(0f, resolved);

            var tmp = ConfigureText(go.AddComponent<TextMeshProUGUI>(), value, size, color);
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.enableWordWrapping = height > 0f;

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = resolved;
            // A vertical stack sizes children from their preferred width, and a text row has none worth
            // reporting until TMP has measured it. Claiming the flexible axis is what makes the row span
            // the panel so centring and word wrapping behave; without it a wrapped paragraph collapses to
            // its intrinsic single-line width.
            element.flexibleWidth = 1f;
            return tmp;
        }

        /// <summary>
        /// A text that fills whatever already positions it — a button caption, an input's content — and
        /// therefore claims no layout slot of its own.
        /// </summary>
        public static TextMeshProUGUI Label(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);

            var tmp = ConfigureText(go.AddComponent<TextMeshProUGUI>(), value, size, color);
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        public static GameObject Button(string name, Transform parent, string label, Style style,
            float fontSize = 17f)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, ButtonHeight);
            // See Text(): a layout group sizes children from their preferred width, which a button has
            // no intrinsic claim to, so the flexible axis is claimed explicitly. Callers that want a
            // fixed-width button (inside a horizontal row) clear it again via FlexibleWidth.
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = ButtonHeight;
            element.flexibleWidth = 1f;

            Color normal = style switch
            {
                Style.Primary => Primary,
                Style.Google => Google,
                Style.Danger => Danger,
                _ => Ghost
            };
            Color foreground = style switch
            {
                Style.Google => new Color(0.25f, 0.25f, 0.25f),
                Style.Ghost => Accent,
                _ => TextBright
            };

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            // ColorTint writes the state colour straight onto the graphic, so per-state brightness
            // belongs here and not on image.color — Selectable overwrites that the moment it enables.
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Tint(normal, 1.14f),
                pressedColor = Tint(normal, 0.82f),
                selectedColor = normal,
                disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.06f
            };

            Label(go.transform, "Label", label, style == Style.Ghost ? 14f : fontSize, foreground)
                .fontStyle = FontStyles.Bold;
            return go;
        }

        /// <summary>
        /// A single-line text input, built the way <c>TMP_InputField</c> requires: the object stays
        /// inactive while the caret host, mask and text references are put in place, and only then is
        /// it parented and activated.
        /// </summary>
        public static TMP_InputField InputField(string name, Transform parent, string placeholder, int charLimit,
            TMP_InputField.ContentType contentType)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RowHeight);

            go.AddComponent<Image>().color = InputBackground;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.flexibleWidth = 1f;

            var area = Child("Text Area", go.transform);
            var areaRect = area.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(12f, 4f);
            areaRect.offsetMax = new Vector2(-12f, -4f);
            area.AddComponent<RectMask2D>();

            var placeholderTmp = Label(area.transform, "Placeholder", placeholder, 16f, TextDim,
                TextAlignmentOptions.MidlineLeft);
            placeholderTmp.overflowMode = TextOverflowModes.Overflow;

            var textTmp = Label(area.transform, "Text", "", 16f, TextBright, TextAlignmentOptions.MidlineLeft);
            textTmp.overflowMode = TextOverflowModes.Overflow;

            var field = go.AddComponent<TMP_InputField>();
            field.textComponent = textTmp;
            field.placeholder = placeholderTmp;
            field.textViewport = areaRect;
            field.contentType = contentType;
            field.characterLimit = charLimit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretBlinkRate = 0.6f;
            field.scrollSensitivity = 18f;
            field.richText = false;
            field.asteriskChar = '•';

            go.transform.SetParent(parent, false);
            go.SetActive(true);
            return field;
        }

        /// <summary>The scene's root overlay canvas, or null when it has none.</summary>
        public static Canvas FindOverlayCanvas()
        {
            var found = Object.FindObjectOfType<Canvas>();
            return found != null && found.isRootCanvas && found.renderMode == RenderMode.ScreenSpaceOverlay
                ? found
                : null;
        }

        /// <summary>
        /// A dedicated root overlay canvas scaled against 1920×1080, so authored sizes behave the same
        /// at every resolution.
        /// </summary>
        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// Creates the input module if the scene has none. It stays scene-local deliberately: a
        /// persistent one collides with the EventSystem the Net demo scenes bring, and Unity only
        /// honours the first it finds.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static Color Tint(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }

        static TextMeshProUGUI ConfigureText(TextMeshProUGUI tmp, string value, float size, Color color)
        {
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.richText = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0f;
            return tmp;
        }
    }
}
