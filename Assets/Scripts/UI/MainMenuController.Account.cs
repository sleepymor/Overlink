using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Builds the main menu's account surface at runtime when the prefab does not carry one.
    /// <para>
    /// The menu itself is authored in <c>Prefabs/MainMenu.prefab</c> and stays that way; only the account
    /// widgets — the player-name corner and the ID dialog — are constructed here. That split keeps the
    /// designer's menu intact while making the new login flow self-installing, so nothing about it can
    /// be missing from a prefab that predates it.
    /// </para>
    /// </summary>
    public sealed partial class MainMenuController
    {
        const float WidgetWidth = 300f;
        const float DialogWidth = 440f;

        /// <summary>Title, three identity rows, the copy toast and the close button, plus the stack's padding.</summary>
        const float DialogHeight = 400f;

        void BuildAccountUi()
        {
            UiKit.EnsureEventSystem();
            var canvas = UiKit.FindOverlayCanvas() ?? UiKit.CreateCanvas("Main Menu Canvas");

            BuildCornerWidget(canvas.transform);
            BuildProfileDialog(canvas.transform);
        }

        /// <summary>The always-visible corner: who is signed in, and how to leave.</summary>
        void BuildCornerWidget(Transform parent)
        {
            var widget = UiKit.Child("Account Widget", parent);
            var rect = widget.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-28f, -24f);
            rect.sizeDelta = new Vector2(WidgetWidth, 92f);

            var stack = widget.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 8;
            stack.childAlignment = TextAnchor.UpperRight;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = false;
            stack.childForceExpandHeight = false;

            playerNameText = UiKit.Text("Player Name", widget.transform, "Player", 22f, UiKit.TextBright,
                TextAlignmentOptions.MidlineRight, FontStyles.Bold);

            var row = UiKit.Child("Account Buttons", widget.transform);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, UiKit.ButtonHeight);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childAlignment = TextAnchor.MiddleRight;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = UiKit.ButtonHeight;

            btnProfile = FixedWidth(UiKit.Button("Profile Button", row.transform, "Profile", UiKit.Style.Ghost), 110f)
                .GetComponent<Button>();
            btnSignOut = FixedWidth(UiKit.Button("Sign Out Button", row.transform, "Sign Out", UiKit.Style.Danger), 110f)
                .GetComponent<Button>();
        }

        /// <summary>The modal that shows the identity in full and copies its parts individually.</summary>
        void BuildProfileDialog(Transform parent)
        {
            profilePanel = UiKit.Child("Profile Panel", parent);
            var panelRect = profilePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            // The scrim is the dismiss affordance. It is a sibling of the card, never its ancestor:
            // Unity bubbles a click up the hierarchy until it finds a handler, so a button on a shared
            // parent would close the dialog no matter where inside it the player clicked.
            var scrimGo = UiKit.Child("Scrim", profilePanel.transform);
            UiKit.Stretch(scrimGo.GetComponent<RectTransform>()).color = UiKit.Scrim;

            var dismiss = scrimGo.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.targetGraphic = scrimGo.GetComponent<Image>();
            dismiss.onClick.AddListener(HideProfilePanel);

            var cardGo = UiKit.Child("Card", profilePanel.transform);
            var card = cardGo.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(DialogWidth, DialogHeight);
            cardGo.AddComponent<Image>().color = UiKit.Card;

            var stack = UiKit.Stack("Content", card.transform);
            UiKit.Text("Title", stack.transform, "Player ID", 26f, UiKit.TextBright,
                TextAlignmentOptions.Center, FontStyles.Bold);

            txtProfileUsername = IdentityRow(stack.transform, "Username", out btnCopyUsername);
            txtProfileTag = IdentityRow(stack.transform, "Tag", out btnCopyTag);
            txtProfileFullID = IdentityRow(stack.transform, "Full ID", out btnCopyID);

            copyFeedback = UiKit.Text("Copy Feedback", stack.transform, "", 13f, UiKit.Accent,
                TextAlignmentOptions.Center);
            copyFeedback.CrossFadeAlpha(0f, 0f, false);

            btnCloseProfile = UiKit.Button("Close Button", stack.transform, "Close", UiKit.Style.Primary)
                .GetComponent<Button>();
        }

        /// <summary>One "Label — value — Copy" row of the identity dialog.</summary>
        TextMeshProUGUI IdentityRow(Transform parent, string label, out Button copyButton)
        {
            var row = UiKit.Child($"{label} Row", parent);
            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, UiKit.RowHeight);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = UiKit.RowHeight;

            var caption = UiKit.Label(row.transform, "Caption", label, 14f, UiKit.TextDim,
                TextAlignmentOptions.MidlineLeft);
            caption.gameObject.AddComponent<LayoutElement>().minWidth = 74f;

            var value = UiKit.Label(row.transform, "Value", "—", 16f, UiKit.TextBright,
                TextAlignmentOptions.MidlineLeft);
            value.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            copyButton = FixedWidth(UiKit.Button("Copy Button", row.transform, "Copy", UiKit.Style.Ghost), 74f)
                .GetComponent<Button>();
            return value;
        }

        /// <summary>
        /// Pins a button's width. UiKit buttons claim the flexible axis so they fill a vertical stack;
        /// inside a horizontal row that would let the row stretch them past the intended size, so the
        /// claim is withdrawn here and preferred and minimum widths are set together
        /// (<c>LayoutElement</c> offers no maximum).
        /// </summary>
        static GameObject FixedWidth(GameObject button, float width)
        {
            var element = button.GetComponent<LayoutElement>();
            element.flexibleWidth = 0f;
            element.minWidth = element.preferredWidth = width;
            return button;
        }
    }
}
