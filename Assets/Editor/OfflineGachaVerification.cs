using System.Collections.Generic;
using System.IO;
using Overlink.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.EditorTools
{
    /// <summary>
    /// One-click verification for the offline Gacha screens. Tools:
    ///   • Verify Scene Bindings — edit-mode audit that every serialized controller reference in both
    ///     <c>Gacha.unity</c> and <c>Collection.unity</c> is wired.
    ///   • Reset Pack State — clears the persisted reveal so the next play-through starts face down.
    ///   • Assert Ten-Pack Rules — edit-mode audit of the Milestone 3 data layer: fifty cards across ten
    ///     packs, the Epic-or-better guarantee, the bundle duplicate flag, and the §5 loyalty threshold
    ///     crossing (96 purchased + 10 = 106 grants exactly one bonus pack).
    ///   • Capture Reveal Sequence / Capture Ten-Pack Sequence / Capture Collection Screen — enter play,
    ///     then record scripted stills to <c>Assets/Screenshots</c> so each flow can be reviewed without
    ///     hand-driving the Editor.
    ///
    /// These are Editor conveniences around the same runtime the player uses; none of them change the
    /// shipped behaviour of the controllers.
    /// </summary>
    public static class OfflineGachaVerification
    {
        const string GachaScenePath = "Assets/Scenes/Gacha.unity";
        const string CollectionScenePath = "Assets/Scenes/Collection.unity";
        internal const string ScreenshotDir = "Assets/Screenshots";

        static readonly string[] ControllerRefs =
        {
            "progressText", "collectionText", "statusText", "instructionText",
            "templateCatalog", "frameCatalog", "tierMaterials",
            "revealNextButton", "revealRemainingButton", "resetButton",
            "backButton", "collectionButton", "reducedMotionToggle",
            "singlePackButton", "tenPackButton", "packHeader", "queueRoot", "capsuleTemplate",
            "summaryButton", "summaryPanel", "summaryCloseButton", "summaryContent",
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
            int missing = AuditGachaScene();
            missing += AuditCollectionScene();
            if (missing == 0)
            {
                Debug.Log("[OfflineGachaVerification] PASS — Gacha + Collection bindings fully wired.");
            }
            else
            {
                Debug.LogError("[OfflineGachaVerification] FAIL — " + missing + " unbound reference(s).");
            }
        }

        static int AuditGachaScene()
        {
            OfflinePackOpeningController controller = UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>();
            if (controller == null)
            {
                EnsureSceneOpen(GachaScenePath);
                controller = UnityEngine.Object.FindObjectOfType<OfflinePackOpeningController>();
            }

            if (controller == null)
            {
                Debug.LogError("[OfflineGachaVerification] No OfflinePackOpeningController found in " + GachaScenePath);
                return 1;
            }

            SerializedObject so = new SerializedObject(controller);
            int missing = AuditRefs(controller, ControllerRefs, "Gacha controller");

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
                Debug.Log("[OfflineGachaVerification] Gacha PASS — "
                    + (slots != null ? slots.arraySize : 0) + " slots, catalogs, "
                    + (fixtures != null ? fixtures.arraySize : 0) + " rarity fixtures, "
                    + ControllerRefs.Length + " widget refs; cardWidth="
                    + so.FindProperty("cardWidth").floatValue.ToString("0.###") + ".");
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
            EnsureSceneOpen(GachaScenePath);
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
            EnsureSceneOpen(GachaScenePath);
            StartCapture(CaptureDriver.FlowTenPack, "ten-pack queue → pack 1 reveals → bundle summary");
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

        internal const int FlowSingle = 0;
        internal const int FlowTenPack = 1;
        internal const int FlowCollection = 2;

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
                case 5: if (!driven.IsRevealInProgress && driven.RevealedCount >= 5) { Debug.Log("[OfflineGachaVerification] step5 allfaces t=" + t.ToString("0.00")); Shot("gacha-anim-4-allfaces.png"); SetStep(6); } break;
                case 6: if (t > 0.4f) { SetStep(7); EditorApplication.isPlaying = false; } break;
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
            File.WriteAllBytes(Path.Combine(OfflineGachaVerification.ScreenshotDir, fileName), png);
            Debug.Log("[OfflineGachaVerification] Captured " + fileName + " (camera-exact)");
        }

        static void SetStep(int value)
        {
            SessionState.SetInt(StepKey, value);
        }

        static void Shot(string fileName)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(OfflineGachaVerification.ScreenshotDir, fileName));
            Debug.Log("[OfflineGachaVerification] Captured " + fileName);
        }
    }
}
