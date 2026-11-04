# Board Skin Pass Implementation Report

Date: 2026-10-28
Stage: 3g — commit `dc460db` ("feat(gameplay): the board has five skin slots, and they work while it is running")

## Scope

The tabletop stopped being code-only. Five `Material` slots and thirteen serialized albedos now live on
`BattlefieldPresenter`, so the board can be re-skinned by dropping assets on a component in the scene instead of
editing constants, rebuilding a board, and re-running a scene builder. No rules moved, no geometry moved, and
`Assets/Scenes/Gameplay.unity` is byte-identical to the state it was in before this pass.

This closes the last unmet piece of the standing requirement that *everything be editable in the scene, not by
script only, so a texture or skin can be dropped in easily*. The card side got there first
(`CardFrameCatalog`, `CardTemplateCatalog`, `CardTierMaterialSet` are all droppable); the board was the remaining
surface in the match that was not.

## Why this needed care rather than five fields

The board has **two construction paths** — the procedural build and the bind off the hand-authored
`Assets/Prefabs/Battlefield.prefab` — and it has **one per-frame repaint** that overrides every tile's colour
through a `MaterialPropertyBlock`. A skin feature that ignored either of those would have looked finished and
been broken: slots that only work on a rebuilt board, or a skin that erases the state colours a player reads
combat from. Both are addressed below, and both are pinned by tests.

## The Inspector surface

On `Match` → **Battlefield Presenter**, two new headers.

| Slot | Family it claims |
|---|---|
| `tableSkin` | The backdrop slab the whole board lies on (1 body) |
| `socketSkin` | Every slot tile: Active, Bench, Protocol, both halves, all five battlefields (36 bodies) |
| `plateSkin` | The two Core plates and the four Node plates (6 bodies) |
| `pileSkin` | Deck and discard, top card and buried layers alike (6 bodies) |
| `rimSkin` | The border cube behind every Active socket (10 bodies) |

Fifty-nine cubes in total, and the partition is asserted, not assumed — see the guard below.

| Albedo | Default | Albedo | Default |
|---|---|---|---|
| `tableTint` | `0.09, 0.11, 0.15` | `corePlateTint` | `0.16, 0.18, 0.25` |
| `activeEmpty` | `0.30, 0.27, 0.19` | `nodeFree` | `0.26, 0.28, 0.32` |
| `benchEmpty` | `0.13, 0.15, 0.19` | `nodePlayer` | `0.14, 0.48, 0.28` |
| `protocolEmpty` | `0.23, 0.18, 0.31` | `nodeOpponent` | `0.56, 0.38, 0.12` |
| `activeRim` | `0.80, 0.64, 0.28` | `deckTint` | `0.13, 0.19, 0.25` |
| `playerTint` | `0.20, 0.42, 0.72` | `discardTint` | `0.21, 0.17, 0.14` |
| `opponentTint` | `0.62, 0.24, 0.24` | | |

Every default is the value the shipped framing was measured and signed off against.

## Design rules

**A body's family comes from its name, off the same vocabulary as its colour.** `SurfaceFor(name)`
(`BattlefieldPresenter.cs:890`) sits directly above `BaseColorForSlot(name)` (`:908`) and tests the same names in
the same order. That is deliberate rather than convenient: the prefab already cannot be relied on to carry a
per-tile colour, which is why the empty colour keys off the name, so keying the skin off the same name makes a
hand-regrouped prefab and a procedural board skin *identically* — one resolver to disagree with the other, rather
than two mechanisms that drift apart silently. The ordering hazard is name-level and pinned at name level: the
gold border cube is literally named `Socket` while no tile's own name contains the word, so the rim branch must
precede the socket fallback. Flip those two lines and the board renders perfectly with every rim wearing the
socket skin.

`BaseColorForSlot` consequently changed from `static` to an instance method. It is private, and its only two call
sites are the build path and the bind path.

**A skin multiplies; it does not replace.** The per-frame repaint still overrides the surface colour through the
property block (`SetBodyColor`, `:2319`), so one material serves a family whose members are three different hues
— Active, Bench and Protocol are deliberately three hues so a row never reads as three identical slots. A skin
meant to be seen at full strength wants its family's albedo raised toward white, which is exactly why the albedos
are serialized beside the slots rather than left as constants. A material whose shader declares neither
`_BaseColor` nor `_Color` takes no tint and renders as authored — legitimate, but it means the albedos stop
meaning anything for that one family.

**The albedos are code defaults, not scene data.** A scene or prefab saved before the fields existed carries no
entry, so Unity leaves the field initializer standing and the board renders as the one that shipped. This is what
makes "everything is now editable" safe to land on top of hand-authored work rather than a regression against it.

**What stayed code-owned: the pointer marks and `BlockMark`.** Those are not surfaces, they are statements. Cool
against warm is how a player tells "a thing of yours you may trade places with" from "a thing you may hit" at a
glance, across the whole table at once, and a tuning surface that lets the two land on the same hue is a rules bug
waiting for an artist. The HUD's node-list row colours went with them for the same reason.

### The hazard the slots would otherwise have shipped with

`SetBodyColor` used to resolve **one static property id** off the first tile's shader *name* and reuse it for
every renderer on the table. Harmless while the board was built from two known shaders. With five droppable
materials the board can genuinely mix shaders, and the cache would then tint one half through `_BaseColor` or the
other through `_Color` on a shader that does not declare it — so tiles silently never change colour, on whichever
half the first tile did not decide. The choice is now made per material via `HasProperty`
(`baseColorId` / `colorId`, `:2294`), which is both cheaper and more honest, and is **identical in behaviour for
the two shaders the board has ever used**.

### When a slot is read

Three places, because each answers a different workflow:

1. **At build time** — `MakeBody` → `MaterialFor(color, name)` (`:2587`) consults the slot before falling back to
   the generated per-colour material.
2. **At bind time** — `TryBindBoard` calls `ApplySkins()` on success (`:540`), so a skin dropped on a scene whose
   board comes from the prefab takes effect rather than sitting inert until somebody rebuilds a board.
3. **Every frame** — `ApplySkinsIfChanged()` (`:2679`) from `LateUpdate` (`:404`), guarded by five reference
   comparisons so an untouched slot set costs five reads and never walks the hierarchy. Same
   derived-not-remembered shape the control rail publishes with, and public for the same reason `BuildBoard` is:
   EditMode never ticks a `MonoBehaviour`.

`ApplySkins` is additionally `[ContextMenu("Apply Board Skins")]`, which is the only way a serialized field can
act on a live edit-mode scene without a custom editor.

**Clearing a slot is one-directional on purpose.** A cleared slot cannot recover the colour that generated the
material underneath, so it leaves the body alone rather than guessing — guessing would trade a hand-authored
per-tile material for a generated one of the same shade, which is the opposite of what an artist just did by hand.
Back to the shipped look means rebuilding the board, which consults the palette from scratch.

## Guard: `BoardSkinTests` (10)

`Assets/Tests/Match/BoardSkinTests.cs`, the first class on the board that guards a **tuning surface** rather than
a geometry.

| Test | What it pins |
|---|---|
| `Albedos_AreTheShippedFrame` | All thirteen albedos against **copies** of the shipped values, not against the fields they validate |
| `Slots_StartEmptySoAnUntouchedSceneLooksUntouched` | All five slots `null` on a fresh component |
| `EveryBodyOnTheBoardIsClaimedByExactlyOneFamily` | Per-family counts derived from the same `Board` constants the presenter lays itself out from, **plus the total**, so an unclassified cube cannot hide inside a correct-looking family |
| `SurfaceFor_TellsTheRimApartFromASocket` | The name-level ordering hazard, including `null` and `""` |
| `NoSkin_RendersTheGeneratedMaterialForItsOwnColour` | Every branch of the colour resolver, through the hex in the generated material's asset name, and that each is a real `.mat` on disk rather than an in-memory material a prefab save would flatten |
| `ASkinClaimsItsWholeFamilyAndNotOneBodyOutsideIt` | Five distinct probes at once, so a body wearing the wrong one is a *positive* statement about which two families overlap |
| `ApplySkins_IsOneDirectional` | A set slot acts, an empty slot reaches no further than its own family, and a cleared slot leaves the body alone |
| `ASkinSharesOneMaterialAcrossColoursThatDiffer` | The multiplication contract, next to the assertion that without a skin three colours mean three materials |
| `ASkinDroppedAfterTheBuildLandsWithoutRebuilding` | The per-frame path, and the dedupe observed through its consequence rather than a counter |
| `ApplySkins_BeforeABoardExistsIsHarmless` | A no-op, not a null crash, and no board built as a side effect |

**Mutation-tested, because nine of ten went green on the first run.** That is precisely when an assertion that
cannot fail looks identical to a working one. The rim branch was deleted from `SurfaceFor` in *shipped code* (not
in the test) and the class re-run: exactly two tests went red — `EveryBodyOnTheBoardIsClaimedByExactlyOneFamily`
and `SurfaceFor_TellsTheRimApartFromASocket` — while the other seven stayed green. That split is the signature of
a live guard; a dead one goes red everywhere or nowhere. The mutation was reverted and the revert verified by
grep before the next compile. The probe ran while the class had nine tests; the tenth was written afterwards and
is covered by the full-suite run below.

## Live verification

Through the real path, and through the path the slots exist for rather than a rebuild: `Assets/Scenes/MainMenu.unity`
loaded non-additively, `Btn_Play` invoked for real, and at 1920×1080 **in the middle of a live lineup offer**
`pileSkin` was set over the bridge to an existing board material
(`Assets/Materials/Battlefield/Slot_CCA347.mat`, the Active gold).

- The deck tile went from dark navy to gold on the next frame, with the albedos left dark — which demonstrates the
  multiplication and the per-frame check at once.
- No board rebuild, no menu click, no scene save.
- Zero console errors attributable to the board.
- The drop was play-mode only, so `Gameplay.unity` carries no churn from this pass.
- Capture files were scratch and are not committed; the repo has no stray PNGs under `Assets/`.

## Suite state

- Compilation: clean (`debug_get_errors` count 0 after each refresh).
- **EditMode: 1356 leaves (+10), 1351 passed, 4 skipped, 1 failed.**
- The one failure is the standing third-party `UnitySkills.Tests.Core.PerceptionSkillsTests.SceneSummarize_CountsObjectsCorrectly`,
  which counts objects in whichever scene happens to be open.
- The five `ReviewFixRouterTests` cases that were red last stage **did not reproduce** across two full-suite runs
  with nothing under `Assets/` touched mid-run. That retires them as a mystery and leaves them as what they are:
  a busy router. `docs/TESTING.md` says so now.

## Corrections against my own work this session

Recorded so nothing above is trusted harder than it should be.

1. I reported "two real failures" from a `read` that came back garbled — the file was intact and the run was 9/9
   green. The garbled read was harness noise, and I should have verified the file's shape before announcing
   defects.
2. I quoted "1345 passed / 6 failed" from a job that was still `running`. Polls during a run are live counters,
   not results; every number in this report is from a `completed` poll.
3. I lost one test job by starting an EditMode run while the editor was in play mode. The job reports
   `status: "failed"` with `totalTests: 0` and a message that does not name play mode anywhere in its own
   payload. Now written into `docs/TESTING.md` as step 0.5, because it is the cheapest cause to rule out and the
   easiest to blame on the class under test.
4. An attempted bulk rename through PowerShell silently no-op'd (a `MatchEvaluator` delegate marshalling failure).
   Nothing was written, so no harm, but "the command exited 0" was not evidence of the change landing — the
   follow-up grep is what proved it, and the edit tool did the work.

## Not done, and said so rather than left implied

- **No artwork has been authored for the board.** The slots are proven with an existing material used as a probe.
  Dropping real textures and skins in is the next thing, and it is now yours to do in the editor without me.
- **The HUD still has no font or material slot.** It inherits `TMP_Settings.defaultFontAsset`, so its ~40
  serialized knobs tune colour and size only. That is the same gap this pass closes for the table, still open on
  the overlay.
- **The idle-hand sinker still runs during lineup setup** (the offer drops, the deck tile reads out, the deck
  browser stays reachable while picking). Offered for veto last stage; no answer yet.
- **Per-tile skin granularity.** Five families is five slots, not sixty. A body can already be re-skinned by hand
  in the prefab, and `ApplySkins` will not overwrite it while its family slot is empty — but there is no
  Inspector surface for "this one socket, differently". Deliberate: the families are what the board's reading
  depends on, and a per-tile surface invites exactly the drift the name-keyed resolver exists to prevent.

## How to review it

1. Open `Assets/Scenes/Gameplay.unity`, select `Match`, scroll to **Battlefield Presenter**. The two new headers
   sit directly under the existing `Coordinator` and `Hud` references, above everything else on the component.
2. Drop any material into `pileSkin` and press Play. The two piles change on the first frame of the match; nothing
   else does. In edit mode, the same drop followed by the component's gear menu → **Apply Board Skins** does it
   without entering play.
3. Raise `deckTint` toward white with a skin in `pileSkin` and watch the skin come up to full strength — that is
   the multiply contract, visible in one minute without reading a line of code.
4. To check the partition claim yourself: `BoardSkinTests.EveryBodyOnTheBoardIsClaimedByExactlyOneFamily` prints
   the owed-vs-owned count per family in its failure message.

## Files and anchors

| File | Anchors |
|---|---|
| `Assets/Scripts/Match/BattlefieldPresenter.cs` | skin region `:72`, albedo region `:88`, per-frame call `:404`, bind call `:540`, `Surface` `:865`, `SurfaceFor` `:890`, `BaseColorForSlot` `:908`, property ids `:2294`, `SetBodyColor` `:2319`, `MaterialFor` `:2587`, `SkinFor` `:2597`, `ApplySkins` `:2627`, `ApplySkinsIfChanged` `:2679` |
| `Assets/Tests/Match/BoardSkinTests.cs` | new, 10 tests, `:235`–`:503` |
| `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md` | §3.7 skin paragraph, status table rows "Board skin / art pass" and "NUnit execution" |
| `docs/TESTING.md` | play-mode runner hazard, "Known state after the board skin pass", `BoardSkinTests` and mutation-test write-ups |
| `Assets/Scenes/Gameplay.unity` | **unchanged** |
| `Assets/Prefabs/Battlefield.prefab` | **unchanged** |
