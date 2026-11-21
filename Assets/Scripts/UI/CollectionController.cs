using System;
using System.Collections.Generic;
using Overlink.Cards;
using Overlink.Core;
using Overlink.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Collection binder view for the Gacha flow.
    ///
    /// The screen is authored in <c>Assets/Scenes/Collection.unity</c> (rebuild it through
    /// <c>Overlink/Scenes/Build Collection Binder Scene</c>); this component only binds the authored
    /// widgets, clones the authored tile template per card, and applies filter/sort/detail state. It
    /// reads the same deterministic ownership data as the pack opener and deliberately has
    /// no purchase, server, or monetization surface.
    /// <para>
    /// The grid has two views: COLLECTION (the gacha catalog, unchanged) and DECK (the active saved
    /// deck). Both views read the deck through <see cref="DeckLibrary"/>, the same store the deck
    /// builder writes, so a deck saved there is exactly what this screen lists and edits.
    /// </para>
    /// </summary>
    public sealed class CollectionController : MonoBehaviour
    {
        [Header("Summary")]
        [SerializeField] TextMeshProUGUI countText;
        [SerializeField] TextMeshProUGUI fragmentText;

        [Header("Filters")]
        [SerializeField] Button typeFilterButton;
        [SerializeField] Button factionFilterButton;
        [SerializeField] Button rarityFilterButton;
        [SerializeField] Button viewToggleButton;
        [SerializeField] Button ownershipFilterButton;
        [SerializeField] Button sortButton;

        [Header("Binder")]
        [SerializeField] Transform gridRoot;
        [SerializeField] RectTransform gridViewport;
        [SerializeField] GameObject tileTemplate;
        [SerializeField] TextMeshProUGUI emptyText;
        [SerializeField] Transform worldCardRoot;
        [SerializeField] Camera worldCardCamera;
        [SerializeField] CardTemplateCatalog templateCatalog;
        [SerializeField] CardFrameCatalog frameCatalog;
        [SerializeField] CardTierMaterialSet tierMaterials;
        [SerializeField] CardPresentationFixture[] fixturesByRarity = new CardPresentationFixture[0];

        [Header("Detail")]
        [SerializeField] TextMeshProUGUI detailTitle;
        TextMeshProUGUI detailHeader;
        TextMeshProUGUI detailOwnedBadge;
        [SerializeField] TextMeshProUGUI detailOwnership;
        [SerializeField] TextMeshProUGUI detailBody;
        [SerializeField] TextMeshProUGUI detailRarity;
        [SerializeField] TextMeshProUGUI detailFactionType;
        [SerializeField] TextMeshProUGUI detailCost;
        [SerializeField] RectTransform detailCardAnchor;
        TextMeshProUGUI detailStats;

        [Header("Deck")]
        [SerializeField] TextMeshProUGUI deckText;
        [SerializeField] Button deckChooserButton;
        [SerializeField] Button editDeckButton;

        [Header("Navigation")]
        [SerializeField] Button openPackButton;
        [SerializeField] Button backButton;

        readonly List<GameObject> tiles = new List<GameObject>();

        // The three summary readouts roll through UiCounter (the gacha screens' widget) instead of
        // snapping: bound once in Awake, refreshed by value so filter clicks that change nothing
        // stay still while ownership changes animate. The formatters own the surrounding copy, so
        // the strings can never drift from what the static screen used to print.
        UiCounter countCounter;
        UiCounter fragmentCounter;
        UiCounter deckCounter;

        int typeFilter;
        int factionFilter;
        int rarityFilter;
        int ownershipFilter;
        int sortMode;
        bool deckView;
        string openedDeckId = string.Empty;
        [Header("Tabs")]
        [SerializeField] Button tabCardsButton;
        [SerializeField] Button tabDecksButton;
        [SerializeField] RectTransform deckListRoot;
        [SerializeField] RectTransform deckRows;
        [SerializeField] Button decksBackButton;
        [SerializeField] Button newDeckButton;
        CollectionData.CollectionCard selectedCard;

        sealed class FaceEntry
        {
            public RectTransform tile;
            public CardFaceView face;
            public Color shade;
            public CanvasGroup fadeGroup;
        }

        readonly List<FaceEntry> worldFaces = new List<FaceEntry>();
        CardFaceView detailFace;

        void Awake()
        {
            CollectionData.EnsureResolved();
            if (gridViewport == null && gridRoot != null)
                gridViewport = gridRoot.parent as RectTransform;
            ConfigureGrid();
            ResolveDeckViewRefs();
            BuildDetailHeaders();
            if (detailRarity != null) detailRarity.richText = true;
            if (detailFactionType != null) detailFactionType.richText = true;
            if (detailCost != null) detailCost.richText = true;
            if (detailBody != null) detailBody.richText = true;

            if (typeFilterButton != null) typeFilterButton.onClick.AddListener(CycleType);
            if (factionFilterButton != null) factionFilterButton.onClick.AddListener(CycleFaction);
            if (rarityFilterButton != null) rarityFilterButton.onClick.AddListener(CycleRarity);
            if (tabCardsButton != null) tabCardsButton.onClick.AddListener(() => SetDeckView(false));
            if (tabDecksButton != null) tabDecksButton.onClick.AddListener(() => SetDeckView(true));
            if (decksBackButton != null) decksBackButton.onClick.AddListener(() => SetDeckView(true));
            if (newDeckButton != null) newDeckButton.onClick.AddListener(CreateDeckAndEdit);
            if (ownershipFilterButton != null) ownershipFilterButton.onClick.AddListener(CycleOwnership);
            if (sortButton != null) sortButton.onClick.AddListener(CycleSort);
            if (editDeckButton != null) editDeckButton.onClick.AddListener(OpenDeckBuilder);
            if (openPackButton != null) openPackButton.onClick.AddListener(() => SceneLoader.Load(SceneIndex.Gacha));
            ApplyChamferedCorner(editDeckButton);
            ApplyChamferedCorner(openPackButton);
            if (backButton != null) backButton.onClick.AddListener(() => SceneLoader.Return());

            if (tileTemplate != null) tileTemplate.SetActive(false);
            if (deckListRoot != null) deckListRoot.gameObject.SetActive(false);
            if (newDeckButton != null) newDeckButton.gameObject.SetActive(false);
            if (decksBackButton != null) decksBackButton.gameObject.SetActive(false);
            RefreshViewTabs();
            countCounter = BindCounter(countText,
                v => "GAMEPLAY CARDS  " + v + " / " + CollectionData.BinderCards().Count + " UNIQUE");
            fragmentCounter = BindCounter(fragmentText, v => "DATA FRAGMENTS  " + v);
            deckCounter = BindCounter(deckText, v => DeckLabel(v));
            RefreshFilters();
        }

        /// <summary>Attaches a rolling readout to an authored summary label, seeded at zero so the
        /// first refresh rolls up to the real value as the screen's arrival beat.</summary>
        static UiCounter BindCounter(TextMeshProUGUI label, Func<int, string> format)
        {
            if (label == null) return null;
            UiCounter counter = label.GetComponent<UiCounter>();
            if (counter == null) counter = label.gameObject.AddComponent<UiCounter>();
            counter.Bind(format, 0);
            return counter;
        }

        void LateUpdate()
        {
            // The viewport can change with window size or canvas scaling. Reflow the same cards
            // without rebuilding them, and keep the current scroll position when the column count changes.
            if (UpdateGridColumns()) ResizeGridContent(tiles.Count, false);
            // ScrollRect and GridLayoutGroup finish their pass after Awake. Positioning in LateUpdate
            // also keeps the real prefab faces aligned while the binder is scrolled.
            PositionWorldFaces();
        }

        /// <summary>The header readout doubles as the deck chooser, so it names the active deck and
        /// advertises that it cycles.</summary>
        static string DeckLabel(int size)
        {
            DeckSummary active = DeckLibrary.Active;
            if (active == null)
                return "DECK  NONE  •  OPEN DECK BUILDER";
            return "DECK  " + active.Name.ToUpperInvariant() + "  " + size + "/" + CollectionData.DeckLimit
                + (active.IsLegal ? string.Empty : "  DRAFT") + "  < >";
        }

        void RefreshFilters()
        {
            List<CollectionData.CollectionCard> matches = new List<CollectionData.CollectionCard>();
            if (deckView) CollectDeckCards(matches);
            else CollectCatalogCards(matches);

            if (sortMode == 1) matches.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            else if (sortMode == 2) matches.Sort((a, b) => b.Rarity.CompareTo(a.Rarity));

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null) continue;
                tiles[i].SetActive(false);
                Destroy(tiles[i]);
            }
            tiles.Clear();
            for (int i = 0; i < worldFaces.Count; i++)
            {
                if (worldFaces[i].face != null) Destroy(worldFaces[i].face.gameObject);
            }
            worldFaces.Clear();
            for (int i = 0; i < matches.Count; i++) tiles.Add(CreateTile(matches[i]));
            ResizeGridContent(matches.Count);

            if (matches.Count > 0)
            {
                var selection = matches.Find(card => selectedCard != null && card.Id == selectedCard.Id);
                ShowDetail(selection ?? matches[0]);
            }
            else
            {
                selectedCard = null;
                if (detailFace != null) Destroy(detailFace.gameObject);
                detailFace = null;
                if (detailTitle != null) detailTitle.text = "SELECT A CARD";
                if (detailOwnership != null) detailOwnership.text = string.Empty;
                if (detailStats != null) detailStats.text = string.Empty;
                if (detailBody != null) detailBody.text = string.Empty;
            }

            UpdateEmptyText(matches.Count == 0);

            int ownedUnique = 0;
            List<CollectionData.CollectionCard> binderCards = CollectionData.BinderCards();
            for (int i = 0; i < binderCards.Count; i++)
            {
                if (CollectionData.Owned(binderCards[i].Id) > 0) ownedUnique++;
            }

            if (countCounter != null) countCounter.Set(ownedUnique);
            if (fragmentCounter != null) fragmentCounter.Set(CollectionData.Fragments());
            if (deckCounter != null) deckCounter.Set(CollectionData.DeckSize());
            UpdateFilterLabels();
        }

        void CollectCatalogCards(List<CollectionData.CollectionCard> into)
        {
            List<CollectionData.CollectionCard> binderCards = CollectionData.BinderCards();
            for (int i = 0; i < binderCards.Count; i++)
            {
                CollectionData.CollectionCard card = binderCards[i];
                if (!Matches(card)) continue;
                into.Add(card);
            }
        }

        /// <summary>One entry per unique card in the active deck; copies are shown as a badge and in the
        /// detail panel, so the deck reads as a list of choices rather than a pile of duplicates.</summary>
        void CollectDeckCards(List<CollectionData.CollectionCard> into)
        {
            string active = DeckLibrary.ActiveId;
            if (active.Length == 0) return;

            DeckData data = DeckLibrary.Load(active);
            foreach (var entry in data.Cards)
            {
                if (entry.Value <= 0) continue;
                CollectionData.CollectionCard card = CollectionData.Resolve(entry.Key);
                if (card == null || !Matches(card)) continue;
                into.Add(card);
            }
        }

        bool Matches(CollectionData.CollectionCard card)
        {
            if (typeFilter > 0 && (int)card.Type != typeFilter - 1) return false;
            if (factionFilter > 0 && (int)card.Faction != factionFilter - 1) return false;
            if (rarityFilter > 0 && (int)card.Rarity != rarityFilter - 1) return false;
            bool owned = CollectionData.Owned(card.Id) > 0;
            if (ownershipFilter == 1 && !owned) return false;
            if (ownershipFilter == 2 && owned) return false;
            return true;
        }

        void UpdateEmptyText(bool empty)
        {
            if (emptyText == null) return;
            emptyText.gameObject.SetActive(empty);
            if (!empty) return;

            if (deckView && !DeckLibrary.HasActive)
                emptyText.text = "NO SAVED DECK  •  OPEN THE DECK BUILDER TO CREATE ONE";
            else if (deckView)
                emptyText.text = "THIS DECK HAS NO CARDS YET";
            else
                emptyText.text = "NO CARDS MATCH THE CURRENT FILTERS";
        }

        GameObject CreateTile(CollectionData.CollectionCard card)
        {
            if (tileTemplate == null || gridRoot == null)
            {
                return null;
            }

            GameObject tile = Instantiate(tileTemplate, gridRoot);
            tile.name = "Card " + card.Id;
            tile.SetActive(true);

            bool owned = CollectionData.Owned(card.Id) > 0;
            Image image = tile.GetComponent<Image>();
            if (image != null)
            {
                image.color = owned ? new Color(0.07f, 0.105f, 0.17f, 1f) : new Color(0.045f, 0.055f, 0.08f, 1f);
            }

            Outline outline = tile.GetComponent<Outline>();
            if (outline != null) outline.effectColor = CollectionData.RarityColor(card.Rarity);

            Button button = tile.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowDetail(card));
            }

            // The binder tile is now an interaction shell only. The visible card is the same authored
            // CardFaceView prefab used by Gacha and the Card Manager; the old flat TMP tile content
            // made the collection look like a separate, non-production card system.
            DisableTileText(tile);
            SetTileCaption(tile, card, owned);
            SpawnWorldFace(tile, card, owned);
            SpawnOwnedCount(tile, CollectionData.Owned(card.Id));
            return tile;
        }

        /// <summary>
        /// The mockup's owned count sits outside the card's bottom-right corner. It is a sibling of the tile's
        /// label set, so it is created here rather than authored, and it never takes clicks.
        /// </summary>
        static void SpawnOwnedCount(GameObject tile, int owned)
        {
            Transform existing = tile.transform.Find("OwnedCount");
            TextMeshProUGUI label = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (label == null)
            {
                GameObject obj = new GameObject("OwnedCount", typeof(RectTransform));
                obj.transform.SetParent(tile.transform, false);
                RectTransform rect = obj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(6f, 4f);
                rect.sizeDelta = new Vector2(44f, 18f);
                label = obj.AddComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.fontSize = 13f;
                label.alignment = TextAlignmentOptions.Left;
                label.color = new Color(0.65f, 0.75f, 0.84f, 1f);
            }

            label.text = "\u00d7" + owned;
            label.gameObject.SetActive(owned > 0);
        }

        static void DisableTileText(GameObject tile)
        {
            TextMeshProUGUI[] labels = tile.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++) labels[i].gameObject.SetActive(false);
        }

        static void SetTileCaption(GameObject tile, CollectionData.CollectionCard card, bool owned)
        {
            Transform nameNode = tile.transform.Find("Name");
            Transform metaNode = tile.transform.Find("Meta");
            TextMeshProUGUI name = nameNode != null ? nameNode.GetComponent<TextMeshProUGUI>() : null;
            TextMeshProUGUI meta = metaNode != null ? metaNode.GetComponent<TextMeshProUGUI>() : null;
            if (name != null)
            {
                name.gameObject.SetActive(true);
                name.text = owned ? card.Name : "UNKNOWN SIGNAL";
                name.fontSize = 20f;
                name.alignment = TextAlignmentOptions.Left;
                name.raycastTarget = false;
                name.rectTransform.anchorMin = new Vector2(0.08f, 0.09f);
                name.rectTransform.anchorMax = new Vector2(0.92f, 0.16f);
                name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            }
            if (meta != null)
            {
                meta.gameObject.SetActive(true);
                meta.text = card.Type.ToString().ToUpperInvariant() + " / " + card.Rarity.ToString().ToUpperInvariant();
                meta.fontSize = 12f;
                meta.alignment = TextAlignmentOptions.Left;
                meta.raycastTarget = false;
                meta.rectTransform.anchorMin = new Vector2(0.08f, 0.02f);
                meta.rectTransform.anchorMax = new Vector2(0.92f, 0.08f);
                meta.rectTransform.offsetMin = meta.rectTransform.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// Reuses the authored Meta label as an in-deck marker instead of authoring another tile node.
        /// The badge sits over the top-right of the face, where it cannot collide with the card's own
        /// name strip.
        /// </summary>
        static void ShowDeckBadge(GameObject tile, int copies)
        {
            if (copies <= 0) return;

            Transform meta = tile.transform.Find("Meta");
            TextMeshProUGUI label = meta != null ? meta.GetComponent<TextMeshProUGUI>() : null;
            if (label == null) return;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.90f);
            rect.anchorMax = new Vector2(0.95f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            label.gameObject.SetActive(true);
            label.text = "IN DECK  x" + copies;
            label.color = new Color(0.16f, 0.87f, 0.95f, 1f);
            label.fontSize = 14f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.TopRight;
            label.raycastTarget = false;
        }

        void SpawnWorldFace(GameObject tile, CollectionData.CollectionCard card, bool owned)
        {
            if (worldCardRoot == null || worldCardCamera == null || templateCatalog == null) return;
            CardTemplateFixture(card, out CardPresentationFixture fixture);
            CardFaceView template = templateCatalog.Get(card.Type, card.Rarity);
            if (template == null) return;

            CardFaceData data = new CardFaceData(card.Id, card.Name, card.Type, card.Faction, card.Rarity,
                card.Cost, CardLibraryBootstrap.ArtFor(card.Id) ?? (fixture != null ? fixture.artSprite : null),
                card.Type == CardType.Unit ? (int?)card.Atk : null,
                card.Type == CardType.Unit ? (int?)card.Hp : null,
                card.Type == CardType.Unit ? (int?)card.Shield : null,
                string.IsNullOrEmpty(card.Rules) && fixture != null ? fixture.abilityText : card.Rules);

            CardFaceView face = Instantiate(template.gameObject, worldCardRoot, false).GetComponent<CardFaceView>();
            if (face == null) return;
            face.name = "CollectionCard " + card.Id;
            face.PreviewRarity = card.Rarity;
            face.Bind(data, frameCatalog, tierMaterials);
            face.SetSortBaseline(worldFaces.Count * 100 + 100);
            Color shade = owned ? Color.white : new Color(0.28f, 0.34f, 0.43f, 1f);
            face.SetShade(shade);
            CanvasGroup fadeGroup = tile.GetComponent<CanvasGroup>() ?? tile.AddComponent<CanvasGroup>();
            worldFaces.Add(new FaceEntry { tile = tile.GetComponent<RectTransform>(), face = face, shade = shade, fadeGroup = fadeGroup });
        }

        void CardTemplateFixture(CollectionData.CollectionCard card, out CardPresentationFixture fixture)
        {
            fixture = null;
            int index = (int)card.Rarity;
            if (fixturesByRarity != null && index >= 0 && index < fixturesByRarity.Length)
            {
                fixture = fixturesByRarity[index];
            }
        }

        void PositionWorldFaces()
        {
            if (worldCardCamera == null || worldFaces.Count == 0 || Screen.width <= 0 || Screen.height <= 0) return;
            float worldHeight = worldCardCamera.orthographicSize * 2f;
            float worldWidth = worldHeight * worldCardCamera.aspect;
            float worldPerPixel = worldWidth / Screen.width;
            bool gridShown = gridRoot != null && gridRoot.parent.gameObject.activeSelf;
            for (int i = 0; i < worldFaces.Count; i++)
            {
                FaceEntry entry = worldFaces[i];
                if (entry == null || entry.tile == null || entry.face == null) continue;
                if (!gridShown)
                {
                    entry.face.gameObject.SetActive(false);
                    continue;
                }
                Vector3 tileWorld = entry.tile.TransformPoint(entry.tile.rect.center);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, tileWorld);
                Vector3 world = worldCardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
                    Mathf.Abs(worldCardCamera.transform.position.z)));
                // A portrait face must fit inside the authored grid cell in both dimensions. The
                // previous width-only calculation made a 1.46:1 card taller than the 320px cell,
                // so the second binder row visually climbed into the first one.
                float cellWidth = entry.tile.rect.width * Mathf.Abs(entry.tile.lossyScale.x);
                float cellHeight = entry.tile.rect.height * Mathf.Abs(entry.tile.lossyScale.y);
                float pixelWidth = Mathf.Min(cellWidth, cellHeight * 0.78f / 1.4615f) * 0.95f;
                float width = pixelWidth * worldPerPixel;
                screen.y += cellHeight * 0.09f;
                entry.face.transform.position = new Vector3(world.x, world.y + cellHeight * worldPerPixel * 0.09f, 0f);
                entry.face.SetSize(new Vector3(width, width * 1.4615f, 0.01f));
                float opacity = GridEdgeOpacity(screen, pixelWidth, pixelWidth * 1.4615f);
                entry.face.SetShade(new Color(entry.shade.r, entry.shade.g, entry.shade.b, opacity));
                entry.face.gameObject.SetActive(opacity > 0.001f);
                if (entry.fadeGroup != null)
                {
                    entry.fadeGroup.alpha = opacity;
                    entry.fadeGroup.blocksRaycasts = opacity > 0.1f;
                }
            }
            if (detailFace != null && detailCardAnchor != null)
            {
                Vector3 anchor = detailCardAnchor.TransformPoint(detailCardAnchor.rect.center);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, anchor);
                Vector3 world = worldCardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
                    Mathf.Abs(worldCardCamera.transform.position.z)));
                float width = Mathf.Min(detailCardAnchor.rect.width, detailCardAnchor.rect.height / 1.4615f) * worldPerPixel;
                detailFace.transform.position = new Vector3(world.x, world.y, 0f);
                detailFace.SetSize(new Vector3(width, width * 1.4615f, 0.01f));
            }
        }

        // The card faces use SpriteRenderers, so the uGUI viewport mask cannot clip them. Fade only the
        // cards that the viewport actually cuts through; a card fully inside the grid stays opaque.
        float GridEdgeOpacity(Vector2 center, float pixelWidth, float pixelHeight)
        {
            if (gridViewport == null) return 1f;

            Vector3[] corners = new Vector3[4];
            gridViewport.GetWorldCorners(corners);
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            float halfWidth = pixelWidth * 0.5f;
            float halfHeight = pixelHeight * 0.5f;
            float horizontal = Mathf.Min(center.x - halfWidth - minX, maxX - center.x - halfWidth);
            float vertical = Mathf.Min(center.y - halfHeight - minY, maxY - center.y - halfHeight);
            float fadeDistance = Mathf.Max(1f, pixelHeight * 0.04f);
            return Mathf.Clamp01(Mathf.Min(horizontal, vertical) / fadeDistance);
        }

        void ConfigureGrid()
        {
            GridLayoutGroup layout = gridRoot != null ? gridRoot.GetComponent<GridLayoutGroup>() : null;
            if (layout == null) return;
            layout.cellSize = new Vector2(210f, 300f);
            layout.spacing = new Vector2(3f, 3f);
            layout.padding.left = 4;
            layout.padding.right = 4;
            layout.padding.top = 12;
            layout.padding.bottom = 12;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = BinderColumns;
            UpdateGridColumns();
        }

        /// <summary>The binder is a fixed 5 columns by 2 visible rows. Cells are sized from the viewport so the
        /// two rows fill its height, which makes each card larger; any further cards scroll.</summary>
        public const int BinderColumns = 5;
        public const int BinderVisibleRows = 2;

        bool UpdateGridColumns()
        {
            GridLayoutGroup layout = gridRoot != null ? gridRoot.GetComponent<GridLayoutGroup>() : null;
            if (layout == null || gridViewport == null || gridViewport.rect.width <= 0f) return false;

            float availableWidth = Mathf.Max(0f, gridViewport.rect.width - layout.padding.horizontal
                - layout.spacing.x * (BinderColumns - 1));
            float cellWidth = availableWidth / BinderColumns;
            float cellHeight = cellWidth * (layout.cellSize.y / layout.cellSize.x);
            float availableHeight = Mathf.Max(0f, gridViewport.rect.height - layout.padding.vertical
                - layout.spacing.y * (BinderVisibleRows - 1));
            float maxHeightRows = availableHeight / BinderVisibleRows;
            if (maxHeightRows < cellHeight)
            {
                cellHeight = maxHeightRows;
                cellWidth = cellHeight * (layout.cellSize.x / layout.cellSize.y);
            }

            Vector2 size = new Vector2(Mathf.Floor(cellWidth), Mathf.Floor(cellHeight));
            bool changed = layout.constraintCount != BinderColumns || layout.cellSize != size;
            layout.constraintCount = BinderColumns;
            layout.cellSize = size;
            return changed;
        }

        void ResizeGridContent(int count, bool resetScroll = true)
        {
            RectTransform content = gridRoot as RectTransform;
            GridLayoutGroup layout = gridRoot != null ? gridRoot.GetComponent<GridLayoutGroup>() : null;
            if (content == null || layout == null) return;
            int columns = Mathf.Max(1, layout.constraintCount);
            int rows = Mathf.CeilToInt((float)count / columns);
            float height = layout.padding.vertical + rows * layout.cellSize.y + Mathf.Max(0, rows - 1) * layout.spacing.y;
            if (gridViewport != null) height = Mathf.Max(height, gridViewport.rect.height);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ScrollRect scroll = gridViewport != null ? gridViewport.GetComponent<ScrollRect>() : null;
            if (scroll != null && resetScroll)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        void ShowDetail(CollectionData.CollectionCard card)
        {
            selectedCard = card;
            int owned = CollectionData.Owned(card.Id);
            if (detailFace != null) Destroy(detailFace.gameObject);
            detailFace = null;
            if (detailCardAnchor != null && worldCardRoot != null && templateCatalog != null)
            {
                CardTemplateFixture(card, out CardPresentationFixture fixture);
                CardFaceView template = templateCatalog.Get(card.Type, card.Rarity);
                if (template != null)
                {
                    detailFace = Instantiate(template.gameObject, worldCardRoot, false).GetComponent<CardFaceView>();
                    if (detailFace != null)
                    {
                        var data = new CardFaceData(card.Id, card.Name, card.Type, card.Faction, card.Rarity,
                            card.Cost, CardLibraryBootstrap.ArtFor(card.Id) ?? (fixture != null ? fixture.artSprite : null),
                            card.Type == CardType.Unit ? (int?)card.Atk : null,
                            card.Type == CardType.Unit ? (int?)card.Hp : null,
                            card.Type == CardType.Unit ? (int?)card.Shield : null,
                            string.IsNullOrEmpty(card.Rules) && fixture != null ? fixture.abilityText : card.Rules);
                        detailFace.PreviewRarity = card.Rarity;
                        detailFace.Bind(data, frameCatalog, tierMaterials);
                        detailFace.SetSortBaseline(10000);
                        if (owned <= 0) detailFace.SetShade(new Color(0.28f, 0.34f, 0.43f, 1f));
                    }
                }
            }
            if (detailTitle != null) detailTitle.text = owned > 0 ? card.Name : "UNKNOWN SIGNAL";
            if (detailOwnership != null)
            {
                int duplicates = Mathf.Max(0, owned - 1);
                detailOwnership.text = owned > 0
                    ? "Owned · Original copy · " + (duplicates == 0 ? "No duplicates"
                        : duplicates == 1 ? "1 duplicate" : duplicates + " duplicates")
                    : "MISSING · CRAFTING PATH NOT YET AVAILABLE";
            }

            if (detailStats != null)
            {
                detailStats.text = card.Type == CardType.Unit
                    ? "ATK " + card.Atk + "   HP " + card.Hp + "   SHIELD " + card.Shield
                    : string.Empty;
            }

            if (detailHeader != null) detailHeader.text = "SELECTED CARD  /  " + card.Id.Substring(card.Id.Length - 3);
            if (detailOwnedBadge != null) detailOwnedBadge.text = "OWNED  ×" + owned;
            if (detailRarity != null) detailRarity.text = DetailField("RARITY", card.Rarity.ToString().ToUpperInvariant());
            if (detailFactionType != null) detailFactionType.text = DetailField("TYPE / FACTION", card.Type.ToString().ToUpperInvariant() + " / " + FactionLabel(card.Faction));
            if (detailCost != null)
            {
                detailCost.text = "<size=13><color=#8296A9>DEPLOY COST</color></size>\n<size=64>" + card.Cost.ToString("00")
                    + "</size><size=22> NP</size>";
            }
            if (detailBody != null)
            {
                CardTemplateFixture(card, out CardPresentationFixture bodyFixture);
                detailBody.text = string.IsNullOrEmpty(card.Rules) && bodyFixture != null ? bodyFixture.abilityText : card.Rules;
            }
        }

        // ── Views and deck selection ──────────────────────────────────────────────────────────

        void SetDeckView(bool value)
        {
            deckView = value;
            openedDeckId = string.Empty;
            selectedCard = null;
            if (deckListRoot != null) deckListRoot.gameObject.SetActive(deckView);
            if (newDeckButton != null) newDeckButton.gameObject.SetActive(deckView);
            if (decksBackButton != null) decksBackButton.gameObject.SetActive(false);
            if (gridRoot != null) gridRoot.parent.gameObject.SetActive(!deckView);
            RefreshViewTabs();
            RefreshFilters();
            RefreshDeckList();
        }

        /// <summary>Marks the active CARD / DECK COLLECTION tab. The tabs replace the old VIEW filter.</summary>
        void RefreshViewTabs()
        {
            if (viewToggleButton != null) viewToggleButton.gameObject.SetActive(false);
            StyleViewTab(tabCardsButton, !deckView);
            StyleViewTab(tabDecksButton, deckView);
        }

        static void StyleViewTab(Button tab, bool active)
        {
            if (tab == null) return;
            Color fill = active ? new Color(0.161f, 0.875f, 0.949f, 1f) : new Color(0.106f, 0.149f, 0.204f, 1f);
            Color hot = active ? new Color(0.196f, 1f, 1f, 1f) : new Color(0.16f, 0.22f, 0.29f, 1f);
            Color pressed = active ? new Color(0.126f, 0.683f, 0.740f, 1f) : new Color(0.10f, 0.32f, 0.48f, 1f);

            // Button transitions repaint the graphic from its colour block, so the block has to carry the
            // active state too. Setting only the graphic is overwritten the next time the button changes state.
            ColorBlock colors = tab.colors;
            colors.normalColor = fill;
            colors.highlightedColor = hot;
            colors.pressedColor = pressed;
            colors.selectedColor = hot;
            tab.colors = colors;

            Image background = tab.GetComponent<Image>();
            if (background != null) background.color = fill;
            TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = active ? new Color(0.03f, 0.08f, 0.11f, 1f) : new Color(0.871f, 0.910f, 0.933f, 1f);
        }

        /// <summary>Lists every saved deck with its size, legality, and battle tag. Rows are rebuilt on each refresh.</summary>
        void RefreshDeckList()
        {
            if (deckRows == null) return;
            for (int i = deckRows.childCount - 1; i >= 0; i--)
            {
                GameObject child = deckRows.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            VerticalLayoutGroup stack = deckRows.GetComponent<VerticalLayoutGroup>();
            if (stack == null) stack = deckRows.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 8f;
            stack.padding = new RectOffset(12, 12, 12, 12);
            stack.childControlWidth = true;
            stack.childControlHeight = false;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            foreach (DeckSummary deck in DeckLibrary.All())
            {
                GameObject row = new GameObject("Deck " + deck.Id);
                RectTransform rect = row.AddComponent<RectTransform>();
                rect.SetParent(deckRows, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 64f);

                Image image = row.AddComponent<Image>();
                image.color = deck.IsActive ? new Color(0.1f, 0.3f, 0.4f, 1f) : new Color(0.07f, 0.1f, 0.15f, 1f);

                Button button = row.AddComponent<Button>();
                string id = deck.Id;
                button.onClick.AddListener(() => OpenDeck(id));

                string status = deck.IsLegal ? "READY" : "DRAFT";
                string battle = DeckLibrary.IsBattle(deck.Id) ? "  •  IN BATTLE" : string.Empty;
                CreateRowLabel(rect, deck.Name.ToUpperInvariant(), new Vector2(0.02f, 0f), new Vector2(0.36f, 1f), 18, TextAlignmentOptions.Left);
                CreateRowLabel(rect, deck.CardCount + " / " + CollectionData.DeckLimit + "  •  " + status + battle,
                    new Vector2(0.36f, 0f), new Vector2(0.6f, 1f), 14, TextAlignmentOptions.Right);

                GameObject useObj = new GameObject("UseInBattle");
                RectTransform useRect = useObj.AddComponent<RectTransform>();
                useRect.SetParent(rect, false);
                useRect.anchorMin = new Vector2(0.62f, 0f);
                useRect.anchorMax = new Vector2(0.98f, 1f);
                useRect.offsetMin = new Vector2(0f, 8f);
                useRect.offsetMax = new Vector2(0f, -8f);
                Button useButton = useObj.AddComponent<Button>();
                useObj.AddComponent<Image>().color = new Color(0.16f, 0.48f, 0.7f, 1f);
                useButton.interactable = deck.IsLegal && !DeckLibrary.IsBattle(deck.Id);
                CreateRowLabel(useRect, "USE IN BATTLE", Vector2.zero, Vector2.one, 13, TextAlignmentOptions.Center);
                useButton.onClick.AddListener(() => SetBattleDeck(id));
            }
        }

        static void CreateRowLabel(RectTransform parent, string text, Vector2 min, Vector2 max, int size, TextAlignmentOptions align)
        {
            GameObject obj = new GameObject("Label");
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.raycastTarget = false;
        }

        void ResolveDeckViewRefs()
        {
            if (tabCardsButton == null) tabCardsButton = FindNamed<Button>("Tab_Cards");
            if (tabDecksButton == null) tabDecksButton = FindNamed<Button>("Tab_Decks");
            if (deckListRoot == null) deckListRoot = FindNamed<RectTransform>("DeckList");
            if (deckRows == null) deckRows = deckListRoot;
            if (decksBackButton == null) decksBackButton = FindNamed<Button>("DecksBack");
            if (newDeckButton == null) newDeckButton = FindNamed<Button>("NewDeck");
        }

        void BuildDetailHeaders()
        {
            if (detailCardAnchor == null) return;
            RectTransform panel = detailCardAnchor.parent as RectTransform;
            if (panel == null) return;

            detailHeader = CreateDetailText(panel, "DetailHeader", TextAlignmentOptions.Left, 14,
                new Vector2(0.04f, 0.94f), new Vector2(0.5f, 0.99f));
            detailOwnedBadge = CreateDetailText(panel, "DetailOwnedBadge", TextAlignmentOptions.Right, 14,
                new Vector2(0.5f, 0.94f), new Vector2(0.96f, 0.99f));
            detailStats = CreateDetailText(panel, "DetailStats", TextAlignmentOptions.Left, 22,
                new Vector2(0.04f, 0.265f), new Vector2(0.96f, 0.305f));
            detailStats.color = new Color(0.87f, 0.91f, 0.93f, 1f);
        }

        static TextMeshProUGUI CreateDetailText(RectTransform parent, string name, TextAlignmentOptions alignment,
            int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.51f, 0.588f, 0.663f, 1f);
            return label;
        }

        T FindNamed<T>(string name) where T : Component
        {
            foreach (T candidate in GetComponentsInChildren<T>(true))
            {
                if (candidate.name == name) return candidate;
            }
            return null;
        }

        /// <summary>Marks a legal deck as the battle deck. Drafts are refused, matching the match loader.</summary>
        void SetBattleDeck(string id)
        {
            DeckData data = DeckLibrary.Load(id);
            if (DeckValidator.Validate(CollectionData.ExpandDeck(data)).HasErrors) return;
            DeckLibrary.SetBattle(id);
            RefreshDeckList();
        }

        void OpenDeck(string id)
        {
            DeckLibrary.SetActive(id);
            openedDeckId = id;
            if (deckListRoot != null) deckListRoot.gameObject.SetActive(false);
            if (newDeckButton != null) newDeckButton.gameObject.SetActive(false);
            if (decksBackButton != null) decksBackButton.gameObject.SetActive(true);
            if (gridRoot != null) gridRoot.parent.gameObject.SetActive(true);
            RefreshFilters();
        }

        /// <summary>
        /// Cycles the active deck. With no saved decks at all the chooser is not a dead end: it hands
        /// the player to the deck builder, which creates the first deck on entry.
        /// </summary>
        void CycleDeck()
        {
            IReadOnlyList<DeckSummary> decks = DeckLibrary.All();
            if (decks.Count == 0)
            {
                OpenDeckBuilder();
                return;
            }

            int current = 0;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i].IsActive)
                {
                    current = i;
                    break;
                }
            }

            DeckLibrary.SetActive(decks[(current + 1) % decks.Count].Id);
            selectedCard = null;
            RefreshFilters();
        }

        void OpenDeckBuilder()
        {
            SceneLoader.LoadFrom(SceneIndex.Deckbuilder, SceneIndex.Collection);
        }

        void CreateDeckAndEdit()
        {
            string id = DeckLibrary.Create(string.Empty, Faction.Fsti);
            DeckLibrary.SetActive(id);
            OpenDeckBuilder();
        }

        void CycleType() { typeFilter = (typeFilter + 1) % 5; RefreshFilters(); }
        void CycleFaction() { factionFilter = (factionFilter + 1) % 4; RefreshFilters(); }
        void CycleRarity() { rarityFilter = (rarityFilter + 1) % 5; RefreshFilters(); }
        void CycleOwnership() { ownershipFilter = (ownershipFilter + 1) % 3; RefreshFilters(); }
        void CycleSort() { sortMode = (sortMode + 1) % 3; RefreshFilters(); }

        void UpdateFilterLabels()
        {
            if (typeFilterButton != null) SetButtonLabel(typeFilterButton, "TYPE  •  " + (typeFilter == 0 ? "ANY" : ((CardType)(typeFilter - 1)).ToString().ToUpperInvariant()));
            if (factionFilterButton != null) SetButtonLabel(factionFilterButton, "FACTION  •  " + (factionFilter == 0 ? "ANY" : FactionLabel((Faction)(factionFilter - 1))));
            if (rarityFilterButton != null) SetButtonLabel(rarityFilterButton, "RARITY  •  " + (rarityFilter == 0 ? "ANY" : ((Rarity)(rarityFilter - 1)).ToString().ToUpperInvariant()));
            if (viewToggleButton != null) SetButtonLabel(viewToggleButton, "VIEW  •  " + (deckView ? "DECK" : "COLLECTION"));
            if (ownershipFilterButton != null) SetButtonLabel(ownershipFilterButton, "OWNERSHIP  •  " + (ownershipFilter == 0 ? "ALL" : ownershipFilter == 1 ? "OWNED" : "MISSING"));
            if (sortButton != null) SetButtonLabel(sortButton, "SORT  •  " + (sortMode == 0 ? "CATALOG" : sortMode == 1 ? "NAME" : "RARITY"));
        }

        static string DetailField(string label, string value)
        {
            return "<size=13><color=#8296A9>" + label + "</color></size>\n<size=26>" + value + "</size>";
        }

        static string FactionLabel(Faction faction)
        {
            return faction == Faction.Fsti ? "FSTI" : faction == Faction.Frti ? "FRTI" : "FPB";
        }

        static Sprite chamferSprite;

        static void ApplyChamferedCorner(Button button)
        {
            Image image = button != null ? button.GetComponent<Image>() : null;
            if (image == null) return;

            if (chamferSprite == null) chamferSprite = BuildChamferSprite(48, 14);
            image.sprite = chamferSprite;
            image.type = Image.Type.Sliced;
        }

        static Sprite BuildChamferSprite(int size, int cut)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool bevelled = (size - 1 - x) + y < cut;
                    pixels[y * size + x] = bevelled ? new Color32(255, 255, 255, 0) : new Color32(255, 255, 255, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(cut, cut, cut, cut));
        }

        static void SetButtonLabel(Button button, string value)
        {
            TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (text != null) text.text = value;
        }
    }
}
