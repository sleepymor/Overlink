// Assets/Scripts/UI/DeckbuilderController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Overlink.Core;
using Overlink.Persistence;
using Overlink.Vfx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Functional deck builder with faction filter, add/remove with 3-copy limit,
    /// 30-40 card size validation, cost curve visualization, and multiple saved decks in the shared
    /// <see cref="DeckLibrary"/> store (the same records the binder shows and edits).
    /// <para>
    /// The layout is built from fixed insets rather than percentage anchors: the header and the bottom
    /// action strip are reserved first, and the two list panels fill whatever is left between them. That
    /// keeps the lists out from under the action strip at every resolution, which percentage anchors did
    /// not (they overlapped, and the strip is drawn last so it covered them).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeckbuilderController : MonoBehaviour
    {
        // Reserved bands, in canvas reference units (CanvasScaler pins the canvas to 1920x1080).
        // The header holds the title/back/faction row; the bottom bar holds the cost curve, the
        // validation line and Save/Load/Clear. Both are fixed heights, so the panels between them
        // cannot collide with either.
        const float HeaderHeight = 185f;
        const float BottomBarHeight = 140f;
        const float Gutter = 10f;
        const float RowHeight = 40f;
        const float CostBarWidth = 22f;
        const float CostBarSpacing = 6f;
        const float CostBarMaxHeight = 62f;
        const float CostBarBaseline = 26f;

        // UI references (created at runtime)
        private Button btnBack;
        private Button btnPrevDeck;
        private Button btnNextDeck;
        private Button btnNewDeck;
        private TMP_InputField deckNameInput;
        private TMP_Text titleLabel;
        private Button btnFactionFsti;
        private Button btnFactionFrti;
        private Button btnFactionFpb;
        private ScrollRect collectionScroll;
        private RectTransform collectionContent;
        private ScrollRect deckScroll;
        private RectTransform deckContent;
        private TMP_Text collectionTitleLabel;
        private TMP_Text deckSizeLabel;
        private TMP_Text validationLabel;
        private TMP_Text deckHintLabel;
        private Button btnSave;
        private Button btnLoad;
        private Button btnClear;
        private TMP_Text[] statValueLabels;
        private GameObject emptyStateObject;
        private TMP_Text emptyStateTitle;
        private TMP_Text emptyStateBody;
        private TMP_Text deckCountLabel;

        // Cost curve: eleven persistent bars, tweened to their new heights instead of rebuilt, so the
        // curve reads as movement rather than as a redraw.
        private readonly RectTransform[] costBars = new RectTransform[11];
        private readonly Image[] costBarImages = new Image[11];
        private readonly TMP_Text[] costBarCounts = new TMP_Text[11];

        // State
        private Faction selectedFaction = Faction.Fsti;
        private Dictionary<string, int> currentDeck = new Dictionary<string, int>(); // cardId -> count
        private IReadOnlyList<CardDefinition> allCards;
        private string activeDeckId = string.Empty;
        private bool uiBuilt = false;

        // One-shot feedback requests, consumed by the next rebuild.
        private string pendingPopCardId;
        private string pendingFlashCardId;
        private GameObject confirmPanel;
        private TMP_Text confirmLabel;
        private Action confirmedAction;

        void Awake()
        {
            // The deckable pool is the library plus the binder's authored catalog, so a card added in
            // the binder is still here when the deck is opened in this screen.
            allCards = OfflineCollectionData.DeckPool(Faction.Fsti)
                .Concat(OfflineCollectionData.DeckPool(Faction.Frti))
                .Concat(OfflineCollectionData.DeckPool(Faction.Fpb))
                .ToList();

            // The binder's per-card deck and this screen's old single slot both feed the shared
            // library, so a deck built before the unification is still here. One entry point runs both
            // imports in the right order, whichever screen the player opened first.
            OfflineCollectionData.EnsureDeckMigration();
            EnsureActiveDeck();

            if (!uiBuilt)
            {
                BuildUI();
                uiBuilt = true;
            }
            LoadDeck();
            RefreshUI();
            FocusFirstControl();
        }

        void BuildUI()
        {
            // Persistent services can have their own Canvas (e.g. the debug console). Parenting
            // the builder there leaves it visible after returning to the collection, so only use a
            // canvas belonging to this controller's scene.
            Canvas canvas = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                canvas = root.GetComponentInChildren<Canvas>(false);
                if (canvas != null) break;
            }
            if (canvas == null)
            {
                Debug.LogError("[DeckbuilderController] No Canvas found in scene.");
                return;
            }

            // A zero-scaled canvas renders and raycasts unpredictably. Keep its coordinate system valid
            // even in scenes authored with an accidentally collapsed transform.
            if (canvas.transform.localScale.x == 0f || canvas.transform.localScale.y == 0f)
                canvas.transform.localScale = Vector3.one;

            // Ensure Canvas has GraphicRaycaster for UI interaction
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            // Ensure Canvas has CanvasScaler for resolution independence
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // Balance between width and height

            // Ensure EventSystem exists
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
                Debug.Log("[DeckbuilderController] Created EventSystem.");
            }

            Transform canvasTransform = canvas.transform;

            // Background (dark)
            GameObject bgObj = new GameObject("Background");
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.SetParent(canvasTransform, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.067f, 0.09f, 1f);
            bgImage.raycastTarget = false;

            // Main container
            GameObject mainObj = new GameObject("Main Container");
            RectTransform mainRect = mainObj.AddComponent<RectTransform>();
            mainRect.SetParent(canvasTransform, false);
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.offsetMin = new Vector2(20, 20);
            mainRect.offsetMax = new Vector2(-20, -20);

            // Title
            titleLabel = CreateLabel(mainRect, "Title", "DECK BUILDER", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -26), new Vector2(620, 40), 30);

            // Back button (top-left). Centre pivot means the position is the centre, so x = 20 + 75
            // keeps the whole box on screen where a raw (20, -20) used to clip half of it away.
            btnBack = CreateButton(mainRect, "Btn_Back", "\u2190 MAIN MENU", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(95, -26), new Vector2(150, 40));
            // Top-most in the container: the exit is the one control that must never end up underneath
            // something a later layout change drops into the header band.
            btnBack.transform.SetAsLastSibling();
            btnBack.onClick.AddListener(OnBackClicked);

            // Deck slot: the saved-deck chooser. Saving writes the active deck, so the player can hold
            // several decks without any of them overwriting the others.
            GameObject deckBar = CreatePanel(mainRect, "Deck Slot",
                new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            RectTransform deckBarRect = deckBar.GetComponent<RectTransform>();
            deckBarRect.pivot = new Vector2(0f, 1f);
            deckBarRect.anchoredPosition = new Vector2(-620, -40);
            deckBarRect.sizeDelta = new Vector2(330, 60);
            deckBar.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 1f);
            deckHintLabel = CreateLabel(mainRect, "Deck Hint", "", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-620, -108), new Vector2(360, 18), 12);
            deckHintLabel.alignment = TextAlignmentOptions.Left;
            deckHintLabel.color = new Color(0.65f, 0.75f, 0.84f);

            btnPrevDeck = CreateButton(deckBar.transform, "Btn_PrevDeck", "<", new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(40, 40));
            btnPrevDeck.onClick.AddListener(() => CycleDeck(-1));

            deckNameInput = CreateInputField(deckBar.transform, "Deck Name", string.Empty, new Vector2(0, 0), new Vector2(0, 0));
            RectTransform nameRect = deckNameInput.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(64, 6);
            nameRect.offsetMax = new Vector2(-64, -6);

            btnNextDeck = CreateButton(deckBar.transform, "Btn_NextDeck", ">", new Vector2(1, 0.5f),
                new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(40, 40));
            btnNextDeck.onClick.AddListener(() => CycleDeck(1));

            btnNewDeck = CreateButton(mainRect, "Btn_NewDeck", "NEW DECK", new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-150, -58), new Vector2(250, 50));
            btnNewDeck.onClick.AddListener(OnNewDeckClicked);

            // Faction filter: three buttons, one per faction. Changing faction with cards in the deck
            // asks first, because the deck is single-faction.
            GameObject factionPanel = new GameObject("Faction Selector");
            RectTransform factionPanelRect = factionPanel.AddComponent<RectTransform>();
            factionPanelRect.SetParent(mainRect, false);
            factionPanelRect.anchorMin = new Vector2(0f, 1f);
            factionPanelRect.anchorMax = new Vector2(0f, 1f);
            factionPanelRect.pivot = new Vector2(0f, 1f);
            factionPanelRect.anchoredPosition = new Vector2(0, -144);
            factionPanelRect.sizeDelta = new Vector2(500, 36);

            btnFactionFsti = CreateButton(factionPanelRect, "Btn_Fsti", "FSTI",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(140, 36));
            btnFactionFsti.onClick.AddListener(() => OnFactionButtonClicked(Faction.Fsti));

            btnFactionFrti = CreateButton(factionPanelRect, "Btn_Frti", "FRTI",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(220, 0), new Vector2(140, 36));
            btnFactionFrti.onClick.AddListener(() => OnFactionButtonClicked(Faction.Frti));

            btnFactionFpb = CreateButton(factionPanelRect, "Btn_Fpb", "FPB",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370, 0), new Vector2(140, 36));
            btnFactionFpb.onClick.AddListener(() => OnFactionButtonClicked(Faction.Fpb));

            UpdateFactionButtonStates();

            // Left panel: Card Collection table, between the header and the footer.
            GameObject collectionPanel = CreatePanel(mainRect, "Collection Panel",
                new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(Gutter, BottomBarHeight + Gutter),
                new Vector2(-Gutter * 0.5f, -HeaderHeight));
            collectionTitleLabel = CreateLabel(collectionPanel.transform, "Collection Title", "CARD COLLECTION",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -22), new Vector2(400, 30), 22);
            collectionTitleLabel.alignment = TextAlignmentOptions.Left;
            collectionTitleLabel.rectTransform.pivot = new Vector2(0f, 1f);

            collectionScroll = CreateScrollView(collectionPanel.transform, "Collection Scroll",
                Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -60));
            collectionContent = collectionScroll.content;

            // Right panel: Current Deck, with stats and deck requirements.
            GameObject deckPanel = CreatePanel(mainRect, "Deck Panel",
                new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(Gutter * 0.5f, BottomBarHeight + Gutter),
                new Vector2(-Gutter, -HeaderHeight));
            deckSizeLabel = CreateLabel(deckPanel.transform, "Deck Title", "CURRENT DECK",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -22), new Vector2(400, 30), 22);
            deckSizeLabel.alignment = TextAlignmentOptions.Left;
            deckSizeLabel.rectTransform.pivot = new Vector2(0f, 1f);
            deckCountLabel = CreateLabel(deckPanel.transform, "Deck Count", "00 / 30\u201340",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -22), new Vector2(200, 30), 22);
            deckCountLabel.alignment = TextAlignmentOptions.Right;
            deckCountLabel.rectTransform.pivot = new Vector2(1f, 1f);

            // Stats row: total ATK, HP, shield and average NP, as the mockup lays them out.
            string[] statTitles = { "TOTAL ATK", "TOTAL HP", "TOTAL SHIELD", "AVG. NP" };
            statValueLabels = new TMP_Text[statTitles.Length];
            for (int i = 0; i < statTitles.Length; i++)
            {
                float left = 20f + i * 150f;
                GameObject statCard = CreatePanel(deckPanel.transform, "Stat_" + i,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -150), new Vector2(left + 136f, -62));
                statCard.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.21f, 1f);
                TMP_Text statTitle = CreateLabel(statCard.transform, "Title", statTitles[i],
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -12), new Vector2(120, 16), 11);
                statTitle.alignment = TextAlignmentOptions.Left;
                statTitle.rectTransform.pivot = new Vector2(0f, 1f);
                statValueLabels[i] = CreateLabel(statCard.transform, "Value", "0",
                    new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 28);
                statValueLabels[i].alignment = TextAlignmentOptions.Left;
                statValueLabels[i].rectTransform.offsetMin = new Vector2(12, 6);
                statValueLabels[i].rectTransform.offsetMax = new Vector2(-12, -36);
                statValueLabels[i].color = new Color(0.16f, 0.87f, 0.95f);
            }

            // Empty state, shown only while the deck has no cards.
            emptyStateObject = new GameObject("Empty State", typeof(RectTransform));
            RectTransform emptyRect = emptyStateObject.GetComponent<RectTransform>();
            emptyRect.SetParent(deckPanel.transform, false);
            emptyRect.anchorMin = new Vector2(0f, 1f);
            emptyRect.anchorMax = new Vector2(1f, 1f);
            emptyRect.pivot = new Vector2(0.5f, 1f);
            emptyRect.anchoredPosition = new Vector2(0, -180);
            emptyRect.sizeDelta = new Vector2(-40, 120);
            emptyStateTitle = CreateLabel(emptyRect, "Title", "Start building your deck",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -10), new Vector2(0, 40), 26);
            emptyStateBody = CreateLabel(emptyRect, "Body",
                "Add cards from your collection to shape your strategy.\nUse + to add a card. Up to 3 copies of each.",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -68), new Vector2(0, 60), 15);
            emptyStateBody.color = new Color(0.65f, 0.75f, 0.84f);

            // Deck requirements, pinned to the bottom of the right panel above the footer.
            GameObject requirements = CreatePanel(deckPanel.transform, "Requirements",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20, 14), new Vector2(-20, 76));
            requirements.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            TMP_Text heading = CreateLabel(requirements.transform, "Heading", "DECK REQUIREMENTS",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0, -14), new Vector2(300, 16), 11);
            heading.alignment = TextAlignmentOptions.Left;
            heading.rectTransform.pivot = new Vector2(0f, 1f);
            string[] requirementTexts = { "30\u201340 cards", "Single faction", "Max. 3 copies" };
            for (int i = 0; i < requirementTexts.Length; i++)
            {
                TMP_Text req = CreateLabel(requirements.transform, "Req_" + i, requirementTexts[i],
                    new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f), Vector2.zero, Vector2.zero, 17);
                req.alignment = TextAlignmentOptions.Left;
                req.verticalAlignment = VerticalAlignmentOptions.Bottom;
                req.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 8f, 2f);
                req.rectTransform.offsetMax = new Vector2(0f, -30f);
            }

            deckScroll = CreateScrollView(deckPanel.transform, "Deck Scroll",
                Vector2.zero, Vector2.one, new Vector2(10, 150), new Vector2(-10, -160));
            deckContent = deckScroll.content;
            // The empty state sits over the deck scroll, so it must draw after it to be visible.
            emptyStateObject.transform.SetAsLastSibling();

            // Footer: cost curve on the left, status line and actions on the right.
            GameObject bottomPanel = CreatePanel(mainRect, "Bottom Panel",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(Gutter, Gutter), new Vector2(-Gutter, Gutter + BottomBarHeight));
            bottomPanel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.11f, 1f);

            GameObject costCurveObj = CreatePanel(bottomPanel.transform, "Cost Curve",
                new Vector2(0, 0), new Vector2(0.3f, 1), new Vector2(10, 10), new Vector2(-5, -10));
            costCurveObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            CreateLabel(costCurveObj.transform, "Cost Curve Title", "COST CURVE",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10, -14), new Vector2(200, 20), 13);
            BuildCostCurve(costCurveObj.transform);

            GameObject actionsPanel = CreatePanel(bottomPanel.transform, "Actions",
                new Vector2(0.3f, 0), new Vector2(1, 1), new Vector2(10, 10), new Vector2(-10, -10));
            actionsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            // Status line, left-aligned, to the left of the action buttons.
            validationLabel = CreateLabel(actionsPanel.transform, "Validation", "",
                new Vector2(0, 0.5f), new Vector2(0.62f, 1), Vector2.zero, Vector2.zero, 14);
            validationLabel.alignment = TextAlignmentOptions.TopLeft;
            var validationRect = validationLabel.rectTransform;
            validationRect.offsetMin = new Vector2(8, 4);
            validationRect.offsetMax = new Vector2(-8, -6);
            validationLabel.raycastTarget = false;

            // Actions are pinned to the right edge of the footer so they stay inside it at any width.
            btnSave = CreateButton(actionsPanel.transform, "Btn_Save", "SAVE DECK",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(200, 60));
            btnSave.onClick.AddListener(OnSaveClicked);
            // The mockup's primary action is the cyan bar, so Save takes the accent fill rather than the
            // neutral button colours. Disabled state stays dark so an unsavable deck reads as blocked.
            Image saveImage = btnSave.targetGraphic as Image;
            saveImage.color = new Color(0.16f, 0.875f, 0.949f, 1f);
            var saveColors = btnSave.colors;
            saveColors.normalColor = new Color(0.16f, 0.875f, 0.949f, 1f);
            saveColors.highlightedColor = new Color(0.196f, 1f, 1f, 1f);
            saveColors.pressedColor = new Color(0.126f, 0.683f, 0.74f, 1f);
            saveColors.selectedColor = new Color(0.196f, 1f, 1f, 1f);
            saveColors.disabledColor = new Color(0.10f, 0.36f, 0.42f, 1f);
            btnSave.colors = saveColors;
            btnSave.GetComponentInChildren<TMP_Text>().color = Color.white;
            btnSave.GetComponentInChildren<TMP_Text>().text = "SAVE DECK\n<size=11>SAVE YOUR CURRENT DRAFT</size>";

            btnClear = CreateButton(actionsPanel.transform, "Btn_Clear", "CLEAR DECK",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(160, 54));
            btnClear.onClick.AddListener(OnClearClicked);

            btnLoad = CreateButton(actionsPanel.transform, "Btn_Load", "LOAD DECK",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-500, 0), new Vector2(160, 54));
            btnLoad.onClick.AddListener(OnLoadClicked);

            // Header controls must remain above panel backgrounds if the canvas is narrow.
            deckBar.transform.SetAsLastSibling();
            factionPanel.transform.SetAsLastSibling();
            btnBack.transform.SetAsLastSibling();
            BuildConfirmation(mainRect);
            ConfigureNavigation();
        }

        /// <summary>
        /// Explicit focus graph for the chrome, so a keyboard or gamepad player lands somewhere sensible
        /// and can never fall into a control with no way out. The per-card rows are rebuilt on every
        /// refresh and rely on automatic navigation among themselves.
        /// </summary>
        void ConfigureNavigation()
        {
            Link(btnBack, null, btnPrevDeck, btnNewDeck, btnPrevDeck);
            Link(btnPrevDeck, btnBack, btnFactionFsti, btnNewDeck, deckNameInput);
            Link(deckNameInput, btnBack, btnFactionFsti, btnPrevDeck, btnNextDeck);
            Link(btnNextDeck, btnBack, btnFactionFsti, deckNameInput, btnNewDeck);
            Link(btnNewDeck, btnBack, btnFactionFsti, btnNextDeck, btnFactionFsti);
            Link(btnFactionFsti, btnPrevDeck, btnSave, btnBack, btnFactionFrti);
            Link(btnFactionFrti, btnPrevDeck, btnSave, btnFactionFsti, btnFactionFpb);
            Link(btnFactionFpb, btnPrevDeck, btnSave, btnFactionFrti, btnFactionFsti);
            Link(btnSave, btnFactionFsti, null, btnClear, btnLoad);
            Link(btnLoad, btnFactionFrti, null, btnSave, btnClear);
            Link(btnClear, btnFactionFpb, null, btnLoad, btnSave);
        }

        static void Link(Selectable target, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (target == null) return;

            Navigation nav = target.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = up;
            nav.selectOnDown = down;
            nav.selectOnLeft = left;
            nav.selectOnRight = right;
            target.navigation = nav;
        }

        void FocusFirstControl()
        {
            if (EventSystem.current == null || btnFactionFsti == null) return;
            EventSystem.current.SetSelectedGameObject(btnFactionFsti.gameObject);
        }

        void BuildConfirmation(Transform parent)
        {
            confirmPanel = new GameObject("Confirmation");
            var rect = confirmPanel.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var backdrop = confirmPanel.AddComponent<Image>();
            backdrop.color = new Color(0.01f, 0.02f, 0.04f, 0.85f);
            backdrop.raycastTarget = true;

            var dialog = CreatePanel(rect, "Dialog", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-270, -100), new Vector2(270, 100));
            confirmLabel = CreateLabel(dialog.transform, "Message", "", new Vector2(0, 0.5f),
                Vector2.one, Vector2.zero, Vector2.zero, 20);
            confirmLabel.rectTransform.offsetMin = new Vector2(20, 0);
            confirmLabel.rectTransform.offsetMax = new Vector2(-20, -12);

            var cancel = CreateButton(dialog.transform, "Btn_Cancel", "Cancel", new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(-80, 38), new Vector2(140, 42));
            var confirm = CreateButton(dialog.transform, "Btn_Confirm", "Continue", new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(80, 38), new Vector2(140, 42));
            Link(cancel, null, null, confirm, confirm);
            Link(confirm, null, null, cancel, cancel);
            cancel.onClick.AddListener(() => CloseConfirmation(false));
            confirm.onClick.AddListener(() => CloseConfirmation(true));
            confirmPanel.SetActive(false);
        }

        void ShowConfirmation(string message, Action action)
        {
            confirmedAction = action;
            confirmLabel.text = message;
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
            var cancel = confirmPanel.transform.Find("Dialog/Btn_Cancel");
            if (EventSystem.current != null && cancel != null)
                EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }

        void CloseConfirmation(bool confirmed)
        {
            Action action = confirmedAction;
            confirmedAction = null;
            confirmPanel.SetActive(false);
            if (confirmed) action?.Invoke();
            FocusFirstControl();
        }

        void RefreshUI()
        {
            RefreshDeckIdentity();
            UpdateFactionButtonStates();
            RefreshCollection();
            RefreshDeck();
            RefreshValidation();
            RefreshCostCurve();
        }

        void UpdateFactionButtonStates()
        {
            SetFactionStyle(btnFactionFsti, selectedFaction == Faction.Fsti);
            SetFactionStyle(btnFactionFrti, selectedFaction == Faction.Frti);
            SetFactionStyle(btnFactionFpb, selectedFaction == Faction.Fpb);
        }

        static void SetFactionStyle(Button button, bool selected)
        {
            if (button == null) return;
            var colors = button.colors;
            colors.normalColor = selected ? new Color(0.16f, 0.48f, 0.7f) : new Color(0.16f, 0.22f, 0.29f);
            colors.selectedColor = selected ? new Color(0.22f, 0.58f, 0.8f) : new Color(0.25f, 0.36f, 0.46f);
            button.colors = colors;
            // Selectable's color transition controls the tint; update it immediately after a switch.
            button.targetGraphic.CrossFadeColor(colors.normalColor, 0f, true, true);
        }

        void RefreshCollection()
        {
            ClearChildren(collectionContent);

            var factionCards = OfflineCollectionData.DeckPool(selectedFaction)
                .OrderBy(c => c.Cost)
                .ThenBy(c => c.Name)
                .ToList();

            int total = TotalCards();
            bool atCap = total >= DeckValidator.MaxDeckSize;

            if (collectionTitleLabel != null)
            {
                collectionTitleLabel.text = atCap
                    ? $"Card Collection - deck full ({total}/{DeckValidator.MaxDeckSize})"
                    : "Card Collection";
                collectionTitleLabel.color = atCap ? new Color(1f, 0.78f, 0.35f) : Color.white;
            }

            float yPos = -8f;

            BuildCollectionHeader(collectionContent, yPos);
            yPos -= RowHeight;

            foreach (var card in factionCards)
            {
                GameObject cardRow = new GameObject($"Card_{card.Id}");
                RectTransform rowRect = cardRow.AddComponent<RectTransform>();
                rowRect.SetParent(collectionContent, false);
                rowRect.anchorMin = new Vector2(0, 1);
                rowRect.anchorMax = new Vector2(1, 1);
                rowRect.pivot = new Vector2(0, 1);
                rowRect.anchoredPosition = new Vector2(0, yPos);
                rowRect.sizeDelta = new Vector2(-10, 35); // -10 for padding

                Image rowImage = cardRow.AddComponent<Image>();
                rowImage.color = new Color(1f, 1f, 1f, 0.03f);
                rowImage.raycastTarget = false;

                // Units carry ATK / HP / SHD. Other card types have no combat stats, so the mockup's
                // "stat data unavailable" dash stands in for them.
                bool hasStats = card.Type == CardType.Unit;
                string Stat(int value) => hasStats ? value.ToString() : "\u2014";

                CreateCell(rowRect, "NP", card.Cost.ToString("00"), 0, TextAlignmentOptions.Left, 16, new Color(0.35f, 0.85f, 1f));
                CreateCell(rowRect, "Name", card.Name, 1, TextAlignmentOptions.Left, 16, Color.white);
                CreateCell(rowRect, "Atk", Stat(card.Atk), 2, TextAlignmentOptions.Center, 14, new Color(0.35f, 0.85f, 1f));
                CreateCell(rowRect, "Hp", Stat(card.Hp), 3, TextAlignmentOptions.Center, 14, new Color(0.35f, 0.85f, 1f));
                CreateCell(rowRect, "Shd", Stat(card.Shield), 4, TextAlignmentOptions.Center, 14, new Color(0.35f, 0.85f, 1f));
                CreateCell(rowRect, "Rarity", card.Rarity.ToString().ToUpperInvariant(), 5, TextAlignmentOptions.Left, 12, RarityColor(card.Rarity));

                // In-deck count, out of the copy limit, as the mockup shows it.
                int currentCount = currentDeck.ContainsKey(card.Id) ? currentDeck[card.Id] : 0;
                TMP_Text countText = CreateCell(rowRect, "Count",
                    $"{currentCount} / {DeckValidator.MaxCopiesPerCard}", 6, TextAlignmentOptions.Center, 14, Color.white);

                // Add button, pinned to the row's right edge.
                Button addBtn = CreateButton(rowRect, "Btn_Add", "+",
                    new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(40, 30));
                string cardId = card.Id;
                addBtn.onClick.AddListener(() => AddCard(cardId));
                // Both limits bite here: the third copy, and the fortieth card.
                addBtn.interactable = currentCount < DeckValidator.MaxCopiesPerCard && !atCap;

                if (pendingPopCardId == card.Id)
                {
                    UiTween.Pop(countText.transform, new Vector3(1.4f, 1.4f, 1f));
                    pendingPopCardId = null;
                }

                if (pendingFlashCardId == card.Id)
                {
                    var hot = new Color(0.35f, 0.62f, 0.95f, 0.30f);
                    var rest = new Color(1f, 1f, 1f, 0.03f);
                    Image captured = rowImage;
                    captured.color = hot;
                    UiTween.On(captured.transform).PlayStep(0.45f, UiEase.OutCubic,
                        k => captured.color = Color.Lerp(hot, rest, k));
                    pendingFlashCardId = null;
                }

                yPos -= RowHeight;
            }

            // Set content size to fit the header and every card, with padding.
            float totalHeight = (factionCards.Count + 1) * RowHeight + 16f;
            collectionContent.sizeDelta = new Vector2(0, totalHeight);
        }

        void RefreshDeck()
        {
            ClearChildren(deckContent);

            var deckCards = currentDeck
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => new { CardId = kvp.Key, Count = kvp.Value, Card = allCards.FirstOrDefault(c => c.Id == kvp.Key) })
                .Where(x => x.Card != null)
                .OrderBy(x => x.Card.Cost)
                .ThenBy(x => x.Card.Name)
                .ToList();

            int totalCount = TotalCards();
            deckSizeLabel.text = "CURRENT DECK";
            deckSizeLabel.color = Color.white;
            if (deckCountLabel != null)
            {
                deckCountLabel.text = totalCount.ToString("00") + " / 30\u201340";
                deckCountLabel.color = totalCount > DeckValidator.MaxDeckSize
                    ? new Color(1f, 0.3f, 0.3f)
                    : new Color(0.16f, 0.875f, 0.949f, 1f);
            }
            RefreshDeckStats(deckCards.Select(x => (x.Card, x.Count)), totalCount);
            if (emptyStateObject != null) emptyStateObject.SetActive(totalCount == 0);

            float yPos = -8f;

            foreach (var entry in deckCards)
            {
                GameObject cardRow = new GameObject($"DeckCard_{entry.CardId}");
                RectTransform rowRect = cardRow.AddComponent<RectTransform>();
                rowRect.SetParent(deckContent, false);
                rowRect.anchorMin = new Vector2(0, 1);
                rowRect.anchorMax = new Vector2(1, 1);
                rowRect.pivot = new Vector2(0, 1);
                rowRect.anchoredPosition = new Vector2(0, yPos);
                rowRect.sizeDelta = new Vector2(-10, 35); // -10 for padding

                Image rowImage = cardRow.AddComponent<Image>();
                rowImage.color = new Color(1f, 1f, 1f, 0.03f);
                rowImage.raycastTarget = false;

                // Card info. Same stretched, left-aligned treatment as the collection rows.
                TMP_Text infoText = CreateLabel(rowRect, "Info",
                    $"[{entry.Card.Cost}] {entry.Card.Name}",
                    new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, 14);
                infoText.alignment = TextAlignmentOptions.Left;
                infoText.raycastTarget = false;
                infoText.rectTransform.offsetMin = new Vector2(8, 0);
                infoText.rectTransform.offsetMax = new Vector2(-120, 0);

                // Count display
                TMP_Text countText = CreateLabel(rowRect, "Count",
                    $"x{entry.Count}",
                    new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(40, 30), 12);
                countText.raycastTarget = false;

                // Remove button
                Button removeBtn = CreateButton(rowRect, "Btn_Remove", "-",
                    new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(40, 30));
                string cardId = entry.CardId;
                removeBtn.onClick.AddListener(() => RemoveCard(cardId));

                yPos -= RowHeight;
            }

            // Set content size to fit all cards with padding
            float totalHeight = Mathf.Max(deckCards.Count * RowHeight + 16f, 40f);
            deckContent.sizeDelta = new Vector2(0, totalHeight);
        }

        /// <summary>
        /// Totals the stats row from the deck's cards. Non-unit cards carry no combat stats, so they add
        /// nothing to ATK, HP or shield. Average NP is the mean cost across every copy, shown as a dash
        /// while the deck is empty rather than a misleading zero.
        /// </summary>
        void RefreshDeckStats(IEnumerable<(CardDefinition Card, int Count)> deckCards, int totalCount)
        {
            if (statValueLabels == null || statValueLabels.Length < 4) return;

            int atk = 0, hp = 0, shield = 0, costTotal = 0;
            foreach (var entry in deckCards)
            {
                CardDefinition card = entry.Card;
                int copies = entry.Count;
                costTotal += card.Cost * copies;
                if (card.Type != CardType.Unit) continue;
                atk += card.Atk * copies;
                hp += card.Hp * copies;
                shield += card.Shield * copies;
            }

            statValueLabels[0].text = atk.ToString();
            statValueLabels[1].text = hp.ToString();
            statValueLabels[2].text = shield.ToString();
            statValueLabels[3].text = totalCount > 0 ? (costTotal / (float)totalCount).ToString("0.0") : "\u2014";
        }

        void RefreshValidation()
        {
            var deck = BuildDeckList();
            var result = DeckValidator.Validate(deck);

            int total = TotalCards();
            const string keptNote = "Drafts are kept when switching decks or going back.";

            if (result.HasErrors)
            {
                string line;
                if (total < DeckValidator.MinDeckSize)
                    line = "Add " + (DeckValidator.MinDeckSize - total) + " cards to reach the minimum.";
                else if (total > DeckValidator.MaxDeckSize)
                    line = "Remove " + (total - DeckValidator.MaxDeckSize) + " cards to reach the maximum.";
                else
                    line = result.Issues.First(i => i.Severity == Severity.Error).Message;

                validationLabel.text = "<color=#FFB020>DRAFT  /  " + total + (total == 1 ? " CARD" : " CARDS") + "</color>\n<size=18>" + line
                    + "</size>\n<size=13><color=#9AA8B8>" + keptNote + "</color></size>";
                btnSave.interactable = false;
            }
            else
            {
                validationLabel.text = "<color=#29DFF2>READY  /  " + total + (total == 1 ? " CARD" : " CARDS") + "</color>\n<size=18>Deck is legal and ready to save.</size>\n<size=13><color=#9AA8B8>"
                    + keptNote + "</color></size>";
                btnSave.interactable = true;
            }
        }

        /// <summary>
        /// Builds the eleven bars once. Heights are then tweened in <see cref="RefreshCostCurve"/> so the
        /// curve moves instead of being torn down and rebuilt on every card change.
        /// </summary>
        void BuildCostCurve(Transform parent)
        {
            float totalWidth = (CostBarWidth + CostBarSpacing) * costBars.Length;
            float startX = -totalWidth / 2f;

            for (int cost = 0; cost < costBars.Length; cost++)
            {
                float xPos = startX + (CostBarWidth + CostBarSpacing) * cost + CostBarWidth / 2f;

                GameObject barObj = new GameObject($"Bar_{cost}");
                RectTransform barRect = barObj.AddComponent<RectTransform>();
                barRect.SetParent(parent, false);
                barRect.anchorMin = new Vector2(0.5f, 0);
                barRect.anchorMax = new Vector2(0.5f, 0);
                barRect.pivot = new Vector2(0.5f, 0);
                barRect.anchoredPosition = new Vector2(xPos, CostBarBaseline);
                barRect.sizeDelta = new Vector2(CostBarWidth, 0f);

                Image barImage = barObj.AddComponent<Image>();
                barImage.color = new Color(0.3f, 0.6f, 1f, 0.85f);
                barImage.raycastTarget = false;

                // Count rides the top of its own bar.
                TMP_Text count = CreateLabel(barRect, "Count", string.Empty,
                    new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 9), new Vector2(CostBarWidth, 14), 10);
                count.raycastTarget = false;

                // Cost axis label stays pinned to the panel so it never rides the bar.
                TMP_Text axis = CreateLabel(parent, $"Cost_{cost}", cost.ToString(),
                    new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(xPos, 11), new Vector2(CostBarWidth, 14), 10);
                axis.raycastTarget = false;

                costBars[cost] = barRect;
                costBarImages[cost] = barImage;
                costBarCounts[cost] = count;
            }
        }

        void RefreshCostCurve()
        {
            var deck = BuildDeckList();
            int[] costCounts = new int[11]; // 0-10
            foreach (var card in deck)
            {
                if (card != null && card.Cost >= 0 && card.Cost <= 10)
                {
                    costCounts[card.Cost]++;
                }
            }

            int maxCount = costCounts.Max();
            if (maxCount == 0) maxCount = 1;

            for (int cost = 0; cost < costBars.Length; cost++)
            {
                RectTransform bar = costBars[cost];
                if (bar == null) continue;

                float from = bar.sizeDelta.y;
                float to = (costCounts[cost] / (float)maxCount) * CostBarMaxHeight;

                if (costBarCounts[cost] != null)
                {
                    costBarCounts[cost].text = costCounts[cost] > 0 ? costCounts[cost].ToString() : string.Empty;
                }

                if (costBarImages[cost] != null)
                {
                    costBarImages[cost].color = costCounts[cost] > 0
                        ? new Color(0.3f, 0.6f, 1f, 0.85f)
                        : new Color(0.3f, 0.6f, 1f, 0.35f);
                }

                if (!Mathf.Approximately(from, to))
                {
                    RectTransform captured = bar;
                    UiTween.On(captured).PlayStep(0.18f, UiEase.OutCubic,
                        k => captured.sizeDelta = new Vector2(captured.sizeDelta.x, Mathf.Lerp(from, to, k)));
                }
            }
        }

        void AddCard(string cardId)
        {
            if (TotalCards() >= DeckValidator.MaxDeckSize)
            {
                ShakeValidation();
                return;
            }

            int current = currentDeck.TryGetValue(cardId, out int existing) ? existing : 0;
            if (current >= DeckValidator.MaxCopiesPerCard) return;

            currentDeck[cardId] = current + 1;
            pendingPopCardId = cardId;
            pendingFlashCardId = cardId;
            RefreshUI();
        }

        void RemoveCard(string cardId)
        {
            if (!currentDeck.ContainsKey(cardId) || currentDeck[cardId] <= 0) return;

            currentDeck[cardId]--;
            if (currentDeck[cardId] == 0)
                currentDeck.Remove(cardId);

            // The row is rebuilt on refresh, so the acknowledgement is a short shrink on the count the
            // player just decremented rather than a fade of a node that is about to be destroyed.
            pendingPopCardId = cardId;
            RefreshUI();
        }

        void OnFactionButtonClicked(Faction faction)
        {
            if (selectedFaction == faction)
                return; // Already selected

            if (TotalCards() > 0)
            {
                ShowConfirmation("Changing faction clears this deck. Continue?", () => ChangeFaction(faction));
                return;
            }
            ChangeFaction(faction);
        }

        void ChangeFaction(Faction faction)
        {
            selectedFaction = faction;
            currentDeck.Clear();
            RefreshUI();
        }

        void OnSaveClicked()
        {
            CommitDeckName();

            // Validate deck before saving
            var deckList = BuildDeckList();
            var validationResult = DeckValidator.Validate(deckList);

            if (validationResult.HasErrors)
            {
                // Show first error to user
                var firstError = validationResult.Issues.FirstOrDefault(i => i.Severity == Severity.Error);
                if (firstError != null)
                {
                    validationLabel.text = "ERROR: " + firstError.Message;
                    validationLabel.color = new Color(1f, 0.3f, 0.3f);
                    Debug.LogWarning("[DeckbuilderController] Cannot save invalid deck: " + firstError.Message);
                }
                else
                {
                    validationLabel.text = "ERROR: Deck validation failed.";
                    validationLabel.color = new Color(1f, 0.3f, 0.3f);
                }

                ShakeValidation();
                return;
            }

            // Deck is valid — write it to its slot in the shared library. The store accepts an
            // illegal draft too (the binder grows decks one card at a time); this validation is the
            // builder's own "you have a playable deck" confirmation, not a write precondition.
            EnsureActiveDeck();
            DeckData data = new DeckData
            {
                Version = 1,
                Faction = selectedFaction,
                Cards = currentDeck.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            DeckLibrary.Save(activeDeckId, DeckLibrary.NameOf(activeDeckId), data);

            Debug.Log("[DeckbuilderController] Deck saved: " + DeckLibrary.NameOf(activeDeckId));
            validationLabel.text = "OK - " + DeckLibrary.NameOf(activeDeckId) + " saved.";
            validationLabel.color = new Color(0.3f, 1f, 0.3f);
            PulseValidation(new Color(0.3f, 1f, 0.3f));
            RefreshDeckIdentity();
        }

        void OnLoadClicked()
        {
            ShowConfirmation("Reload the saved deck and discard current changes?", () =>
            {
                LoadDeck();
                RefreshUI();
            });
        }

        void OnClearClicked()
        {
            if (TotalCards() == 0) return;
            ShowConfirmation("Remove all cards from this deck?", () =>
            {
                currentDeck.Clear();
                RefreshUI();
            });
        }

        void OnBackClicked()
        {
            Debug.Log("[DeckbuilderController] Back pressed.");

            // Write through before leaving: this screen and the binder edit the same record, so a
            // card added here must already be in the binder's deck view when the player returns.
            // Persisting is not allowed to take the exit with it: this button is the screen's only way
            // out, and a bad record must not strand the player here.
            try
            {
                CommitDeckName();
                PersistActiveDeck();
            }
            catch (Exception error)
            {
                Debug.LogError("[DeckbuilderController] Could not write the deck before leaving.");
                Debug.LogException(error);
            }

            // Back belongs to the binder: that is the screen this one is an editor for, and the only
            // screen that opens it. Falling back to it also means Back works when the deck builder is
            // played directly from the Editor, where no launching screen was ever recorded.
            SceneLoader.Return(SceneIndex.Collection);
        }

        // ── Deck identity: which saved deck this screen is editing ────────────────────────────

        /// <summary>
        /// Makes sure there is always a deck to edit. A first-run player lands here with an empty
        /// library; creating the slot on entry means the Save button, the deck bar and the binder's
        /// chooser all have the same deck to talk about.
        /// </summary>
        void EnsureActiveDeck()
        {
            if (DeckLibrary.HasActive) return;
            DeckLibrary.Create(string.Empty, selectedFaction);
        }

        /// <summary>Binds the deck bar to the active deck: name field, position readout, arrows.</summary>
        void RefreshDeckIdentity()
        {
            IReadOnlyList<DeckSummary> decks = DeckLibrary.All();
            DeckSummary active = DeckLibrary.Active;

            if (active == null)
            {
                activeDeckId = string.Empty;
                if (deckNameInput != null) deckNameInput.SetTextWithoutNotify(string.Empty);
                if (titleLabel != null) titleLabel.text = "DECK BUILDER";
                if (btnPrevDeck != null) btnPrevDeck.interactable = false;
                if (btnNextDeck != null) btnNextDeck.interactable = false;
                return;
            }

            bool deckChanged = activeDeckId != active.Id;
            activeDeckId = active.Id;

            if (deckNameInput != null && (deckChanged || !deckNameInput.isFocused) && deckNameInput.text != active.Name)
                deckNameInput.SetTextWithoutNotify(active.Name);

            if (titleLabel != null)
            {
                int index = 0;
                for (int i = 0; i < decks.Count; i++)
                {
                    if (decks[i].Id == active.Id)
                    {
                        index = i;
                        break;
                    }
                }

                titleLabel.text = decks.Count > 1
                    ? "DECK BUILDER  \u2022  " + (index + 1) + " / " + decks.Count
                    : "DECK BUILDER";
            }

            // With a single deck the arrows would be a no-op; disable them rather than lie about it.
            bool many = decks.Count > 1;
            if (btnPrevDeck != null) btnPrevDeck.interactable = many;
            if (btnNextDeck != null) btnNextDeck.interactable = many;
            if (deckNameInput != null && btnNewDeck != null)
            {
                Link(btnBack, null, many ? (Selectable)btnPrevDeck : deckNameInput, btnNewDeck, deckNameInput);
                Link(deckNameInput, btnBack, btnFactionFsti, many ? (Selectable)btnPrevDeck : btnNewDeck,
                    many ? (Selectable)btnNextDeck : btnNewDeck);
                Link(btnNewDeck, btnBack, btnFactionFsti, many ? (Selectable)btnNextDeck : deckNameInput, deckNameInput);
                Link(btnFactionFsti, deckNameInput, btnSave, btnBack, btnFactionFrti);
                Link(btnFactionFrti, deckNameInput, btnSave, btnFactionFsti, btnFactionFpb);
                Link(btnFactionFpb, deckNameInput, btnSave, btnFactionFrti, btnFactionFsti);
            }
            if (deckHintLabel != null)
                deckHintLabel.text = many ? "Use arrows to switch decks. Drafts are kept." : "Create another deck to enable the arrows.";
        }

        /// <summary>Switches to the neighbouring saved deck, keeping unsaved edits on the one left behind.</summary>
        void CycleDeck(int step)
        {
            IReadOnlyList<DeckSummary> decks = DeckLibrary.All();
            if (decks.Count < 2) return;

            CommitDeckName();
            PersistActiveDeck();

            int current = 0;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i].Id == activeDeckId)
                {
                    current = i;
                    break;
                }
            }

            int next = ((current + step) % decks.Count + decks.Count) % decks.Count;
            DeckLibrary.SetActive(decks[next].Id);
            LoadDeck();
            RefreshUI();
        }

        /// <summary>Creates an empty deck and switches to it, so decks never overwrite each other.</summary>
        void OnNewDeckClicked()
        {
            CommitDeckName();
            PersistActiveDeck();

            // Blank name: the library auto-names ("Deck 3") and keeps names unique, so the player can
            // jump straight into building and rename later.
            string id = DeckLibrary.Create(string.Empty, selectedFaction);
            DeckLibrary.SetActive(id);
            LoadDeck();
            RefreshUI();
        }

        /// <summary>Renames the active deck on end-edit. Blank input keeps the current name.</summary>
        void OnDeckNameChanged(string value)
        {
            if (activeDeckId.Length == 0) return;

            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0 || trimmed == DeckLibrary.NameOf(activeDeckId))
            {
                // Restore the field so it can never render a nameless deck.
                if (deckNameInput != null) deckNameInput.SetTextWithoutNotify(DeckLibrary.NameOf(activeDeckId));
                return;
            }

            DeckLibrary.Save(activeDeckId, trimmed, DeckLibrary.Load(activeDeckId));
            RefreshDeckIdentity();
        }

        /// <summary>Writes the name field onto the active deck before the screen switches decks.</summary>
        void CommitDeckName()
        {
            if (deckNameInput == null) return;
            OnDeckNameChanged(deckNameInput.text);
        }

        /// <summary>Writes the on-screen deck to its slot without validating, so switching screens or
        /// decks never discards work in progress. Save is the "this deck is playable" gate.</summary>
        void PersistActiveDeck()
        {
            // Nothing loaded means nothing to write. Writing anyway would overwrite the player's saved
            // deck with an empty one, which is worse than skipping the write.
            if (string.IsNullOrEmpty(activeDeckId) || currentDeck == null) return;

            DeckData data = new DeckData
            {
                Version = 1,
                Faction = selectedFaction,
                Cards = currentDeck.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            DeckLibrary.Save(activeDeckId, DeckLibrary.NameOf(activeDeckId), data);
        }

        /// <summary>Brief green bloom on the validation line after a successful save.</summary>
        void PulseValidation(Color rest)
        {
            if (validationLabel == null) return;

            Color hot = new Color(0.75f, 1f, 0.75f);
            TMP_Text captured = validationLabel;
            UiTween.On(captured.transform).PlayStep(0.4f, UiEase.OutCubic,
                k => captured.color = Color.Lerp(hot, rest, k));
        }

        /// <summary>Short decaying shake used when an action is refused, so the refusal is felt as well as read.</summary>
        void ShakeValidation()
        {
            if (validationLabel == null || !UiVfxGate.Motion) return;

            RectTransform rect = validationLabel.rectTransform;
            float baseX = rect.anchoredPosition.x;
            UiTween.On(rect).PlayStep(0.3f, UiEase.Linear, k =>
            {
                float amplitude = 8f * (1f - k);
                float offset = Mathf.Sin(k * Mathf.PI * 6f) * amplitude;
                rect.anchoredPosition = new Vector2(baseX + offset, rect.anchoredPosition.y);
            }, () => rect.anchoredPosition = new Vector2(baseX, rect.anchoredPosition.y));
        }

        int TotalCards()
        {
            int total = 0;
            foreach (var count in currentDeck.Values)
                total += count;
            return total;
        }

        void LoadDeck()
        {
            EnsureActiveDeck();
            DeckSummary active = DeckLibrary.Active;
            if (active == null)
            {
                currentDeck = new Dictionary<string, int>();
                activeDeckId = string.Empty;
                return;
            }

            activeDeckId = active.Id;
            DeckData data = DeckLibrary.Load(active.Id);
            if (deckNameInput != null) deckNameInput.SetTextWithoutNotify(active.Name);

            // Validate that all card IDs exist in the library
            var validCards = new Dictionary<string, int>();
            int discardedCount = 0;

            if (data != null && data.Cards != null)
            {
                foreach (var kvp in data.Cards)
                {
                    var card = allCards.FirstOrDefault(c => c.Id == kvp.Key);
                    if (card != null && kvp.Value > 0 && kvp.Value <= DeckValidator.MaxCopiesPerCard)
                    {
                        validCards[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        discardedCount += kvp.Value;
                        Debug.LogWarning($"[DeckbuilderController] Discarded invalid card entry: {kvp.Key} x{kvp.Value}");
                    }
                }
            }

            currentDeck = validCards;
            selectedFaction = data != null ? data.Faction : selectedFaction;

            if (discardedCount > 0)
            {
                Debug.LogWarning($"[DeckbuilderController] Loaded deck with {discardedCount} invalid card(s) discarded.");
            }

            Debug.Log($"[DeckbuilderController] Deck loaded: {active.Name}, {currentDeck.Count} unique cards, faction {selectedFaction}");
        }

        List<CardDefinition> BuildDeckList()
        {
            var list = new List<CardDefinition>();
            foreach (var kvp in currentDeck)
            {
                var card = allCards.FirstOrDefault(c => c.Id == kvp.Key);
                if (card != null)
                {
                    for (int i = 0; i < kvp.Value; i++)
                    {
                        list.Add(card);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Destroys the previous rows and hides them in the same frame. Destroy is deferred to the end of
        /// the frame, so without the SetActive(false) the outgoing rows keep rendering underneath the
        /// rebuilt ones for a frame.
        /// </summary>
        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        // Helper UI creation methods
        GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject obj = new GameObject(name);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.09f, 0.13f, 0.18f, 0.96f);
            image.raycastTarget = false;

            return obj;
        }

        TMP_Text CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, int fontSize)
        {
            GameObject obj = new GameObject(name);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TMP_Text tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            // Labels are decoration: a label that ate clicks would sit invisibly in front of whatever
            // the next sibling puts under it (the title band overlaps the Back button on narrow windows).
            tmp.raycastTarget = false;

            return tmp;
        }

        // Collection table columns, as fractions of the row width. The Add button sits in the last
        // 5% of the row, so the Count column stops short of it.
        static readonly float[] CollectionColumnStart = { 0f, 0.08f, 0.42f, 0.52f, 0.62f, 0.72f, 0.86f };
        static readonly float[] CollectionColumnEnd = { 0.08f, 0.42f, 0.52f, 0.62f, 0.72f, 0.86f, 0.95f };
        static readonly TextAlignmentOptions[] CollectionColumnAlign =
        {
            TextAlignmentOptions.Left, TextAlignmentOptions.Left, TextAlignmentOptions.Center,
            TextAlignmentOptions.Center, TextAlignmentOptions.Center, TextAlignmentOptions.Left,
            TextAlignmentOptions.Center
        };

        void BuildCollectionHeader(Transform content, float yPos)
        {
            GameObject header = new GameObject("Collection Header");
            RectTransform rect = header.AddComponent<RectTransform>();
            rect.SetParent(content, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, yPos);
            rect.sizeDelta = new Vector2(-10, 30);

            Image background = header.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.06f);
            background.raycastTarget = false;

            string[] titles = { "NP", "NAME", "ATK", "HP", "SHD", "RARITY", "IN DECK" };
            Color dim = new Color(0.65f, 0.75f, 0.84f);
            for (int i = 0; i < titles.Length; i++)
                CreateCell(rect, "Head_" + titles[i], titles[i], i, CollectionColumnAlign[i], 12, dim);
        }

        TMP_Text CreateCell(Transform row, string name, string text, int column, TextAlignmentOptions align,
            int fontSize, Color color)
        {
            TMP_Text cell = CreateLabel(row, name, text,
                new Vector2(CollectionColumnStart[column], 0), new Vector2(CollectionColumnEnd[column], 1),
                Vector2.zero, Vector2.zero, fontSize);
            cell.alignment = align;
            cell.color = color;
            cell.enableWordWrapping = false;
            cell.overflowMode = TextOverflowModes.Ellipsis;
            cell.rectTransform.offsetMin = new Vector2(column == 0 ? 8 : 4, 0);
            cell.rectTransform.offsetMax = new Vector2(-4, 0);
            return cell;
        }

        static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return new Color(0.35f, 0.62f, 0.95f);
                case Rarity.Epic: return new Color(0.72f, 0.45f, 0.95f);
                case Rarity.Legendary: return new Color(1f, 0.7f, 0.25f);
                default: return new Color(0.75f, 0.78f, 0.82f);
            }
        }

        Button CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true; // Ensure button receives clicks

            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;

            // Set up color transitions for visual feedback
            var colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.22f, 0.29f, 1f);
            colors.highlightedColor = new Color(0.25f, 0.42f, 0.55f, 1f);
            colors.pressedColor = new Color(0.10f, 0.32f, 0.48f, 1f);
            colors.selectedColor = new Color(0.22f, 0.38f, 0.5f, 1f);
            colors.disabledColor = new Color(0.12f, 0.14f, 0.17f, 0.65f);
            button.colors = colors;

            // Button text
            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false; // Text doesn't need to block raycasts

            return button;
        }

        /// <summary>
        /// Single-line name field for the deck bar. TMP_InputField needs a viewport rect and a text
        /// component handed to it explicitly; this builds the minimum pair by hand rather than
        /// instantiating TMP's runtime template, which is a hierarchy this screen does not need.
        /// </summary>
        TMP_InputField CreateInputField(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            // Built inactive: TMP_InputField.OnEnable wires the text component, the caret renderer and
            // the vertex callbacks, and AddComponent runs it immediately. Enabling the field only after
            // its viewport and text exist is what makes that pass see a complete field.
            obj.SetActive(false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.16f, 0.16f, 0.16f, 1f);
            image.raycastTarget = true;

            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 2f);
            textRect.offsetMax = new Vector2(-10f, -2f);

            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = 14;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Left;
            textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
            textComponent.raycastTarget = false;

            TMP_InputField input = obj.AddComponent<TMP_InputField>();
            input.textViewport = textRect;
            input.textComponent = textComponent;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 24;
            input.pointSize = 14;
            input.text = text;
            input.onEndEdit.AddListener(OnDeckNameChanged);

            obj.SetActive(true);
            return input;
        }

        ScrollRect CreateScrollView(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject obj = new GameObject(name);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            image.raycastTarget = true; // Enable raycast for scroll interaction

            ScrollRect scroll = obj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            // Viewport with RectMask2D. The previous build used a Mask whose graphic was set to
            // Color.clear; a Mask clips by the graphic's alpha, so a clear graphic clipped every row
            // to nothing and the lists rendered empty. RectMask2D clips by rectangle and needs no
            // graphic at all, so there is no colour that can switch it off.
            GameObject viewportObj = new GameObject("Viewport");
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.SetParent(rect, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.pivot = new Vector2(0, 1);
            viewportObj.AddComponent<RectMask2D>();

            // Content
            GameObject contentObj = new GameObject("Content");
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 500);

            scroll.content = contentRect;
            scroll.viewport = viewportRect;

            return scroll;
        }
    }
}
