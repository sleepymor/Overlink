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

        // Figma design tokens — Overlink/Color palette
        public static readonly Color Background = new Color(0.039f, 0.055f, 0.102f); // #0A0E1A
        public static readonly Color Card = new Color(0.106f, 0.149f, 0.204f);       // #1B2634
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color InputBackground = new Color(0.039f, 0.055f, 0.102f);
        public static readonly Color Primary = new Color(0.161f, 0.875f, 0.949f);    // #29DFF2 cyan
        public static readonly Color Google = new Color(0.953f, 0.953f, 0.953f);
        public static readonly Color Ghost = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color Danger = new Color(0.784f, 0.250f, 0.230f);
        public static readonly Color TextBright = new Color(0.871f, 0.910f, 0.933f); // #DDE8ED
        public static readonly Color TextDim = new Color(0.510f, 0.588f, 0.663f);    // #8296A9
        public static readonly Color TextError = new Color(0.949f, 0.494f, 0.443f);
        public static readonly Color Accent = Primary;
        public static readonly Color CardBorder = new Color(0.149f, 0.227f, 0.290f, 0.35f); // #263A4A

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

            // Accent top-edge stripe: 2 px signal line that reads as a "notch".
            Color accentEdge = style switch
            {
                Style.Primary => Accent,
                Style.Danger => new Color(1f, 0.55f, 0.45f, 0.7f),
                Style.Ghost => new Color(Accent.r, Accent.g, Accent.b, 0.3f),
                _ => Color.clear
            };
            if (accentEdge.a > 0f)
            {
                var edge = Child("AccentEdge", go.transform);
                var edgeRect = edge.GetComponent<RectTransform>();
                edgeRect.anchorMin = new Vector2(0f, 1f);
                edgeRect.anchorMax = Vector2.one;
                edgeRect.pivot = new Vector2(0.5f, 1f);
                edgeRect.sizeDelta = new Vector2(0f, 2f);
                var edgeImage = edge.AddComponent<Image>();
                edgeImage.color = accentEdge;
                edgeImage.raycastTarget = false;
            }

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            go.AddComponent<UiFxButton>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Tint(normal, 1.22f),
                pressedColor = Tint(normal, 0.78f),
                selectedColor = Tint(normal, 1.10f),
                disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
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

        /// <summary>Adds a subtle 1 px border outline to a card/dialog panel.</summary>
        public static void ApplyCardBorder(GameObject cardGo)
        {
            var outline = cardGo.AddComponent<Outline>();
            outline.effectColor = CardBorder;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        static TextMeshProUGUI ConfigureText(TextMeshProUGUI tmp, string value, float size, Color color)
        {
            tmp.text = value;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.font = DisplayFont;
            tmp.richText = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0f;
            return tmp;
        }

        static TextMeshProUGUI ConfigureText(TextMeshProUGUI tmp, string value, float size, Color color,
            TextAlignmentOptions alignment)
        {
            ConfigureText(tmp, value, size, color);
            tmp.alignment = alignment;
            return tmp;
        }

        static TMP_FontAsset DisplayFont
        {
            get
            {
                if (displayFont == null)
                    displayFont = Resources.Load<TMP_FontAsset>("Fonts/Rajdhani-SemiBold");
                return displayFont;
            }
        }

        static TMP_FontAsset displayFont;

        // ── New shared UI symbols from the revamped Figma design ──────────────────────

        /// <summary>
        /// Angular command plate: a vector-backed command button used across Main Menu,
        /// Card Binder deck action, Battle phase commands, and Gacha open commands.
        /// </summary>
        public static GameObject AngularCommandPlate(string name, Transform parent,
            string action, string detail, float height, string iconName = null)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1f;

            // Angular plate background
            var plate = go.AddComponent<Image>();
            plate.color = Card;
            plate.raycastTarget = true;

            // Border line
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Card,
                highlightedColor = Tint(Card, 1.15f),
                pressedColor = Tint(Card, 0.75f),
                selectedColor = Tint(Card, 1.05f),
                disabledColor = new Color(Card.r, Card.g, Card.b, Card.a * 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            go.AddComponent<UiFxButton>();

            // Caption area
            var caption = Child("Command caption", go.transform);
            var captionRect = caption.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0f, 0.5f);
            captionRect.anchorMax = new Vector2(0f, 0.5f);
            captionRect.pivot = new Vector2(0f, 0.5f);
            captionRect.anchoredPosition = new Vector2(20f, 0f);
            captionRect.sizeDelta = new Vector2(height * 0.6f, height * 0.55f);

            var actionText = Child("Action", caption.transform);
            var actionRect = actionText.GetComponent<RectTransform>();
            actionRect.anchorMin = Vector2.zero;
            actionRect.anchorMax = Vector2.one;
            actionRect.offsetMin = Vector2.zero;
            actionRect.offsetMax = Vector2.zero;
            var actionTmp = ConfigureText(actionText.AddComponent<TextMeshProUGUI>(), action, 22f, TextBright,
                TextAlignmentOptions.MidlineLeft);
            actionTmp.fontStyle = FontStyles.Bold;

            var detailText = Child("Action detail", caption.transform);
            var detailRect = detailText.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0f, 1f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.pivot = new Vector2(0f, 1f);
            detailRect.anchoredPosition = new Vector2(0f, -29f);
            detailRect.sizeDelta = new Vector2(height * 0.6f, 12f);
            ConfigureText(detailText.AddComponent<TextMeshProUGUI>(), detail, 10f, TextDim,
                TextAlignmentOptions.MidlineLeft);

            // Icon on right edge
            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = Child(iconName, go.transform);
                var iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(1f, 0.5f);
                iconRect.anchorMax = new Vector2(1f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(-22f, 0f);
                iconRect.sizeDelta = new Vector2(22f, 22f);
                icon.AddComponent<Image>().color = Primary;
            }

            return go;
        }

        /// <summary>
        /// Link signature: Overlink insignia + link text, used in Main Menu and Battle headers.
        /// </summary>
        public static GameObject LinkSignature(string name, Transform parent, string linkText)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(254f, 36f);

            var insignia = Child("Overlink insignia", go.transform);
            var insigniaRect = insignia.GetComponent<RectTransform>();
            insigniaRect.anchorMin = Vector2.zero;
            insigniaRect.anchorMax = Vector2.zero;
            insigniaRect.pivot = new Vector2(0f, 0f);
            insigniaRect.anchoredPosition = Vector2.zero;
            insigniaRect.sizeDelta = new Vector2(36f, 36f);
            insignia.AddComponent<Image>().color = Primary;

            var label = Child("Tactical label", go.transform);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(48f, 0f);
            labelRect.sizeDelta = new Vector2(206f, 13f);
            ConfigureText(label.AddComponent<TextMeshProUGUI>(), linkText, 13f, TextBright,
                TextAlignmentOptions.MidlineLeft);

            return go;
        }

        /// <summary>
        /// Account bar: player identity + profile/sign-out actions, shared across screens.
        /// </summary>
        public static GameObject AccountBar(string name, Transform parent)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(259f, 40f);

            var identity = Child("Identity", go.transform);
            var identityRect = identity.GetComponent<RectTransform>();
            identityRect.anchorMin = Vector2.zero;
            identityRect.anchorMax = Vector2.zero;
            identityRect.pivot = new Vector2(0f, 0f);
            identityRect.sizeDelta = new Vector2(107f, 40f);

            var playerName = Child("Player name", identity.transform);
            var nameRect = playerName.GetComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.zero;
            nameRect.pivot = new Vector2(0f, 0f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(59f, 26f);
            ConfigureText(playerName.AddComponent<TextMeshProUGUI>(), "Player", 16f, TextBright,
                TextAlignmentOptions.MidlineLeft);

            var tacticalLabel = Child("Tactical label", identity.transform);
            var labelRect = tacticalLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -11f);
            labelRect.sizeDelta = new Vector2(107f, 11f);
            ConfigureText(tacticalLabel.AddComponent<TextMeshProUGUI>(), "TACTICAL LABEL", 8f, TextDim,
                TextAlignmentOptions.MidlineLeft);

            var profileAction = Child("Profile action", go.transform);
            var profileRect = profileAction.GetComponent<RectTransform>();
            profileRect.anchorMin = new Vector2(0f, 0.5f);
            profileRect.anchorMax = new Vector2(0f, 0.5f);
            profileRect.pivot = new Vector2(0f, 0.5f);
            profileRect.anchoredPosition = new Vector2(123f, 0f);
            profileRect.sizeDelta = new Vector2(39f, 18f);
            ConfigureText(profileAction.AddComponent<TextMeshProUGUI>(), "PROFILE", 13f, TextBright,
                TextAlignmentOptions.MidlineLeft);

            var logout = Child("log-out", go.transform);
            var logoutRect = logout.GetComponent<RectTransform>();
            logoutRect.anchorMin = new Vector2(0f, 0.5f);
            logoutRect.anchorMax = new Vector2(0f, 0.5f);
            logoutRect.pivot = new Vector2(0.5f, 0.5f);
            logoutRect.anchoredPosition = new Vector2(178f, 0f);
            logoutRect.sizeDelta = new Vector2(20f, 20f);
            logout.AddComponent<Image>().color = TextDim;

            var signOut = Child("Sign out action", go.transform);
            var signOutRect = signOut.GetComponent<RectTransform>();
            signOutRect.anchorMin = new Vector2(0f, 0.5f);
            signOutRect.anchorMax = new Vector2(0f, 0.5f);
            signOutRect.pivot = new Vector2(0f, 0.5f);
            signOutRect.anchoredPosition = new Vector2(214f, 0f);
            signOutRect.sizeDelta = new Vector2(45f, 13f);
            ConfigureText(signOut.AddComponent<TextMeshProUGUI>(), "SIGN OUT", 10f, TextDim,
                TextAlignmentOptions.MidlineLeft);

            return go;
        }

        /// <summary>
        /// Filter chip: dropdown filter with a chevron, used in Card Binder controls row.
        /// </summary>
        public static GameObject FilterChip(string name, Transform parent, string filterValue)
        {
            var go = Child(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(146f, 36f);

            var valueText = Child("Filter value", go.transform);
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0f, 0.5f);
            valueRect.anchorMax = new Vector2(0f, 0.5f);
            valueRect.pivot = new Vector2(0f, 0.5f);
            valueRect.anchoredPosition = new Vector2(11f, 0f);
            valueRect.sizeDelta = new Vector2(99f, 16f);
            ConfigureText(valueText.AddComponent<TextMeshProUGUI>(), filterValue, 12f, TextBright,
                TextAlignmentOptions.MidlineLeft);

            var chevron = Child("chevron-down", go.transform);
            var chevronRect = chevron.GetComponent<RectTransform>();
            chevronRect.anchorMin = new Vector2(1f, 0.5f);
            chevronRect.anchorMax = new Vector2(1f, 0.5f);
            chevronRect.pivot = new Vector2(0.5f, 0.5f);
            chevronRect.anchoredPosition = new Vector2(-13f, 0f);
            chevronRect.sizeDelta = new Vector2(12f, 12f);
            chevron.AddComponent<Image>().color = TextDim;

            return go;
        }
    }
}
