using System.Collections;
using System.Collections.Generic;
using Overlink.Cards;
using Overlink.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.UI
{
    /// <summary>
    /// Deterministic, offline implementation of Gacha plan Milestones 1 and 3.
    ///
    /// The screen is authored in <c>Assets/Scenes/Gacha.unity</c> (rebuild it through
    /// <c>Overlink/Scenes/Build Gacha Pack Scene</c>). The card row is world-space and renders through
    /// the real presentation pipeline: each revealed card instantiates its
    /// <c>Assets/Cards/Presentation/Templates</c> prefab via <see cref="CardTemplateCatalog"/> and binds
    /// it exactly like the Card Manager preview does, so the pack shows the shipped frames, art windows
    /// and label plates rather than a stand-in container. Unrevealed slots show an authored card back,
    /// face down, and flip over with a lift arc when revealed.
    ///
    /// Single-pack mode resolves the fixed five-card test manifest (Milestone 1). Ten-pack mode resolves
    /// ten real standard packs — fifty cards, per-pack grouping in the queue strip and the final
    /// summary, the Epic-or-better bundle guarantee surfaced as an audit line, and the loyalty counter
    /// advanced by exactly ten with threshold crossings granting free bonus packs (Milestone 3).
    ///
    /// The server/economy boundary is deliberately not represented here: manifests resolve locally and
    /// deterministically, and the reveal position survives leaving and returning to the screen.
    /// Attach it to the full-screen background image so the swipe-up gesture has a raycast surface.
    /// </summary>
    public sealed class OfflinePackOpeningController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>One authored world-space slot: anchor, flip pivot, card back, face holder, result line.</summary>
        [System.Serializable]
        public sealed class PackSlot
        {
            public Transform anchor;
            public Transform pivot;
            public GameObject back;
            public Transform faceHolder;
            public TextMeshPro result;
        }

        enum PackMode
        {
            Single = 0,
            Ten = 1
        }

        [Header("Status")]
        [SerializeField] TextMeshProUGUI progressText;
        [SerializeField] TextMeshProUGUI collectionText;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] TextMeshProUGUI instructionText;

        [Header("Presentation")]
        [SerializeField] CardTemplateCatalog templateCatalog;
        [SerializeField] CardFrameCatalog frameCatalog;
        [SerializeField] CardTierMaterialSet tierMaterials;
        [SerializeField] CardPresentationFixture[] fixturesByRarity = new CardPresentationFixture[0];
        [SerializeField] PackSlot[] slots = new PackSlot[0];
        [SerializeField] float cardWidth = 1.05f;

        [Header("Controls")]
        [SerializeField] Button revealNextButton;
        [SerializeField] Button revealRemainingButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button backButton;
        [SerializeField] Button collectionButton;
        [SerializeField] Toggle reducedMotionToggle;

        [Header("Ten-Pack")]
        [SerializeField] Button singlePackButton;
        [SerializeField] Button tenPackButton;
        [SerializeField] TextMeshProUGUI packHeader;
        [SerializeField] Transform queueRoot;
        [SerializeField] GameObject capsuleTemplate;
        [SerializeField] Button summaryButton;
        [SerializeField] GameObject summaryPanel;
        [SerializeField] Button summaryCloseButton;
        [SerializeField] Transform summaryContent;
        [SerializeField] GameObject summaryLineTemplate;
        [SerializeField] TextMeshProUGUI summaryGuaranteeText;
        [SerializeField] TextMeshProUGUI summaryLoyaltyText;

        const string ReducedMotionKey = "Overlink.OfflinePack.ReducedMotion";
        /// <summary>Public so the Editor verification captures can select the pack mode deterministically.</summary>
        public const string ModeKey = "Overlink.OfflinePack.Mode";
        const string DefaultInstruction = "SWIPE UP  •  SPACE / ENTER  •  GAMEPAD SUBMIT";
        const string ReducedMotionInstruction = "REDUCED MOTION ENABLED  •  SWIPE UP / SPACE / ENTER";
        const float CardAspect = 1.4615f;
        const float HoverAmplitude = 0.035f;
        const int CardsPerPack = 5;

        struct RevealProfile
        {
            public float duration;
            public float lift;
            public float roll;
            public float popScale;
            public float landingDuration;
            public Color materializeShade;
            public Color vfxColor;
            public int ringCount;
            public int rayCount;
            public int particleBurst;
            public bool grand;

            public RevealProfile(float duration, float lift, float roll, float popScale,
                float landingDuration, Color materializeShade, Color vfxColor,
                int ringCount, int rayCount, int particleBurst, bool grand)
            {
                this.duration = duration;
                this.lift = lift;
                this.roll = roll;
                this.popScale = popScale;
                this.landingDuration = landingDuration;
                this.materializeShade = materializeShade;
                this.vfxColor = vfxColor;
                this.ringCount = ringCount;
                this.rayCount = rayCount;
                this.particleBurst = particleBurst;
                this.grand = grand;
            }
        }

        static RevealProfile ProfileFor(Rarity rarity)
        {
            // Each tier has a distinct motion signature, while still using the same readable 180° flip:
            // Common is restrained, Rare adds lift and a cool accent, Epic gets a heavier roll/pop, and
            // Legendary earns the longest arc and strongest impact.
            switch (rarity)
            {
                case Rarity.Rare:
                    return new RevealProfile(1.14f, 0.62f, 1.5f, 1.10f, 0.32f,
                        new Color(0.42f, 0.68f, 0.95f), new Color(0.18f, 0.72f, 1f),
                        2, 10, 14, false);
                case Rarity.Epic:
                    return new RevealProfile(1.24f, 0.76f, 3.5f, 1.14f, 0.40f,
                        new Color(0.72f, 0.45f, 0.95f), new Color(0.78f, 0.20f, 1f),
                        3, 14, 22, false);
                case Rarity.Legendary:
                    return new RevealProfile(1.42f, 0.94f, 6.0f, 1.20f, 0.50f,
                        new Color(0.98f, 0.72f, 0.35f), new Color(1f, 0.55f, 0.08f),
                        5, 24, 36, true);
                default:
                    return new RevealProfile(1.05f, 0.50f, 0f, 1.07f, 0.26f,
                        new Color(0.55f, 0.62f, 0.75f), new Color(0.32f, 0.58f, 0.82f),
                        1, 0, 7, false);
            }
        }

        sealed class RevealVfxState
        {
            public GameObject root;
            public SpriteRenderer flash;
            public LineRenderer[] rings;
            public LineRenderer[] rays;
            public ParticleSystem particles;
            public ParticleSystem secondBurst;
            public Color color;
            public float duration;
            public bool secondBurstEmitted;
            public bool firstBurstEmitted;
            public bool grand;
        }

        static Material revealVfxMaterial;
        static Material revealVfxAdditiveMaterial;
        static Sprite revealVfxSprite;
        static Texture2D revealVfxTexture;
        static Texture2D revealVfxStarTexture;

        static Material RevealVfxMaterial
        {
            get
            {
                if (revealVfxMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        revealVfxMaterial = new Material(shader);
                        revealVfxMaterial.mainTexture = RevealVfxTexture;
                    }
                }

                return revealVfxMaterial;
            }
        }

        static Material RevealVfxAdditiveMaterial
        {
            get
            {
                if (revealVfxAdditiveMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        revealVfxAdditiveMaterial = new Material(shader);
                        revealVfxAdditiveMaterial.mainTexture = RevealVfxStarTexture;
                    }
                }

                return revealVfxAdditiveMaterial != null ? revealVfxAdditiveMaterial : RevealVfxMaterial;
            }
        }

        static Sprite RevealVfxSprite
        {
            get
            {
                if (revealVfxSprite == null && RevealVfxTexture != null)
                {
                    revealVfxSprite = Sprite.Create(RevealVfxTexture,
                        new Rect(0f, 0f, RevealVfxTexture.width, RevealVfxTexture.height),
                        new Vector2(0.5f, 0.5f), RevealVfxTexture.width);
                    revealVfxSprite.name = "OfflineRevealVfxPixel";
                }

                return revealVfxSprite;
            }
        }

        static Texture2D RevealVfxTexture
        {
            get
            {
                if (revealVfxTexture != null) return revealVfxTexture;

                const int size = 256;
                revealVfxTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                revealVfxTexture.name = "OfflineRevealVfxSoftCircle";
                revealVfxTexture.filterMode = FilterMode.Bilinear;
                revealVfxTexture.wrapMode = TextureWrapMode.Clamp;
                Color[] pixels = new Color[size * size];
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float radius = size * 0.48f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float normalized = distance / radius;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - normalized), 1.8f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                revealVfxTexture.SetPixels(pixels);
                revealVfxTexture.Apply(false, true);
                return revealVfxTexture;
            }
        }

        static Texture2D RevealVfxStarTexture
        {
            get
            {
                if (revealVfxStarTexture != null) return revealVfxStarTexture;

                const int size = 128;
                revealVfxStarTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                revealVfxStarTexture.name = "OfflineRevealVfxStar";
                revealVfxStarTexture.filterMode = FilterMode.Bilinear;
                revealVfxStarTexture.wrapMode = TextureWrapMode.Clamp;
                Color[] pixels = new Color[size * size];
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 delta = new Vector2(x, y) - center;
                        float distance = delta.magnitude / (size * 0.5f);
                        float radial = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.5f);
                        float cross = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(delta.x) / (size * 0.5f)), 8f)
                            + Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(delta.y) / (size * 0.5f)), 8f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f,
                            Mathf.Clamp01(radial * 0.45f + cross * 0.55f));
                    }
                }

                revealVfxStarTexture.SetPixels(pixels);
                revealVfxStarTexture.Apply(false, true);
                return revealVfxStarTexture;
            }
        }

        readonly Dictionary<int, CardFaceView> faces = new Dictionary<int, CardFaceView>();
        readonly Dictionary<int, Vector3> anchorRest = new Dictionary<int, Vector3>();
        readonly List<GameObject> capsules = new List<GameObject>();
        readonly List<GameObject> summaryLines = new List<GameObject>();
        readonly List<GameObject> activeRevealVfx = new List<GameObject>();
        OfflineCollectionData.PackCard[] activeManifest = OfflineCollectionData.PackManifest;
        PackMode mode = PackMode.Single;
        int revealed;
        int fragments;
        Vector2 pointerDown;
        bool pointerTracking;
        bool isRevealing;
        bool revealAllRunning;

        bool ReducedMotion => reducedMotionToggle != null && reducedMotionToggle.isOn;

        void Awake()
        {
            if (revealNextButton != null) revealNextButton.onClick.AddListener(RevealNext);
            if (revealRemainingButton != null) revealRemainingButton.onClick.AddListener(RevealRemaining);
            if (resetButton != null) resetButton.onClick.AddListener(ResetDemo);
            if (backButton != null) backButton.onClick.AddListener(LeavePackOpening);
            if (collectionButton != null) collectionButton.onClick.AddListener(LeaveToCollection);
            if (singlePackButton != null) singlePackButton.onClick.AddListener(SelectSinglePack);
            if (tenPackButton != null) tenPackButton.onClick.AddListener(SelectTenPack);
            if (summaryButton != null) summaryButton.onClick.AddListener(ToggleSummary);
            if (summaryCloseButton != null) summaryCloseButton.onClick.AddListener(() => SetSummaryOpen(false));

            if (reducedMotionToggle != null)
            {
                reducedMotionToggle.isOn = PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;
                reducedMotionToggle.onValueChanged.AddListener(OnReducedMotionChanged);
                ApplyInstructionText();
            }

            mode = PlayerPrefs.GetInt(ModeKey, 0) == 1 ? PackMode.Ten : PackMode.Single;
            OfflineCollectionData.EnsureResolved();
            if (mode == PackMode.Ten) OfflineCollectionData.EnsureBundleResolved();
            BuildActiveManifest();
            LoadProgress();
            CacheAnchorRest();
            SyncSlots();
            BuildQueue();
            RefreshTexts();
            UpdateButtons();
            SetSummaryOpen(false);
        }

        void Start()
        {
            // Keyboard/gamepad players get the primary action focused immediately.
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem != null && revealNextButton != null)
            {
                eventSystem.SetSelectedGameObject(revealNextButton.gameObject);
            }
        }

        void Update()
        {
            // Submit is the Unity UI action used by keyboard/gamepad in the existing EventSystem.
            // The button remains the primary focus target, so this is an accessibility fallback rather
            // than the only input path.
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetButtonDown("Submit"))
            {
                RevealNext();
            }

            HoverUnrevealed();
        }

        void OnDestroy()
        {
            faces.Clear();
            ClearRevealVfx();
        }

        // ── Mode and manifest ───────────────────────────────────────────────────────────────────

        /// <summary>Ten packs of five cards, plus one standard pack per loyalty reward the purchase
        /// crossed into existence (§5.2), so a granted bonus pack is openable from the same queue.</summary>
        void BuildActiveManifest()
        {
            if (mode == PackMode.Single)
            {
                activeManifest = OfflineCollectionData.PackManifest;
                return;
            }

            var cards = new List<OfflineCollectionData.PackCard>(OfflineCollectionData.TenPackManifest);
            int loyaltyPacks = OfflineCollectionData.BundleLoyaltyPacks();
            for (int l = 0; l < loyaltyPacks; l++)
            {
                cards.AddRange(OfflineCollectionData.PackManifest);
            }

            activeManifest = cards.ToArray();
        }

        public void SelectSinglePack()
        {
            SetMode(PackMode.Single);
        }

        public void SelectTenPack()
        {
            SetMode(PackMode.Ten);
        }

        void SetMode(PackMode next)
        {
            if (mode == next) return;
            mode = next;
            PlayerPrefs.SetInt(ModeKey, mode == PackMode.Ten ? 1 : 0);
            PlayerPrefs.Save();
            if (mode == PackMode.Ten) OfflineCollectionData.EnsureBundleResolved();
            BuildActiveManifest();
            LoadProgress();
            ClearFaces();
            SyncSlots();
            BuildQueue();
            RefreshTexts();
            UpdateButtons();
            SetSummaryOpen(false);
        }

        string RevealedKey => mode == PackMode.Ten ? OfflineCollectionData.BundleRevealedKey : OfflineCollectionData.RevealedKey;

        int CurrentPack => Mathf.Min(revealed / CardsPerPack, Mathf.Max(0, activeManifest.Length / CardsPerPack - 1));

        int PackCount => Mathf.Max(1, activeManifest.Length / CardsPerPack);

        // ── Input ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Unrevealed backs breathe gently so the pack reads as live; reduced motion stills them.</summary>
        void HoverUnrevealed()
        {
            if (ReducedMotion) return;
            if (revealed >= activeManifest.Length) return;
            float time = Time.time;
            for (int s = revealed % CardsPerPack; s < slots.Length; s++)
            {
                PackSlot slot = slots[s];
                if (slot == null || slot.anchor == null) continue;
                if (!anchorRest.TryGetValue(s, out Vector3 rest)) continue;
                slot.anchor.localPosition = rest + new Vector3(0f, Mathf.Sin(time * 1.7f + s * 0.9f) * HoverAmplitude, 0f);
            }
        }

        void CacheAnchorRest()
        {
            anchorRest.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                PackSlot slot = slots[i];
                if (slot != null && slot.anchor != null) anchorRest[i] = slot.anchor.localPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = eventData.position;
            pointerTracking = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pointerTracking)
            {
                return;
            }

            pointerTracking = false;
            Vector2 delta = eventData.position - pointerDown;
            if (delta.y > 55f && Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 1.15f)
            {
                RevealNext();
            }
        }

        void OnReducedMotionChanged(bool value)
        {
            PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyInstructionText();
            if (value)
            {
                // Still the hover immediately; the flip coroutine checks the toggle per reveal.
                for (int i = 0; i < slots.Length; i++)
                {
                    PackSlot slot = slots[i];
                    if (slot != null && slot.anchor != null && anchorRest.TryGetValue(i, out Vector3 rest))
                    {
                        slot.anchor.localPosition = rest;
                    }
                }
            }
        }

        void ApplyInstructionText()
        {
            if (instructionText == null)
            {
                return;
            }

            instructionText.text = ReducedMotion ? ReducedMotionInstruction : DefaultInstruction;
        }

        // ── Progress ─────────────────────────────────────────────────────────────────────────────

        void LoadProgress()
        {
            revealed = Mathf.Clamp(PlayerPrefs.GetInt(RevealedKey, 0), 0, activeManifest.Length);
            fragments = Mathf.Max(0, PlayerPrefs.GetInt(OfflineCollectionData.FragmentsKey, 0));
        }

        void SaveProgress()
        {
            PlayerPrefs.SetInt(RevealedKey, revealed);
            PlayerPrefs.Save();
        }

        void RefreshTexts()
        {
            int owned = 0;
            var uniqueIds = new HashSet<string>();
            for (int i = 0; i < OfflineCollectionData.Catalog.Length; i++)
            {
                if (OfflineCollectionData.Owned(OfflineCollectionData.Catalog[i].Id) > 0) owned++;
            }

            if (progressText != null) progressText.text = "REVEALED  " + revealed + " / " + activeManifest.Length;
            if (collectionText != null)
            {
                collectionText.text = "COLLECTION  " + owned + " UNIQUE  •  DATA FRAGMENTS  " + fragments
                    + "  •  LOYALTY  " + OfflineCollectionData.LoyaltyProgress() + " / " + OfflineCollectionData.LoyaltyThreshold;
            }

            if (packHeader != null)
            {
                if (mode == PackMode.Single)
                {
                    packHeader.text = "SINGLE PACK  •  STANDARD FIVE-CARD RESOLUTION";
                }
                else if (revealed >= activeManifest.Length)
                {
                    packHeader.text = "PACK " + PackCount + " / " + PackCount + "  •  BUNDLE COMPLETE";
                }
                else
                {
                    packHeader.text = "PACK " + (CurrentPack + 1) + " / " + PackCount + "  •  CARD "
                        + ((revealed % CardsPerPack) + 1) + " / " + CardsPerPack;
                }
            }

            if (statusText != null)
            {
                if (mode == PackMode.Single)
                {
                    statusText.text = revealed >= activeManifest.Length
                        ? "PACK COMPLETE  //  ALL FIVE CARDS SECURED  //  DUPLICATES CONVERTED TRANSPARENTLY"
                        : "PACK RESOLVED  //  CARD " + (revealed + 1) + " IS READY  //  RESULT PERSISTS IF YOU LEAVE";
                }
                else if (revealed >= activeManifest.Length)
                {
                    statusText.text = "BUNDLE COMPLETE  //  " + activeManifest.Length + " CARDS SECURED  //  SUMMARY AVAILABLE";
                }
                else
                {
                    statusText.text = "TEN-PACK QUEUE  //  PACK " + (CurrentPack + 1) + " CARD " + ((revealed % CardsPerPack) + 1)
                        + " IS READY  //  RESULT PERSISTS IF YOU LEAVE";
                }
            }
        }

        void UpdateButtons()
        {
            bool more = revealed < activeManifest.Length && !isRevealing;
            if (revealNextButton != null) revealNextButton.interactable = more;
            if (revealRemainingButton != null) revealRemainingButton.interactable = more;
            if (summaryButton != null) summaryButton.interactable = mode == PackMode.Ten && revealed > 0;
            if (singlePackButton != null) SetModeButtonLabel(singlePackButton, "SINGLE PACK", mode == PackMode.Single);
            if (tenPackButton != null) SetModeButtonLabel(tenPackButton, "TEN-PACK  •  50 CARDS", mode == PackMode.Ten);
        }

        static void SetModeButtonLabel(Button button, string label, bool active)
        {
            if (button == null) return;
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = (active ? "▸ " : string.Empty) + label;
        }

        // ── Queue strip ──────────────────────────────────────────────────────────────────────────

        /// <summary>One capsule per pack in the bundle (plus loyalty packs): sealed, open, or done.
        /// The whole strip is hidden in single-pack mode, where there is no queue to speak of.</summary>
        void BuildQueue()
        {
            if (queueRoot == null || capsuleTemplate == null) return;
            for (int i = 0; i < capsules.Count; i++)
            {
                if (capsules[i] != null) Destroy(capsules[i]);
            }

            capsules.Clear();
            queueRoot.gameObject.SetActive(mode == PackMode.Ten);
            if (mode == PackMode.Single) return;

            int currentPack = revealed >= activeManifest.Length ? PackCount - 1 : CurrentPack;
            for (int p = 0; p < PackCount; p++)
            {
                GameObject capsule = Instantiate(capsuleTemplate, queueRoot);
                capsule.name = "Capsule " + (p + 1);
                capsule.SetActive(true);
                capsules.Add(capsule);

                bool loyalty = p >= OfflineCollectionData.PacksInBundle;
                string state = p < currentPack || revealed >= activeManifest.Length ? "DONE" : p == currentPack ? "OPEN" : "SEALED";
                SetCapsuleText(capsule, "Number", loyalty ? "L" + (p - OfflineCollectionData.PacksInBundle + 1) : (p + 1).ToString("00"));
                SetCapsuleText(capsule, "State", loyalty ? "LOYALTY PACK  •  " + state : state);
                Image image = capsule.GetComponent<Image>();
                if (image != null)
                {
                    image.color = state == "OPEN"
                        ? new Color(0.10f, 0.22f, 0.42f, 1f)
                        : state == "DONE" ? new Color(0.06f, 0.13f, 0.22f, 1f) : new Color(0.05f, 0.07f, 0.11f, 1f);
                }
            }
        }

        static void SetCapsuleText(GameObject capsule, string childName, string value)
        {
            Transform child = capsule.transform.Find(childName);
            TextMeshProUGUI text = child != null ? child.GetComponent<TextMeshProUGUI>() : null;
            if (text != null) text.text = value;
        }

        // ── Reveal ───────────────────────────────────────────────────────────────────────────────

        /// <summary>Restores every slot to its persisted state without animation (load, reset, reveal-all).</summary>
        void SyncSlots()
        {
            int packRevealed = revealed % CardsPerPack;
            bool bundleDone = revealed >= activeManifest.Length && revealed > 0;
            int pack = bundleDone ? PackCount - 1 : CurrentPack;
            for (int s = 0; s < slots.Length; s++)
            {
                int manifestIndex = pack * CardsPerPack + s;
                bool show = bundleDone || s < packRevealed;
                if (show && manifestIndex < activeManifest.Length) ShowFace(s, manifestIndex);
                else HideFace(s);
            }
        }

        public void RevealNext()
        {
            if (isRevealing || revealAllRunning || revealed >= activeManifest.Length)
            {
                return;
            }

            StartRevealNext();
        }

        /// <summary>Starts one reveal without exposing it as an input action to the reveal-all loop.</summary>
        void StartRevealNext()
        {
            if (isRevealing || revealed >= activeManifest.Length)
            {
                return;
            }

            int manifestIndex = revealed;
            int slotIndex = manifestIndex % CardsPerPack;
            isRevealing = true;
            UpdateButtons();

            // Crossing a pack boundary resets the row to five fresh face-down backs.
            if (slotIndex == 0)
            {
                ClearFaces();
                for (int s = 0; s < slots.Length; s++) HideFace(s);
                BuildQueue();
            }

            if (ReducedMotion)
            {
                ShowFace(slotIndex, manifestIndex);
                CommitReveal(manifestIndex);
                return;
            }

            PackSlot slot = Slot(slotIndex);
            CardFaceView view = EnsureFace(slotIndex, manifestIndex);
            if (slot == null || view == null)
            {
                ShowFace(slotIndex, manifestIndex);
                CommitReveal(manifestIndex);
                return;
            }

            // Face stays hidden and dimmed until the pivot passes 90°; the back flips away first.
            view.gameObject.SetActive(false);
            view.SetShade(ProfileFor(activeManifest[manifestIndex].Rarity).materializeShade);
            if (slot.back != null) slot.back.SetActive(true);
            RevealProfile profile = ProfileFor(activeManifest[manifestIndex].Rarity);
            if (!ReducedMotion)
            {
                RevealVfxState vfx = CreateRevealVfx(slot, profile);
                if (vfx != null) StartCoroutine(PlayRevealVfx(vfx, profile));
            }
            StartCoroutine(Flip(slotIndex, manifestIndex, view, profile));
        }

        RevealVfxState CreateRevealVfx(PackSlot slot, RevealProfile profile)
        {
            if (slot == null || slot.anchor == null || RevealVfxMaterial == null || RevealVfxSprite == null)
            {
                return null;
            }

            GameObject root = new GameObject("Reveal VFX " + activeManifest[revealed].Rarity);
            root.transform.SetParent(slot.anchor, false);
            root.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            activeRevealVfx.Add(root);

            RevealVfxState state = new RevealVfxState
            {
                root = root,
                color = profile.vfxColor,
                duration = profile.grand ? 2.30f : profile.ringCount > 1 ? 1.75f : 1.35f,
                grand = profile.grand
            };

            GameObject flashObject = new GameObject("Tier Flash");
            flashObject.transform.SetParent(root.transform, false);
            flashObject.transform.localPosition = Vector3.zero;
            flashObject.transform.localScale = new Vector3(1.12f, CardAspect * 1.12f, 1f);
            state.flash = flashObject.AddComponent<SpriteRenderer>();
            state.flash.sprite = RevealVfxSprite;
            state.flash.color = WithAlpha(profile.vfxColor, 0f);
            state.flash.sortingOrder = 500;

            state.rings = new LineRenderer[profile.ringCount];
            for (int i = 0; i < state.rings.Length; i++)
            {
                state.rings[i] = CreateRing(root.transform, "Ring " + i, 0.38f + i * 0.09f,
                    profile.vfxColor, 500 + i);
            }

            state.rays = new LineRenderer[profile.rayCount];
            for (int i = 0; i < state.rays.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, profile.rayCount);
                state.rays[i] = CreateRay(root.transform, "Ray " + i, angle, 0.32f,
                    0.50f + (i % 3) * 0.07f, profile.vfxColor, 520);
            }

            state.particles = CreateBurst(root.transform, "Tier Sparks", profile.vfxColor,
                profile.particleBurst, 0.085f, 0.72f, 0.55f, 0.42f);
            if (profile.grand)
            {
                state.secondBurst = CreateBurst(root.transform, "Grand Crown Burst",
                    new Color(1f, 0.92f, 0.58f), 28, 0.12f, 1.0f, 0.75f, 0.55f);
            }

            return state;
        }

        static LineRenderer CreateRing(Transform parent, string name, float radius, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.material = RevealVfxAdditiveMaterial;
            line.startWidth = 0.018f;
            line.endWidth = 0.018f;
            line.sortingOrder = order;
            Color transparent = WithAlpha(color, 0f);
            line.startColor = transparent;
            line.endColor = transparent;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius, 0f));
            }

            return line;
        }

        static LineRenderer CreateRay(Transform parent, string name, float angle, float start,
            float length, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.material = RevealVfxAdditiveMaterial;
            line.startWidth = 0.028f;
            line.endWidth = 0.001f;
            line.sortingOrder = order;
            Color transparent = WithAlpha(color, 0f);
            line.startColor = transparent;
            line.endColor = transparent;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            line.SetPosition(0, direction * start);
            line.SetPosition(1, direction * (start + length));
            return line;
        }

        static ParticleSystem CreateBurst(Transform parent, string name, Color color, int count,
            float size, float speed, float lifetime, float radius)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.1f;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.Max(64, count * 2);
            main.scalingMode = ParticleSystemScalingMode.Local;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = RevealVfxAdditiveMaterial;
            renderer.sortingOrder = 530;
            return particles;
        }

        IEnumerator PlayRevealVfx(RevealVfxState state, RevealProfile profile)
        {
            if (state == null || state.root == null) yield break;
            float elapsed = 0f;
            float impactAt = profile.grand ? 0.48f : 0.58f;
            while (elapsed < state.duration && state.root != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / state.duration);
                float impact = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, state.duration * impactAt));
                float ringPhase = Mathf.Clamp01((impact - 0.18f) / 0.82f);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.48f) / 0.52f));

                if (state.flash != null)
                {
                    float flashAlpha = Mathf.Sin(Mathf.Clamp01(impact * Mathf.PI)) * (profile.grand ? 0.34f : 0.20f);
                    state.flash.color = WithAlpha(state.color, flashAlpha * fade);
                }

                if (state.rings != null)
                {
                    for (int i = 0; i < state.rings.Length; i++)
                    {
                        LineRenderer ring = state.rings[i];
                        if (ring == null) continue;
                        float scale = Mathf.Lerp(0.28f, 1.28f + i * 0.10f, ringPhase);
                        ring.transform.localScale = new Vector3(scale, scale, 1f);
                        Color ringColor = WithAlpha(state.color, (0.78f - i * 0.08f) * fade);
                        ring.startColor = ringColor;
                        ring.endColor = WithAlpha(state.color, 0.05f * fade);
                    }
                }

                if (state.rays != null)
                {
                    for (int i = 0; i < state.rays.Length; i++)
                    {
                        LineRenderer ray = state.rays[i];
                        if (ray == null) continue;
                        float rayIn = Mathf.Clamp01((impact - 0.22f) / 0.45f);
                        ray.transform.localScale = new Vector3(rayIn, rayIn, 1f);
                        ray.transform.localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Sin(t * Mathf.PI * 2f) * (profile.grand ? 8f : 3f));
                        ray.startColor = WithAlpha(state.color, 0.70f * rayIn * fade);
                        ray.endColor = WithAlpha(state.color, 0.02f * fade);
                    }
                }

                if (state.particles != null && !state.firstBurstEmitted && impact > 0.48f)
                {
                    state.particles.Emit(profile.particleBurst);
                    state.firstBurstEmitted = true;
                }

                if (state.grand && !state.secondBurstEmitted && state.secondBurst != null && impact > 0.78f)
                {
                    state.secondBurst.Emit(28);
                    state.secondBurstEmitted = true;
                }

                yield return null;
            }

            if (state.root != null) Object.Destroy(state.root);
            activeRevealVfx.Remove(state.root);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        void ClearRevealVfx()
        {
            for (int i = 0; i < activeRevealVfx.Count; i++)
            {
                if (activeRevealVfx[i] != null) Object.Destroy(activeRevealVfx[i]);
            }

            activeRevealVfx.Clear();
        }

        /// <summary>
        /// The reveal: back rotates away with an ease-in, the face takes over past 90° with an ease-out,
        /// and the whole pivot rides a sine lift toward the camera so the card feels picked up.
        /// </summary>
        IEnumerator Flip(int slotIndex, int manifestIndex, CardFaceView view, RevealProfile profile)
        {
            PackSlot slot = Slot(slotIndex);
            if (slot == null || slot.pivot == null)
            {
                ShowFace(slotIndex, manifestIndex);
                CommitReveal(manifestIndex);
                yield break;
            }

            Transform pivot = slot.pivot;
            float elapsed = 0f;
            while (elapsed < profile.duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / profile.duration);
                float half = t < 0.5f ? t * 2f : (t - 0.5f) * 2f;
                float angle = t < 0.5f
                    ? 90f * half * half
                    : 90f + 90f * (1f - (1f - half) * (1f - half));
                bool showFace = angle > 90f;
                if (slot.back != null) slot.back.SetActive(!showFace);
                if (view != null) view.gameObject.SetActive(showFace);
                float roll = Mathf.Sin(Mathf.PI * t) * profile.roll;
                pivot.localRotation = Quaternion.Euler(0f, angle, roll);
                float grandScale = profile.grand
                    ? 1f + Mathf.Sin(Mathf.PI * t) * 0.16f
                    : 1f;
                pivot.localScale = new Vector3(grandScale, grandScale, 1f);
                pivot.localPosition = new Vector3(0f, 0f, -Mathf.Sin(Mathf.PI * t) * profile.lift);
                yield return null;
            }

            ShowFace(slotIndex, manifestIndex);
            pivot.localScale = Vector3.one;
            // Keep the persisted reveal cursor locked until the materialization/landing motion has
            // completed. This prevents a scene leave or a second submit during the landing window from
            // making the next load appear to have already opened the first card.
            view.SetShade(profile.materializeShade);
            yield return StartCoroutine(LandingPop(slotIndex, view, profile));
            CommitReveal(manifestIndex);
        }

        /// <summary>Commits a card only after its face has landed. Persisting before the flip made a
        /// scene reload turn the first card instantly, and a submit event arriving during the flip could
        /// advance the pack twice. The reveal lock and this single commit point keep both modes atomic.</summary>
        void CommitReveal(int manifestIndex)
        {
            if (manifestIndex != revealed) return;
            revealed = manifestIndex + 1;
            isRevealing = false;
            SaveProgress();
            RefreshTexts();
            UpdateButtons();
            BuildQueue();
            if (revealed >= activeManifest.Length) OnBundleComplete();
        }

        /// <summary>Landing: a brief overshoot scale plus the shade easing to white, so the card "materializes".</summary>
        IEnumerator LandingPop(int slotIndex, CardFaceView view, RevealProfile profile)
        {
            PackSlot slot = Slot(slotIndex);
            Transform target = slot != null ? slot.anchor : null;
            if (target == null) yield break;

            float elapsed = 0f;
            while (elapsed < profile.landingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / profile.landingDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(profile.popScale, 1f, eased);
                target.localScale = new Vector3(scale, scale, 1f);
                if (view != null) view.SetShade(Color.Lerp(profile.materializeShade, Color.white, eased));
                yield return null;
            }

            target.localScale = Vector3.one;
            if (view != null) view.SetShade(Color.white);
        }

        /// <summary>Reveals every remaining card sequentially, preserving the normal flip feedback.</summary>
        public void RevealRemaining()
        {
            if (isRevealing || revealAllRunning || revealed >= activeManifest.Length)
            {
                return;
            }

            StartCoroutine(RevealRemainingRoutine());
        }

        /// <summary>
        /// Resolves the remaining cards through the same flip and landing pipeline as the primary
        /// action. Each card must finish before the next one starts, so Reveal Remaining is a fast
        /// sequence of individual reveals rather than an instant state jump.
        /// </summary>
        IEnumerator RevealRemainingRoutine()
        {
            revealAllRunning = true;
            UpdateButtons();

            while (revealed < activeManifest.Length)
            {
                StartRevealNext();
                while (isRevealing)
                {
                    yield return null;
                }

                // Give the completed face one frame to settle before starting the next card. This is
                // also important in reduced-motion mode, where the commit happens synchronously.
                yield return null;
            }

            revealAllRunning = false;
            UpdateButtons();
        }

        /// <summary>Editor verification and UI automation can wait for a sequential reveal to settle.</summary>
        public bool IsRevealInProgress => isRevealing || revealAllRunning;
        public int RevealedCount => revealed;

        void LeavePackOpening()
        {
            StopAllCoroutines();
            isRevealing = false;
            revealAllRunning = false;
            OfflineCollectionData.ClearPackRevealProgress();
            SceneLoader.LoadMainMenu();
        }

        void LeaveToCollection()
        {
            StopAllCoroutines();
            isRevealing = false;
            revealAllRunning = false;
            OfflineCollectionData.ClearPackRevealProgress();
            SceneLoader.Load(SceneIndex.Collection);
        }

        void OnBundleComplete()
        {
            if (mode != PackMode.Ten) return;
            BuildSummary();
            SetSummaryOpen(true);
        }

        void ShowFace(int slotIndex, int manifestIndex)
        {
            PackSlot slot = Slot(slotIndex);
            if (slot == null) return;
            CardFaceView view = EnsureFace(slotIndex, manifestIndex);
            if (slot.back != null) slot.back.SetActive(false);
            if (slot.pivot != null)
            {
                slot.pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                slot.pivot.localPosition = Vector3.zero;
            }
            // Settle the anchor exactly on its rest pose: an unrevealed card is hover-bobbed, and the
            // reveal increments the count before the flip, so without this the landed card would keep
            // whatever bob offset it had at the instant it was picked.
            if (slot.anchor != null && anchorRest.TryGetValue(slotIndex, out Vector3 rest))
            {
                slot.anchor.localPosition = rest;
                slot.anchor.localScale = Vector3.one;
            }
            if (view != null)
            {
                view.gameObject.SetActive(true);
                view.SetShade(Color.white);
            }
            SetResult(slotIndex, manifestIndex);
        }

        void HideFace(int slotIndex)
        {
            PackSlot slot = Slot(slotIndex);
            if (slot == null) return;
            if (faces.TryGetValue(slotIndex, out CardFaceView view) && view != null)
            {
                Object.Destroy(view.gameObject);
            }
            faces.Remove(slotIndex);
            if (slot.back != null) slot.back.SetActive(true);
            if (slot.pivot != null)
            {
                slot.pivot.localRotation = Quaternion.identity;
                slot.pivot.localPosition = Vector3.zero;
            }
            if (slot.anchor != null && anchorRest.TryGetValue(slotIndex, out Vector3 rest))
            {
                slot.anchor.localPosition = rest;
                slot.anchor.localScale = Vector3.one;
            }
            if (slot.result != null) slot.result.text = string.Empty;
        }

        void ClearFaces()
        {
            foreach (KeyValuePair<int, CardFaceView> pair in faces)
            {
                if (pair.Value != null) Object.Destroy(pair.Value.gameObject);
            }
            faces.Clear();
        }

        CardFaceView EnsureFace(int slotIndex, int manifestIndex)
        {
            if (faces.TryGetValue(slotIndex, out CardFaceView existing) && existing != null) return existing;
            CardFaceView view = SpawnFace(slotIndex, manifestIndex);
            if (view != null) faces[slotIndex] = view;
            return view;
        }

        /// <summary>
        /// Instantiates the authored template prefab for this manifest card and binds it the same way the
        /// Card Manager preview does: catalog-selected template, frame catalog, tier materials, fixture
        /// art fallback. No stand-in card container is built here.
        /// </summary>
        CardFaceView SpawnFace(int slotIndex, int manifestIndex)
        {
            PackSlot slot = Slot(slotIndex);
            if (slot == null || slot.faceHolder == null || templateCatalog == null) return null;
            if (manifestIndex < 0 || manifestIndex >= activeManifest.Length) return null;
            OfflineCollectionData.PackCard card = activeManifest[manifestIndex];
            CardPresentationFixture fixture = FixtureFor(card.Rarity);
            bool unit = card.Type == CardType.Unit;
            CardFaceData face = new CardFaceData(
                card.Id,
                card.Name,
                card.Type,
                card.Faction,
                card.Rarity,
                card.Cost,
                fixture != null ? fixture.artSprite : null,
                unit ? (int?)card.Atk : null,
                unit ? (int?)card.Hp : null,
                unit ? (int?)0 : null,
                string.IsNullOrEmpty(card.Rules) && fixture != null ? fixture.abilityText : card.Rules);

            CardFaceView template = templateCatalog.Get(card.Type, card.Rarity);
            if (template == null) return null;

            GameObject instance = Object.Instantiate(template.gameObject, slot.faceHolder);
            instance.name = "PackCard " + card.Id;
            CardFaceView view = instance.GetComponent<CardFaceView>();
            if (view == null)
            {
                Object.Destroy(instance);
                return null;
            }

            view.PreviewRarity = card.Rarity;
            view.Bind(face, frameCatalog, tierMaterials);
            view.SetSize(new Vector3(cardWidth, cardWidth * CardAspect, 0.01f));
            // One sorting slice per slot keeps five co-planar cards from bleeding labels across neighbours.
            view.SetSortBaseline(slotIndex * 100 + 50);
            instance.transform.localPosition = Vector3.zero;
            // Pre-rotated so the face reads upright once the pivot completes its 180° flip.
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            instance.transform.localScale = Vector3.one;
            return view;
        }

        CardPresentationFixture FixtureFor(Rarity rarity)
        {
            int index = (int)rarity;
            if (fixturesByRarity != null && index >= 0 && index < fixturesByRarity.Length && fixturesByRarity[index] != null)
            {
                return fixturesByRarity[index];
            }

            if (fixturesByRarity != null)
            {
                for (int i = 0; i < fixturesByRarity.Length; i++)
                {
                    if (fixturesByRarity[i] != null) return fixturesByRarity[i];
                }
            }

            return null;
        }

        void SetResult(int slotIndex, int manifestIndex)
        {
            PackSlot slot = Slot(slotIndex);
            if (slot == null || slot.result == null) return;
            slot.result.text = IsDuplicate(manifestIndex)
                ? "DUPLICATE  •  +" + activeManifest[manifestIndex].FragmentValue + " DATA FRAGMENTS"
                : "NEW CARD  •  ADDED TO COLLECTION";
        }

        PackSlot Slot(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        /// <summary>
        /// Duplicate audit per mode: the single pack checks earlier copies inside its own manifest; the
        /// bundle defers to the committed-ownership audit in <see cref="OfflineCollectionData"/> so the
        /// flag stays correct across relaunches and prior packs.
        /// </summary>
        bool IsDuplicate(int manifestIndex)
        {
            if (mode == PackMode.Ten && manifestIndex < OfflineCollectionData.TenPackManifest.Length)
            {
                return OfflineCollectionData.IsBundleDuplicate(manifestIndex);
            }

            for (int i = 0; i < manifestIndex; i++)
            {
                if (activeManifest[i].Id == activeManifest[manifestIndex].Id) return true;
            }

            return false;
        }

        // ── Final summary (§6.3) ─────────────────────────────────────────────────────────────────

        void ToggleSummary()
        {
            SetSummaryOpen(summaryPanel == null || !summaryPanel.activeSelf);
        }

        void SetSummaryOpen(bool open)
        {
            if (summaryPanel != null) summaryPanel.SetActive(open);
            if (open) BuildSummary();
        }

        /// <summary>Groups the revealed cards by pack boundary with per-card ownership/fragment results,
        /// then surfaces the guarantee and loyalty audit lines once the bundle is complete so all fifty
        /// cards stay accountable (§6.3). Opened mid-bundle it lists only what has been revealed, so the
        /// SUMMARY button never spoils sealed packs.</summary>
        void BuildSummary()
        {
            if (summaryContent == null || summaryLineTemplate == null) return;
            for (int i = 0; i < summaryLines.Count; i++)
            {
                if (summaryLines[i] != null) Destroy(summaryLines[i]);
            }

            summaryLines.Clear();

            int shown = Mathf.Clamp(revealed, 0, activeManifest.Length);
            bool complete = shown >= activeManifest.Length;
            for (int p = 0; p < PackCount && p * CardsPerPack < shown; p++)
            {
                bool loyalty = p >= OfflineCollectionData.PacksInBundle;
                AddSummaryLine("PACK " + (p + 1).ToString("00") + (loyalty ? "  •  LOYALTY BONUS PACK" : string.Empty),
                    new Color(0.42f, 0.72f, 1f), 20f, TextAnchor.MiddleLeft);
                for (int s = 0; s < CardsPerPack; s++)
                {
                    int index = p * CardsPerPack + s;
                    if (index >= shown) break;
                    OfflineCollectionData.PackCard card = activeManifest[index];
                    bool duplicate = IsDuplicate(index);
                    AddSummaryLine("    " + card.Name + "   •   " + card.Rarity.ToString().ToUpperInvariant()
                        + "   •   " + (duplicate ? "DUPLICATE  +" + card.FragmentValue + " FRAGMENTS" : "NEW CARD"),
                        OfflineCollectionData.RarityColor(card.Rarity), 16f, TextAnchor.MiddleLeft);
                }
            }

            if (!complete)
            {
                AddSummaryLine("REMAINING  " + (activeManifest.Length - shown)
                    + " CARDS UNREVEALED  •  OPEN THEM OR REVEAL ALL FOR THE FULL AUDIT",
                    new Color(0.52f, 0.57f, 0.65f), 16f, TextAnchor.MiddleLeft);
            }

            if (summaryGuaranteeText != null)
            {
                int guarantee = complete ? OfflineCollectionData.BundleGuaranteeIndex() : -2;
                if (guarantee == -2)
                {
                    summaryGuaranteeText.text = "TEN-PACK GUARANTEE  •  AUDIT UNLOCKS WHEN THE BUNDLE IS COMPLETE";
                }
                else if (guarantee >= 0)
                {
                    OfflineCollectionData.PackCard card = OfflineCollectionData.TenPackManifest[guarantee];
                    summaryGuaranteeText.text = "TEN-PACK GUARANTEE  •  EPIC-OR-BETTER SATISFIED BY PACK "
                        + ((guarantee / CardsPerPack) + 1) + " SLOT " + ((guarantee % CardsPerPack) + 1) + "  —  "
                        + card.Name.ToUpperInvariant() + " (" + card.Rarity.ToString().ToUpperInvariant() + ")";
                }
                else
                {
                    summaryGuaranteeText.text = "TEN-PACK GUARANTEE  •  NO QUALIFYING RESULT (AUDIT FAILURE)";
                }
            }

            if (summaryLoyaltyText != null)
            {
                summaryLoyaltyText.text = "LOYALTY  •  PURCHASED PACKS " + OfflineCollectionData.PurchasedPacks()
                    + " (THIS BUNDLE +10)  •  PROGRESS " + OfflineCollectionData.LoyaltyProgress() + " / "
                    + OfflineCollectionData.LoyaltyThreshold + "  •  BONUS PACKS GRANTED "
                    + OfflineCollectionData.LoyaltyGranted();
            }
        }

        void AddSummaryLine(string value, Color color, float size, TextAnchor anchor)
        {
            GameObject line = Instantiate(summaryLineTemplate, summaryContent);
            line.SetActive(true);
            summaryLines.Add(line);
            TextMeshProUGUI text = line.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = value;
                text.color = color;
                text.fontSize = size;
                text.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            }
        }

        // ── Reset ────────────────────────────────────────────────────────────────────────────────

        void ResetDemo()
        {
            OfflineCollectionData.Reset();
            revealed = 0;
            fragments = 0;
            OfflineCollectionData.EnsureResolved();
            if (mode == PackMode.Ten) OfflineCollectionData.EnsureBundleResolved();
            BuildActiveManifest();
            fragments = OfflineCollectionData.Fragments();
            ClearFaces();
            for (int i = 0; i < slots.Length; i++) HideFace(i);
            BuildQueue();
            RefreshTexts();
            UpdateButtons();
            SetSummaryOpen(false);
        }
    }
}
