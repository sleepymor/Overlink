using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Overlink.Match;
using Overlink.UI;

namespace Overlink.EditorTools
{
    /// <summary>Editor-only gate proving that foundational player UI is serialized and not screenshot-backed.</summary>
    public static class SerializedUiIntegrityAudit
    {
        static readonly string[] ScreenScenes =
        {
            "Assets/Scenes/Auth.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Gacha.unity",
            "Assets/Scenes/RevealGacha.unity",
            "Assets/Scenes/Collection.unity",
            "Assets/Scenes/Gameplay.unity",
            "Assets/Scenes/Settings.unity"
        };

        [MenuItem("Overlink/UI/Audit Serialized UI Integrity")]
        public static void Audit()
        {
            int errors = 0;
            foreach (string path in ScreenScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogError("[SerializedUIAudit] Missing target scene: " + path);
                    errors++;
                    continue;
                }
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                errors += AuditScene(scene, path);
            }

            errors += AuditKnownRuntimeBuilders();
            errors += AuditReferenceImagesAreNotActiveComposition();
            if (errors == 0) Debug.Log("[SerializedUIAudit] PASS: required UI foundations are serialized and no active full-screen base raster was found.");
            else Debug.LogError("[SerializedUIAudit] FAIL: " + errors + " issue(s) found. Runtime fallback construction and raster composition remain blocked.");
        }

        public static void BatchAudit()
        {
            Audit();
        }

        static int AuditScene(Scene scene, string path)
        {
            int errors = 0;
            string screen = Path.GetFileNameWithoutExtension(path);
            Canvas canvas = FindCanvas(scene);
            if (canvas == null)
            {
                Debug.LogError("[SerializedUIAudit] " + screen + ": no Canvas.");
                return 1;
            }

            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(image.sprite);
                string file = Path.GetFileName(assetPath);
                if (file.Equals("MainMenuBase.png", StringComparison.OrdinalIgnoreCase)
                    || file.Equals("SignalBayBase.png", StringComparison.OrdinalIgnoreCase))
                {
                    bool active = image.gameObject.activeInHierarchy && image.color.a > 0.001f;
                    if (active)
                    {
                        Debug.LogError("[SerializedUIAudit] " + screen + ": active full-screen raster on " + image.transform.GetHierarchyPath());
                        errors++;
                    }
                }
            }

            if (screen == "Auth")
            {
                errors += Require(canvas.transform, "Card/Loading Panel/Status", typeof(TMPro.TextMeshProUGUI));
                errors += Require(canvas.transform, "Card/Sign-In Panel/Username Input", typeof(TMPro.TMP_InputField));
                errors += Require(canvas.transform, "Card/Sign-In Panel/Password Input", typeof(TMPro.TMP_InputField));
                errors += Require(canvas.transform, "Card/Sign-In Panel/Submit Button", typeof(Button));
                errors += Require(canvas.transform, "Card/Sign-In Panel/Create Account Button", typeof(Button));
                errors += Require(canvas.transform, "Card/Setup Panel/Age Step/Age Input", typeof(TMPro.TMP_InputField));
                errors += Require(canvas.transform, "Card/Setup Panel/Age Step/Continue Button", typeof(Button));
                errors += Require(canvas.transform, "Card/Setup Panel/Identity Step/Username Input", typeof(TMPro.TMP_InputField));
                errors += Require(canvas.transform, "Card/Setup Panel/Identity Step/Tag Input", typeof(TMPro.TMP_InputField));
                errors += Require(canvas.transform, "Card/Setup Panel/Identity Step/Email Input", typeof(TMPro.TMP_InputField));
                errors += Require(canvas.transform, "Card/Setup Panel/Identity Step/Save Button", typeof(Button));
            }
            else if (screen == "MainMenu")
            {
                errors += Require(canvas.transform, "Profile Panel", typeof(RectTransform));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Username Row/Value", typeof(TMPro.TextMeshProUGUI));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Tag Row/Value", typeof(TMPro.TextMeshProUGUI));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Full ID Row/Value", typeof(TMPro.TextMeshProUGUI));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Username Row/Copy Button", typeof(Button));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Tag Row/Copy Button", typeof(Button));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Full ID Row/Copy Button", typeof(Button));
                errors += Require(canvas.transform, "Profile Panel/Card/Content/Close Button", typeof(Button));
                errors += Require(canvas.transform, "Title and commands/Play commands/Matchmaking command", typeof(Button));
                errors += Require(canvas.transform, "Title and commands/Play commands/VS AI command", typeof(Button));
                errors += Require(canvas.transform, "Title and commands/Play commands/Deckbuilder command", typeof(Button));
                errors += Require(canvas.transform, "Title and commands/Play commands/Gacha command", typeof(Button));
                errors += Require(canvas.transform, "Title and commands/System commands/Settings command", typeof(Button));
                errors += Require(canvas.transform, "Title and commands/System commands/Quit command", typeof(Button));
            }
            else if (screen == "Settings")
            {
                errors += Require(canvas.transform, "SettingsPanel", typeof(RectTransform));
                errors += Require(canvas.transform, "SettingsPanel/PostFxToggle", typeof(Toggle));
                errors += Require(canvas.transform, "SettingsPanel/ReduceShakeToggle", typeof(Toggle));
                errors += Require(canvas.transform, "SettingsPanel/ReduceFlashToggle", typeof(Toggle));
                errors += Require(canvas.transform, "SettingsPanel/ReducedMotionToggle", typeof(Toggle));
            }
            else if (screen == "Gacha")
            {
                Transform bay = FindSceneTransform(scene, "Figma Signal Bay");
                if (bay == null) { Debug.LogError("[SerializedUIAudit] Gacha: Figma Signal Bay missing."); errors++; }
                else
                    for (int i = 1; i <= 3; i++) errors += Require(bay, "Pack Choice " + i, typeof(Button));
            }
            else if (screen == "Gameplay")
            {
                errors += AuditGameplayHud(scene);
            }

            Debug.Log("[SerializedUIAudit] " + screen + ": " + (errors == 0 ? "PASS" : "FAIL") + ".");
            return errors;
        }

        static int AuditKnownRuntimeBuilders()
        {
            int errors = 0;
            string[] forbidden =
            {
                "MainMenuController.Account.cs:BuildProfileDialog",
                "SettingsController.cs:CreateVisualToggle",
                "OfflineSignalBayController.cs:AddComponent<Button>"
            };
            string[] files =
            {
                "Assets/Scripts/UI/MainMenuController.Account.cs",
                "Assets/Scripts/UI/SettingsController.cs",
                "Assets/Scripts/UI/OfflineSignalBayController.cs"
            };
            for (int i = 0; i < files.Length; i++)
            {
                if (!File.Exists(files[i])) continue;
                string text = File.ReadAllText(files[i]);
                string token = forbidden[i].Substring(forbidden[i].IndexOf(':') + 1);
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    Debug.LogError("[SerializedUIAudit] Forbidden runtime foundation builder remains: " + forbidden[i]);
                    errors++;
                }
            }

            string[] hudCallers =
            {
                "Assets/Scripts/Match/GameCoordinator.cs",
                "Assets/Scripts/Match/BattlefieldPresenter.cs"
            };
            for (int i = 0; i < hudCallers.Length; i++)
            {
                if (!File.Exists(hudCallers[i])) continue;
                string text = File.ReadAllText(hudCallers[i]);
                if (text.IndexOf("BattlefieldHud.Ensure()", StringComparison.Ordinal) >= 0)
                {
                    Debug.LogError("[SerializedUIAudit] Gameplay runtime may not call BattlefieldHud.Ensure(): " + hudCallers[i]);
                    errors++;
                }
            }
            return errors;
        }

        static int AuditGameplayHud(Scene scene)
        {
            int errors = 0;
            Transform hud = FindSceneTransform(scene, BattlefieldHud.RootName);
            if (hud == null)
            {
                Debug.LogError("[SerializedUIAudit] Gameplay: missing " + BattlefieldHud.RootName + " root.");
                return 1;
            }

            errors += Require(hud, "PhaseControls", typeof(RectTransform));
            string[] rail =
            {
                "EndPhaseButton", "DoneBlockingButton", "SwingCoreButton", "SwingNode1Button",
                "SwingNode2Button", "SwingNode3Button", "SwingNode4Button", "ActivateButton",
                "EndTurnButton", "ConcedeButton", "StateButton", "PilePrevButton", "PileNextButton",
                "RerollLineupButton", "ConfirmLineupButton"
            };
            for (int i = 0; i < rail.Length; i++)
            {
                errors += Require(hud, "PhaseControls/" + rail[i], typeof(Button));
                errors += Require(hud, "PhaseControls/" + rail[i] + "/Label", typeof(TextMeshProUGUI));
            }

            errors += Require(hud, "Banner", typeof(RectTransform));
            errors += Require(hud, "Banner/BannerText", typeof(TextMeshProUGUI));
            errors += Require(hud, "Banner/TagLeft", typeof(TextMeshProUGUI));
            errors += Require(hud, "Banner/TagRight", typeof(TextMeshProUGUI));
            errors += Require(hud, "ControlsHint", typeof(RectTransform));
            errors += Require(hud, "ControlsHint/ControlsHintText", typeof(TextMeshProUGUI));
            errors += Require(hud, "OpponentChip", typeof(RectTransform));
            errors += Require(hud, "OpponentChip/OpponentChipText", typeof(TextMeshProUGUI));
            errors += Require(hud, "OpponentChip/Body", typeof(TextMeshProUGUI));
            errors += Require(hud, "OpponentChip/CoreMeter", typeof(UiBar));
            errors += Require(hud, "OpponentChip/NpMeter", typeof(UiBar));
            errors += Require(hud, "PlayerChip", typeof(RectTransform));
            errors += Require(hud, "PlayerChip/PlayerChipText", typeof(TextMeshProUGUI));
            errors += Require(hud, "PlayerChip/Body", typeof(TextMeshProUGUI));
            errors += Require(hud, "PlayerChip/CoreMeter", typeof(UiBar));
            errors += Require(hud, "PlayerChip/NpMeter", typeof(UiBar));
            errors += Require(hud, "NodeList", typeof(RectTransform));
            for (int i = 0; i < 4; i++) errors += Require(hud, "NodeList/NodeRow" + i, typeof(TextMeshProUGUI));
            errors += Require(hud, "Notice", typeof(RectTransform));
            errors += Require(hud, "Notice/NoticeText", typeof(TextMeshProUGUI));
            errors += Require(hud, "Result", typeof(RectTransform));
            errors += Require(hud, "Result/ResultText", typeof(TextMeshProUGUI));
            errors += Require(hud, "LogPanel", typeof(RectTransform));
            errors += Require(hud, "LogPanel/LogHeader", typeof(TextMeshProUGUI));
            errors += Require(hud, "LogPanel/LogText", typeof(TextMeshProUGUI));
            errors += Require(hud, "ReaderPanel", typeof(RectTransform));
            errors += Require(hud, "ReaderPanel/CardFace", typeof(RectTransform));
            errors += Require(hud, "ReaderPanel/CardFace/ReaderTitle", typeof(TextMeshProUGUI));
            errors += Require(hud, "ReaderPanel/CardFace/ReaderBody", typeof(TextMeshProUGUI));
            errors += Require(hud, "ReaderPanel/CardFace/ReaderFoot", typeof(TextMeshProUGUI));
            errors += Require(hud, "ReaderPanel/CardFace/ReaderHint", typeof(TextMeshProUGUI));
            errors += Require(hud, "ReaderPanel/InspectionDetails/Text", typeof(TextMeshProUGUI));

            bool eventSystemFound = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<EventSystem>(true) != null)
                {
                    eventSystemFound = true;
                    break;
                }
            }
            if (!eventSystemFound)
            {
                Debug.LogError("[SerializedUIAudit] Gameplay: no serialized EventSystem.");
                errors++;
            }

            BattlefieldHud component = hud.GetComponent<BattlefieldHud>();
            if (component == null)
            {
                Debug.LogError("[SerializedUIAudit] Gameplay: Battlefield HUD root has no BattlefieldHud component.");
                errors++;
            }

            return errors;
        }

        static int AuditReferenceImagesAreNotActiveComposition()
        {
            int errors = 0;
            foreach (string path in ScreenScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                string text = File.ReadAllText(path);
                if (text.IndexOf("guid: 6f1a2b3c4d5e4f60a7b8c9d0e1f2a3b4", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogError("[SerializedUIAudit] " + path + ": MainMenuBase.png is referenced; remove it from the active composition.");
                    errors++;
                }
                if (text.IndexOf("guid: 1669d02134423594ba166e9d21dd3e70", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogError("[SerializedUIAudit] " + path + ": SignalBayBase.png is referenced; remove it from the active composition.");
                    errors++;
                }
            }
            return errors;
        }

        static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null) return canvas;
            }
            return null;
        }

        static int Require(Transform root, string path, Type type)
        {
            Transform node = FindTransform(root, path);
            if (node == null || node.GetComponent(type) == null)
            {
                Debug.LogError("[SerializedUIAudit] Missing " + type.Name + " at " + root.GetHierarchyPath() + "/" + path);
                return 1;
            }
            return 0;
        }

        static Transform FindTransform(Transform root, string path)
        {
            return root.Find(path);
        }

        static Transform FindSceneTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindSceneTransform(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindSceneTransform(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindSceneTransform(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }

    static class SerializedUiAuditExtensions
    {
        public static string GetHierarchyPath(this Transform transform)
        {
            var parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
