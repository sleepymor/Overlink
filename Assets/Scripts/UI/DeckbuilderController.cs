// Assets/Scripts/UI/DeckbuilderController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Overlink.Cards;
using Overlink.Core;
using Overlink.Net;
using Overlink.Persistence;
using Overlink.Vfx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Functional deck builder with faction, type, rarity and sort filters over a card grid, add/remove with 3-copy limit,
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
        const float CardTileWidth = 212f;
        const float CardTileHeight = 264f;
        const int CollectionColumns = 4;
        static readonly CardType[] TypeOrder = { CardType.Unit, CardType.Ability, CardType.Relay, CardType.Protocol };
        static readonly string[] SortNames = { "COST", "NAME", "RARITY" };

        // UI references (created at runtime)
        private Button btnBack;
        private TMP_InputField deckNameInput;
        private TMP_Text titleLabel;
        private Button btnFactionFsti;
        private Button btnFactionFrti;
        private Button btnFactionFpb;
        private Button btnTypeFilter;
        private Button btnRarityFilter;
        private Button btnSortFilter;
        private ScrollRect collectionScroll;
        private RectTransform collectionContent;
        private ScrollRect deckScroll;
        private RectTransform deckContent;
        private TMP_Text collectionTitleLabel;
        private TMP_Text deckSizeLabel;
        private TMP_Text validationLabel;
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
        [SerializeField] private Sprite backgroundArt;

        private Faction selectedFaction = Faction.Fsti;
        private int typeFilter;   // 0 = all, otherwise TypeOrder[typeFilter - 1]
        private int rarityFilter; // 0 = all, otherwise (Rarity)(rarityFilter - 1)
        private int sortMode;     // index into SortNames
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
            allCards = CollectionData.DeckPool(Faction.Fsti)
                .Concat(CollectionData.DeckPool(Faction.Frti))
                .Concat(CollectionData.DeckPool(Faction.Fpb))
                .ToList();

            // The binder's per-card deck and this screen's old single slot both feed the shared
            // library, so a deck built before the unification is still here. One entry point runs both
            // imports in the right order, whichever screen the player opened first.
            CollectionData.EnsureDeckMigration();
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

        bool quitting;
        bool persistedOnExit;
        CanvasGroup transitionInputBlocker;
        void OnApplicationQuit() => quitting = true;

        void OnEnable() => LobbyManager.PrepareForGameplayAsync = PrepareDraftForGameplayAsync;

        void OnDisable()
        {
            if (LobbyManager.PrepareForGameplayAsync == (Func<Task>)PrepareDraftForGameplayAsync)
                LobbyManager.PrepareForGameplayAsync = null;
        }

        async Task PrepareDraftForGameplayAsync()
        {
            if (string.IsNullOrEmpty(activeDeckId) || currentDeck == null) return;
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud == null || !cloud.IsReady)
                throw new InvalidOperationException("Cloud progression is not ready.");

            Canvas canvas = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                canvas = root.GetComponentInChildren<Canvas>(false);
                if (canvas != null) break;
            }
            if (canvas != null)
            {
                transitionInputBlocker = canvas.GetComponent<CanvasGroup>();
                if (transitionInputBlocker == null) transitionInputBlocker = canvas.gameObject.AddComponent<CanvasGroup>();
            }
            bool wasInteractable = transitionInputBlocker == null || transitionInputBlocker.interactable;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (transitionInputBlocker != null) transitionInputBlocker.interactable = false;
            try
            {
                string name = deckNameInput != null ? deckNameInput.text.Trim() : "";
                if (string.IsNullOrEmpty(name)) name = DeckLibrary.NameOf(activeDeckId);
                var draft = new DeckData
                {
                    Version = 1,
                    Faction = selectedFaction,
                    Cards = currentDeck.ToDictionary(entry => entry.Key, entry => entry.Value)
                };
                await cloud.SaveDraftConfirmedAsync(activeDeckId, name, draft);
                if (this == null) throw new InvalidOperationException("Deck editor closed before the draft was confirmed.");
            }
            finally
            {
                if (transitionInputBlocker != null) transitionInputBlocker.interactable = wasInteractable;
            }
        }

        void OnDestroy()
        {
            if (!Application.isPlaying || quitting || persistedOnExit || !uiBuilt) return;
            // Matchmaking may transition away while this screen has uncommitted edits.
            // Save the draft separately; the account's last-valid battle snapshot survives it.
            try
            {
                CommitDeckName();
                PersistActiveDeck();
            }
            catch (Exception error)
            {
                Debug.LogError("[DeckbuilderController] Could not save the draft before the match: " + error.Message);
            }
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

            // Signal bay art over the dark base. Letterboxing on other aspect ratios shows the base colour.
            if (backgroundArt != null)
            {
                GameObject artObj = new GameObject("Background Art");
                RectTransform artRect = artObj.AddComponent<RectTransform>();
                artRect.SetParent(bgRect, false);
                artRect.anchorMin = Vector2.zero;
                artRect.anchorMax = Vector2.one;
                artRect.offsetMin = Vector2.zero;
                artRect.offsetMax = Vector2.zero;
                Image art = artObj.AddComponent<Image>();
                art.sprite = backgroundArt;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }

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
            btnBack = CreateButton(mainRect, "Btn_Back", "BACK TO MENU", new Vector2(0, 1), new Vector2(0, 1),
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
            deckBar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            deckNameInput = CreateInputField(deckBar.transform, "Deck Name", string.Empty, new Vector2(0, 0), new Vector2(0, 0));
            RectTransform nameRect = deckNameInput.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(0, 6);
            nameRect.offsetMax = new Vector2(0, -6);

            // Left panel: the card grid, with the filter row between its title and the list.
            GameObject collectionPanel = CreatePanel(mainRect, "Collection Panel",
                new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(Gutter, BottomBarHeight + Gutter),
                new Vector2(-Gutter * 0.5f, -HeaderHeight));
            collectionPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            collectionTitleLabel = CreateLabel(collectionPanel.transform, "Collection Title", "CARD COLLECTION",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -22), new Vector2(400, 30), 22);
            collectionTitleLabel.alignment = TextAlignmentOptions.Left;
            collectionTitleLabel.rectTransform.pivot = new Vector2(0f, 1f);

            BuildFilterRow(collectionPanel.transform);

            collectionScroll = CreateScrollView(collectionPanel.transform, "Collection Scroll",
                Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -110));
            collectionContent = collectionScroll.content;
            collectionScroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            GridLayoutGroup grid = collectionContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CardTileWidth, CardTileHeight);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CollectionColumns;
            grid.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = collectionContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
                statCard.GetComponent<Image>().color = UiKit.Card;
                TMP_Text statTitle = CreateLabel(statCard.transform, "Title", statTitles[i],
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -12), new Vector2(120, 16), 11);
                statTitle.alignment = TextAlignmentOptions.Left;
                statTitle.rectTransform.pivot = new Vector2(0f, 1f);
                statValueLabels[i] = CreateLabel(statCard.transform, "Value", "0",
                    new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 28);
                statValueLabels[i].alignment = TextAlignmentOptions.Left;
                statValueLabels[i].rectTransform.offsetMin = new Vector2(12, 6);
                statValueLabels[i].rectTransform.offsetMax = new Vector2(-12, -36);
                statValueLabels[i].color = UiKit.Primary;
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
            deckScroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            deckPanel.GetComponent<Image>().color = new Color(UiKit.Card.r, UiKit.Card.g, UiKit.Card.b, 0.6f);
            // The empty state sits over the deck scroll, so it must draw after it to be visible.
            emptyStateObject.transform.SetAsLastSibling();

            // Footer: cost curve on the left, status line and actions on the right.
            GameObject bottomPanel = CreatePanel(mainRect, "Bottom Panel",
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(Gutter, Gutter), new Vector2(-Gutter, Gutter + BottomBarHeight));
            bottomPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            GameObject costCurveObj = CreatePanel(bottomPanel.transform, "Cost Curve",
                new Vector2(0, 0), new Vector2(0.3f, 1), new Vector2(10, 10), new Vector2(-5, -10));
            costCurveObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            TMP_Text curveTitle = CreateLabel(costCurveObj.transform, "Cost Curve Title", "COST CURVE",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0, -14), new Vector2(200, 20), 13);
            curveTitle.alignment = TextAlignmentOptions.Left;
            curveTitle.rectTransform.pivot = new Vector2(0f, 1f);
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
            saveImage.color = UiKit.Primary;
            var saveColors = btnSave.colors;
            saveColors.normalColor = UiKit.Primary;
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
            Link(btnBack, null, deckNameInput, null, deckNameInput);
            Link(deckNameInput, btnBack, btnFactionFsti, null, null);
            Link(btnFactionFsti, deckNameInput, btnTypeFilter, btnBack, btnFactionFrti);
            Link(btnFactionFrti, deckNameInput, btnTypeFilter, btnFactionFsti, btnFactionFpb);
            Link(btnFactionFpb, deckNameInput, btnTypeFilter, btnFactionFrti, btnTypeFilter);
            Link(btnTypeFilter, btnFactionFsti, btnSave, btnFactionFpb, btnRarityFilter);
            Link(btnRarityFilter, btnFactionFsti, btnSave, btnTypeFilter, btnSortFilter);
            Link(btnSortFilter, btnFactionFsti, btnSave, btnRarityFilter, btnSave);
            Link(btnSave, btnSortFilter, null, btnClear, btnLoad);
            Link(btnLoad, btnRarityFilter, null, btnSave, btnClear);
            Link(btnClear, btnTypeFilter, null, btnLoad, btnSave);
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

        static void SetFactionStyle(Button button, bool selected) => UiKit.StyleTab(button, selected);

        void RefreshCollection()
        {
            ClearChildren(collectionContent);

            var cards = FilteredCollection();

            int total = TotalCards();
            bool atCap = total >= DeckValidator.MaxDeckSize;

            if (collectionTitleLabel != null)
            {
                collectionTitleLabel.text = atCap
                    ? $"Card Collection - deck full ({total}/{DeckValidator.MaxDeckSize})"
                    : "Card Collection";
                collectionTitleLabel.color = atCap ? new Color(1f, 0.78f, 0.35f) : Color.white;
            }

            foreach (var card in cards)
            {
                int currentCount = currentDeck.ContainsKey(card.Id) ? currentDeck[card.Id] : 0;
                bool canAdd = currentCount < DeckValidator.MaxCopiesPerCard && !atCap;
                GameObject tile = BuildCardTile(card, currentCount, canAdd);

                if (pendingPopCardId == card.Id)
                {
                    UiTween.Pop(tile.transform, new Vector3(1.08f, 1.08f, 1f));
                    pendingPopCardId = null;
                }

                if (pendingFlashCardId == card.Id)
                {
                    var hot = new Color(0.35f, 0.62f, 0.95f, 0.30f);
                    Image captured = tile.GetComponent<Image>();
                    captured.color = hot;
                    UiTween.On(captured.transform).PlayStep(0.45f, UiEase.OutCubic,
                        k => captured.color = Color.Lerp(hot, TileRestColor, k));
                    pendingFlashCardId = null;
                }
            }

            if (cards.Count == 0 && collectionTitleLabel != null)
                collectionTitleLabel.text = "NO CARDS MATCH THE FILTERS";
        }

        static readonly Color TileRestColor = new Color(0.07f, 0.105f, 0.17f, 1f);

        /// <summary>The faction's pool after the type, rarity and sort filters. Faction is always applied:
        /// a deck is single-faction, so the grid only ever shows the faction being built.</summary>
        List<CardDefinition> FilteredCollection()
        {
            var cards = CollectionData.DeckPool(selectedFaction).AsEnumerable();
            if (typeFilter > 0) cards = cards.Where(c => c.Type == TypeOrder[typeFilter - 1]);
            if (rarityFilter > 0) cards = cards.Where(c => (int)c.Rarity == rarityFilter - 1);

            switch (sortMode)
            {
                case 1: cards = cards.OrderBy(c => c.Name).ThenBy(c => c.Cost); break;
                case 2: cards = cards.OrderByDescending(c => c.Rarity).ThenBy(c => c.Cost).ThenBy(c => c.Name); break;
                default: cards = cards.OrderBy(c => c.Cost).ThenBy(c => c.Name); break;
            }
            return cards.ToList();
        }

        /// <summary>
        /// One binder-style tile: the card's art, a rarity outline, the cost badge, the name and type line,
        /// and the copy count. The whole tile is the add target, so a click adds one copy.
        /// </summary>
        GameObject BuildCardTile(CardDefinition card, int currentCount, bool canAdd)
        {
            GameObject tile = new GameObject($"Card_{card.Id}");
            RectTransform rect = tile.AddComponent<RectTransform>();
            rect.SetParent(collectionContent, false);
            rect.sizeDelta = new Vector2(CardTileWidth, CardTileHeight);

            Image background = tile.AddComponent<Image>();
            background.color = currentCount > 0 ? new Color(0.09f, 0.16f, 0.23f, 1f) : TileRestColor;
            background.raycastTarget = true;

            Outline outline = tile.AddComponent<Outline>();
            outline.effectColor = RarityColor(card.Rarity);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = tile.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.16f, 0.24f, 0.33f, 1f);
            colors.pressedColor = new Color(0.10f, 0.32f, 0.48f, 1f);
            colors.disabledColor = new Color(0.06f, 0.07f, 0.09f, 1f);
            button.colors = colors;
            string cardId = card.Id;
            button.onClick.AddListener(() => AddCard(cardId));
            button.interactable = canAdd;

            // Art fills the upper part of the tile, under a name strip.
            GameObject artObj = new GameObject("Art", typeof(RectTransform));
            artObj.transform.SetParent(tile.transform, false);
            RectTransform artRect = artObj.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0f, 0.5f);
            artRect.anchorMax = new Vector2(1f, 1f);
            artRect.offsetMin = new Vector2(6f, 0f);
            artRect.offsetMax = new Vector2(-6f, -6f);
            Image art = artObj.AddComponent<Image>();
            art.raycastTarget = false;
            art.preserveAspect = true;
            Sprite sprite = CardLibraryBootstrap.ArtFor(card.Id);
            art.sprite = sprite;
            art.color = sprite != null ? Color.white : new Color(0.14f, 0.2f, 0.27f, 1f);

            // Bands are fractions of the 264px tile, bottom-up: name, type/rarity, stats, then the text.
            TMP_Text name = CreateLabel(tile.transform, "Name", card.Name,
                new Vector2(0f, 0.386f), new Vector2(1f, 0.485f), Vector2.zero, Vector2.zero, 15);
            name.alignment = TextAlignmentOptions.Left;
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.rectTransform.offsetMin = new Vector2(8f, 0f);
            name.rectTransform.offsetMax = new Vector2(-8f, 0f);

            TMP_Text meta = CreateLabel(tile.transform, "Meta",
                card.Type.ToString().ToUpperInvariant() + " / " + card.Rarity.ToString().ToUpperInvariant(),
                new Vector2(0f, 0.318f), new Vector2(1f, 0.379f), Vector2.zero, Vector2.zero, 10);
            meta.alignment = TextAlignmentOptions.Left;
            meta.color = RarityColor(card.Rarity);
            meta.rectTransform.offsetMin = new Vector2(8f, 0f);
            meta.rectTransform.offsetMax = new Vector2(-8f, 0f);

            bool isUnit = card.Type == CardType.Unit;
            if (isUnit)
            {
                TMP_Text stats = CreateLabel(tile.transform, "Stats",
                    "ATK " + card.Atk + "   SHD " + card.Shield + "   HP " + card.Hp,
                    new Vector2(0f, 0.235f), new Vector2(1f, 0.311f), Vector2.zero, Vector2.zero, 12);
                stats.alignment = TextAlignmentOptions.Left;
                stats.color = new Color(0.35f, 0.85f, 1f);
                stats.enableWordWrapping = false;
                stats.rectTransform.offsetMin = new Vector2(8f, 0f);
                stats.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }

            // Units show their ability; other cards show what they do.
            TMP_Text body = CreateLabel(tile.transform, "Body", card.AbilityText ?? string.Empty,
                new Vector2(0f, 0.015f), new Vector2(1f, isUnit ? 0.227f : 0.303f), Vector2.zero, Vector2.zero, 11);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(0.8f, 0.86f, 0.92f);
            body.overflowMode = TextOverflowModes.Ellipsis;
            body.rectTransform.offsetMin = new Vector2(8f, 0f);
            body.rectTransform.offsetMax = new Vector2(-8f, 0f);

            // Cost badge, top-left, as the binder's NP corner.
            TMP_Text cost = CreateLabel(tile.transform, "Cost", "NP " + card.Cost.ToString("00"),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(64f, 24f), 16);
            cost.rectTransform.pivot = new Vector2(0f, 1f);
            cost.color = new Color(0.35f, 0.85f, 1f);
            cost.fontStyle = FontStyles.Bold;

            // Copies in the deck, top-right. Shown only when the card is in the deck.
            if (currentCount > 0)
            {
                TMP_Text copies = CreateLabel(tile.transform, "InDeck", "x" + currentCount + " / " + DeckValidator.MaxCopiesPerCard,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(90f, 22f), 13);
                copies.rectTransform.pivot = new Vector2(1f, 1f);
                copies.alignment = TextAlignmentOptions.Right;
                copies.color = UiKit.Primary;
                copies.fontStyle = FontStyles.Bold;
            }

            return tile;
        }

        /// <summary>
        /// Faction buttons, type / rarity / sort filters for the card grid. Faction stays a separate
        /// control because it clears the deck; the other three only narrow the view.
        /// </summary>
        void BuildFilterRow(Transform parent)
        {
            GameObject row = new GameObject("Filter Row", typeof(RectTransform));
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(20f, -58f);
            rowRect.sizeDelta = new Vector2(-40f, 40f);

            // Centres are measured from the row's left edge; the six buttons total 840 of the ~885 available.
            btnFactionFsti = CreateButton(rowRect, "Btn_Fsti", "FSTI",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(55f, 0f), new Vector2(110f, 34f));
            btnFactionFsti.onClick.AddListener(() => OnFactionButtonClicked(Faction.Fsti));

            btnFactionFrti = CreateButton(rowRect, "Btn_Frti", "FRTI",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(173f, 0f), new Vector2(110f, 34f));
            btnFactionFrti.onClick.AddListener(() => OnFactionButtonClicked(Faction.Frti));

            btnFactionFpb = CreateButton(rowRect, "Btn_Fpb", "FPB",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(291f, 0f), new Vector2(110f, 34f));
            btnFactionFpb.onClick.AddListener(() => OnFactionButtonClicked(Faction.Fpb));

            btnTypeFilter = CreateButton(rowRect, "Btn_Type", "TYPE - ANY",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(429f, 0f), new Vector2(150f, 34f));
            btnTypeFilter.onClick.AddListener(CycleType);

            btnRarityFilter = CreateButton(rowRect, "Btn_Rarity", "RARITY - ANY",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(597f, 0f), new Vector2(170f, 34f));
            btnRarityFilter.onClick.AddListener(CycleRarity);

            btnSortFilter = CreateButton(rowRect, "Btn_Sort", "SORT - COST",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(765f, 0f), new Vector2(150f, 34f));
            btnSortFilter.onClick.AddListener(CycleSort);

            UpdateFactionButtonStates();
            RefreshFilterLabels();
        }

        void CycleType()
        {
            typeFilter = (typeFilter + 1) % (TypeOrder.Length + 1);
            RefreshFilterLabels();
            RefreshUI();
        }

        void CycleRarity()
        {
            rarityFilter = (rarityFilter + 1) % 5;
            RefreshFilterLabels();
            RefreshUI();
        }

        void CycleSort()
        {
            sortMode = (sortMode + 1) % SortNames.Length;
            RefreshFilterLabels();
            RefreshUI();
        }

        // Filter labels use the binder's "TYPE - ANY" wording and cyan text on a dark chip.
        void RefreshFilterLabels()
        {
            if (btnTypeFilter != null)
            {
                string type = typeFilter == 0 ? "ANY" : TypeOrder[typeFilter - 1].ToString().ToUpperInvariant();
                SetFilterText(btnTypeFilter, "TYPE - " + type);
            }
            if (btnRarityFilter != null)
            {
                string rarity = rarityFilter == 0 ? "ANY" : ((Rarity)(rarityFilter - 1)).ToString().ToUpperInvariant();
                SetFilterText(btnRarityFilter, "RARITY - " + rarity);
            }
            if (btnSortFilter != null)
                SetFilterText(btnSortFilter, "SORT - " + SortNames[sortMode]);
        }

        static void SetFilterText(Button chip, string text)
        {
            TMP_Text label = chip.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.color = UiKit.Primary;
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
                    : UiKit.Primary;
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
            statValueLabels[3].text = totalCount > 0 ? (costTotal / (float)totalCount).ToString("0.0") : "-";
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
            // Bars start at the panel's left edge, so the first bar lines up with the title and the
            // BACK TO MENU button above it.
            for (int cost = 0; cost < costBars.Length; cost++)
            {
                float xPos = (CostBarWidth + CostBarSpacing) * cost + CostBarWidth / 2f;

                GameObject barObj = new GameObject($"Bar_{cost}");
                RectTransform barRect = barObj.AddComponent<RectTransform>();
                barRect.SetParent(parent, false);
                barRect.anchorMin = new Vector2(0f, 0);
                barRect.anchorMax = new Vector2(0f, 0);
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
                    new Vector2(0f, 0), new Vector2(0f, 0), new Vector2(xPos, 11), new Vector2(CostBarWidth, 14), 10);
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
                persistedOnExit = true;
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

        /// <summary>Binds the deck bar to the active deck: the name field and the title.</summary>
        void RefreshDeckIdentity()
        {
            DeckSummary active = DeckLibrary.Active;

            if (active == null)
            {
                activeDeckId = string.Empty;
                if (deckNameInput != null) deckNameInput.SetTextWithoutNotify(string.Empty);
                if (titleLabel != null) titleLabel.text = "DECK BUILDER";
                return;
            }

            bool deckChanged = activeDeckId != active.Id;
            activeDeckId = active.Id;

            if (deckNameInput != null && (deckChanged || !deckNameInput.isFocused) && deckNameInput.text != active.Name)
                deckNameInput.SetTextWithoutNotify(active.Name);
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
            if (UiKit.DisplayFont != null) tmp.font = UiKit.DisplayFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            // Labels are decoration: a label that ate clicks would sit invisibly in front of whatever
            // the next sibling puts under it (the title band overlaps the Back button on narrow windows).
            tmp.raycastTarget = false;

            return tmp;
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

            // Same ghost style as the binder's buttons: dark card fill, bright label, cyan top edge.
            var colors = button.colors;
            colors.normalColor = UiKit.Card;
            colors.highlightedColor = new Color(0.16f, 0.22f, 0.29f, 1f);
            colors.pressedColor = new Color(0.10f, 0.32f, 0.48f, 1f);
            colors.selectedColor = new Color(0.16f, 0.22f, 0.29f, 1f);
            colors.disabledColor = new Color(0.12f, 0.14f, 0.17f, 0.65f);
            button.colors = colors;

            GameObject edge = new GameObject("AccentEdge", typeof(RectTransform));
            RectTransform edgeRect = edge.GetComponent<RectTransform>();
            edgeRect.SetParent(rect, false);
            edgeRect.anchorMin = new Vector2(0f, 1f);
            edgeRect.anchorMax = new Vector2(1f, 1f);
            edgeRect.pivot = new Vector2(0.5f, 1f);
            edgeRect.sizeDelta = new Vector2(0f, 2f);
            Image edgeImage = edge.AddComponent<Image>();
            edgeImage.color = new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.3f);
            edgeImage.raycastTarget = false;

            // Button text
            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (UiKit.DisplayFont != null) tmp.font = UiKit.DisplayFont;
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiKit.TextBright;
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
            image.color = UiKit.InputBackground;
            image.raycastTarget = true;

            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 2f);
            textRect.offsetMax = new Vector2(-10f, -2f);

            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            if (UiKit.DisplayFont != null) textComponent.font = UiKit.DisplayFont;
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
