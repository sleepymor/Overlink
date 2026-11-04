using System;
using System.Collections.Generic;
using Overlink.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.EditorTools
{
    /// <summary>Snapshot the offline prototype's integer preferences before its destructive capture.</summary>
    [InitializeOnLoad]
    static class PreservedBayFlowCapture
    {
        [Serializable] sealed class Entry { public string key; public bool exists; public int value; }
        [Serializable] sealed class Snapshot { public Entry[] entries; public string capturePrefix; }
        static string BackupKey => "Overlink.BayFlowBackup." + Application.dataPath;

        static PreservedBayFlowCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.HasKey(BackupKey)) Restore();
            };
        }

        [MenuItem("Tools/Offline Gacha/Capture Bay Flow Preserving Player Data")]
        static void Capture()
        {
            CaptureWithBackup(OfflineGachaVerification.CaptureBayToRevealFlow);
        }

        [MenuItem("Tools/Offline Gacha/Verify Ten Pack Pointer Flow Preserving Player Data")]
        static void CaptureTenPointer()
        {
            CaptureWithBackup(OfflineGachaVerification.VerifyTenPackPointerFlow);
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Ten Flow Preserving Player Data")]
        static void CaptureEditableTenPointer()
        {
            CaptureWithBackup(OfflineGachaVerification.VerifyEditableBayTenPointerFlow);
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Return Preserving Player Data")]
        static void CaptureEditableReturnPointer()
        {
            CaptureWithBackup(OfflineGachaVerification.VerifyEditableBayReturnPointerFlow);
        }

        [MenuItem("Tools/Offline Gacha/Verify Single Pack Pointer Flow Preserving Player Data")]
        static void CaptureSinglePointer()
        {
            CaptureWithBackup(OfflineGachaVerification.VerifySinglePackPointerFlow);
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Single Flow Preserving Player Data")]
        static void CaptureEditableSinglePointer()
        {
            CaptureWithBackup(OfflineGachaVerification.VerifyEditableBaySinglePointerFlow);
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Single 16 by 10 Preserving Player Data")]
        static void CaptureEditableSingleSixteenByTen()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, true);
                OfflineGachaVerification.VerifyEditableBaySinglePointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Single 4 by 3 Preserving Player Data")]
        static void CaptureEditableSingleFourByThree()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, true);
                OfflineGachaVerification.VerifyEditableBaySinglePointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Ten 4 by 3 Preserving Player Data")]
        static void CaptureEditableTenFourByThree()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, true);
                OfflineGachaVerification.VerifyEditableBayTenPointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Offline Gacha/Verify Editable Bay Return 16 by 10 Preserving Player Data")]
        static void CaptureEditableReturnSixteenByTen()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, true);
                OfflineGachaVerification.VerifyEditableBayReturnPointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Offline Gacha/Verify Single Loyalty Reveal Preserving Player Data")]
        static void CaptureSingleLoyalty()
        {
            CaptureWithBackup(OfflineGachaVerification.VerifySingleLoyaltyReveal);
        }

        static void CaptureWithBackup(Action start)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.HasKey(BackupKey))
            {
                Debug.LogError("[PreservedBayFlow] Editor busy or a prior backup needs recovery; no data changed.");
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[PreservedBayFlow] Save open scene changes before capture; no data changed.");
                    return;
                }
            }
            var keys = new List<string>
            {
                OfflineCollectionData.ResolvedKey, OfflineCollectionData.RevealedKey,
                OfflineCollectionData.FragmentsKey, OfflineCollectionData.BundleResolvedKey,
                OfflineCollectionData.BundleRevealedKey, OfflineCollectionData.BundleLoyaltyPackKey,
                OfflineCollectionData.SingleLoyaltyPackKey,
                OfflineCollectionData.PurchasedPacksKey, OfflineCollectionData.LoyaltyGrantedKey,
                OfflinePackOpeningController.ModeKey, OfflineCollectionData.SelectedPackKey
            };
            foreach (var card in OfflineCollectionData.Catalog)
            {
                keys.Add(OfflineCollectionData.OwnedKey + card.Id);
                keys.Add(OfflineCollectionData.DeckKey + card.Id);
            }
            var entries = new List<Entry>();
            foreach (string key in keys)
                entries.Add(new Entry { key = key, exists = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key) });
            var snapshot = new Snapshot { entries = entries.ToArray(), capturePrefix = "preserved-" + Guid.NewGuid().ToString("N") + "-" };
            EditorPrefs.SetString(BackupKey, JsonUtility.ToJson(snapshot));
            try
            {
                start();
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore();
            }
            catch (Exception)
            {
                Restore();
                throw;
            }
        }

        internal static string CaptureFileName(string fileName)
        {
            if (!EditorPrefs.HasKey(BackupKey)) return fileName;
            var snapshot = JsonUtility.FromJson<Snapshot>(EditorPrefs.GetString(BackupKey));
            return snapshot.capturePrefix + fileName;
        }

        [MenuItem("Tools/Offline Gacha/Recover Preserved Bay Flow Data")]
        static void Restore()
        {
            if (!EditorPrefs.HasKey(BackupKey)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PreservedBayFlow] Stop Play mode before recovering the backup.");
                return;
            }
            var snapshot = JsonUtility.FromJson<Snapshot>(EditorPrefs.GetString(BackupKey));
            foreach (var entry in snapshot.entries)
            {
                if (entry.exists) PlayerPrefs.SetInt(entry.key, entry.value);
                else PlayerPrefs.DeleteKey(entry.key);
            }
            PlayerPrefs.Save();
            foreach (var entry in snapshot.entries)
                if (PlayerPrefs.HasKey(entry.key) != entry.exists ||
                    (entry.exists && PlayerPrefs.GetInt(entry.key) != entry.value))
                {
                    Debug.LogError("[PreservedBayFlow] Restore verification failed; backup retained.");
                    return;
                }
            EditorPrefs.DeleteKey(BackupKey);
            Debug.Log("[PreservedBayFlow] Restored and verified " + snapshot.entries.Length + " preference keys, including absent keys.");
        }
    }
}
