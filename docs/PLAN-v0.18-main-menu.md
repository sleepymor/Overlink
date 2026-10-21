# Overlink — v0.18 Main Menu Implementation Plan

**Scope:** A working main menu scene that connects to all other scenes. Destination
scenes do not need to be complete — placeholder stubs are acceptable. The menu must
compile and navigate in the Unity editor with no missing references.

**OpenCode instructions:** Work strictly within the file allowlist in §6. Do not touch
`ProjectSettings/**` directly — add scenes through the `SceneRegistrar` editor script
described in §4. Commit after each milestone passes its exit condition.

---

## 0. Constraints (read before touching anything)

| Rule | Detail |
|---|---|
| No new packages | Use only what is already in `Packages/manifest.json`: TMP 3.0.7, uGUI 1.0.0, URP 14.0.12 |
| Assembly boundary | Create exactly one new approved assembly, `Overlink.UI`, for menu code at `Assets/Scripts/UI/`. No other new assemblies are permitted. |
| `ProjectSettings/**` protected | Use the editor helper in §4 to register scenes, not direct YAML edits |
| Placeholder scenes are stubs | A stub is a minimal scene with a Canvas, a scene-name label, a Back button, and `PlaceholderSceneController`. No gameplay or network logic is required. |
| No net/auth dependency in the menu | The menu must load and run without Unity Gaming Services initialised |
| Naming convention | Scene files: `PascalCase.unity`. Script files: `PascalCase.cs`. No spaces |

---

## 1. Scene inventory after this plan

| Index | Scene file | Status after v0.18 | Enabled in build |
|---|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | **New — fully implemented** | ✅ |
| 1 | `Assets/Scenes/Gameplay.unity` | Exists, complete | ✅ |
| 2 | `Assets/Scenes/Deckbuilder.unity` | **New — placeholder stub** | ✅ |
| 3 | `Assets/Scenes/Collection.unity` | **New — placeholder stub** | ✅ |
| 4 | `Assets/Scenes/Settings.unity` | **New — placeholder stub** | ✅ |
| 5 | `Assets/Scenes/NetPresenceDemo.unity` | Exists, keep enabled | ✅ |
| — | `Assets/Scenes/SampleScene.unity` | Exists, keep disabled | ❌ |

---

## 2. MainMenu scene hierarchy

```
MainMenu (scene root)
└── UI Root [GameObject]
    ├── EventSystem [EventSystem + StandaloneInputModule]
    ├── Main Camera [Camera, AudioListener, URP UniversalAdditionalCameraData]
    └── Canvas [Canvas (Screen Space – Overlay), CanvasScaler (1920×1080 reference),
               GraphicRaycaster]
        ├── Background [Image — solid dark colour #0D1117]
        ├── Logo [Image or TMP_Text — "OVERLINK" — centred, top third]
        ├── ButtonPanel [VerticalLayoutGroup, centred]
        │   ├── Btn_Play        [Button + TMP_Text "Play"]
        │   ├── Btn_Deckbuilder [Button + TMP_Text "Deckbuilder"]
        │   ├── Btn_Collection  [Button + TMP_Text "Collection"]
        │   ├── Btn_Settings    [Button + TMP_Text "Settings"]
        │   ├── Btn_NetDemo     [Button + TMP_Text "Network Demo"]
        │   └── Btn_Quit        [Button + TMP_Text "Quit"]
        └── VersionLabel [TMP_Text — bottom-right, tiny, reads "v0.18"]
```

---

## 3. Scripts — `Assets/Scripts/UI/`

### 3.1 `SceneIndex.cs`
Centralized scene-name constants. Navigation must use scene names, not build indices, so it remains safe before and after build-settings registration.

```csharp
// Assets/Scripts/UI/SceneIndex.cs
namespace Overlink.UI
{
    public static class SceneIndex
    {
        public const string MainMenu    = "MainMenu";
        public const string Gameplay    = "Gameplay";
        public const string Deckbuilder = "Deckbuilder";
        public const string Collection  = "Collection";
        public const string Settings    = "Settings";
        public const string NetDemo     = "NetPresenceDemo";
    }
}
```

> **Note for OpenCode:** these constants are scene names, not build indices. They must match the
> actual `.unity` file names. Build-settings order is still verified separately by `SceneRegistrar`.

---

### 3.2 `SceneLoader.cs`
Thin wrapper around `SceneManager.LoadScene`. Provides a static `Load(string)` so buttons
can call it without coupling to a MonoBehaviour instance.

```csharp
// Assets/Scripts/UI/SceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overlink.UI
{
    public static class SceneLoader
    {
        public static void Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not enabled or registered in Build Settings.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        public static void LoadMainMenu() => Load(SceneIndex.MainMenu);
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
```

---

### 3.3 `MainMenuController.cs`
MonoBehaviour that wires button `onClick` events in `Awake` and exposes named methods
so the inspector can also wire them directly.

```csharp
// Assets/Scripts/UI/MainMenuController.cs
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] Button btnPlay;
        [SerializeField] Button btnDeckbuilder;
        [SerializeField] Button btnCollection;
        [SerializeField] Button btnSettings;
        [SerializeField] Button btnNetDemo;
        [SerializeField] Button btnQuit;

        void Awake()
        {
            AddListener(btnPlay,        () => SceneLoader.Load(SceneIndex.Gameplay));
            AddListener(btnDeckbuilder, () => SceneLoader.Load(SceneIndex.Deckbuilder));
            AddListener(btnCollection,  () => SceneLoader.Load(SceneIndex.Collection));
            AddListener(btnSettings,    () => SceneLoader.Load(SceneIndex.Settings));
            AddListener(btnNetDemo,     () => SceneLoader.Load(SceneIndex.NetDemo));
            AddListener(btnQuit,        SceneLoader.Quit);
        }

        void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(MainMenuController)}] A menu button is not wired.", this);
                return;
            }

            button.onClick.AddListener(action);
        }

        void OnValidate()
        {
            ValidateButton(btnPlay, nameof(btnPlay));
            ValidateButton(btnDeckbuilder, nameof(btnDeckbuilder));
            ValidateButton(btnCollection, nameof(btnCollection));
            ValidateButton(btnSettings, nameof(btnSettings));
            ValidateButton(btnNetDemo, nameof(btnNetDemo));
            ValidateButton(btnQuit, nameof(btnQuit));
        }

        void ValidateButton(Button button, string fieldName)
        {
            if (button == null)
                Debug.LogError($"[{nameof(MainMenuController)}] Assign {fieldName} in the Inspector.", this);
        }
    }
}
```

Attach to: **UI Root** GameObject in MainMenu.unity.
Wire: drag each button into the matching serialized field in the Inspector.

---

### 3.4 `PlaceholderSceneController.cs`
Minimal MonoBehaviour placed in every stub scene. Shows its own scene name and a
Back-to-Menu button.

```csharp
// Assets/Scripts/UI/PlaceholderSceneController.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlink.UI
{
    [DisallowMultipleComponent]
    public sealed class PlaceholderSceneController : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Button   btnBack;

        void Awake()
        {
            if (label == null || btnBack == null)
            {
                Debug.LogError($"[{nameof(PlaceholderSceneController)}] Label and Back button must be wired.", this);
                return;
            }

            label.text = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().name + "\n<size=60%>(placeholder)</size>";
            btnBack.onClick.AddListener(SceneLoader.LoadMainMenu);
        }

        void OnValidate()
        {
            if (label == null)
                Debug.LogError($"[{nameof(PlaceholderSceneController)}] Assign label in the Inspector.", this);
            if (btnBack == null)
                Debug.LogError($"[{nameof(PlaceholderSceneController)}] Assign btnBack in the Inspector.", this);
        }
    }
}
```

---

### 3.5 `Overlink.UI.asmdef`
Place at `Assets/Scripts/UI/Overlink.UI.asmdef`.

```json
{
    "name": "Overlink.UI",
    "rootNamespace": "Overlink.UI",
    "references": [
        "Unity.TextMeshPro",
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> **Note:** `autoReferenced: true` controls whether predefined Unity assemblies automatically
> reference `Overlink.UI`; it does not control Inspector visibility and does not make
> arbitrary custom assemblies reference it automatically. The menu scripts are visible
> because they compile as MonoBehaviours. Do not add a reference
> from `Overlink.UI` back to `Overlink.Match`, `Overlink.Core`, `Overlink.Cards`, or
> `Overlink.Net` — that would violate the dependency boundary.

---

## 4. Editor helper — `SceneRegistrar.cs`

Registers all scenes in the correct order in `EditorBuildSettings` from one explicit,
validated editor menu command. OpenCode must invoke this command after all scene assets
exist instead of editing `ProjectSettings/` YAML directly.

```csharp
// Assets/Editor/SceneRegistrar.cs
using System.Collections.Generic;
using UnityEditor;

namespace Overlink.Editor
{
    public static class SceneRegistrar
    {
        // Desired build order — edit here if the list changes.
        private static readonly (string path, bool enabled)[] Scenes =
        {
            ("Assets/Scenes/MainMenu.unity",       true),
            ("Assets/Scenes/Gameplay.unity",       true),
            ("Assets/Scenes/Deckbuilder.unity",    true),
            ("Assets/Scenes/Collection.unity",     true),
            ("Assets/Scenes/Settings.unity",       true),
            ("Assets/Scenes/NetPresenceDemo.unity",true),
            ("Assets/Scenes/SampleScene.unity",    false),
        };

        [MenuItem("Overlink/Register Scenes in Build Settings")]
        public static void Register()
        {
            var missing = new List<string>();
            foreach (var (path, _) in Scenes)
            {
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                    missing.Add(path);
            }

            if (missing.Count > 0)
            {
                UnityEngine.Debug.LogError(
                    "[SceneRegistrar] Registration aborted; missing scene assets:\n" +
                    string.Join("\n", missing));
                return;
            }

            var list = new List<EditorBuildSettingsScene>();
            foreach (var (path, enabled) in Scenes)
                list.Add(new EditorBuildSettingsScene(path, enabled));

            EditorBuildSettings.scenes = list.ToArray();

            var actual = EditorBuildSettings.scenes;
            if (actual.Length != list.Count)
            {
                UnityEngine.Debug.LogError(
                    $"[SceneRegistrar] Verification failed: expected {list.Count} scenes, got {actual.Length}.");
                return;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (actual[i].path != Scenes[i].path || actual[i].enabled != Scenes[i].enabled)
                {
                    UnityEngine.Debug.LogError(
                        $"[SceneRegistrar] Verification failed at index {i}: " +
                        $"expected {Scenes[i].path} (enabled={Scenes[i].enabled}), " +
                        $"got {actual[i].path} (enabled={actual[i].enabled}).");
                    return;
                }
            }

            UnityEngine.Debug.Log("[SceneRegistrar] Build settings updated and verified.");
        }
    }
}
```

**OpenCode must not add a static constructor or `InitializeOnLoad` hook.** After creating
all scene files and waiting for Unity to import them, invoke the menu command
`Overlink/Register Scenes in Build Settings` once. Then verify the resulting
`EditorBuildSettings.scenes` list matches §1 exactly: paths and enabled flags must both
match. This is the only step allowed to mutate build settings for this milestone.

---

## 5. Placeholder stub scene structure

Each stub (`Deckbuilder.unity`, `Collection.unity`, `Settings.unity`) has:

```
[Scene name] (root)
└── UI Root
    ├── EventSystem
    ├── Main Camera
    └── Canvas [Screen Space – Overlay, 1920×1080]
        ├── Background [Image #0D1117]
        ├── Label [TMP_Text — filled by PlaceholderSceneController.Awake()]
        └── Btn_Back [Button + TMP_Text "← Main Menu"]
```

`PlaceholderSceneController` is attached to **UI Root**. Wire `label` → Label, `btnBack` → Btn_Back.

---

## 6. File allowlist for OpenCode

OpenCode may create or modify only the files listed below. Any path not in this list
requires an explicit owner decision before touching it.

**Create (new files):**
```
Assets/Scenes/MainMenu.unity
Assets/Scenes/Deckbuilder.unity
Assets/Scenes/Collection.unity
Assets/Scenes/Settings.unity
Assets/Scripts/UI/SceneIndex.cs
Assets/Scripts/UI/SceneLoader.cs
Assets/Scripts/UI/MainMenuController.cs
Assets/Scripts/UI/PlaceholderSceneController.cs
Assets/Scripts/UI/Overlink.UI.asmdef
Assets/Editor/SceneRegistrar.cs
```

**Modify (existing files):**
```
No existing file should need modification. `SceneRegistrar` uses only `UnityEditor`,
`UnityEngine.Object`, and `UnityEngine.Debug`, all already available to the existing
Editor assembly. If Unity reports an assembly-reference error, stop and request an
owner decision rather than modifying unrelated assemblies.
```

**Do not touch:**
```
ProjectSettings/**          ← use SceneRegistrar instead
Assets/Scenes/Gameplay.unity
Assets/Scenes/NetPresenceDemo.unity
Assets/Scripts/Match/**
Assets/Scripts/Core/**
Assets/Scripts/Cards/**
Assets/Scripts/Net/**
Assets/Tests/**
```

---

## 7. Milestones and exit conditions

| # | Milestone | Exit condition |
|---|---|---|
| **M1** | Scripts compile | `Overlink.UI` asmdef + 4 scripts exist; no compile errors in Editor |
| **M2** | Stub scenes created | `Deckbuilder`, `Collection`, `Settings` scenes open in Editor with no missing references |
| **M3** | MainMenu scene built | `MainMenu.unity` opens; all 6 action buttons (Play, Deckbuilder, Collection, Settings, Network Demo, Quit) are present and wired; no missing script warnings; `OnValidate()` reports no unassigned references |
| **M4** | Build settings registered | After all scene assets are imported, the menu command runs without errors; `EditorBuildSettings.scenes` contains all 7 entries from §1 in exactly that order with exactly those enabled flags |
| **M5** | Navigation works | From MainMenu, Play loads Gameplay, Deckbuilder/Collection/Settings/Network Demo each load the intended scene by name, and each new placeholder Back button returns to MainMenu; no navigation is tested before M4 succeeds |

---

## 8. Commit strategy

One commit per milestone, message format:

```
feat(ui): M1 - Overlink.UI assembly and menu scripts
feat(ui): M2 - placeholder stub scenes (Deckbuilder, Collection, Settings)
feat(ui): M3 - MainMenu scene assembled and wired
feat(ui): M4 - SceneRegistrar, build settings updated
feat(ui): M5 - navigation verified, v0.18 main menu complete
```

Tag after M5: `v0.18.0`

---

## 9. What OpenCode must NOT do

- Do not initialise Unity Gaming Services in the menu or stubs.
- Do not reference `Overlink.Match`, `Overlink.Core`, `Overlink.Cards`, or
  `Overlink.Net` from `Overlink.UI` — the menu has zero game-logic dependency.
- Do not add any async/await loading screen — `SceneManager.LoadScene` (synchronous)
  is sufficient for this milestone.
- Do not place a `NetworkManager` or `GameCoordinator` in the menu scene.
- Do not hard-code scene names in runtime navigation code; always use `SceneIndex` constants.
  The editor-only `SceneRegistrar` is explicitly exempt because its declared scene-path
  list is the source of the required build-settings order.
- Do not edit `ProjectSettings/EditorBuildSettings.asset` directly.
- Do not add automatic scene registration through a static constructor or `InitializeOnLoad`.
- Do not claim Gameplay or NetPresenceDemo have Back-to-Menu controls in this milestone;
  those existing scenes are explicitly out of the file allowlist. The v0.18 requirement
  is that every listed destination is reachable from MainMenu, with Back navigation
  required only for the three new placeholder scenes.
