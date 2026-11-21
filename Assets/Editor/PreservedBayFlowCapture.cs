using System;
using System.Collections.Generic;
using Overlink.Persistence;
using Overlink.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.EditorTools
{
    /// <summary>Snapshot the Gacha flow's saved preferences before its destructive capture.</summary>
    [InitializeOnLoad]
    static class PreservedBayFlowCapture
    {
        [Serializable] sealed class Entry { public string key; public bool exists; public int value; }
        [Serializable] sealed class Snapshot { public Entry[] entries; public DeckSnapshot decks; public string capturePrefix; }

        /// <summary>
        /// Saved decks are string-valued and keyed by generated ids, so they cannot ride in the integer
        /// <see cref="Entry"/> list. The library is flattened here: the id order, then one name and one
        /// payload per id, plus the counter and the one-shot migration flags.
        /// </summary>
        [Serializable] sealed class DeckSnapshot
        {
            public string index;
            public string active;
            public string legacy;
            public int sequence;
            public int savedDeckMigrated;
            public int countsMigrated;
            public string[] names;
            public string[] payloads;
        }
        static string BackupKey => "Overlink.BayFlowBackup." + Application.dataPath;

        static PreservedBayFlowCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.HasKey(BackupKey)) Restore();
            };
        }

        [MenuItem("Tools/Gacha/Capture Bay Flow Preserving Player Data")]
        static void Capture()
        {
            CaptureWithBackup(GachaVerification.CaptureBayToRevealFlow);
        }

        [MenuItem("Tools/Gacha/Verify Ten Pack Pointer Flow Preserving Player Data")]
        static void CaptureTenPointer()
        {
            CaptureWithBackup(GachaVerification.VerifyTenPackPointerFlow);
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Ten Flow Preserving Player Data")]
        static void CaptureEditableTenPointer()
        {
            CaptureWithBackup(GachaVerification.VerifyEditableBayTenPointerFlow);
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Return Preserving Player Data")]
        static void CaptureEditableReturnPointer()
        {
            CaptureWithBackup(GachaVerification.VerifyEditableBayReturnPointerFlow);
        }

        [MenuItem("Tools/Gacha/Verify Single Pack Pointer Flow Preserving Player Data")]
        static void CaptureSinglePointer()
        {
            CaptureWithBackup(GachaVerification.VerifySinglePackPointerFlow);
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Single Flow Preserving Player Data")]
        static void CaptureEditableSinglePointer()
        {
            CaptureWithBackup(GachaVerification.VerifyEditableBaySinglePointerFlow);
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Single 16 by 10 Preserving Player Data")]
        static void CaptureEditableSingleSixteenByTen()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, true);
                GachaVerification.VerifyEditableBaySinglePointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Single 4 by 3 Preserving Player Data")]
        static void CaptureEditableSingleFourByThree()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, true);
                GachaVerification.VerifyEditableBaySinglePointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Ten 4 by 3 Preserving Player Data")]
        static void CaptureEditableTenFourByThree()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, true);
                GachaVerification.VerifyEditableBayTenPointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewFourOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Gacha/Verify Editable Bay Return 16 by 10 Preserving Player Data")]
        static void CaptureEditableReturnSixteenByTen()
        {
            CaptureWithBackup(() =>
            {
                SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, true);
                GachaVerification.VerifyEditableBayReturnPointerFlow();
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    SessionState.SetBool(SignalBayAspectPreview.PreviewOnPlayKey, false);
            });
        }

        [MenuItem("Tools/Gacha/Verify Single Loyalty Reveal Preserving Player Data")]
        static void CaptureSingleLoyalty()
        {
            CaptureWithBackup(GachaVerification.VerifySingleLoyaltyReveal);
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
                CollectionData.ResolvedKey, CollectionData.RevealedKey,
                CollectionData.FragmentsKey, CollectionData.BundleResolvedKey,
                CollectionData.BundleRevealedKey, CollectionData.BundleLoyaltyPackKey,
                CollectionData.SingleLoyaltyPackKey,
                CollectionData.PurchasedPacksKey, CollectionData.LoyaltyGrantedKey,
                PackOpeningController.ModeKey, CollectionData.SingleDrawKey,
                CollectionData.BundleDrawKey, CollectionData.LoyaltyDrawKey
            };
            foreach (var card in CollectionData.BinderCards())
            {
                keys.Add(CollectionData.OwnedKey + card.Id);
                keys.Add(CollectionData.DeckKey + card.Id);
            }
            var entries = new List<Entry>();
            foreach (string key in keys)
                entries.Add(new Entry { key = key, exists = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key) });
            var snapshot = new Snapshot { entries = entries.ToArray(), decks = CaptureDecks(), capturePrefix = "preserved-" + Guid.NewGuid().ToString("N") + "-" };
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

        [MenuItem("Tools/Gacha/Recover Preserved Bay Flow Data")]
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
            if (!RestoreDecks(snapshot.decks))
            {
                Debug.LogError("[PreservedBayFlow] Deck restore verification failed; backup retained.");
                return;
            }
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

        // ── Saved decks ─────────────────────────────────────────────────────────────────────

        static DeckSnapshot CaptureDecks()
        {
            List<string> ids = DeckIds();
            var names = new string[ids.Count];
            var payloads = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                names[i] = PlayerPrefs.GetString(DeckLibrary.NameKeyPrefix + ids[i], string.Empty);
                payloads[i] = PlayerPrefs.GetString(DeckLibrary.DataKeyPrefix + ids[i], string.Empty);
            }

            return new DeckSnapshot
            {
                index = PlayerPrefs.GetString(DeckLibrary.IndexKey, string.Empty),
                active = PlayerPrefs.GetString(DeckLibrary.ActiveKey, string.Empty),
                legacy = PlayerPrefs.GetString(DeckLibrary.LegacyDeckKey, string.Empty),
                sequence = PlayerPrefs.GetInt(DeckLibrary.SequenceKey, 0),
                savedDeckMigrated = PlayerPrefs.GetInt(DeckLibrary.SavedDeckMigratedKey, 0),
                countsMigrated = PlayerPrefs.GetInt(DeckLibrary.CountsMigratedKey, 0),
                names = names,
                payloads = payloads
            };
        }

        static bool RestoreDecks(DeckSnapshot snapshot)
        {
            if (snapshot == null) return true;

            List<string> saved = SplitIndex(snapshot.index);

            // Decks created during the capture are not in the snapshot; drop them so the library is
            // exactly what it was, including its active pointer.
            List<string> current = DeckIds();
            for (int i = 0; i < current.Count; i++)
            {
                if (saved.Contains(current[i])) continue;
                PlayerPrefs.DeleteKey(DeckLibrary.NameKeyPrefix + current[i]);
                PlayerPrefs.DeleteKey(DeckLibrary.DataKeyPrefix + current[i]);
            }

            for (int i = 0; i < saved.Count; i++)
            {
                PlayerPrefs.SetString(DeckLibrary.NameKeyPrefix + saved[i], snapshot.names[i]);
                PlayerPrefs.SetString(DeckLibrary.DataKeyPrefix + saved[i], snapshot.payloads[i]);
            }

            WriteOrClear(DeckLibrary.IndexKey, snapshot.index);
            WriteOrClear(DeckLibrary.ActiveKey, snapshot.active);
            WriteOrClear(DeckLibrary.LegacyDeckKey, snapshot.legacy);
            PlayerPrefs.SetInt(DeckLibrary.SequenceKey, snapshot.sequence);
            PlayerPrefs.SetInt(DeckLibrary.SavedDeckMigratedKey, snapshot.savedDeckMigrated);
            PlayerPrefs.SetInt(DeckLibrary.CountsMigratedKey, snapshot.countsMigrated);
            PlayerPrefs.Save();

            if (PlayerPrefs.GetString(DeckLibrary.IndexKey, string.Empty) != snapshot.index) return false;
            for (int i = 0; i < saved.Count; i++)
            {
                if (PlayerPrefs.GetString(DeckLibrary.NameKeyPrefix + saved[i], string.Empty) != snapshot.names[i]) return false;
                if (PlayerPrefs.GetString(DeckLibrary.DataKeyPrefix + saved[i], string.Empty) != snapshot.payloads[i]) return false;
            }

            return true;
        }

        static void WriteOrClear(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
        }

        static List<string> DeckIds()
        {
            return SplitIndex(PlayerPrefs.GetString(DeckLibrary.IndexKey, string.Empty));
        }

        static List<string> SplitIndex(string raw)
        {
            var ids = new List<string>();
            if (string.IsNullOrEmpty(raw)) return ids;
            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (id.Length > 0 && !ids.Contains(id)) ids.Add(id);
            }

            return ids;
        }
    }
}
