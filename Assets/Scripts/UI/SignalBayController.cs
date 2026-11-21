// Assets/Scripts/UI/SignalBayController.cs
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Overlink.Cards;
using Overlink.Core;
using Overlink.Net;
using Overlink.Vfx;

namespace Overlink.UI
{
    /// <summary>
    /// The shop half of the Gacha flow. The Signal Bay screen sells the pack - choice
    /// cards, pricing, milestone track - and the reveal that follows it lives in its own scene
    /// (<see cref="SceneIndex.RevealGacha"/>), so the bay's only runtime job is to carry the player's
    /// pack resolution across the scene boundary.
    ///
    /// The choice travels through the same PlayerPrefs key the reveal controller reads in its Awake
    /// (<see cref="PackOpeningController.ModeKey"/>), which is how the two scenes agree on what
    /// is being opened without an object having to survive the load. Persisting it also means OPEN
    /// AGAIN on the reveal screen re-resolves the same pack size; the flip cursor itself never
    /// persists — every entry into the reveal is cleared to a fresh, face-down resolution here.
    /// </summary>
    public sealed class SignalBayController : MonoBehaviour
    {
        [SerializeField] Button openOneButton;
        [SerializeField] Button openTenButton;
        [SerializeField] Button returnButton;
        [SerializeField] CardTemplateCatalog templateCatalog;
        [SerializeField] CardFrameCatalog frameCatalog;
        [SerializeField] CardTierMaterialSet tierMaterials;

        TMP_Text selectedPackLabel;
        TMP_Text packCountLabel;
        TMP_Text infoDescription;
        TMP_Text compositionBody;
        TMP_Text contentsRates;
        TMP_Text singleCost;
        TMP_Text singleDetail;
        TMP_Text statusText;

        /// <summary>The bay art's own pricing: the balance is derived from packs purchased this demo
        /// run, so the credit readout is live data rather than baked fiction.</summary>
        const int StartCredits = 2400;
        const int CreditsPerPack = 100;

        // The featured cards render on their own layer through a dedicated camera into a transparent
        // texture. The bay canvas is opaque, so world-space cards on the main camera would be hidden.
        const string FeaturedLayerName = "Featured Card";
        const int FeaturedTextureWidth = 1320;
        const int FeaturedTextureHeight = 1064;
        const float FeaturedHalfHeight = 2f;
        const float CardAspect = 1.4615f;
        const float FloatHeight = 0.06f;
        const float FloatTilt = 0.6f;
        const float FloatSpeed = 1.4f;

        RenderTexture featuredTarget;
        Camera featuredCamera;
        readonly List<FloatingCard> floatingCards = new List<FloatingCard>();

        struct FloatingCard
        {
            public readonly Transform Root;
            public readonly Vector2 Base;
            public readonly float Angle;
            public readonly float Phase;

            public FloatingCard(Transform root, Vector2 basePosition, float angle, float phase)
            {
                Root = root;
                Base = basePosition;
                Angle = angle;
                Phase = phase;
            }
        }

        // Live overlays authored by Overlink/Scenes/Wire Bay Live Counters over the erased regions of
        // the derived base texture; absent on scenes that predate the wiring, in which case the baked
        // art simply stays static.
        UiCounter creditCounter;
        UiCounter milestoneCounter;
        UiCounter remainingCounter;
        UiBar milestoneBar;
        TMP_Text playerIdentityText;
        PlayerProfileService profileService;
        AccountManager accountManager;
        bool purchaseInFlight;
        SynchronizationContext unityContext;

        void Awake()
        {
            float startedAt = Time.realtimeSinceStartup;
            Debug.Log($"[SignalBay] Awake begin at {startedAt:F3}s");
            if (openOneButton != null) openOneButton.onClick.AddListener(() => OpenReveal(0));
            if (openTenButton != null) openTenButton.onClick.AddListener(() => OpenReveal(1));
            if (returnButton != null) returnButton.onClick.AddListener(() => SceneLoader.Load(SceneIndex.Collection));
            BindLiveCounters();
            playerIdentityText = transform.Find("Figma Signal Bay/Account")?.GetComponent<TMP_Text>();
            selectedPackLabel = transform.Find("Figma Signal Bay/Selected Label")?.GetComponent<TMP_Text>();
            packCountLabel = transform.Find("Figma Signal Bay/Pack Count")?.GetComponent<TMP_Text>();
            infoDescription = transform.Find("Figma Signal Bay/Info Description")?.GetComponent<TMP_Text>();
            compositionBody = transform.Find("Figma Signal Bay/Composition Body")?.GetComponent<TMP_Text>();
            contentsRates = transform.Find("Figma Signal Bay/Contents Rates")?.GetComponent<TMP_Text>();
            singleCost = transform.Find("Figma Signal Bay/Single Cost")?.GetComponent<TMP_Text>();
            singleDetail = transform.Find("Figma Signal Bay/Single Detail")?.GetComponent<TMP_Text>();
            statusText = transform.Find("Figma Signal Bay/Header Status")?.GetComponent<TMP_Text>();
            if (statusText == null)
                statusText = transform.Find("Header Status")?.GetComponent<TMP_Text>();
            ApplyBanner();
            BuildFeaturedCards();
            UpdatePlayerIdentity();
            RollLiveCounters();
            UpdateButtons();
            Debug.Log($"[SignalBay] Awake finished in {Time.realtimeSinceStartup - startedAt:F2}s; cloudReady="
                + (CloudPlayerDataService.Instance != null && CloudPlayerDataService.Instance.IsReady));
        }

        void Start()
        {
            unityContext = SynchronizationContext.Current;
            profileService = PlayerProfileService.Instance;
            if (profileService != null)
            {
                profileService.ProfileLoaded += OnProfileLoaded;
                profileService.StatusChanged += OnProfileStatusChanged;
            }
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            if (cloud != null)
            {
                cloud.StateChanged += OnCloudStateChanged;
                cloud.BusyChanged += OnCloudBusyChanged;
            }
            accountManager = AccountManager.Instance;
            if (accountManager != null) accountManager.AccountChanged += OnAccountChanged;
            UpdatePlayerIdentity();
            RollLiveCounters();
            UpdateButtons();
        }

        void OnDestroy()
        {
            if (profileService != null)
            {
                profileService.ProfileLoaded -= OnProfileLoaded;
                profileService.StatusChanged -= OnProfileStatusChanged;
            }
            if (CloudPlayerDataService.Instance != null)
            {
                CloudPlayerDataService.Instance.StateChanged -= OnCloudStateChanged;
                CloudPlayerDataService.Instance.BusyChanged -= OnCloudBusyChanged;
            }
            if (accountManager != null) accountManager.AccountChanged -= OnAccountChanged;

            if (featuredCamera != null) Destroy(featuredCamera.gameObject);
            if (featuredTarget != null)
            {
                featuredTarget.Release();
                Destroy(featuredTarget);
            }
        }

        /// <summary>
        /// The single banner: one pack, five random cards drawn from the full library. The three featured
        /// legendaries are shown by the showcase cards, so the copy only describes the pack itself.
        /// </summary>
        void ApplyBanner()
        {
            if (selectedPackLabel != null) selectedPackLabel.text = "SIGNAL ARCHIVE";
            if (packCountLabel != null) packCountLabel.text = "1 SIGNAL AVAILABLE";
            if (infoDescription != null)
                infoDescription.text = "Featured legendary units from each faction. Packs draw from the full card library.";
            if (compositionBody != null) compositionBody.text = "RANDOM DRAW  /  ALL FACTIONS\nFEATURED: LUMEN  •  KAELIN  •  VASARI";
            if (contentsRates != null) contentsRates.text = "PACK CONTENTS  /  SERVER RESOLVED";
            if (singleCost != null) singleCost.text = "100 CREDITS";
            if (singleDetail != null) singleDetail.text = "5 CARDS  /  RANDOM";
        }

        /// <summary>
        /// Renders the three featured legendaries as real card faces (template, frame, tier material and
        /// art, the same path the binder and reveal use) into a texture shown inside Selected Pack Showcase.
        /// Positions are in the featured camera's world units, centred on the showcase.
        /// </summary>
        void BuildFeaturedCards()
        {
            float startedAt = Time.realtimeSinceStartup;
            Transform showcase = transform.Find("Figma Signal Bay/Selected Pack Showcase");
            if (showcase == null || showcase.Find("Featured Render") != null) return;
            if (templateCatalog == null || frameCatalog == null)
            {
                Debug.LogWarning("[SignalBay] Featured card catalogs are not wired; the showcase has no cards.", this);
                return;
            }

            int layer = LayerMask.NameToLayer(FeaturedLayerName);
            if (layer < 0)
            {
                Debug.LogError("[SignalBay] Missing layer '" + FeaturedLayerName + "'; the showcase has no cards.", this);
                return;
            }

            Camera worldCamera = GameObject.Find("Gacha Card Camera")?.GetComponent<Camera>();
            if (worldCamera != null) worldCamera.cullingMask &= ~(1 << layer);

            featuredTarget = new RenderTexture(FeaturedTextureWidth, FeaturedTextureHeight, 16, RenderTextureFormat.ARGB32);
            featuredTarget.name = "Signal Bay Featured Cards";
            featuredTarget.Create();
            featuredCamera = CreateFeaturedCamera(featuredTarget, layer);

            var render = new GameObject("Featured Render", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)render.transform;
            rect.SetParent(showcase, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var raw = render.GetComponent<RawImage>();
            raw.texture = featuredTarget;
            raw.raycastTarget = false;

            string[] ids = CollectionData.FeaturedIds;
            Vector2[] positions = { new Vector2(-1.35f, -0.2f), new Vector2(0f, 0.85f), new Vector2(1.35f, -0.2f) };
            float[] widths = { 1.25f, 1.25f, 1.25f };
            float[] angles = { 6f, 0f, -6f };
            for (int i = 0; i < ids.Length && i < positions.Length; i++)
            {
                CardDefinition card = CollectionData.LibraryCard(ids[i]);
                if (card == null)
                {
                    Debug.LogWarning("[SignalBay] Featured card is not in the library: " + ids[i], this);
                    continue;
                }

                BuildFeaturedCard(card, positions[i], widths[i], angles[i], i, layer);
            }
            Debug.Log($"[SignalBay] Featured cards built in {Time.realtimeSinceStartup - startedAt:F2}s");
        }

        void BuildFeaturedCard(CardDefinition card, Vector2 position, float width, float angle, int index, int layer)
        {
            CardFaceView template = templateCatalog.Get(card.Type, card.Rarity);
            if (template == null) return;

            var root = new GameObject("Featured " + card.Id);
            root.transform.position = new Vector3(position.x, position.y, 0f);
            root.transform.localEulerAngles = new Vector3(0f, 0f, angle);

            CardFaceData face = new CardFaceData(card.Id, card.Name, card.Type, card.Faction, card.Rarity,
                card.Cost, CardLibraryBootstrap.ArtFor(card.Id),
                card.Type == CardType.Unit ? (int?)card.Atk : null,
                card.Type == CardType.Unit ? (int?)card.Hp : null,
                card.Type == CardType.Unit ? (int?)card.Shield : null,
                card.AbilityText);

            CardFaceView view = Instantiate(template.gameObject, root.transform, false).GetComponent<CardFaceView>();
            if (view == null)
            {
                Destroy(root);
                return;
            }

            view.name = "Featured Face " + card.Id;
            view.PreviewRarity = card.Rarity;
            view.Bind(face, frameCatalog, tierMaterials);
            view.SetSize(new Vector3(width, width * CardAspect, 0.01f));
            view.SetSortBaseline(index * 100 + 50);
            SetLayerRecursively(root.transform, layer);
            floatingCards.Add(new FloatingCard(root.transform, position, angle, index * 2.1f));
        }

        void Update()
        {
            float time = Time.time;
            for (int i = 0; i < floatingCards.Count; i++)
            {
                FloatingCard card = floatingCards[i];
                if (card.Root == null) continue;
                float wave = Mathf.Sin(time * FloatSpeed + card.Phase);
                card.Root.position = new Vector3(card.Base.x, card.Base.y + wave * FloatHeight, 0f);
                card.Root.localEulerAngles = new Vector3(0f, 0f, card.Angle + wave * FloatTilt);
            }
        }

        static Camera CreateFeaturedCamera(RenderTexture target, int layer)
        {
            var go = new GameObject("Featured Card Camera");
            go.transform.position = new Vector3(0f, 0f, -12f);
            go.transform.rotation = Quaternion.identity;
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = FeaturedHalfHeight;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << layer;
            camera.allowMSAA = false;
            camera.targetTexture = target;
            EnsureUniversalCameraData(camera);
            return camera;
        }

        static void EnsureUniversalCameraData(Camera camera)
        {
            Type type = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type != null && camera.GetComponent(type) == null)
            {
                camera.gameObject.AddComponent(type);
            }
        }

        static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            for (int i = 0; i < target.childCount; i++) SetLayerRecursively(target.GetChild(i), layer);
        }

        void OnProfileLoaded(PlayerProfile profile) => UpdatePlayerIdentity();
        void OnProfileStatusChanged(string status) => UpdatePlayerIdentity();
        void OnCloudStateChanged(CloudPlayerState state)
        {
            RunOnUnityThread(() =>
            {
                RollLiveCounters();
                UpdateButtons();
            });
        }
        void OnCloudBusyChanged(bool busy) => RunOnUnityThread(UpdateButtons);

        void RunOnUnityThread(Action action)
        {
            if (unityContext == null || SynchronizationContext.Current == unityContext)
            {
                if (this != null) action();
                return;
            }
            unityContext.Post(_ => { if (this != null) action(); }, null);
        }
        void OnAccountChanged(AccountKind kind)
        {
            RollLiveCounters();
            UpdateButtons();
        }

        void UpdatePlayerIdentity()
        {
            if (playerIdentityText == null) return;
            var profile = PlayerProfileService.Instance?.CurrentProfile;
            var identity = profile != null && profile.IsComplete() ? profile.Identity : default;
            playerIdentityText.text = identity.IsValid ? identity.DisplayName : "Player";
        }

        /// <summary>Entry beat: every readout rolls up from zero while the milestone bar fills.</summary>
        void RollLiveCounters()
        {
            int progress = CollectionData.LoyaltyProgress();
            int threshold = CollectionData.LoyaltyThreshold;
            CloudPlayerDataService cloud = CloudPlayerDataService.Instance;
            bool cloudAccount = AccountManager.Instance != null && AccountManager.Instance.IsSignedIn;
            bool cloudReady = cloudAccount && cloud != null && cloud.IsReady && cloud.State != null;
            int credits = cloudReady
                ? Mathf.Max(0, cloud.State.signalCredits)
                : 0;
            if (!cloudAccount)
                credits = Mathf.Max(0, StartCredits - CreditsPerPack * CollectionData.PurchasedPacks());
            if (creditCounter != null) creditCounter.Set(credits);
            if (milestoneCounter != null) milestoneCounter.Set(progress);
            if (remainingCounter != null) remainingCounter.Set(Mathf.Max(0, threshold - progress));
            if (milestoneBar != null) milestoneBar.Set(progress, threshold);

            if (cloudAccount && !cloudReady)
                SetStatus("CLOUD PROGRESSION LOADING  //  PURCHASES LOCKED");
            else if (cloudReady && cloud.State.loyaltyPacks > 0)
                SetStatus("LOYALTY PACKS READY  //  " + cloud.State.loyaltyPacks
                    + " FREE ENTITLEMENT(S)  //  OPEN SEPARATELY");
            else if (cloudReady)
                SetStatus("SIGNAL BAY  //  READY");
            else if (!cloudAccount)
                SetStatus("SIGNAL BAY DEMO  //  SIGN IN FOR CLOUD BALANCE");
        }

        void BindLiveCounters()
        {
            creditCounter = Counter("Credit Live");
            milestoneCounter = Counter("Milestone Number Live");
            remainingCounter = Counter("Remaining Live");
            Transform bar = transform.Find("Milestone Bar Live");
            milestoneBar = bar != null ? bar.GetComponent<UiBar>() : null;

            int threshold = CollectionData.LoyaltyThreshold;
            if (creditCounter != null) creditCounter.Bind(v => v.ToString("N0"), 0);
            if (milestoneCounter != null)
            {
                milestoneCounter.Bind(v => "<color=#29DFF2>" + v + "</color>  <color=#8296A9><size=15>/ " + threshold + "</size></color>", 0);
            }

            if (remainingCounter != null) remainingCounter.Bind(v => v + " more pulls to your next free pack", 0);
        }

        UiCounter Counter(string node)
        {
            Transform hit = transform.Find(node);
            return hit != null ? hit.GetComponent<UiCounter>() : null;
        }

        bool CloudAccountReady(out CloudPlayerDataService cloud)
        {
            cloud = CloudPlayerDataService.Instance;
            return AccountManager.Instance != null && AccountManager.Instance.IsSignedIn
                && cloud != null && cloud.IsReady && cloud.State != null;
        }

        void UpdateButtons()
        {
            CloudPlayerDataService cloud;
            bool signedIn = AccountManager.Instance != null && AccountManager.Instance.IsSignedIn;
            bool ready = CloudAccountReady(out cloud);
            bool busy = cloud != null && cloud.IsBusy;
            bool oneAllowed = !purchaseInFlight && (!signedIn
                || (ready && !busy && cloud.State.signalCredits >= CloudPlayerDataService.SinglePackCost));
            bool tenAllowed = !purchaseInFlight && (!signedIn
                || (ready && !busy && cloud.State.signalCredits >= CloudPlayerDataService.TenPackCost));

            if (openOneButton != null) openOneButton.interactable = oneAllowed;
            if (openTenButton != null) openTenButton.interactable = tenAllowed;

            if (signedIn && ready && !busy && cloud.State.signalCredits < CloudPlayerDataService.SinglePackCost)
                SetStatus("NOT ENOUGH SIGNAL CREDITS  //  ONE PACK COSTS " + CloudPlayerDataService.SinglePackCost);
        }

        void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value;
        }

        /// <summary>Opens a single standard pack in the reveal scene.</summary>
        public void OpenSinglePack() => OpenReveal(0);

        /// <summary>Opens the ten-pack bundle. Loyalty rewards remain separate free entitlements.</summary>
        public void OpenTenPack() => OpenReveal(1);

        void OpenReveal(int tenPackMode)
        {
            bool signedIn = AccountManager.Instance != null && AccountManager.Instance.IsSignedIn;
            if (signedIn)
            {
                CloudPlayerDataService cloud;
                if (!CloudAccountReady(out cloud) || cloud.IsBusy)
                {
                    SetStatus("CLOUD PROGRESSION IS STILL LOADING  //  PURCHASES LOCKED");
                    return;
                }

                int cost = tenPackMode == 1 ? CloudPlayerDataService.TenPackCost : CloudPlayerDataService.SinglePackCost;
                if (cloud.State.signalCredits < cost)
                {
                    SetStatus("NOT ENOUGH SIGNAL CREDITS  //  NEED " + cost);
                    return;
                }

                if (purchaseInFlight) return;
                PurchaseAndLoadRevealAsync(tenPackMode == 1);
                return;
            }

            LoadRevealScene(tenPackMode);
        }

        async void PurchaseAndLoadRevealAsync(bool tenPack)
        {
            purchaseInFlight = true;
            UpdateButtons();
            string requestKey = tenPack
                ? CollectionData.PendingBundlePurchaseKey
                : CollectionData.PendingSinglePurchaseKey;
            string requestId = PlayerPrefs.GetString(requestKey, string.Empty);
            if (string.IsNullOrEmpty(requestId))
            {
                requestId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(requestKey, requestId);
                PlayerPrefs.Save();
            }

            try
            {
                await CloudPlayerDataService.Instance.PurchasePackAsync(tenPack, requestId);
                LoadRevealScene(tenPack ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                SetStatus("PACK PURCHASE FAILED  //  " + e.Message);
            }
            finally
            {
                purchaseInFlight = false;
                UpdateButtons();
            }
        }

        void LoadRevealScene(int tenPackMode)
        {
            PlayerPrefs.SetInt(PackOpeningController.ModeKey, tenPackMode);
            PlayerPrefs.Save();
            // Fresh-resolution contract: wipe any flip cursor trace from a previous session so the
            // reveal scene always starts face down, regardless of how the last visit ended.
            CollectionData.BeginFreshPack(tenPackMode == 1);
            SceneLoader.Load(SceneIndex.RevealGacha);
        }
    }
}
