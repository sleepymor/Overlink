// Assets/Scripts/UI/OfflineSignalBayController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Overlink.Net;
using Overlink.Vfx;

namespace Overlink.UI
{
    /// <summary>
    /// The shop half of the offline Gacha prototype. The Signal Bay screen sells the pack - choice
    /// cards, pricing, milestone track - and the reveal that follows it lives in its own scene
    /// (<see cref="SceneIndex.RevealGacha"/>), so the bay's only runtime job is to carry the player's
    /// pack resolution across the scene boundary.
    ///
    /// The choice travels through the same PlayerPrefs key the reveal controller reads in its Awake
    /// (<see cref="OfflinePackOpeningController.ModeKey"/>), which is how the two scenes agree on what
    /// is being opened without an object having to survive the load. Persisting it also means OPEN
    /// AGAIN on the reveal screen re-resolves the same pack size; the flip cursor itself never
    /// persists — every entry into the reveal is cleared to a fresh, face-down resolution here.
    /// </summary>
    public sealed class OfflineSignalBayController : MonoBehaviour
    {
        [SerializeField] Button openOneButton;
        [SerializeField] Button openTenButton;
        [SerializeField] Button returnButton;

        Button[] packChoiceButtons = new Button[3];
        TMP_Text selectedPackLabel;
        TMP_Text packCountLabel;
        TMP_Text infoDescription;
        TMP_Text compositionBody;
        TMP_Text contentsRates;
        TMP_Text singleCost;
        TMP_Text singleDetail;

        /// <summary>The bay art's own pricing: the balance is derived from packs purchased this demo
        /// run, so the credit readout is live data rather than baked fiction.</summary>
        const int StartCredits = 2400;
        const int CreditsPerPack = 100;

        // Live overlays authored by Overlink/Scenes/Wire Bay Live Counters over the erased regions of
        // the derived base texture; absent on scenes that predate the wiring, in which case the baked
        // art simply stays static.
        UiCounter creditCounter;
        UiCounter milestoneCounter;
        UiCounter remainingCounter;
        UiBar milestoneBar;
        TMP_Text playerIdentityText;
        PlayerProfileService profileService;

        void Awake()
        {
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
            BindPackChoices();
            UpdatePlayerIdentity();
            RollLiveCounters();
        }

        void Start()
        {
            profileService = PlayerProfileService.Instance;
            if (profileService != null)
            {
                profileService.ProfileLoaded += OnProfileLoaded;
                profileService.StatusChanged += OnProfileStatusChanged;
            }
            UpdatePlayerIdentity();
        }

        void OnDestroy()
        {
            if (profileService != null)
            {
                profileService.ProfileLoaded -= OnProfileLoaded;
                profileService.StatusChanged -= OnProfileStatusChanged;
            }
        }

        void BindPackChoices()
        {
            Transform bay = transform.Find("Figma Signal Bay");
            if (bay == null) return;
            for (int i = 0; i < packChoiceButtons.Length; i++)
            {
                Transform choice = bay.Find("Pack Choice " + (i + 1));
                if (choice == null) continue;
                packChoiceButtons[i] = choice.GetComponent<Button>();
                if (packChoiceButtons[i] == null)
                {
                    Debug.LogError("[SignalBay] Serialized pack choice is missing Button: " + choice.name);
                    continue;
                }
                // The editable accent hierarchy is decorative. Its nested Images must not win the
                // EventSystem raycast over the choice's parent Button.
                foreach (Graphic graphic in choice.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = graphic.transform == choice;
                // The title, metadata and price are authored as sibling text nodes over the card.
                // They are presentation only and otherwise become the first EventSystem hit.
                foreach (string suffix in new[] { "Label", "Meta", "Price" })
                {
                    Graphic overlay = bay.Find("Pack Choice " + (i + 1) + " " + suffix)?.GetComponent<Graphic>();
                    if (overlay != null) overlay.raycastTarget = false;
                }
                int captured = i;
                packChoiceButtons[i].onClick.AddListener(() => SelectPack((OfflineCollectionData.SignalPackId)captured));
            }
            ApplySelectedPack(OfflineCollectionData.SelectedPack());
        }

        void SelectPack(OfflineCollectionData.SignalPackId pack)
        {
            OfflineCollectionData.SelectPack(pack);
            ApplySelectedPack(pack);
        }

        void ApplySelectedPack(OfflineCollectionData.SignalPackId pack)
        {
            if (selectedPackLabel != null) selectedPackLabel.text = OfflineCollectionData.PackName(pack);
            if (packCountLabel != null) packCountLabel.text = "1 SIGNAL AVAILABLE";
            if (infoDescription != null)
                infoDescription.text = pack == OfflineCollectionData.SignalPackId.RelayNetwork
                    ? "Relay sparks, couriers and abilities from the network collection."
                    : pack == OfflineCollectionData.SignalPackId.FrontlineProtocol
                        ? "Bulwark units and repair abilities from the frontline collection."
                        : "Discover units, relays and abilities from the core collection.";
            if (compositionBody != null)
                compositionBody.text = pack == OfflineCollectionData.SignalPackId.RelayNetwork
                    ? "RELAYS / ABILITIES\nDETERMINISTIC OFFLINE PACKS"
                    : pack == OfflineCollectionData.SignalPackId.FrontlineProtocol
                        ? "FRONTLINE / UNITS\nDETERMINISTIC OFFLINE PACKS"
                        : "UNITS / RELAYS / ABILITIES\nDETERMINISTIC OFFLINE PACKS";
            if (contentsRates != null) contentsRates.text = "PACK CONTENTS  /  OFFLINE DEMO";
            if (singleCost != null) singleCost.text = "100 CREDITS";
            if (singleDetail != null) singleDetail.text = "5 CARDS  /  DETERMINISTIC";
            for (int i = 0; i < packChoiceButtons.Length; i++)
                if (packChoiceButtons[i] != null) packChoiceButtons[i].interactable = true;
        }

        void OnProfileLoaded(PlayerProfile profile) => UpdatePlayerIdentity();
        void OnProfileStatusChanged(string status) => UpdatePlayerIdentity();

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
            int progress = OfflineCollectionData.LoyaltyProgress();
            int threshold = OfflineCollectionData.LoyaltyThreshold;
            int credits = Mathf.Max(0, StartCredits - CreditsPerPack * OfflineCollectionData.PurchasedPacks());
            if (creditCounter != null) creditCounter.Set(credits);
            if (milestoneCounter != null) milestoneCounter.Set(progress);
            if (remainingCounter != null) remainingCounter.Set(Mathf.Max(0, threshold - progress));
            if (milestoneBar != null) milestoneBar.Set(progress, threshold);
        }

        void BindLiveCounters()
        {
            creditCounter = Counter("Credit Live");
            milestoneCounter = Counter("Milestone Number Live");
            remainingCounter = Counter("Remaining Live");
            Transform bar = transform.Find("Milestone Bar Live");
            milestoneBar = bar != null ? bar.GetComponent<UiBar>() : null;

            int threshold = OfflineCollectionData.LoyaltyThreshold;
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

        /// <summary>Opens a single standard pack in the reveal scene.</summary>
        public void OpenSinglePack() => OpenReveal(0);

        /// <summary>Opens the ten-pack bundle (plus any loyalty packs it grants) in the reveal scene.</summary>
        public void OpenTenPack() => OpenReveal(1);

        void OpenReveal(int tenPackMode)
        {
            OfflineCollectionData.SignalPackId pack = OfflineCollectionData.SelectedPack();
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, tenPackMode);
            PlayerPrefs.Save();
            // Fresh-resolution contract: wipe any flip cursor trace from a previous session so the
            // reveal scene always starts face down, regardless of how the last visit ended.
            OfflineCollectionData.BeginFreshPack(tenPackMode == 1, pack);
            SceneLoader.Load(SceneIndex.RevealGacha);
        }
    }
}
