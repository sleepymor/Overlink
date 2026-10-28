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

**The assembly-classification cause is not the only one, and the other is easier to miss.** An assembly that
*currently* fails to compile is absent from the tree too, which therefore keeps serving whatever its last good
build contained. Nothing logs, no dialog appears, and the symptom is byte-identical to the one above: a
filtered run for one class never leaves `starting`, while a full-suite run completes happily with a total that
has simply not moved. So step 0 is `debug_get_errors` read right now — the sentence in this file's own bridge
mechanics that says a compile error freezes discovery is stronger than it reads like, because it also freezes
the tree at a version that predates your new file.

This cost three runs landing Stage 3f. The class compiled clean when it was written; three `CS1503` arrived
with a later edit to it; the fixes were applied; and the refresh that would have cleared them was never run
before the test job was started. The full suite reported 1330 leaves both before the file existed and after it
did, which was the whole story, and discovery confirmed it once the refresh ran: **1346**. Read the leaf count
of a full-suite run when a new class will not filter — a total that ignores a brand-new class means the
assembly did not rebuild, not that the filter is wrong.

**A third cause, and the cheapest to rule out: the editor is in play mode.** An EditMode job accepted while a
play-mode session is running completes as `status: "failed"` with `totalTests: 0`, `passedTests: 0`, and the
message `An unexpected error happened while running tests.`; the console carries the real reason one line
further down — `InvalidOperationException: This cannot be used during play mode` from the runner's
`SaveModiedSceneTask`. Nothing about the job's own payload names play mode, so it reads like a broken class.
`editor_get_state`'s `isPlaying` is the one-line check, and it costs nothing to make it step 0.5. This is worth
more than a footnote in the case that matters: a live verification session left running is exactly the state
somebody else will try to run tests in.

## Known state after the board skin pass (2026-10-28)

EditMode: **1356 leaves, 1351 passed, 4 skipped, 1 failed.** The one failure is inside the UnitySkills package
and is not ours:

- `PerceptionSkillsTests.SceneSummarize_CountsObjectsCorrectly` counts objects in whichever scene happens to be
  open. It was red before the Node rule landed, it is not ours, and it is the only *standing* red.
- Five `ReviewFixRouterTests` cases POST to the bridge's own REST router from inside a test run and got
  `COMPILING` / 503 back, or a reply missing the field under test, because the router was busy serving the
  session that started the run. The class is **48/48 green when run on its own**, so those five are load
  artefacts of a full-suite run: re-run the class before believing any of them. Landing the board skin pass is
  the confirmation — two consecutive full-suite runs (1355 and 1356 leaves) reproduced only the standing
  `PerceptionSkills` red, with nothing under `Assets/` touched while either ran. The five are not a mystery
  failure any more; they are a busy router, and the cure is to stop editing assets mid-run.

The 22 tests in `Overlink.Cards.Tests` are not in that count. They belong to PlayMode and no run of either
mode has ever executed them.

**`IdleHandSinkTests` (16) is the third geometry class, and it earned its keep on its first run.** Six of its
tests measure the resting fan's screen footprint through the shipped rig, one anchors the rig against the
tabletop's documented 0.096..0.8648 span first, and nine are pure arithmetic on synthetic rects. The anchor
matters for the same reason it does for the status layer: a projection that measures nothing still passes an
overlap sweep. What it caught was worth the trouble — `RestingFootprintViewport` resolved the fan root's
*parent-space* rest pose through the root's own transform, which doubles the fan's offset inside the camera and
puts the row nowhere near where the player sees it. The tell is the split: every pile-side and rule-side test
stayed green while all six fan tests went red, which is the shape a half-broken projection takes, and the sink's
own round-trip assertion passed throughout because both directions shared the error. A self-consistent unit is
not a verified unit; the anchor is what distinguishes them.

**`BoardSkinTests` (10) is the first class on the board that guards a tuning surface rather than a geometry**,
and it exists because serialized fields fail quietly in a way constants cannot. It pins the thirteen albedos
against copies of the shipped values (not against the fields they validate, which would prove nothing), the five
slots against `null`, the partition of the board's fifty-nine cubes against counts derived from the same `Board`
constants the presenter lays itself out from, every branch of the name-keyed colour resolver through the hex in
its generated material's asset name, and the multiplication contract — one material across a family whose three
albedos provably differ, which is only meaningful next to the assertion that without a skin three colours mean
three materials.

**Nine of the ten went green on the first run, which is precisely when this project's discipline says to suspect
the tenth kind of failure: an assertion that cannot fail.** So the guard was mutation-tested against shipped code
rather than believed — the rim branch was deleted from `SurfaceFor`, and exactly two tests went red
(`EveryBodyOnTheBoardIsClaimedByExactlyOneFamily` and `SurfaceFor_TellsTheRimApartFromASocket`) while the other
seven stayed green. That split is the signature of a live guard: a dead one goes red everywhere or nowhere, and
the two that moved are the only two that depend on the branch. The mutation was reverted and the revert verified
by grep before the next compile.

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
