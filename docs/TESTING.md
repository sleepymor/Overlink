# Testing

Two ways to verify this project, and they answer different questions.

- **Headless (EditMode NUnit)** — the rules: `Assets/Tests/Core`, `Assets/Tests/Match`, `Assets/Tests/Net`.
  Fast, deterministic, no scene. This is where a rule change is proven.
- **The live path** — presentation and feel. Load `Assets/Scenes/MainMenu.unity` non-additively, `editor_play`,
  then invoke `Btn_Play`'s `onClick` (`UI Root/Canvas/ButtonPanel/Btn_Play`). Never load `Gameplay.unity`
  directly: the coordinator is wired from the menu, and a directly-loaded Gameplay scene is a state the game
  cannot reach.

Bridge mechanics worth remembering:

- `debug_get_errors` immediately *before* a run is the only reliable compile check. `console_get_logs` has
  returned `[]` while a CS0103 was live, and a compile error freezes discovery, domain reload and play mode.
- `test_get_result` takes `jobId` only. Fetching with a large `limit` has stalled the REST dispatcher.
- Verify `totalTests` against the number of `[Test]` methods in the file, not just the verdict — a run that
  matches nothing reports `total: 0`, and a stale list reports brand-new names as Passed.
- Never run EditMode tests with `CardPresentationPreview` open. Its `MainCamera`-tagged preview camera hijacks
  `Camera.main` inside the presenter and fakes `DropTargetTests` failures.

## An EditMode test assembly has to be Editor-only

This cost a session, so the mechanism is recorded here rather than in a comment.

**Symptom.** A filtered run for one class never leaves `starting` and times out at 90 seconds. The full-suite
run completes happily and its total is simply smaller than the number of tests in the tree. Nothing logs an
error, and the assembly compiles fine.

**Cause.** `EditorLoadedTestAssemblyProvider.GetAssembliesGroupedByTypeAsync` classifies each test assembly by
`AssemblyFlags.EditorOnly` (`Library/PackageCache/com.unity.test-framework@1.1.33/UnityEditor.TestRunner/
TestRunner/Utils/EditorLoadedTestAssemblyProvider.cs:60`). An asmdef with an empty `includePlatforms` is a
**PlayMode** assembly, so every test inside it is silently absent from the EditMode tree. The runner is not
broken and its cache is not stale: it is answering exactly the question that was asked of it.

**Required shape** (`Overlink.Match.Tests` and `Overlink.Net.Tests` are the models):

```json
"includePlatforms": ["Editor"],
"references": [... , "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
"overrideReferences": true,
"precompiledReferences": ["nunit.framework.dll"],
"defineConstraints": ["UNITY_INCLUDE_TESTS"]
```

**History.** `Overlink.Core.Tests` and `Overlink.Cards.Tests` carried the legacy
`optionalUnityReferences: ["TestAssemblies"]` shape instead, so **199 Core tests and 22 Cards tests had never
run** in either mode. Found on 2026-10-27 while landing Stage 2's opening-lineup tests: the new class compiled,
was demonstrably loaded, and still could not be filtered on. `Core.Tests` is Editor-only now; `Cards.Tests`
belongs to PlayMode for its own reasons, below.

## Cards.Tests is a PlayMode assembly on purpose

`CardHandTests` drives real presentation objects and calls `Object.DontDestroyOnLoad` and `Object.Destroy`,
which throw in EditMode (`DontDestroyOnLoad ... cannot be part of an editor script`, `Destroy may not be
called from edit mode! Use DestroyImmediate instead`). Forcing that assembly Editor-only produced 16 failures
of exactly that shape and no others. Its 22 tests have never been executed by either mode and are unverified;
they need a PlayMode run, not a reclassification.

## When a run cannot see a class that certainly exists

In this order, because each step is cheap and the last one is expensive:

1. **Is the assembly Editor-only?** Read its asmdef before reading anything else.
2. **Is the class in Unity's tree?** `TestRunnerApi.RetrieveTestList(TestMode.EditMode, ...)` from an Editor
   menu item, walking `root.Children` for per-assembly leaf counts. This is the runner's own ground truth and
   it bypasses the bridge entirely.
3. **Is the type loaded?** Reflect over `AppDomain.CurrentDomain.GetAssemblies()` and count methods carrying
   `NUnit.Framework.TestAttribute`.

Two traps in step 3: `debug_get_assembly_info`'s `sourceFiles` is the **compilation pipeline's** view, not the
loaded AppDomain's, so it reports a brand-new file as present regardless of what the runner can see; and a
domain reload — including a play-mode round trip, which is two — does not fix a misclassified assembly, which
is how "the runner's cache must be stale" gets mistaken for a fact.

## Known state after the HUD ink pass (2026-10-28)

EditMode: **1330 leaves, 1325 passed, 4 skipped, 1 failed.** The failure is
`UnitySkills.Tests.Core.PerceptionSkillsTests.SceneSummarize_CountsObjectsCorrectly` — a test inside the
UnitySkills package that counts objects in whichever scene happens to be open. It was red before the Node
rule landed, it is not ours, and it is the only expected red.

The 22 tests in `Overlink.Cards.Tests` are not in that count. They belong to PlayMode and no run of either
mode has ever executed them.

**A geometry test that projects the board has to anchor its own rig.**
`StatusLayer_CoversNoClickableTileAtAnySupportedAspect` is a `[UnityTest]` — it needs a camera whose matrices
have been refreshed, so it yields one frame per aspect — and its first assertion is not about the HUD at all:
it checks that the tabletop's near and far edges land at y 69 and 623 of 720, the span
`BattlefieldCameraController.RestFocusZ` documents. Without that, a projection that measures nothing still
passes an overlap sweep, which is the shape this failure takes: a degenerate or NaN rect intersects nothing,
so every assertion below it goes green at the exact moment it stops testing anything.

**`StatusInk_FitsItsBoxes` is the same argument pointed inward.** Boxes that clear the board can still cover
themselves, and four of the layer's nine panels were doing exactly that in the shipped scene. The guard asks
TMP what it laid out — widest line, block height, and `textInfo.characterCount` against the tag-stripped
length of the source — for every string in the layer, and appends a row per case so one failure reports the
whole table. Four things about that API are not obvious and each of them was a false green or a false red:

- **Truncate does not spill, it deletes.** Every status text is authored with `TextOverflowModes.Truncate`, so
  ink that outruns its slab stops being drawn with no visual evidence at all. `characterCount` falling short
  of the plain length is the only signal that cannot lie. (`visibleCharacterCount` excludes spaces, which
  makes it useless here; rich tags are consumed by the parser, which makes the *stripped* length the right
  denominator.)
- **Overflow, the rail's default, does the opposite.** Its labels are raw `TextMeshProUGUI`, never passed
  through `EnsureText`, so a long label walks out from under its 162x36 button and draws onto the board. Same
  root cause, opposite symptom, and only a live frame shows it.
- **An inactive TMP generates no mesh.** Zero lines, zero characters — indistinguishable from truncation. The
  result overlay and the whole button rail are hidden at build, so the guard calls
  `ForceMeshUpdate(true, true)`: `ignoreActiveState` to lay it out anyway, `forceTextReparsing` because the
  rail's labels already hold the string being assigned and TMP otherwise considers itself clean.
- **Glyphs have a side effect.** Measuring the two pile arrows rasterises U+25C4/U+25BA into the dynamic
  fallback atlas and dirties the font asset on every run, so those two labels are excluded by documented
  choice; one glyph in a 78 pixel slab was never a plausible overflow anyway.

The width half of the floor binds single-line boxes only — a wrapped line sitting near its wall is the
wrapping working, not a squeeze — and the height floor covers both kinds. To find the numbers the first time,
set `MinInkSlack` (or `MinBoardClearance`) to an impossible value like `999f` and run once: every case then
reports, and passing tests report nothing. That harvest is where 819-of-900, 432-of-446, 46-of-56 and the
per-box board seams in §3.7 come from. Recalled geometry is not evidence.

**Adding to this count is where a filter bites.** `test_run {filter}` takes a name fragment, not a regex: a
`"^Namespace\.Class"` pattern matches nothing, and a run that matches nothing is the one that wedges in
`starting` for 90 seconds and reports `total: 0`. Use `test_run_by_name {testName:"ClassName"}` for a single
cluster and read `totalTests`/`passedTests` from `test_get_result`.
