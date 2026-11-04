# UI / VFX Pass — Implementation Report

**Release mark: v2026.11.1 Alpha.** Everything below ships as the v2026.11.1 alpha on the
calendar scheme `[Year].[Month].[Ver]` (successor to v2026.11.0). Both player-facing version
stamps carry the mark: `VersionLabel` in `Prefabs/MainMenu.prefab` (bottom-right corner stamp,
was `v0.18`, a leftover from the pre-calendar scheme — and drawn *under* a static Figma-mockup
backdrop that had the stale string baked in, see P5 for the patch) and the footer tagline in
`Scenes/MainMenu.unity` (`Footer left`, was `OVERLINK • BUILD 2026.11`). The scene instantiates
the prefab for the visible menu and also carries an inline re-authoring; both copies were
stamped.

Scope: every player-facing UI screen in the offline Gacha prototype and the surrounding shell
(main menu, settings, binder, match HUD) received authored motion — counters that roll, bars that
fill, buttons that answer the pointer, panels that arrive with a beat — built from a small
hand-rolled tween kit with no new packages. The pass also split the pack-opening prototype into a
shop scene and a reveal scene so each screen owns its own presentation.

All work lives on `master`, uncommitted by design.

> **Reconstruction status:** This report records the earlier PNG-backed pass; the historical
> P5 description below is no longer the current main-menu implementation. `MainMenu.unity` now
> has an editable authored hierarchy and a null backdrop sprite. Its regenerated scene and
> navigation/responsive behavior still need verification. Signal Bay remains backed by
> `SignalBayBase.png`: its alternate editable hierarchy is incomplete and hidden, so the
> existing image must stay until a replacement has been visually and functionally checked.
> The bay's live-counter wiring command now preserves a non-raster screen rather than restoring
> the derived PNG. Figma identifies the pack as a separate layered object (`131:2892`), but the
> export/design-context request has timed out; no exported pack asset has been integrated.
>
> **Flow check:** `Tools/Offline Gacha/Capture Bay Flow Preserving Player Data` snapshots the
> affected local preference keys, runs the existing single-pack capture, and restores and verifies
> their values and existence afterward. The 2026-11-04 run confirmed fresh ownership on OPEN AGAIN
> and on a second bay entry. This is a scripted single-pack flow, not a pointer-driven or ten-pack
> test, and does not establish that the unfinished hierarchy matches the reference.
> `Tools/Offline Gacha/Verify Ten Pack Pointer Flow Preserving Player Data` additionally
> dispatches synthetic pointer events to EventSystem raycast winners. On 2026-11-04 it
> passed the visible ten-pack button, initial reveal state, REVEAL ALL, summary close,
> completed button state and fresh OPEN AGAIN (20 purchases, 11 additional copies of the
> sampled card); it restored and verified 21 preference keys. This does not simulate a
> physical mouse or approve the editable bay. The corresponding single-pack pointer test
> reached the reveal, completed all five cards, closed the summary and reopened a fresh pack;
> restoration again verified 21 preference keys. Single-pack resolution now records each
> purchase without charging collection initialization. The preserved test checks the 99→100
> loyalty reward and idempotency, then checks two pointer-driven purchases and the returned
> bay readouts: 2,200 credits, 2/100 milestone, 98 remaining. The preserved single-loyalty
> pointer test now verifies that purchase 100 presents the purchased pack and bonus in a
> two-pack, ten-card queue, labels the bonus in the completed summary, and does not repeat
> it on OPEN AGAIN at purchase 101. It restored and verified 22 preference keys. These
> are synthetic EventSystem pointer events rather than physical mouse input.
> The hidden editable pack now also has a wireframe display and two platform rings made
> from non-raycast uGUI segments. The 1920×1080 preview
> `Assets/Screenshots/signal_bay_display_frame.png` shows the addition; the draft still
> differs materially from the reference in layout, typography, panels, and detail. With
> the draft temporarily visible, the three existing raster-aligned shop button centers
> still raycast to their wired controls; this is a hit-target audit, not a full visual or
> purchase-flow approval for the draft. The saved hierarchy remains hidden and the raster
> backdrop remains active.
> The 1920×1080 editable-bay surface audit now samples the center and four inset corners
> of each wired button: all 15 points hit their intended controls, including after the
> return button's white placeholder background was removed. A preserved synthetic-pointer
> run with the draft visible clicked its single-pack button, completed and reopened the
> pack, returned to the bay with the expected counters, and restored 22 preference keys.
> The visible editable ten-pack flow also completed 50 cards, closed the summary and
> reopened fresh (20 purchases, 11 new copies of the sampled card). A separate visible
> editable binder-return pointer flow navigated to Collection. Both preserving wrappers
> restored and verified all 22 preference keys. These are synthetic pointer events, not
> physical-mouse tests. The footer has been moved to the lower right, away from the binder
> label; see `Assets/Screenshots/signal_bay_footer_spacing.png`. The 15-point surface audit
> still passes after that change; the rest of the draft remains visually incomplete.
> The three editable choice rows have since been given darker surfaces, editable icon
> plates, a cyan selected edge and more legible labels. The icon marks are simplified
> placeholders, not reference-accurate artwork, and the rows do not implement pack
> selection. The 1920×1080 preview is
> `Assets/Screenshots/signal_bay_pack_choice_refinement.png`; all 15 purchase/return
> surface-audit points still hit their wired controls with the draft visible.
> The preserved visible-draft single-pack flow was rerun after this refinement: it
> completed five cards, reopened a fresh pack, returned with the expected live counters,
> and restored all 22 preference keys.
> The offline collection contains one implemented pack manifest. The editable draft now
> identifies one available signal and labels Frontline Protocol and Relay Network `NOT
> AVAILABLE` instead of advertising purchasable prices. In a Play-mode synthetic pointer
> audit, the centers of both unavailable rows had no click handler, neither navigated nor
> changed the purchase count, and all 15 purchase/return surface points still passed.
> `Assets/Screenshots/signal_bay_unavailable_choices.png` captures this draft. This does
> not implement or test switching between pack types, and the live raster shop is unchanged.
> The editable pack-information panel now uses a darker surface, a larger title and a
> two-column five-card composition with editable separators. Its copy identifies the
> deterministic offline demo rather than implying implemented drop-rate controls. See
> `Assets/Screenshots/signal_bay_information_refinement.png`; this is an incremental
> approximation, not visual approval of the complete bay.
> The hidden draft's former hardcoded account mockup is now a display-only identity
> label bound to the same complete Cloud Save profile and `Player` fallback as the main
> menu. It listens for profile load and status changes (including profile clearing), and
> has no pretend profile or sign-out actions. With authentication bypassed, the runtime
> preview displayed `Player`, and the 15-point purchase/return surface audit passed; see
> `Assets/Screenshots/signal_bay_live_identity.png`. A real signed-in profile and live
> profile-change event have not yet been verified.
> A 16:10 Game view preview (`Assets/Screenshots/signal_bay_16x10_before.png`) reveals
> clipping at the left edge and button rectangles extending beyond the viewport. The old
> five-point hit audit passed because its inset points were still on-screen; it now checks
> each full button rectangle as well, and correctly fails all three at this aspect ratio.
> The visible draft now fits its 1600×900 design and corresponding live counter overlays
> into narrower viewports, with a small edge margin. Its temporary solid background hides
> raster fragments outside the fitted design; hiding the draft restores the original
> backdrop sprite, colour, button scale, and overlay positions. At 16:10 the full-rectangle
> audit passes all three controls and all 15 sample points; the preview is
> `Assets/Screenshots/signal_bay_16x10_fit_final.png`. A 16:10 synthetic-pointer single-pack
> run completed five cards, reopened fresh, returned with expected counters, and restored
> all 22 saved preference keys. A separate 16:10 synthetic-pointer binder-return run reached
> Collection and restored the same 22 keys. The hidden raster shop's three center targets also passed
> at 1920×1080. The temporary Game view preset is restored on exiting Play mode. This is a
> proportional fit, not a reflow or physical-mouse test; other aspect ratios and small-screen
> legibility remain unverified, and `SignalBayBase` remains in the saved scene.
> Purchase buttons now show their offline demo prices and card counts as editable text;
> obsolete `1 PU`/`9 PU` draft labels were deactivated. The reference itself contains
> `[IMAGE SAMPLE]`, so the pack aperture placeholder is intentionally retained. A 4:3
> preview is `Assets/Screenshots/signal_bay_purchase_labels_4x3.png`: all three button
> rectangles and 15 sampled hit points passed, but the proportional fit leaves small
> text difficult to read. A preserved synthetic-pointer single-pack flow at 4:3 revealed
> five cards, reopened fresh, returned with expected counters, and restored 22 preference
> keys. This is not approval of a small-screen layout or a physical mouse test.
> For the visible draft below a 1.5 aspect ratio, the information description is shortened
> and enlarged from 14 to 18 points, with three other information captions increased from
> 9–12 to 12–15 points. Hiding the draft or widening the viewport restores the original
> text and sizes. The 4:3 capture `Assets/Screenshots/signal_bay_compact_text_4x3.png`
> shows the improvement, and all three full button rectangles and 15 sampled raycasts pass.
> The 4:3 draft now also uses shorter purchase sublabels at 14 instead of 10 points,
> and enlarges pack-choice prices and descriptions to 13 points while retaining the
> unavailable labels. See `Assets/Screenshots/signal_bay_compact_purchase_4x3.png`.
> All three full button rectangles and 15 sampled raycasts pass. Hiding the draft restores
> the original text, sizes, backdrop sprite, colour, and draft scale. Some decorative
> text remains small; this remains a proportional fit rather than a responsive reflow.
> The updated 4:3 preserved single-pack flow passed reveal, fresh reopen, and return,
> then restored 22 preference keys. Its driver now waits for a frame after showing the
> draft, because text/transform changes during the canvas update otherwise leave the
> immediate synthetic raycast using stale graphics. This is not a physical mouse test.
> At 4:3 the draft also enlarges the loyalty rule, reward details, release footer and
> live remaining-pulls text; the counter remains data-bound. Preview:
> `Assets/Screenshots/signal_bay_compact_loyalty_4x3.png`. The 15-point/full-rectangle
> button audit passed, and hiding the draft restored the original label and counter font
> sizes and `SignalBayBase`. A 4:3 preserved synthetic-pointer single-pack run revealed
> five cards, reopened fresh, returned with 98 remaining, and restored all 22 preference
> keys. This remains a proportional fit, not a responsive reflow or physical mouse test.
> The first narrow-layout reflow now places the one- and ten-pack actions side by side
> below the information panel when the visible draft's aspect is below 1.5. The remaining
> bay still uses the proportional fit, so this is not a complete narrow-screen reflow.
> Preview: `Assets/Screenshots/signal_bay_compact_actions_reflow_4x3.png`. All three full
> button rectangles and 15 sampled raycasts passed at 4:3. Hiding the draft restored the
> original stacked button anchors and `SignalBayBase`; the preserved single-pack flow
> revealed, reopened fresh, returned with expected counters, and restored 22 keys. A
> separate preserved 4:3 ten-pack flow revealed 50 cards, reopened fresh, and also
> restored 22 keys. These are synthetic-pointer checks, not physical mouse tests.
> A subsequent broader 4:3 draft reflow arranges the three pack choices in a horizontal
> strip and moves the scaled showcase below it, while keeping the information panel,
> purchase actions and live loyalty overlays separate. See
> `Assets/Screenshots/signal_bay_broader_reflow_4x3.png`. The full rectangles and all
> 15 sampled button points passed; the unavailable choices did not navigate or change
> purchases. Hiding the draft restored the checked selection panel, second choice,
> showcase and one-pack button transforms and the `SignalBayBase` sprite and colour.
> Preserved single- and ten-pack runs passed reveal and fresh reopen; the single-pack
> run returned with expected counters, and both restored 22 preference keys. At 1920×1080
> the checked panels and one-pack button remained in their original wide-screen positions,
> and the full-rectangle/15-point audit passed (`Assets/Screenshots/signal_bay_broader_reflow_wide.png`).
> The design still uses an overall proportional fit, differs from the reference and has
> small text; it is not an approved replacement or a physical-mouse test.
> Header refinement: while the draft is visible, the readiness status moves below the
> live credit balance and the existing inactive draft credit label becomes a small
> `SIGNAL CREDITS` caption. The actual balance remains the separately bound live counter.
> Wide and 4:3 previews are `Assets/Screenshots/signal_bay_header_caption_wide.png` and
> `Assets/Screenshots/signal_bay_header_caption_4x3.png`. The 4:3 button surface audit
> passed all 15 samples and all three full rectangles. Hiding the draft restored the
> readiness position, disabled the caption, and restored the raster sprite and colour.
> A preserved 4:3 synthetic-pointer single-pack run then revealed five cards, reopened
> fresh, returned with the expected live counters, and restored all 22 preference keys.
> The editable showcase now has a non-raycasting, mesh-generated teal ambient glow,
> echoing the reference's lit pack area without flattening the UI or adding a raster
> dependency. Its colour, opacity, size and position remain editable in the hierarchy;
> it follows the showcase in the 4:3 reflow. Previews:
> `Assets/Screenshots/signal_bay_ambient_glow_wide.png` and
> `Assets/Screenshots/signal_bay_ambient_glow_4x3.png`. The 4:3 full-rectangle and
> 15-point surface audit passed; hiding the draft restored `SignalBayBase` and its colour.
> A preserved single-pack synthetic-pointer run also passed reveal, fresh reopen and
> return, restoring and verifying all 22 preference keys.
> The editable loyalty rule and next-reward text now highlight the free-pack phrase
> in amber, closer to the reference, including the compact 4:3 wording. The progress
> bar and values remain independently bound. Previews:
> `Assets/Screenshots/signal_bay_loyalty_accents_wide.png` and
> `Assets/Screenshots/signal_bay_loyalty_accents_4x3.png`. The 4:3 full-rectangle and
> 15-point button audit passed; hiding the draft restored `SignalBayBase` and its colour.
> A preserved 4:3 synthetic-pointer single-pack flow passed reveal, fresh reopen and
> return with expected live counters, then restored and verified 22 preference keys.
> The accent improves hierarchy but does not resolve small-screen text size.
> Compact 4:3 readability pass: choice metadata and prices use 15-point draft text,
> while the loyalty rule and next reward use 19-point text and shorter copy. The
> three price rectangles and loyalty-rule rectangle grow temporarily to fit. In
> `Assets/Screenshots/signal_bay_narrow_readability_final_4x3.png`, the revised copy
> remains separate from the live counters; TMP reported no overflow for the three
> prices, loyalty rule or next reward. The full-rectangle/15-point surface audit
> passed. Hiding the draft restored the checked labels' original copy, sizes and
> rectangles, and restored `SignalBayBase` and its colour. The overall proportional
> fit still makes other secondary text small; this is not physical-input validation.
> The preserved 4:3 single-pack synthetic-pointer flow revealed five cards, reopened
> fresh, returned with expected counters, and restored and verified 22 preference keys.
> The compact loyalty rule was subsequently moved right to sit inside its panel:
> `Assets/Screenshots/signal_bay_compact_loyalty_alignment_4x3.png`. TMP reported
> no overflow for the rule or adjacent reward; the full-rectangle/15-point button
> audit passed. Hiding the draft restored the rule's original position and rectangle,
> as well as the raster backdrop and colour. This is a targeted alignment adjustment,
> not proof of physical input or resolution-independent legibility.
> After the adjustment, the preserved 4:3 single-pack synthetic-pointer flow again
> passed reveal, fresh reopen and return, restoring and verifying 22 preference keys.
> The compact pack-choice titles now use 18-point text in wider bounds, positioned
> to retain space for their price or availability line. The three full names remain
> intact and unavailable packs remain marked `NOT AVAILABLE`. TMP reported no
> overflow on the three titles or price/status lines; all 15 button surface samples
> and three full rectangles passed. Preview:
> `Assets/Screenshots/signal_bay_choice_titles_4x3.png`. Hiding the draft restored
> the three titles' original 16-point sizes, text, positions and rectangles, and
> restored `SignalBayBase` and its colour. Other secondary text remains small.
> A preserved 4:3 synthetic-pointer single-pack flow passed reveal, fresh reopen
> and return with expected counters; all 22 preference keys were restored and verified.
> The compact draft's live remaining-pulls caption now uses 20-point higher-contrast
> text in a wider rectangle without replacing its counter or formatter. In
> `Assets/Screenshots/signal_bay_live_loyalty_readability_4x3.png` TMP reported no
> overflow for the displayed value; the full-rectangle/15-point button audit passed.
> Hiding the draft restored the caption's original font size, colour, rectangle and
> position, and restored `SignalBayBase` and its colour. This checks the displayed
> value at 4:3, not every possible screen size or physical input.
> A preserved 4:3 synthetic-pointer single-pack flow passed reveal, fresh reopen and
> return with expected live counters; all 22 preference keys were restored and verified.
> The compact live overlays now reapply their fitted baseline after `UiCounter`/`UiBar`
> entry pops finish, preventing a transient animation scale from becoming the next
> layout baseline. In `Assets/Screenshots/signal_bay_counter_fit_4x3.png`, the credit,
> milestone, remaining-pulls and bar overlays align with the compact panel; their
> settled scales are all the same fitted baseline. The 4:3 button audit passed all
> 15 samples and three full rectangles. Hiding the draft restored all four overlay
> positions/scales and `SignalBayBase`. The preserved flow again passed reveal,
> fresh reopen and return and restored 22 preference keys.
> After the shared manifest and duplicate-audit changes, the normal single- and ten-pack
> pointer flows were rerun: five and 50 cards respectively completed, each reopened fresh,
> and both restored and verified all 22 preference keys. The single-pack return showed
> 2,200 credits, 2/100 progress, and 98 remaining; the ten-pack reopen recorded 20 purchases.
> These flow checks do not independently validate every duplicate-summary line.

### Data-backed pack selection (2026-11-04)

> The editable choices are now runtime `Button`s with decorative accent and label graphics
> excluded from raycasts. `Tools/Offline Gacha/Audit Editable Bay Pack Choices` passed all
> three center raycasts and selection callbacks without leaving `Gacha`:
> Signal Archive → index 0, Frontline Protocol → index 1, Relay Network → index 2.
> `Tools/Offline Gacha/Audit Selected Pack Manifests` passed all three data paths: each
> selection resolves a distinct five-card single manifest and a 50-card bundle, then restores
> the previous selection. The reveal controller now reads `SelectedPackKey` and builds its
> active single/bundle/loyalty queue from that selected manifest; the default archive retains
> the original manifest. This is synthetic EventSystem evidence, not physical mouse proof.
> PlayerPrefs snapshots now include `SelectedPackKey` so preserved verification restores it too.
> The preserved 4:3 single-pack flow was rerun after the pack-selection changes: reveal,
> fresh reopen and return passed; the flow restored and verified 23 preference keys
> (the original 22 plus the selected-pack key). The final editor state is stopped,
> the editable draft alpha is 0, `SignalBayBase` remains assigned, and the Unity
> console has no errors.

### Serialized image dependency inventory (2026-11-04)

Scanned GUID references in all `Assets/Scenes/*.unity` and `Assets/Prefabs/**/*.prefab`
against image-file `.meta` GUIDs under `Assets`. Direct image references found:

| Serialized asset | Direct image references | Status |
| --- | --- | --- |
| `Assets/Scenes/Gacha.unity` | `Assets/UI/Sprites/SignalBayBase.png` (1), `Assets/Resources/VFX/UiWhite.png` (2) | Retain the bay backdrop until the editable replacement passes visual and functional verification. The white sprite supplies UI graphics. |
| `Assets/Scenes/Gameplay.unity` | `Assets/UI/Sprites/ButtonChamfer.png` (15) | Individual button artwork, not a full-screen backdrop. |
| Other top-level scenes and prefabs under `Assets/Prefabs` | No direct references to image-file GUIDs found | This includes `MainMenu.unity` and `MainMenu.prefab`; it is not proof of runtime appearance or complete dependency closure. |

This inventory counts serialized GUID occurrences, not necessarily active rendered objects. It
does not follow prefab, material, font, or other asset dependencies transitively, inspect packages,
or establish what runtime scripts create or load. For example, `BattlefieldHud` loads
`Resources/VFX/UiWhite` at runtime, and the editor bay wiring code can load `SignalBayBase`.
No image assets were removed. Recheck indirect and runtime dependencies before deleting any art;
main-menu regeneration and the bay replacement still require their planned verification.

---

## Phase log

### P0 — Scene split and reveal contract
- `Gacha.unity` is now the **Signal Bay shop** only; the pack reveal moved to its own scene,
  `RevealGacha.unity`. Both are authored by `Overlink/Scenes/Build Reveal Gacha Scene`
  (`Assets/Editor/BuildOfflineGachaScenes.cs`); the bay keeps its approved art pixel-identical —
  dynamic numbers are a *patch + live overlay*: the baked base sprite was re-derived without the
  counters (`Assets/UI/Sprites/SignalBayBase.png`) and live `UiCounter` readouts sit on top.
- Reveal controller state machine: while cards remain the only actions are `REVEAL` and
  `REVEAL ALL`; once the pull is exhausted they are replaced by `SIGNAL BAY` and `OPEN AGAIN`.
  There is deliberately no back button mid-reveal.
- Every bay→reveal entry and every `OPEN AGAIN` is a **fresh resolution** (`OpenAgain()` reloads
  the reveal scene), so counters, bars and the summary can never show a stale pull.
- The audit/summary panel opens automatically when the pull completes.
- Header copy rule: section headers carry one or two accent words (e.g. `SEAL INTACT`) through an
  authored accent field — no explanatory sentences.

### P1 — Counters and the filled-image trap
- Gacha counters (credits, loyalty, revealed cards, bay stock) roll through `UiCounter` instead of
  snapping; a formatter closure owns the surrounding copy so strings never drift.
- Fixed the silent **Filled-Image-no-sprite trap**: a `Image.Type.Filled` widget with no sprite
  renders nothing at all. All bars now share one authored white pixel,
  `Assets/Resources/VFX/UiWhite.png`, loaded through `Resources` so builders and runtime agree.

### P2 — Match HUD meters
- Core durability and NP chips in the battlefield HUD grew live meters (`UiBar`) that hang below
  their chip slabs, fed by the same presenter values the numbers use.

### P3 — Bars, sheen and the bay overlay
- Bar shimmer and button sheen share the authored additive shader `Assets/VFX/Shaders/UiSheen.shader`
  with two material variants (`UiSheen`, `UiSheenWarm` in `Assets/Resources/VFX/`).
- The bay's live overlay nodes (stock + credit readouts, milestone bar) were wired by
  `Overlink/Scenes/Wire Bay Live Counters`.

### P4 — Menus, Settings, Collection polish and the button reconciliation
- **One button widget.** `UiFxButton` (hover pop, press squash, sheen sweep) is now the single
  response for every uGUI Button; the legacy `UiButtonFeedback` was retired and deleted
  (`.cs` + `.meta`). `UiKit`'s button and command-plate builders and the gacha scene builder's
  `CreateButton` add `UiFxButton` directly.
- `UiFxBootstrap` decorates every Button in every loaded scene at runtime (once per domain load,
  re-run per scene load) so authored scenes need no manual wiring. Full-screen dismiss scrims are
  Buttons too and opt out by name convention (`*Scrim*`) — decorating one would scale the whole
  screen and sweep sheen across everything.
- Keyboard/gamepad parity: `UiFxButton` implements `ISelectHandler`/`IDeselectHandler`, so focus
  counts as hover — the one behaviour the old pointer-only kit lacked.
- Motion runs in **unscaled time** (`UiTween` uses `Time.unscaledDeltaTime`), so paused overlays
  and the settings screen stay responsive.
- Main menu: the Profile button was dead (its dialog builder was never called and the authored
  scene has no dialog). `ShowProfilePanel` now lazily builds the ID dialog on first open, wires
  close/copy buttons, and wakes the dialog `Card` with `UiFxPanel`.
- Settings: arrival beat via runtime-attached `UiFxPanel` on `Canvas/SettingsPanel` (no authored
  scene edit); the four-option visual toggle stack was re-spaced (`-135/-190/-245/-300`) so
  `REDUCED MOTION` sits inside the authored slab with the other three.
- Collection binder: the three summary readouts (`Count`, `Fragments`, `Deck`) roll through
  runtime-attached `UiCounter`s — bound at zero in `Awake`, refreshed by value in
  `RefreshFilters`, so filter clicks that change nothing stay still while ownership changes roll.

### P5 — Auth-routing fix, version stamp, docs, final verification
- Version stamp: main-menu `VersionLabel` bumped `v0.18` → `v2026.11.1 Alpha` and restyled to
  footer weight (12 px, right-aligned, rect widened 110 → 260 units, right-anchored so it grows
  into empty corner space); the scene menu's `Footer left` tagline bumped
  `OVERLINK • BUILD 2026.11` → `OVERLINK • v2026.11.1 Alpha`; this report carries the release
  mark.
- **Static-image audit and the MainMenu patch.** The menu the player sees is not live UI: the
  scene's prefab-instance override pointed the prefab's `Background` Image at the Figma mockup
  `Assets/UI/Example/OVERLINK — Main Menu.png`, a full-screen static image with every string —
  including the `NETWORK STABLE • v0.18` the user saw — baked in, while two instance-level
  suppressions (an `m_IsActive: 0` override on the `VersionLabel` GameObject and an added
  alpha-0 `CanvasGroup`) hid the live stamp beneath it. A guid audit of every mockup in
  `Assets/UI/Example` against all scenes/prefabs/assets showed only two static images mounted
  as UI: this backdrop and `Signal bay.png` (already replaced by the P3 patch + live overlay).
  Fixed in the same pattern, in full: `Assets/UI/Sprites/MainMenuBase.png` is derived from the
  mockup with every baked *data* string erased by row-lerped patches (version line
  x1362..1561/y839..860, identity block x1234..1512/y36..81, `[IMAGE SAMPLE]`
  x427..632/y432..466, footer tagline x42..283/y839..860 of the 1600×900 source); the instance's
  `m_Sprite` override re-points at it; and the live layer above it is un-suppressed —
  `VersionLabel` (`m_IsActive` override → 1, CanvasGroup alpha → 1) plus the two scene groups
  that own live data, `Account overlay` and `Lobby footer` (CanvasGroup alpha 0 → 1). The
  account widget needed styling to sit on the art: both action rects widened to 70 units on a
  shared row (labels no longer wrap), the sprite-less `Log-out icon` (white square) hidden, and
  the placeholder status copy `TACTICAL LABEL` replaced with `LINK ESTABLISHED`. The identity
  corner now shows the real profile name from `PlayerProfileService` and working Profile /
  Sign Out buttons where dead pixels used to be.
- **Verification rule for static-image UI:** screens whose visible art is a static image are
  asserted by *live node state*, never by screenshot pixels — the check reads runtime
  `text` / `isActiveAndEnabled` on stamp and counter nodes and the `sprite` reference of the
  patched backdrop (`Background` Image = `MainMenuBase`); stills are secondary evidence only.
- `AuthFlowController.LoadMainMenu` now follows a **keep-rule**: the auth continuation only routes
  onward when the active scene *is* `Auth`. The flow controller is a persistent service and could
  finish its profile request after an editor `scene_load`, kicking a directly opened protected
  scene (Settings, Collection, Gameplay) back to MainMenu mid-verification. Normal boot, sign-in
  and profile setup all originate in `Auth` and are unchanged (verified: Auth play still lands on
  MainMenu; Settings play now stays Settings).
- This report and the final verification run.

---

## The widget kit

`Assets/Scripts/VFX/` — assembly `Overlink.Vfx` (leaf asmdef; references only `UnityEngine.UI` and
`Unity.TextMeshPro`), namespace `Overlink.UI` kept for call-site continuity.

| Component | Job |
| --- | --- |
| `UiTween` | Eased tweens: transform channels plus a per-frame step callback so colours, fills and anchors ride the same curve. Reduced motion snaps to the end state and still fires the step at 1. Unscaled time. |
| `UiVfxGate` | Central accessibility gates (`Motion`, `Flash`, `Shake`) read from the Settings preferences; widgets never branch on PlayerPrefs themselves. |
| `UiCounter` | Rolling integer readout with a formatter closure; overshoot pop on increase; lazy label resolve so early seeds are not dropped. |
| `UiBar` | Filled-image meter on the shared white sprite, with optional shimmer sweep. |
| `UiFxButton` | Hover pop / press squash / sheen sweep for any Button; focus = hover; sheen state lives in a stretched child Image's vertex alpha on the shared material, so no per-button material instances and batching is untouched. |
| `UiFxPanel` | Panel wake: settle from 0.96 scale (plus CanvasGroup fade when present) with overshoot on enable. |
| `UiFxBootstrap` | Runtime decoration of every Button per scene load, with the `Scrim` name opt-out. |
| `UiVfxMaterials` | Shared material access for the sheen variants. |

Authored assets: `Assets/VFX/Shaders/UiSheen.shader`, `Assets/Resources/VFX/UiSheen.mat`,
`Assets/Resources/VFX/UiSheenWarm.mat`, `Assets/Resources/VFX/UiWhite.png`,
`Assets/UI/Sprites/SignalBayBase.png`, `Assets/UI/Sprites/MainMenuBase.png`.

---

## Verification evidence

- **Compile:** 0 errors after every phase.
- **`Tools/Offline Gacha/Verify Scene Bindings`:** PASS — RevealGacha 5 slots, catalogs, 4 rarity
  fixtures, 21 widget refs (cardWidth 1.05); Gacha bay 3 widget refs; Collection 23 widget refs.
- **`BattlefieldHudTests` (EditMode):** 31/31 passed, 0 failed.
- **Legacy widget purge:** GUID `1d9c2e236f7b4d1a9c4e7b5a8f0d3c61` (`UiButtonFeedback`) has zero
  hits repo-wide after rebuilding both builder scenes.
- **Playtests (editor play via the Unity REST bridge):**
  - Main menu — Profile click builds the dialog, `Card` carries `UiFxPanel`, `Scrim` stays
    undecorated, Close/Copy/Matchmaking carry `UiFxButton`, no legacy feedback component.
    Still: `Assets/Screenshots/p4-menu-profile.png`.
  - Collection — summary counters rolled to live values (`GAMEPLAY CARDS 4 / 6 UNIQUE`,
    `DATA FRAGMENTS 5`, `OFFLINE DECK 0 / 30 · COPY LIMIT 3`).
    Still: `Assets/Screenshots/p4-collection.png`.
  - Settings — scene survives the auth continuation, `SettingsPanel` carries `UiFxPanel` and
    settles at scale (1,1,1), all four accessibility toggles inside the slab.
    Still: `Assets/Screenshots/p4-settings.png`.
  - Auth boot — still routes to MainMenu (keep-rule regression check).
  - **Live-node assertions over static-image UI** (`p5-live-text-check`): MainMenu `VersionLabel`
    reads `NETWORK STABLE • v2026.11.1 Alpha`, active with CanvasGroup alpha 1, and the
    `Background` Image's `sprite` resolves to `MainMenuBase` (patched backdrop, baked `v0.18`
    line erased); `Footer left` reads `OVERLINK • v2026.11.1 Alpha`. The un-suppressed live
    layer asserts too: `Account overlay` and `Lobby footer` CanvasGroups at alpha 1,
    `Player name` carries the real profile name, `Profile action` / `Sign out action` Buttons
    enabled. Bay: `Credit Live` /
    `Milestone Number Live` / `Remaining Live` all carry `UiCounter`, credit rolls to `2,400`.
    Collection: `Count` / `Fragments` / `Deck` (path-selected under
    `Offline Collection Canvas/CollectionScreen`) read `GAMEPLAY CARDS 4 / 6 UNIQUE`,
    `DATA FRAGMENTS 5`, `OFFLINE DECK 0 / 30 · COPY LIMIT 3`. Settings: `SettingsPanel` carries
    `UiFxPanel`. Stills: `Assets/Screenshots/p5-menu-version.png` (full frame, no baked version
    line) plus its bottom-right corner crop showing the live stamp.
- **Bay / reveal flow stills:** `gacha-bay-shop.png`, `gacha-reveal-from-bay.png`,
  `gacha-reveal-fresh-entry.png`, `gacha-reveal-open-again.png`, `gacha-bay-return.png`
  (captured by `Tools/Offline Gacha/Capture Bay To Reveal Flow` and
  `Tools/Offline Gacha/Capture Reveal Sequence`); HUD meters: `hud-meters.png`.

## Known caveats (accepted, not regressions)

- Console noise at boot: `Material 'UiSheen' ... doesn't have a texture property '_MainTex'` plus
  `_Stencil` warnings. The sheen shader is intentionally minimal (additive, no UGUI sprite path);
  the warnings come from Unity's standard-property probe and are harmless.
- Diagonal cyan bands across binder card captures are the **pre-existing animated foil sweep** on
  runtime sleeves caught at different `_Time` phases (older stills show the same diagonals thin);
  the full-width band at the top of screen captures is the intended header accent. All 16 binder
  `Sheen` nodes read `enabled=false, color.a=0` at rest.
- The Settings/Collection visual toggles are runtime-built fallbacks for authored scenes that
  predate them; their values persist and apply through the same handlers as authored controls.
- The MainMenu backdrop remains the Figma mockup as a pure art layer: its baked *design copy*
  (top-left network branding, featured-unit flavor block, button plates and icons) still shows,
  and the decorative inline groups (`Scene designation`, signal rules, `Link signature`,
  `Title and commands`, scrims/fades) stay alpha-suppressed because the art already carries
  their look. Every baked *data* string (version, player identity, footer tagline,
  `[IMAGE SAMPLE]`) is erased and replaced by live nodes. The erased featured-unit art slot is
  intentionally empty until a live art pass fills it.
