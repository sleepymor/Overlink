using System.Collections.Generic;
using Overlink.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Overlink.Core;

namespace Overlink.UI
{
    /// <summary>
    /// Offline binder view for Gacha plan Milestone 2.
    ///
    /// The screen is authored in <c>Assets/Scenes/Collection.unity</c> (rebuild it through
    /// <c>Overlink/Scenes/Build Collection Binder Scene</c>); this component only binds the authored
    /// widgets, clones the authored tile template per catalog card, and applies filter/sort/detail
    /// state. It reads the same deterministic ownership data as the pack opener and deliberately has
    /// no purchase, server, or monetization surface.
    /// </summary>
    public sealed class OfflineCollectionController : MonoBehaviour
    {
        [Header("Summary")]
        [SerializeField] TextMeshProUGUI countText;
        [SerializeField] TextMeshProUGUI fragmentText;

        [Header("Filters")]
        [SerializeField] Button typeFilterButton;
        [SerializeField] Button factionFilterButton;
        [SerializeField] Button rarityFilterButton;
        [SerializeField] Button setFilterButton;
        [SerializeField] Button ownershipFilterButton;
        [SerializeField] Button sortButton;

        [Header("Binder")]
        [SerializeField] Transform gridRoot;
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
        [SerializeField] TextMeshProUGUI detailOwnership;
        [SerializeField] TextMeshProUGUI detailBody;

        [Header("Deck")]
        [SerializeField] TextMeshProUGUI deckText;
        [SerializeField] Button addToDeckButton;

        [Header("Navigation")]
        [SerializeField] Button openPackButton;
        [SerializeField] Button backButton;

        readonly List<GameObject> tiles = new List<GameObject>();
        int typeFilter;
        int factionFilter;
        int rarityFilter;
        int setFilter;
        int ownershipFilter;
        int sortMode;
        OfflineCollectionData.CollectionCard selectedCard;

        sealed class FaceEntry
        {
            public RectTransform tile;
            public CardFaceView face;
        }

        readonly List<FaceEntry> worldFaces = new List<FaceEntry>();

        void Awake()
        {
            OfflineCollectionData.EnsureResolved();

            if (typeFilterButton != null) typeFilterButton.onClick.AddListener(CycleType);
            if (factionFilterButton != null) factionFilterButton.onClick.AddListener(CycleFaction);
            if (rarityFilterButton != null) rarityFilterButton.onClick.AddListener(CycleRarity);
            if (setFilterButton != null) setFilterButton.onClick.AddListener(CycleSet);
            if (ownershipFilterButton != null) ownershipFilterButton.onClick.AddListener(CycleOwnership);
            if (sortButton != null) sortButton.onClick.AddListener(CycleSort);
            if (addToDeckButton != null) addToDeckButton.onClick.AddListener(AddSelectedToDeck);
            if (openPackButton != null) openPackButton.onClick.AddListener(() => SceneLoader.Load(SceneIndex.Gacha));
            if (backButton != null) backButton.onClick.AddListener(SceneLoader.LoadMainMenu);

            if (tileTemplate != null) tileTemplate.SetActive(false);
            RefreshFilters();
        }

        void LateUpdate()
        {
            // ScrollRect and GridLayoutGroup finish their pass after Awake. Positioning in LateUpdate
            // also keeps the real prefab faces aligned while the binder is scrolled.
            PositionWorldFaces();
        }

        void RefreshFilters()
        {
            List<OfflineCollectionData.CollectionCard> matches = new List<OfflineCollectionData.CollectionCard>();
            for (int i = 0; i < OfflineCollectionData.Catalog.Length; i++)
            {
                OfflineCollectionData.CollectionCard card = OfflineCollectionData.Catalog[i];
                if (!Matches(card)) continue;
                matches.Add(card);
            }

            if (sortMode == 1) matches.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            else if (sortMode == 2) matches.Sort((a, b) => b.Rarity.CompareTo(a.Rarity));

            for (int i = 0; i < tiles.Count; i++) Destroy(tiles[i]);
            tiles.Clear();
            for (int i = 0; i < worldFaces.Count; i++)
            {
                if (worldFaces[i].face != null) Destroy(worldFaces[i].face.gameObject);
            }
            worldFaces.Clear();
            for (int i = 0; i < matches.Count; i++) tiles.Add(CreateTile(matches[i]));

            if (emptyText != null) emptyText.gameObject.SetActive(matches.Count == 0);
            int ownedUnique = 0;
            for (int i = 0; i < OfflineCollectionData.Catalog.Length; i++)
            {
                if (OfflineCollectionData.Owned(OfflineCollectionData.Catalog[i].Id) > 0) ownedUnique++;
            }

            if (countText != null) countText.text = "GAMEPLAY CARDS  " + ownedUnique + " / " + OfflineCollectionData.Catalog.Length + " UNIQUE";
            if (fragmentText != null) fragmentText.text = "DATA FRAGMENTS  " + OfflineCollectionData.Fragments();
            if (deckText != null) deckText.text = "OFFLINE DECK  " + OfflineCollectionData.DeckSize() + " / " + OfflineCollectionData.DeckLimit
                + "  •  COPY LIMIT " + OfflineCollectionData.CopyLimit;
            UpdateFilterLabels();
            UpdateDeckButton();
        }

        bool Matches(OfflineCollectionData.CollectionCard card)
        {
            if (typeFilter > 0 && (int)card.Type != typeFilter - 1) return false;
            if (factionFilter > 0 && (int)card.Faction != factionFilter - 1) return false;
            if (rarityFilter > 0 && (int)card.Rarity != rarityFilter - 1) return false;
            // §4: the prototype ships a single set. SET 001 therefore matches the whole catalog today;
            // the comparison goes through the data-layer seam so a second set is a data-only change.
            if (setFilter > 0 && OfflineCollectionData.SetFor(card) != OfflineCollectionData.SetName) return false;
            bool owned = OfflineCollectionData.Owned(card.Id) > 0;
            if (ownershipFilter == 1 && !owned) return false;
            if (ownershipFilter == 2 && owned) return false;
            return true;
        }

        GameObject CreateTile(OfflineCollectionData.CollectionCard card)
        {
            if (tileTemplate == null || gridRoot == null)
            {
                return null;
            }

            GameObject tile = Instantiate(tileTemplate, gridRoot);
            tile.name = "Card " + card.Id;
            tile.SetActive(true);

            bool owned = OfflineCollectionData.Owned(card.Id) > 0;
            Image image = tile.GetComponent<Image>();
            if (image != null)
            {
                image.color = owned ? new Color(0.07f, 0.105f, 0.17f, 1f) : new Color(0.045f, 0.055f, 0.08f, 1f);
            }

            Outline outline = tile.GetComponent<Outline>();
            if (outline != null) outline.effectColor = OfflineCollectionData.RarityColor(card.Rarity);

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
            SpawnWorldFace(tile, card, owned);
            return tile;
        }

        static void DisableTileText(GameObject tile)
        {
            TextMeshProUGUI[] labels = tile.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++) labels[i].gameObject.SetActive(false);
        }

        void SpawnWorldFace(GameObject tile, OfflineCollectionData.CollectionCard card, bool owned)
        {
            if (worldCardRoot == null || worldCardCamera == null || templateCatalog == null) return;
            CardTemplateFixture(card, out CardPresentationFixture fixture);
            CardFaceView template = templateCatalog.Get(card.Type, card.Rarity);
            if (template == null) return;

            CardFaceData data = new CardFaceData(card.Id, card.Name, card.Type, card.Faction, card.Rarity,
                card.Cost, fixture != null ? fixture.artSprite : null,
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
            if (!owned) face.SetShade(new Color(0.28f, 0.34f, 0.43f, 1f));
            worldFaces.Add(new FaceEntry { tile = tile.GetComponent<RectTransform>(), face = face });
        }

        void CardTemplateFixture(OfflineCollectionData.CollectionCard card, out CardPresentationFixture fixture)
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
            for (int i = 0; i < worldFaces.Count; i++)
            {
                FaceEntry entry = worldFaces[i];
                if (entry == null || entry.tile == null || entry.face == null) continue;
                Vector3 tileWorld = entry.tile.TransformPoint(entry.tile.rect.center);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, tileWorld);
                Vector3 world = worldCardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
                    Mathf.Abs(worldCardCamera.transform.position.z)));
                // A portrait face must fit inside the authored grid cell in both dimensions. The
                // previous width-only calculation made a 1.46:1 card taller than the 320px cell,
                // so the second binder row visually climbed into the first one.
                float cellWidth = entry.tile.rect.width;
                float cellHeight = entry.tile.rect.height;
                float pixelWidth = Mathf.Min(cellWidth, cellHeight / 1.4615f) * 0.88f;
                float width = pixelWidth * worldPerPixel;
                entry.face.transform.position = new Vector3(world.x, world.y, 0f);
                entry.face.SetSize(new Vector3(width, width * 1.4615f, 0.01f));
            }
        }

        static void SetTileText(GameObject tile, string childName, string value, Color color, float size)
        {
            Transform child = tile.transform.Find(childName);
            if (child == null) return;
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text == null) return;
            text.text = value;
            text.color = color;
            text.fontSize = size;
        }

        void ShowDetail(OfflineCollectionData.CollectionCard card)
        {
            selectedCard = card;
            int owned = OfflineCollectionData.Owned(card.Id);
            if (detailTitle != null) detailTitle.text = owned > 0 ? card.Name : "UNKNOWN SIGNAL";
            if (detailOwnership != null)
            {
                detailOwnership.text = owned > 0
                    ? "OWNED " + owned + "  •  PLAYABLE COPIES " + Mathf.Min(owned, OfflineCollectionData.CopyLimit)
                        + "  •  EXTRA " + Mathf.Max(0, owned - OfflineCollectionData.CopyLimit)
                    : "MISSING  •  CRAFTING PATH NOT YET AVAILABLE";
            }

            if (detailBody != null)
            {
                detailBody.text = card.Rarity.ToString().ToUpperInvariant() + "  /  " + FactionLabel(card.Faction) + "  /  " + card.Type.ToString().ToUpperInvariant()
                    + "\n\nCOST  " + card.Cost
                    + (card.Type == CardType.Unit ? "\nATK  " + card.Atk + "    HP  " + card.Hp + "    SHIELD  " + card.Shield : string.Empty)
                    + "\n\n" + card.Rules
                    + "\n\nDUPLICATE VALUE  +" + card.FragmentValue + " DATA FRAGMENTS"
                    + "\nIN DECK  " + OfflineCollectionData.DeckCopies(card.Id) + " / " + OfflineCollectionData.CopyLimit
                    + "\nID  " + card.Id;
            }

            UpdateDeckButton();
        }

        void CycleType() { typeFilter = (typeFilter + 1) % 5; RefreshFilters(); }
        void CycleFaction() { factionFilter = (factionFilter + 1) % 4; RefreshFilters(); }
        void CycleRarity() { rarityFilter = (rarityFilter + 1) % 5; RefreshFilters(); }
        void CycleSet() { setFilter = (setFilter + 1) % 2; RefreshFilters(); }
        void CycleOwnership() { ownershipFilter = (ownershipFilter + 1) % 3; RefreshFilters(); }
        void CycleSort() { sortMode = (sortMode + 1) % 3; RefreshFilters(); }

        // ── Offline deck seam (Milestone 2 deck-builder integration) ────────────────────────────

        /// <summary>Adds one copy of the selected card; the data layer enforces ownership, copy limit
        /// and deck size, and the button label explains any refusal.</summary>
        void AddSelectedToDeck()
        {
            if (selectedCard == null) return;
            OfflineCollectionData.AddToDeck(selectedCard.Id);
            RefreshFilters();
            ShowDetail(selectedCard);
        }

        /// <summary>Why the selected card cannot join the deck right now, or null when it can.</summary>
        string DeckBlockReason()
        {
            if (selectedCard == null) return "SELECT A CARD";
            int owned = OfflineCollectionData.Owned(selectedCard.Id);
            int copies = OfflineCollectionData.DeckCopies(selectedCard.Id);
            if (owned <= 0) return "NOT OWNED";
            if (copies >= OfflineCollectionData.CopyLimit) return "COPY LIMIT";
            if (copies >= owned) return "NO SPARE COPY";
            if (OfflineCollectionData.DeckSize() >= OfflineCollectionData.DeckLimit) return "DECK FULL";
            return null;
        }

        void UpdateDeckButton()
        {
            if (addToDeckButton == null) return;
            string block = DeckBlockReason();
            addToDeckButton.interactable = block == null;
            TextMeshProUGUI label = addToDeckButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.text = block == null
                ? "ADD TO DECK  •  " + OfflineCollectionData.DeckCopies(selectedCard.Id) + " / " + OfflineCollectionData.CopyLimit
                : "ADD TO DECK  •  " + block;
        }

        void UpdateFilterLabels()
        {
            if (typeFilterButton != null) SetButtonLabel(typeFilterButton, "TYPE  •  " + (typeFilter == 0 ? "ANY" : ((CardType)(typeFilter - 1)).ToString().ToUpperInvariant()));
            if (factionFilterButton != null) SetButtonLabel(factionFilterButton, "FACTION  •  " + (factionFilter == 0 ? "ANY" : FactionLabel((Faction)(factionFilter - 1))));
            if (rarityFilterButton != null) SetButtonLabel(rarityFilterButton, "RARITY  •  " + (rarityFilter == 0 ? "ANY" : ((Rarity)(rarityFilter - 1)).ToString().ToUpperInvariant()));
            if (setFilterButton != null) SetButtonLabel(setFilterButton, "SET  •  " + (setFilter == 0 ? "ANY" : OfflineCollectionData.SetName));
            if (ownershipFilterButton != null) SetButtonLabel(ownershipFilterButton, "OWNERSHIP  •  " + (ownershipFilter == 0 ? "ALL" : ownershipFilter == 1 ? "OWNED" : "MISSING"));
            if (sortButton != null) SetButtonLabel(sortButton, "SORT  •  " + (sortMode == 0 ? "CATALOG" : sortMode == 1 ? "NAME" : "RARITY"));
        }

        static string FactionLabel(Faction faction)
        {
            return faction == Faction.Fsti ? "FSTI" : faction == Faction.Frti ? "FRTI" : "FPB";
        }

        static void SetButtonLabel(Button button, string value)
        {
            TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (text != null) text.text = value;
        }
    }
}
