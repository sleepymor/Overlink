using System.Collections.Generic;
using System.IO;
using Overlink.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overlink.EditorTools
{
    /// <summary>
    /// One-click verification for the offline Gacha screens. Tools:
    ///   • Verify Scene Bindings — edit-mode audit that every serialized controller reference in
    ///     <c>RevealGacha.unity</c>, <c>Gacha.unity</c> and <c>Collection.unity</c> is wired.
    ///   • Reset Pack State — clears the persisted reveal so the next play-through starts face down.
    ///   • Assert Ten-Pack Rules — edit-mode audit of the Milestone 3 data layer: fifty cards across ten
    ///     packs, the Epic-or-better guarantee, the bundle duplicate flag, and the §5 loyalty threshold
    ///     crossing (96 purchased + 10 = 106 grants exactly one bonus pack).
    ///   • Capture Reveal Sequence / Capture Ten-Pack Sequence / Capture Collection Screen / Capture
    ///     Bay To Reveal Flow — enter play, then record scripted stills to <c>Assets/Screenshots</c> so
    ///     each flow can be reviewed without hand-driving the Editor.
    ///
    /// These are Editor conveniences around the same runtime the player uses; none of them change the
    /// shipped behaviour of the controllers.
    /// </summary>
    public static class OfflineGachaVerification
    {
        const string GachaScenePath = "Assets/Scenes/Gacha.unity";
        const string RevealGachaScenePath = "Assets/Scenes/RevealGacha.unity";
        const string CollectionScenePath = "Assets/Scenes/Collection.unity";
        internal const string ScreenshotDir = "Assets/Screenshots";

        static readonly string[] ControllerRefs =
        {
            "progressText", "collectionText", "statusText", "instructionText", "headerAccent",
            "templateCatalog", "frameCatalog", "tierMaterials",
            "revealNextButton", "revealRemainingButton",
            "backButton", "openAgainButton",
            "packHeader", "queueRoot", "capsuleTemplate",
            "summaryPanel", "summaryCloseButton", "summaryContent",
            "summaryLineTemplate", "summaryGuaranteeText", "summaryLoyaltyText"
        };

        static readonly string[] SlotRefs = { "anchor", "pivot", "back", "faceHolder", "result" };

        static readonly string[] CollectionRefs =
        {
            "countText", "fragmentText", "deckText",
            "typeFilterButton", "factionFilterButton", "rarityFilterButton", "setFilterButton",
            "ownershipFilterButton", "sortButton",
            "gridRoot", "tileTemplate", "emptyText",
            "worldCardRoot", "worldCardCamera", "templateCatalog", "frameCatalog", "tierMaterials",
            "detailTitle", "detailOwnership", "detailBody", "addToDeckButton",
            "openPackButton", "backButton"
        };

        // ── Edit-mode binding audit ────────────────────────────────────────────────────────────

        [MenuItem("Tools/Offline Gacha/Verify Scene Bindings")]
        public static void VerifySceneBindings()
        {
            int missing = AuditRevealScene();
            missing += AuditShopScene();
            missing += AuditCollectionScene();
            if (missing == 0)
            {
                Debug.Log("[OfflineGachaVerification] PASS — RevealGacha + Gacha bay + Collection bindings fully wired.");
            }
            else
            {
                Debug.LogError("[OfflineGachaVerification] FAIL — " + missing + " unbound reference(s).");
            }
        }

        static int AuditRevealScene()
        {
            EnsureSceneOpen(RevealGachaScenePath);
            OfflinePackOpeningController controller = UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>();
            if (controller == null)
            {
                Debug.LogError("[OfflineGachaVerification] No OfflinePackOpeningController found in " + RevealGachaScenePath);
                return 1;
            }

            SerializedObject so = new SerializedObject(controller);
            int missing = AuditRefs(controller, ControllerRefs, "reveal controller");

            SerializedProperty fixtures = so.FindProperty("fixturesByRarity");
            if (fixtures == null || fixtures.arraySize == 0)
            {
                Debug.LogError("[OfflineGachaVerification] fixturesByRarity is empty.");
                missing++;
            }
            else
            {
                for (int i = 0; i < fixtures.arraySize; i++)
                {
                    if (fixtures.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        Debug.LogError("[OfflineGachaVerification] fixturesByRarity[" + i + "] is null.");
                        missing++;
                    }
                }
            }

            SerializedProperty slots = so.FindProperty("slots");
            if (slots == null || slots.arraySize != OfflineCollectionData.PackManifest.Length)
            {
                Debug.LogError("[OfflineGachaVerification] slots count is "
                    + (slots == null ? -1 : slots.arraySize) + ", expected "
                    + OfflineCollectionData.PackManifest.Length + ".");
                missing++;
            }
            else
            {
                for (int i = 0; i < slots.arraySize; i++)
                {
                    SerializedProperty element = slots.GetArrayElementAtIndex(i);
                    foreach (string field in SlotRefs)
                    {
                        SerializedProperty prop = element.FindPropertyRelative(field);
                        if (prop == null || prop.objectReferenceValue == null)
                        {
                            Debug.LogError("[OfflineGachaVerification] slots[" + i + "]." + field + " is null.");
                            missing++;
                        }
                    }
                }
            }

            if (missing == 0)
            {
                Debug.Log("[OfflineGachaVerification] RevealGacha PASS — "
                    + (slots != null ? slots.arraySize : 0) + " slots, catalogs, "
                    + (fixtures != null ? fixtures.arraySize : 0) + " rarity fixtures, "
                    + ControllerRefs.Length + " widget refs; cardWidth="
                    + so.FindProperty("cardWidth").floatValue.ToString("0.###") + ".");
            }

            return missing;
        }

        static readonly string[] ShopRefs = { "openOneButton", "openTenButton", "returnButton" };

        /// <summary>
        /// The bay is the shop half of the split: it must carry its own controller with the three bay
        /// buttons wired, and it must NOT carry the pack controller any more - the reveal lives in
        /// <see cref="RevealGachaScenePath"/> now, and a leftover pack controller in the shop would
        /// steal the submit key and hover the (missing) card row.
        /// </summary>
        static int AuditShopScene()
        {
            EnsureSceneOpen(GachaScenePath);
            OfflineSignalBayController controller = UnityEngine.Object.FindObjectOfType<OfflineSignalBayController>();
            if (controller == null)
            {
                Debug.LogError("[OfflineGachaVerification] No OfflineSignalBayController found in " + GachaScenePath);
                return 1;
            }

            if (UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>() != null)
            {
                Debug.LogError("[OfflineGachaVerification] " + GachaScenePath
                    + " still carries an OfflinePackOpeningController; the reveal moved to " + RevealGachaScenePath + ".");
                return 1;
            }

            int missing = AuditRefs(controller, ShopRefs, "bay controller");
            if (missing == 0)
            {
                Debug.Log("[OfflineGachaVerification] Gacha bay PASS — " + ShopRefs.Length + " widget refs wired.");
            }

            return missing;
        }

        static int AuditCollectionScene()
        {
            // Opening the binder replaces the Gacha scene; the audit runs in edit mode, so leaving
            // Collection.unity open afterwards is harmless.
            EnsureSceneOpen(CollectionScenePath);
            OfflineCollectionController controller = UnityEngine.Object.FindObjectOfType<OfflineCollectionController>();
            if (controller == null)
            {
                Debug.LogError("[OfflineGachaVerification] No OfflineCollectionController found in " + CollectionScenePath);
                return 1;
            }

            int missing = AuditRefs(controller, CollectionRefs, "Collection controller");
            if (missing == 0)
            {
                Debug.Log("[OfflineGachaVerification] Collection PASS — " + CollectionRefs.Length + " widget refs wired.");
            }

            return missing;
        }

        static int AuditRefs(UnityEngine.Object target, string[] fields, string label)
        {
            SerializedObject so = new SerializedObject(target);
            int missing = 0;
            foreach (string field in fields)
            {
                SerializedProperty prop = so.FindProperty(field);
                if (prop == null || prop.objectReferenceValue == null)
                {
                    Debug.LogError("[OfflineGachaVerification] Unbound " + label + " field: " + field);
                    missing++;
                }
            }

            return missing;
        }

        // ── Persisted-state reset ──────────────────────────────────────────────────────────────

        [MenuItem("Tools/Offline Gacha/Reset Pack State")]
        public static void ResetPackState()
        {
            OfflineCollectionData.Reset();
            Debug.Log("[OfflineGachaVerification] Offline pack state cleared; the next play-through starts face down (0 / 5 revealed).");
        }

        // ── Milestone 3 data-layer audit ───────────────────────────────────────────────────────

        [MenuItem("Tools/Offline Gacha/Assert Ten-Pack Rules")]
        public static void AssertTenPackRules()
        {
            int failures = 0;
            OfflineCollectionData.PackCard[] manifest = OfflineCollectionData.TenPackManifest;

            if (manifest.Length != 50)
            {
                Debug.LogError("[OfflineGachaVerification] Bundle card count is " + manifest.Length + ", expected 50.");
                failures++;
            }

            if (OfflineCollectionData.PacksInBundle != 10)
            {
                Debug.LogError("[OfflineGachaVerification] Bundle pack count is " + OfflineCollectionData.PacksInBundle + ", expected 10.");
                failures++;
            }

            for (int i = 0; i < manifest.Length; i++)
            {
                if (OfflineCollectionData.Find(manifest[i].Id) == null)
                {
                    Debug.LogError("[OfflineGachaVerification] TenPackManifest[" + i + "] id '" + manifest[i].Id + "' is not in the catalog.");
                    failures++;
                }
            }

            int guarantee = OfflineCollectionData.BundleGuaranteeIndex();
            if (guarantee < 0)
            {
                Debug.LogError("[OfflineGachaVerification] No Epic-or-better card in the bundle; the ten-pack guarantee would fail.");
                failures++;
            }

            // Deterministic duplicate audit from a clean ledger: exactly the intra-bundle repeats flag
            // as duplicates. Reset clears ownership/fragments/deck, same as Reset Pack State.
            OfflineCollectionData.Reset();
            var seen = new HashSet<string>();
            int expectedDuplicates = 0;
            int flaggedDuplicates = 0;
            for (int i = 0; i < manifest.Length; i++)
            {
                if (!seen.Add(manifest[i].Id)) expectedDuplicates++;
                if (OfflineCollectionData.IsBundleDuplicate(i)) flaggedDuplicates++;
            }

            if (flaggedDuplicates != expectedDuplicates)
            {
                Debug.LogError("[OfflineGachaVerification] Duplicate audit flagged " + flaggedDuplicates
                    + " bundle cards, expected " + expectedDuplicates + ".");
                failures++;
            }

            // §5 loyalty crossing: 96 purchased packs + a ten-pack purchase = 106, crossing the 100
            // threshold exactly once — one bonus pack granted, ledger at 1, progress 6/100, nothing due.
            OfflineCollectionData.Reset();
            OfflineCollectionData.CommitPurchase(96);
            int rewards = OfflineCollectionData.CommitPurchase(10);
            if (rewards != 1 || OfflineCollectionData.PurchasedPacks() != 106
                || OfflineCollectionData.LoyaltyGranted() != 1 || OfflineCollectionData.LoyaltyProgress() != 6
                || OfflineCollectionData.UnclaimedLoyaltyRewards() != 0)
            {
                Debug.LogError("[OfflineGachaVerification] Loyalty crossing failed: granted=" + rewards
                    + " purchased=" + OfflineCollectionData.PurchasedPacks()
                    + " ledger=" + OfflineCollectionData.LoyaltyGranted()
                    + " progress=" + OfflineCollectionData.LoyaltyProgress()
                    + " unclaimed=" + OfflineCollectionData.UnclaimedLoyaltyRewards()
                    + " (expected 1 / 106 / 1 / 6 / 0).");
                failures++;
            }

            // Full purchase path: resolving the bundle at 96 purchased must attach exactly one loyalty
            // pack, which the controller appends to the queue as an extra standard pack.
            OfflineCollectionData.Reset();
            OfflineCollectionData.CommitPurchase(96);
            OfflineCollectionData.EnsureBundleResolved();
            if (OfflineCollectionData.BundleLoyaltyPacks() != 1)
            {
                Debug.LogError("[OfflineGachaVerification] Bundle resolved at 96 purchased attached "
                    + OfflineCollectionData.BundleLoyaltyPacks() + " loyalty packs, expected 1.");
                failures++;
            }

            OfflineCollectionData.Reset();
            if (failures == 0)
            {
                Debug.Log("[OfflineGachaVerification] PASS — ten-pack rules: 50 cards / 10 packs, guarantee at index "
                    + guarantee + " (pack " + ((guarantee / 5) + 1) + " slot " + ((guarantee % 5) + 1) + "), "
                    + expectedDuplicates + " duplicates flagged, loyalty 96→106 grants exactly one bonus pack.");
            }
            else
            {
                Debug.LogError("[OfflineGachaVerification] FAIL — " + failures + " ten-pack rule violation(s). Local demo state was reset.");
            }
        }

        // ── Editor-driven captures ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/Offline Gacha/Capture Reveal Sequence")]
        public static void CaptureRevealSequence()
        {
            if (CaptureBusy()) return;
            // Face down from a clean slate in single-pack mode, so the sequence always starts on five
            // unrevealed backs.
            OfflineCollectionData.Reset();
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, 0);
            PlayerPrefs.Save();
            Directory.CreateDirectory(ScreenshotDir);
            EnsureSceneOpen(RevealGachaScenePath);
            StartCapture(CaptureDriver.FlowSingle, "backs → mid-flip → face → all faces");
        }

        [MenuItem("Tools/Offline Gacha/Capture Ten-Pack Sequence")]
        public static void CaptureTenPackSequence()
        {
            if (CaptureBusy()) return;
            OfflineCollectionData.Reset();
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, 1);
            PlayerPrefs.Save();
            Directory.CreateDirectory(ScreenshotDir);
            EnsureSceneOpen(RevealGachaScenePath);
            StartCapture(CaptureDriver.FlowTenPack, "ten-pack queue → pack 1 reveals → bundle summary");
        }

        /// <summary>
        /// The scene-split flow: the shop still, a real press of OPEN ONE through its serialized
        /// onClick, the reveal scene that press loads, a full REVEAL ALL completion (auto summary +
        /// done-state buttons), OPEN AGAIN's fresh re-resolution, and the bay round trip with a second
        /// fresh entry. Proves the boundary and the freshness contract without hand-driving the Editor.
        /// </summary>
        [MenuItem("Tools/Offline Gacha/Capture Bay To Reveal Flow")]
        public static void CaptureBayToRevealFlow()
        {
            if (CaptureBusy()) return;
            OfflineCollectionData.Reset();
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, 0);
            PlayerPrefs.Save();
            Directory.CreateDirectory(ScreenshotDir);
            EnsureSceneOpen(GachaScenePath);
            StartCapture(CaptureDriver.FlowBayToReveal,
                "bay → open one → reveal all → open again fresh → back to bay → open one fresh");
        }

        internal static void VerifyTenPackPointerFlow() => VerifyTenPackPointerFlow(false);

        internal static void VerifyEditableBayTenPointerFlow() => VerifyTenPackPointerFlow(true);

        static void VerifyTenPackPointerFlow(bool showDraft)
        {
            if (CaptureBusy()) return;
            SessionState.SetBool("Overlink.BayPointer.ShowDraft", showDraft);
            OfflineCollectionData.Reset();
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, 0);
            PlayerPrefs.Save();
            EnsureSceneOpen(GachaScenePath);
            StartCapture(CaptureDriver.FlowBayTenPointer, "pointer bay → ten-pack reveal all → open again");
        }

        internal static void VerifyEditableBayReturnPointerFlow()
        {
            if (CaptureBusy()) return;
            EnsureSceneOpen(GachaScenePath);
            StartCapture(CaptureDriver.FlowBayReturnPointer, "editable bay → collection");
        }

        internal static void VerifySinglePackPointerFlow() => VerifySinglePackPointerFlow(false);

        internal static void VerifyEditableBaySinglePointerFlow() => VerifySinglePackPointerFlow(true);

        static void VerifySinglePackPointerFlow(bool showDraft)
        {
            if (CaptureBusy()) return;
            SessionState.SetBool("Overlink.BayPointer.ShowDraft", showDraft);
            OfflineCollectionData.Reset();
            OfflineCollectionData.EnsureResolved();
            if (OfflineCollectionData.PurchasedPacks() != 0)
                throw new System.InvalidOperationException("Collection initialization charged a purchase.");
            OfflineCollectionData.Reset();
            OfflineCollectionData.CommitPurchase(99);
            OfflineCollectionData.EnsurePurchasedSingleResolved();
            int owned = OfflineCollectionData.Owned(OfflineCollectionData.PackManifest[0].Id);
            OfflineCollectionData.EnsurePurchasedSingleResolved();
            if (OfflineCollectionData.PurchasedPacks() != 100 || OfflineCollectionData.LoyaltyGranted() != 1
                || owned != 4 || OfflineCollectionData.Owned(OfflineCollectionData.PackManifest[0].Id) != owned
                || OfflineCollectionData.Fragments() != 62)
                throw new System.InvalidOperationException("Single-pack threshold or idempotency verification failed.");
            Debug.Log("[BayPointer] PASS: collection initialization uncharged; 99→100 grants one bonus pack exactly once.");
            SessionState.SetBool("Overlink.BayPointer.SingleAccountingVerified", true);
            OfflineCollectionData.Reset();
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, 0);
            PlayerPrefs.Save();
            EnsureSceneOpen(GachaScenePath);
            StartCapture(CaptureDriver.FlowBaySinglePointer, "pointer bay → single-pack reveal all → open again");
        }

        internal static void VerifySingleLoyaltyReveal()
        {
            if (CaptureBusy()) return;
            SessionState.SetBool("Overlink.BayLoyalty.Halfway", false);
            OfflineCollectionData.Reset();
            OfflineCollectionData.CommitPurchase(99);
            PlayerPrefs.SetInt(OfflinePackOpeningController.ModeKey, 0);
            PlayerPrefs.Save();
            EnsureSceneOpen(GachaScenePath);
            StartCapture(CaptureDriver.FlowSingleLoyaltyPointer, "single-pack loyalty bonus pointer reveal");
        }

        [MenuItem("Tools/Offline Gacha/Capture Collection Screen")]
        public static void CaptureCollectionScreen()
        {
            if (CaptureBusy()) return;
            Directory.CreateDirectory(ScreenshotDir);
            EnsureSceneOpen(CollectionScenePath);
            // Seed ownership plus a small deck so the binder and the deck header show a populated state.
            OfflineCollectionData.EnsureResolved();
            OfflineCollectionData.AddToDeck("card_fsti_001");
            OfflineCollectionData.AddToDeck("card_fsti_001");
            OfflineCollectionData.AddToDeck("card_frti_001");
            StartCapture(CaptureDriver.FlowCollection, "collection binder still");
        }

        [MenuItem("Tools/Offline Gacha/Abort Capture")]
        public static void AbortCapture()
        {
            SessionState.SetBool(CaptureDriver.ActiveKey, false);
            CaptureDriver.Unhook();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.Log("[OfflineGachaVerification] Capture aborted.");
        }

        static bool CaptureBusy()
        {
            if (SessionState.GetBool(CaptureDriver.ActiveKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[OfflineGachaVerification] Capture already running or Editor is in play mode.");
                return true;
            }

            return false;
        }

        static void EnsureSceneOpen(string path)
        {
            Scene scene = EditorSceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }

        static void StartCapture(int flow, string description)
        {
            SessionState.SetBool(CaptureDriver.ActiveKey, true);
            SessionState.SetInt(CaptureDriver.FlowKey, flow);
            SessionState.SetInt(CaptureDriver.StepKey, 0);
            SessionState.SetFloat(CaptureDriver.T0Key, -1f);
            CaptureDriver.Hook();
            EditorApplication.isPlaying = true;
            Debug.Log("[OfflineGachaVerification] Entering play to capture " + description + ".");
        }
    }

    /// <summary>
    /// The timed driver behind the capture menus. Entering play mode reloads the script domain, which
    /// destroys static fields and every delegate subscribed before the reload — so the capture state
    /// lives in <see cref="SessionState"/> (which survives reloads) and the editor callbacks are
    /// re-subscribed from the <see cref="InitializeOnLoad"/> constructor after each reload. Step timing
    /// is anchored to <see cref="Time.unscaledTime"/> at the moment play actually begins, so the
    /// schedule is independent of how long the entry reload itself took.
    /// </summary>
    [InitializeOnLoad]
    static class CaptureDriver
    {
        internal const string ActiveKey = "Overlink.OfflineGacha.CaptureActive";
        internal const string FlowKey = "Overlink.OfflineGacha.CaptureFlow";
        internal const string StepKey = "Overlink.OfflineGacha.CaptureStep";
        internal const string T0Key = "Overlink.OfflineGacha.CaptureT0";
        const string OwnedBeforeReopenKey = "Overlink.OfflineGacha.OwnedBeforeReopen";
        const string OwnedBeforeReentryKey = "Overlink.OfflineGacha.OwnedBeforeReentry";

        internal const int FlowSingle = 0;
        internal const int FlowTenPack = 1;
        internal const int FlowCollection = 2;
        internal const int FlowBayToReveal = 3;
        internal const int FlowBayTenPointer = 4;
        internal const int FlowBaySinglePointer = 5;
        internal const int FlowSingleLoyaltyPointer = 6;
        internal const int FlowBayReturnPointer = 7;

        static bool hooked;
        static OfflinePackOpeningController driven;

        static CaptureDriver()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Hook();
            }
        }

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            driven = null;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Time.unscaledTime does not restart at zero when play activates (it also counts the
                // entry reload), so anchor the step schedule to the moment play actually begins.
                SessionState.SetFloat(T0Key, Time.unscaledTime);
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (!SessionState.GetBool(ActiveKey, false)) return;
            SessionState.SetBool(ActiveKey, false);
            Unhook();
            AssetDatabase.Refresh();
            Debug.Log("[OfflineGachaVerification] Capture complete; stills in " + OfflineGachaVerification.ScreenshotDir + ".");
        }

        static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(ActiveKey, false)) return;

            float t0 = SessionState.GetFloat(T0Key, -1f);
            if (t0 < 0f)
            {
                t0 = Time.unscaledTime;
                SessionState.SetFloat(T0Key, t0);
            }
            float t = Time.unscaledTime - t0;

            int flow = SessionState.GetInt(FlowKey, FlowSingle);
            if (flow == FlowCollection)
            {
                // The binder scene has no pack controller to drive; it only needs a settled still.
                UpdateCollection(t);
                return;
            }

            // The bay-to-reveal flow starts in the shop, which deliberately carries no pack controller
            // any more, so it must dispatch before the driven-controller gate below.
            if (flow == FlowBayToReveal)
            {
                UpdateBayToReveal(t);
                return;
            }
            if (flow == FlowSingleLoyaltyPointer)
            {
                UpdateSingleLoyaltyPointer(t);
                return;
            }
            if (flow == FlowBayReturnPointer)
            {
                UpdateBayReturnPointer(t);
                return;
            }
            if (flow == FlowBayTenPointer || flow == FlowBaySinglePointer)
            {
                UpdateBayPointer(t, flow == FlowBayTenPointer);
                return;
            }

            if (driven == null)
            {
                driven = UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>();
                if (driven == null) return;
            }

            if (flow == FlowTenPack) UpdateTenPack(t);
            else UpdateSingle(t);
        }

        // Timed against the common reveal profile: flip ≈ 1.05s, landing pop ≈ 0.26s. Higher tiers
        // intentionally take longer, so the completion check below waits on the controller state.
        static void UpdateSingle(float t)
        {
            switch (SessionState.GetInt(StepKey, 0))
            {
                case 0: if (t > 0.9f) { Debug.Log("[OfflineGachaVerification] step0 backs t=" + t.ToString("0.00")); Shot("gacha-anim-1-backs.png"); SetStep(1); } break;
                case 1: if (t > 1.3f) { Debug.Log("[OfflineGachaVerification] step1 reveal t=" + t.ToString("0.00")); driven.RevealNext(); SetStep(2); } break;
                // The flip is only ~1s long, so the two in-flight stills are captured synchronously from
                // the card camera: CaptureScreenshot defers to a later frame and misses the pose.
                case 2: if (t > 1.95f) { Debug.Log("[OfflineGachaVerification] step2 midflip t=" + t.ToString("0.00")); ShotCamera("gacha-anim-2-midflip.png"); SetStep(3); } break;   // ~127°: face emerging at an angle
                case 3: if (t > 2.8f) { Debug.Log("[OfflineGachaVerification] step3 face t=" + t.ToString("0.00")); ShotCamera("gacha-anim-3-face.png"); SetStep(4); } break;        // flip + landing pop complete
                case 4: if (t > 3.2f) { Debug.Log("[OfflineGachaVerification] step4 remaining t=" + t.ToString("0.00")); driven.RevealRemaining(); SetStep(5); } break;
                // Completion now auto-opens the summary overlay, so the all-faces still is camera-exact
                // (world row only) and a second full-screen still documents the auto-opened summary.
                case 5: if (!driven.IsRevealInProgress && driven.RevealedCount >= 5) { Debug.Log("[OfflineGachaVerification] step5 allfaces t=" + t.ToString("0.00")); ShotCamera("gacha-anim-4-allfaces.png"); SetStep(6); } break;
                case 6: if (t > 0.4f) { Debug.Log("[OfflineGachaVerification] step6 summary t=" + t.ToString("0.00")); Shot("gacha-anim-5-summary.png"); SetStep(7); } break;
                case 7: if (t > 0.8f) { SetStep(8); EditorApplication.isPlaying = false; } break;
            }
        }

        /// <summary>Ten-pack flow: the queue strip with ten sealed capsules over five backs, pack 1 two
        /// cards in (camera-exact, so the real template faces are visible), then — after RevealRemaining
        /// completes the bundle — the grouped summary with its guarantee and loyalty audit lines.</summary>
        static void UpdateTenPack(float t)
        {
            switch (SessionState.GetInt(StepKey, 0))
            {
                case 0: if (t > 0.9f) { Debug.Log("[OfflineGachaVerification] ten step0 queue t=" + t.ToString("0.00")); Shot("gacha-ten-1-queue.png"); SetStep(1); } break;
                case 1: if (t > 1.3f) { Debug.Log("[OfflineGachaVerification] ten step1 reveal t=" + t.ToString("0.00")); driven.RevealNext(); SetStep(2); } break;
                case 2: if (t > 2.9f) { Debug.Log("[OfflineGachaVerification] ten step2 reveal t=" + t.ToString("0.00")); driven.RevealNext(); SetStep(3); } break;
                case 3: if (t > 4.4f) { Debug.Log("[OfflineGachaVerification] ten step3 pack1 t=" + t.ToString("0.00")); ShotCamera("gacha-ten-2-pack1.png"); SetStep(4); } break;
                case 4: if (t > 4.8f) { Debug.Log("[OfflineGachaVerification] ten step4 remaining t=" + t.ToString("0.00")); driven.RevealRemaining(); SetStep(5); } break;
                case 5: if (!driven.IsRevealInProgress && driven.RevealedCount >= 50) { Debug.Log("[OfflineGachaVerification] ten step5 summary t=" + t.ToString("0.00")); Shot("gacha-ten-3-summary.png"); SetStep(6); } break;
                case 6: if (t > 0.4f) { SetStep(7); EditorApplication.isPlaying = false; } break;
            }
        }

        /// <summary>
        /// The scene-split flow. Button presses go through <c>onClick.Invoke</c> on the authored
        /// buttons, so the serialized wiring is what gets exercised, not just the controller methods.
        /// The flow also proves the fresh-resolution contract: REVEAL ALL completes the pack (auto
        /// summary + SIGNAL BAY / OPEN AGAIN), OPEN AGAIN reloads to a face-down pack, and a second
        /// entry from the bay is likewise fresh. Scene loads re-anchor the schedule (T0) because the
        /// settle time that matters after a load is the new scene's, not the play entry's.
        /// </summary>
        static void UpdateBayToReveal(float t)
        {
            switch (SessionState.GetInt(StepKey, 0))
            {
                case 0: if (t > 0.8f) { Debug.Log("[OfflineGachaVerification] bay step0 shop t=" + t.ToString("0.00")); Shot("gacha-bay-shop.png"); SetStep(1); } break;
                case 1:
                {
                    GameObject open = GameObject.Find("Figma Open One");
                    if (open == null) return;
                    Debug.Log("[OfflineGachaVerification] bay step1 open one t=" + t.ToString("0.00"));
                    open.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    SetStep(2);
                    break;
                }
                case 2:
                    if (SceneManager.GetActiveScene().name == SceneIndex.RevealGacha)
                    {
                        Debug.Log("[OfflineGachaVerification] bay step2 reveal loaded t=" + t.ToString("0.00"));
                        SessionState.SetFloat(T0Key, Time.unscaledTime);
                        SetStep(3);
                    }
                    break;
                case 3: if (t > 1.0f) { Debug.Log("[OfflineGachaVerification] bay step3 reveal still t=" + t.ToString("0.00")); Shot("gacha-reveal-from-bay.png"); SetStep(4); } break;
                case 4:
                {
                    OfflinePackOpeningController reveal = RevealController();
                    if (reveal == null) return;
                    Debug.Log("[OfflineGachaVerification] bay step4 reveal all t=" + t.ToString("0.00"));
                    reveal.RevealRemaining();
                    SetStep(5);
                    break;
                }
                case 5:
                {
                    OfflinePackOpeningController reveal = RevealController();
                    if (reveal == null || reveal.IsRevealInProgress || reveal.RevealedCount < 5) return;
                    Debug.Log("[OfflineGachaVerification] bay step5 done t=" + t.ToString("0.00"));
                    Shot("gacha-reveal-done.png");
                    SessionState.SetFloat(T0Key, Time.unscaledTime);
                    SetStep(6);
                    break;
                }
                case 6:
                {
                    // Anchored on the reveal controller, which sits on RevealScreen with the done-state
                    // buttons as direct children. A bare GameObject.Find races against the world card
                    // backs (Card n/Pivot/Back), and the path form of Find only resolves from a scene
                    // root — RevealScreen hangs under Offline Pack Canvas, not a root.
                    OfflinePackOpeningController reveal = RevealController();
                    Transform openAgain = reveal != null ? reveal.transform.Find("Open Again") : null;
                    if (openAgain == null || !openAgain.gameObject.activeSelf) return;
                    SessionState.SetInt(OwnedBeforeReopenKey, OfflineCollectionData.Owned(OfflineCollectionData.PackManifest[0].Id));
                    Debug.Log("[OfflineGachaVerification] bay step6 open again t=" + t.ToString("0.00"));
                    openAgain.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    SetStep(7);
                    break;
                }
                case 7:
                {
                    // The reload is synchronous, so the next tick already sees the fresh controller;
                    // one beat of settle keeps the still clean.
                    if (t > 1.0f)
                    {
                        OfflinePackOpeningController reveal = RevealController();
                        if (reveal == null) return;
                        if (reveal.RevealedCount != 0)
                        {
                            Debug.LogError("[OfflineGachaVerification] OPEN AGAIN did not reset: RevealedCount="
                                + reveal.RevealedCount + ", expected 0.");
                        }
                        else
                        {
                            Debug.Log("[OfflineGachaVerification] bay step7 open-again fresh t=" + t.ToString("0.00"));
                        }
                        CheckFreshResolution(OwnedBeforeReopenKey, "OPEN AGAIN");

                        Shot("gacha-reveal-open-again.png");
                        SetStep(8);
                    }
                    break;
                }
                case 8:
                {
                    OfflinePackOpeningController reveal = RevealController();
                    if (reveal == null) return;
                    Debug.Log("[OfflineGachaVerification] bay step8 reveal all (second) t=" + t.ToString("0.00"));
                    reveal.RevealRemaining();
                    SetStep(9);
                    break;
                }
                case 9:
                {
                    OfflinePackOpeningController reveal = RevealController();
                    if (reveal == null || reveal.IsRevealInProgress || reveal.RevealedCount < 5) return;
                    Transform back = reveal.transform.Find("Back");
                    if (back == null || !back.gameObject.activeSelf) return;
                    SessionState.SetInt(OwnedBeforeReentryKey, OfflineCollectionData.Owned(OfflineCollectionData.PackManifest[0].Id));
                    Debug.Log("[OfflineGachaVerification] bay step9 back t=" + t.ToString("0.00"));
                    back.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    SetStep(10);
                    break;
                }
                case 10:
                    if (SceneManager.GetActiveScene().name == SceneIndex.Gacha)
                    {
                        Debug.Log("[OfflineGachaVerification] bay step10 bay reloaded t=" + t.ToString("0.00"));
                        SessionState.SetFloat(T0Key, Time.unscaledTime);
                        SetStep(11);
                    }
                    break;
                case 11: if (t > 0.6f) { Debug.Log("[OfflineGachaVerification] bay step11 return still t=" + t.ToString("0.00")); Shot("gacha-bay-return.png"); SetStep(12); } break;
                case 12:
                {
                    // One beat after the still, so the deferred screenshot lands on the bay frame and
                    // not on the scene the click loads.
                    if (t <= 1.0f) return;
                    GameObject open = GameObject.Find("Figma Open One");
                    if (open == null) return;
                    Debug.Log("[OfflineGachaVerification] bay step12 open one (second) t=" + t.ToString("0.00"));
                    open.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    SetStep(13);
                    break;
                }
                case 13:
                    if (SceneManager.GetActiveScene().name == SceneIndex.RevealGacha)
                    {
                        Debug.Log("[OfflineGachaVerification] bay step13 reveal reloaded t=" + t.ToString("0.00"));
                        SessionState.SetFloat(T0Key, Time.unscaledTime);
                        SetStep(14);
                    }
                    break;
                case 14:
                {
                    if (t <= 1.0f) return;
                    OfflinePackOpeningController reveal = RevealController();
                    if (reveal == null) return;
                    if (reveal.RevealedCount != 0)
                    {
                        Debug.LogError("[OfflineGachaVerification] Bay re-entry did not reset: RevealedCount="
                            + reveal.RevealedCount + ", expected 0.");
                    }
                    else
                    {
                        Debug.Log("[OfflineGachaVerification] bay step14 fresh entry t=" + t.ToString("0.00"));
                    }
                    CheckFreshResolution(OwnedBeforeReentryKey, "Bay re-entry");

                    Shot("gacha-reveal-fresh-entry.png");
                    SetStep(15);
                    break;
                }
                case 15: if (t > 0.8f) { SetStep(16); EditorApplication.isPlaying = false; } break;
            }
        }

        static OfflinePackOpeningController RevealController()
        {
            return UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>();
        }

        // Dispatch through the raycast winner, rather than invoking Button.onClick directly. Scene
        // changes can destroy the clicked target synchronously, so pointerClick must be the last event.
        static bool PointerClick(Button expected, Vector2 point)
        {
            var events = EventSystem.current;
            if (events == null || expected == null || !expected.IsActive() || !expected.IsInteractable()) return false;
            var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            events.RaycastAll(data, hits);
            if (hits.Count == 0) return false;
            var hit = hits[0].gameObject;
            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != expected.gameObject) return false;
            data.pointerCurrentRaycast = hits[0];
            data.pointerPressRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerClickHandler);
            return true;
        }

        static bool ClickButton(Button button)
        {
            if (button == null) return false;
            var rect = button.transform as RectTransform;
            if (rect == null) return false;
            var canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return PointerClick(button, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)));
        }

        static void PointerFailure(string message)
        {
            Debug.LogError("[BayPointer] FAIL: " + message);
            SessionState.SetBool(ActiveKey, false);
            Unhook();
            EditorApplication.isPlaying = false;
        }

        static void PointerStep(int step)
        {
            SessionState.SetFloat(T0Key, Time.unscaledTime);
            SetStep(step);
        }

        static void UpdateSingleLoyaltyPointer(float t)
        {
            int step = SessionState.GetInt(StepKey, 0);
            if (t > (step == 2 ? 60f : 20f)) { PointerFailure("loyalty flow timeout at step " + step); return; }
            var reveal = RevealController();
            if (step == 0 && t > 1f)
            {
                var bay = UnityEngine.Object.FindObjectOfType<OfflineSignalBayController>();
                var button = bay != null ? new SerializedObject(bay).FindProperty("openOneButton").objectReferenceValue as Button : null;
                var point = new Vector2(1302f / 1600f * Screen.width, (1f - 494f / 900f) * Screen.height);
                if (!PointerClick(button, point)) { PointerFailure("loyalty single-pack shop click missed"); return; }
                PointerStep(1);
            }
            else if (step == 1 && reveal != null && t > 1f)
            {
                var so = new SerializedObject(reveal);
                var queue = so.FindProperty("queueRoot").objectReferenceValue as Transform;
                var progress = so.FindProperty("progressText").objectReferenceValue as TMPro.TMP_Text;
                if (OfflineCollectionData.PurchasedPacks() != 100 || OfflineCollectionData.SingleLoyaltyPacks() != 1
                    || OfflineCollectionData.LoyaltyGranted() != 1 || OfflineCollectionData.Owned(OfflineCollectionData.PackManifest[0].Id) != 4
                    || queue == null || !queue.gameObject.activeInHierarchy || queue.Find("Capsule 2/State") == null
                    || !queue.Find("Capsule 2/State").GetComponent<TMPro.TMP_Text>().text.Contains("LOYALTY PACK")
                    || progress == null || !progress.text.Contains("/ 10"))
                { PointerFailure("single loyalty entry, ten-card progress, ownership, or queue incorrect"); return; }
                if (!ClickButton(reveal.transform.Find("Reveal Remaining").GetComponent<Button>())) { PointerFailure("loyalty REVEAL ALL missed"); return; }
                Debug.Log("[BayLoyalty] PASS: 99→100 gives two queued packs, ten-card progress and one bonus ownership grant.");
                PointerStep(2);
            }
            else if (step == 2 && reveal != null && reveal.RevealedCount >= 5 && reveal.RevealedCount < 10)
            {
                if (!SessionState.GetBool("Overlink.BayLoyalty.Halfway", false))
                {
                    if (!reveal.transform.Find("Reveal Next").gameObject.activeSelf || reveal.transform.Find("Back").gameObject.activeSelf)
                    { PointerFailure("bonus remained but reveal controls already switched to done state"); return; }
                    SessionState.SetBool("Overlink.BayLoyalty.Halfway", true);
                    Debug.Log("[BayLoyalty] PASS: reveal controls remain active while bonus cards remain.");
                }
            }
            else if (step == 2 && reveal != null && !reveal.IsRevealInProgress && reveal.RevealedCount >= 10)
            {
                var next = reveal.transform.Find("Reveal Next");
                var back = reveal.transform.Find("Back");
                var summary = reveal.transform.Find("Bundle Summary");
                var labels = summary != null ? summary.GetComponentsInChildren<TMPro.TMP_Text>(true) : new TMPro.TMP_Text[0];
                bool bonus = false;
                foreach (var label in labels) if (label.gameObject.activeInHierarchy && label.text.Contains("LOYALTY BONUS PACK")) bonus = true;
                if (!SessionState.GetBool("Overlink.BayLoyalty.Halfway", false) || next.gameObject.activeSelf || !back.gameObject.activeSelf || summary == null || !summary.gameObject.activeSelf || !bonus)
                { PointerFailure("ten-card completion or bonus summary incorrect"); return; }
                if (!ClickButton(summary.Find("Summary Close").GetComponent<Button>())) { PointerFailure("loyalty summary close missed"); return; }
                Debug.Log("[BayLoyalty] PASS: ten cards revealed; done state and LOYALTY BONUS PACK summary shown.");
                PointerStep(3);
            }
            else if (step == 3 && reveal != null && t > .4f)
            {
                if (!ClickButton(reveal.transform.Find("Open Again").GetComponent<Button>())) { PointerFailure("loyalty OPEN AGAIN missed"); return; }
                PointerStep(4);
            }
            else if (step == 4 && reveal != null && t > 1f)
            {
                var so = new SerializedObject(reveal);
                var progress = so.FindProperty("progressText").objectReferenceValue as TMPro.TMP_Text;
                if (OfflineCollectionData.PurchasedPacks() != 101 || OfflineCollectionData.SingleLoyaltyPacks() != 0
                    || OfflineCollectionData.LoyaltyGranted() != 1 || reveal.RevealedCount != 0
                    || progress == null || !progress.text.Contains("/ 5"))
                { PointerFailure("reopen incorrectly repeated bonus or did not start a fresh five-card pack"); return; }
                Debug.Log("[BayLoyalty] PASS: OPEN AGAIN advances to 101, starts five cards, and does not repeat the bonus.");
                SessionState.SetBool(ActiveKey, false);
                Unhook();
                EditorApplication.isPlaying = false;
            }
        }

        static void UpdateBayPointer(float t, bool ten)
        {
            string label = ten ? "ten-pack" : "single-pack";
            var manifest = ten ? OfflineCollectionData.TenPackManifest : OfflineCollectionData.PackManifest;
            int step = SessionState.GetInt(StepKey, 0);
            if (t > (step == 2 ? 180f : 20f)) { PointerFailure("timeout at step " + step); return; }
            var reveal = RevealController();
            if (step == 0 && t > 1f)
            {
                if (!ten && SessionState.GetBool("Overlink.BayPointer.SingleAccountingVerified", false))
                {
                    Debug.Log("[BayPointer] PASS: collection initialization uncharged; 99→100 grants one bonus pack exactly once (62 fragments).");
                    SessionState.SetBool("Overlink.BayPointer.SingleAccountingVerified", false);
                }
                var bay = UnityEngine.Object.FindObjectOfType<OfflineSignalBayController>();
                var expected = bay != null ? new SerializedObject(bay).FindProperty(ten ? "openTenButton" : "openOneButton").objectReferenceValue as Button : null;
                var point = new Vector2(1302f / 1600f * Screen.width, (1f - (ten ? 574f : 494f) / 900f) * Screen.height);
                bool showDraft = SessionState.GetBool("Overlink.BayPointer.ShowDraft", false);
                if (showDraft)
                {
                    var group = bay != null ? bay.transform.Find("Figma Signal Bay")?.GetComponent<CanvasGroup>() : null;
                    if (group == null) { PointerFailure("editable bay missing"); return; }
                    if (group.alpha < 0.999f)
                    {
                        group.alpha = 1f;
                        Canvas.ForceUpdateCanvases();
                        // The fit callback changes text and transforms during canvas rendering.
                        // Let the next frame rebuild those graphics before raycasting them.
                        return;
                    }
                }
                if (!(showDraft ? ClickButton(expected) : PointerClick(expected, point))) { PointerFailure("visible " + label + " center did not hit the wired button"); return; }
                if (showDraft) Debug.Log("[BayPointer] PASS: visible editable " + label + " button dispatched pointer events.");
                SessionState.SetBool("Overlink.BayPointer.ShowDraft", false);
                Debug.Log("[BayPointer] PASS: visible " + label + " center dispatched pointer events.");
                PointerStep(1);
            }
            else if (step == 1 && reveal != null && t > 1f)
            {
                if (PlayerPrefs.GetInt(OfflinePackOpeningController.ModeKey) != (ten ? 1 : 0) || OfflineCollectionData.PurchasedPacks() != (ten ? 10 : 1) || reveal.RevealedCount != 0)
                { PointerFailure(label + " entry mode, purchase count, or face-down state incorrect"); return; }
                var next = reveal.transform.Find("Reveal Next");
                var all = reveal.transform.Find("Reveal Remaining");
                var back = reveal.transform.Find("Back");
                var again = reveal.transform.Find("Open Again");
                if (next == null || all == null || back == null || again == null || !next.gameObject.activeSelf || !all.gameObject.activeSelf || back.gameObject.activeSelf || again.gameObject.activeSelf)
                { PointerFailure("initial reveal button state incorrect"); return; }
                if (!ClickButton(all.GetComponent<Button>())) { PointerFailure("REVEAL ALL pointer target missed"); return; }
                Debug.Log("[BayPointer] PASS: " + label + " entry and initial buttons; REVEAL ALL pointer dispatched.");
                PointerStep(2);
            }
            else if (step == 2 && reveal != null && !reveal.IsRevealInProgress && reveal.RevealedCount >= manifest.Length)
            {
                var next = reveal.transform.Find("Reveal Next");
                var all = reveal.transform.Find("Reveal Remaining");
                var back = reveal.transform.Find("Back");
                var again = reveal.transform.Find("Open Again");
                if (next.gameObject.activeSelf || all.gameObject.activeSelf || !back.gameObject.activeSelf || !again.gameObject.activeSelf)
                { PointerFailure("completed reveal button state incorrect"); return; }
                var close = reveal.transform.Find("Bundle Summary/Summary Close");
                if (close == null || !ClickButton(close.GetComponent<Button>())) { PointerFailure("summary close pointer target missed"); return; }
                Debug.Log("[BayPointer] PASS: " + manifest.Length + " cards revealed and done-state buttons active; summary closed via pointer.");
                PointerStep(3);
            }
            else if (step == 3 && reveal != null && t > .4f)
            {
                var again = reveal.transform.Find("Open Again");
                SessionState.SetInt(OwnedBeforeReopenKey, OfflineCollectionData.Owned(manifest[0].Id));
                if (!ClickButton(again.GetComponent<Button>())) { PointerFailure("OPEN AGAIN pointer target missed"); return; }
                Debug.Log("[BayPointer] OPEN AGAIN pointer dispatched.");
                PointerStep(4);
            }
            else if (step == 4 && reveal != null && t > 1f)
            {
                string id = manifest[0].Id;
                int copies = 0;
                foreach (var card in manifest) if (card.Id == id) copies++;
                int delta = OfflineCollectionData.Owned(id) - SessionState.GetInt(OwnedBeforeReopenKey, -1000);
                if (reveal.RevealedCount != 0 || OfflineCollectionData.PurchasedPacks() != (ten ? 20 : 2) || delta != copies)
                { PointerFailure("OPEN AGAIN did not freshly resolve " + label + " (ownership delta " + delta + ", expected " + copies + ")"); return; }
                Debug.Log("[BayPointer] PASS: " + label + " OPEN AGAIN resolved fresh; purchases=" + OfflineCollectionData.PurchasedPacks() + ", ownership +" + delta + ", revealed=0.");
                if (!ten)
                {
                    if (!ClickButton(reveal.transform.Find("Reveal Remaining").GetComponent<Button>()))
                    { PointerFailure("second REVEAL ALL pointer target missed"); return; }
                    PointerStep(5);
                    return;
                }
                SessionState.SetBool(ActiveKey, false);
                Unhook();
                EditorApplication.isPlaying = false;
            }
            else if (step == 5 && reveal != null && !reveal.IsRevealInProgress && reveal.RevealedCount >= manifest.Length)
            {
                var close = reveal.transform.Find("Bundle Summary/Summary Close");
                if (close == null || !ClickButton(close.GetComponent<Button>())) { PointerFailure("second summary close missed"); return; }
                PointerStep(6);
            }
            else if (step == 6 && reveal != null && t > .4f)
            {
                var back = reveal.transform.Find("Back");
                if (back == null || !ClickButton(back.GetComponent<Button>())) { PointerFailure("SIGNAL BAY pointer target missed"); return; }
                PointerStep(7);
            }
            else if (step == 7 && t > 2f)
            {
                var bay = UnityEngine.Object.FindObjectOfType<OfflineSignalBayController>();
                if (bay == null) return;
                var credit = bay.transform.Find("Credit Live")?.GetComponent<TMPro.TMP_Text>();
                var milestone = bay.transform.Find("Milestone Number Live")?.GetComponent<TMPro.TMP_Text>();
                var remaining = bay.transform.Find("Remaining Live")?.GetComponent<TMPro.TMP_Text>();
                if (credit == null || milestone == null || remaining == null || credit.text != "2,200"
                    || !milestone.text.Contains(">2</color>") || !remaining.text.StartsWith("98 more pulls"))
                { PointerFailure("bay live counters incorrect: " + (credit != null ? credit.text : "missing") + "; "
                    + (milestone != null ? milestone.text : "missing") + "; " + (remaining != null ? remaining.text : "missing")); return; }
                Debug.Log("[BayPointer] PASS: SIGNAL BAY pointer returned; live credits=2,200, milestone=2/100, remaining=98.");
                SessionState.SetBool(ActiveKey, false);
                Unhook();
                EditorApplication.isPlaying = false;
            }
        }

        static void UpdateBayReturnPointer(float t)
        {
            int step = SessionState.GetInt(StepKey, 0);
            if (t > 20f) { PointerFailure("editable return timeout at step " + step); return; }
            if (step == 0 && t > 1f)
            {
                var bay = UnityEngine.Object.FindObjectOfType<OfflineSignalBayController>();
                var group = bay != null ? bay.transform.Find("Figma Signal Bay")?.GetComponent<CanvasGroup>() : null;
                var button = bay != null ? new SerializedObject(bay).FindProperty("returnButton").objectReferenceValue as Button : null;
                if (group == null) { PointerFailure("editable bay missing"); return; }
                group.alpha = 1f;
                Canvas.ForceUpdateCanvases();
                if (!ClickButton(button)) { PointerFailure("editable binder return pointer target missed"); return; }
                Debug.Log("[BayReturn] PASS: visible editable binder button dispatched pointer events.");
                PointerStep(1);
            }
            else if (step == 1 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Collection")
            {
                Debug.Log("[BayReturn] PASS: editable binder button navigated to Collection.");
                SessionState.SetBool(ActiveKey, false);
                Unhook();
                EditorApplication.isPlaying = false;
            }
        }

        static void CheckFreshResolution(string baselineKey, string action)
        {
            string id = OfflineCollectionData.PackManifest[0].Id;
            int copiesPerPack = 0;
            foreach (var card in OfflineCollectionData.PackManifest)
                if (card.Id == id) copiesPerPack++;
            int delta = OfflineCollectionData.Owned(id) - SessionState.GetInt(baselineKey, -1000);
            if (delta == copiesPerPack)
                Debug.Log("[OfflineGachaVerification] " + action + " resolved fresh: " + id + " ownership +" + delta + ".");
            else
                Debug.LogError("[OfflineGachaVerification] " + action + " did not resolve a fresh pack: " + id
                    + " ownership delta=" + delta + ", expected " + copiesPerPack + ".");
        }

        /// <summary>Collection flow: one settled full-screen still of the binder — grid, filter row
        /// (including the set filter) and the deck header.</summary>
        static void UpdateCollection(float t)
        {
            switch (SessionState.GetInt(StepKey, 0))
            {
                case 0: if (t > 1.2f) { Debug.Log("[OfflineGachaVerification] collection step0 binder t=" + t.ToString("0.00")); Shot("collection-binder.png"); SetStep(1); } break;
                case 1: if (t > 1.8f) { SetStep(2); EditorApplication.isPlaying = false; } break;
            }
        }

        /// <summary>
        /// Frame-exact still: renders the pack camera straight into a readback texture and writes the PNG
        /// before the next frame can move the flip on. Captures the world row only (no overlay HUD),
        /// which is exactly what the animation stills need to show.
        /// </summary>
        static void ShotCamera(string fileName)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Shot(fileName);
                return;
            }

            int width = Mathf.Max(64, cam.pixelWidth);
            int height = Mathf.Max(64, cam.pixelHeight);
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.Combine(OfflineGachaVerification.ScreenshotDir, PreservedBayFlowCapture.CaptureFileName(fileName)), png);
            Debug.Log("[OfflineGachaVerification] Captured " + fileName + " (camera-exact)");
        }

        static void SetStep(int value)
        {
            SessionState.SetInt(StepKey, value);
        }

        static void Shot(string fileName)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(OfflineGachaVerification.ScreenshotDir, PreservedBayFlowCapture.CaptureFileName(fileName)));
            Debug.Log("[OfflineGachaVerification] Captured " + fileName);
        }
    }
}
