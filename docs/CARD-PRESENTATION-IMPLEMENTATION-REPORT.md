# Card Presentation Placeholder Implementation Report

Date: 2026-10-25

## Scope

Implemented the placeholder card presentation path described by the card-manager phase plan while preserving the existing gameplay card identity and hand interaction seams.

## Runtime implementation

- Added immutable face projection in `Assets/Scripts/Cards/CardFaceData.cs`.
- Added source/fixture projection in `Assets/Scripts/Cards/CardFacePresenter.cs`.
- Added isolated placeholder fixture asset type in `Assets/Scripts/Cards/CardPresentationFixture.cs`.
- Added `CardFaceView` for frame, art, text, ability, faction, and conditional combat-stat presentation.
- Unit faces expose ATK, HP, and Shield; non-Unit faces receive no combat-stat values and hide the combat-stat root.
- Added serialized frame and template catalogs with neutral fallback behavior.
- Added rarity material-set data and bounded tier shader support.
- Preserved `CardData` references, legacy constructors, `CardView.Create` compatibility overloads, root collider, `CardTween`, drag, hover, and hand reuse behavior.
- Frame source assets and the displayed card remain 832×1216 portrait geometry (`1.0 × 1.4615`) for **every** tier; the old Legendary `5:7` root size silently squashed the frame canvas by ~4.6% and is gone. Standard art is cover-fit (cropped, never stretched) into its measured window and clipped by a `SpriteMask`; Legendary is the **full-art** tier, so its art fills the whole canvas unmasked under the translucent frame overlay. See “Legendary layout and resolution pass” and “Full-art restoration, sorting slices and iridescence pass” below.
- Attached live `CardDefinition`/`CardInstance` sources in `GameCoordinator` without changing `CardData` identity semantics.

## Editor implementation

- Added `CardPresentationEditor` actions to refresh frame imports/catalogs, create the placeholder fixture, assign art from `Assets/Cards/Test_art`, generate the 16 template prefabs, generate tier materials, validate the matrix, and wire gameplay references.
- Added visible placeholder/template information to `CardManagerWindow`.
- Generated and validated 48 frame entries and 16 type-by-rarity template entries through Unity Editor APIs.
- No arbitrary runtime `Resources.Load` path was added.

## Dedicated preview scene

Added `Assets/Editor/BuildCardPresentationPreviewScene.cs` and menu action:

`Overlink > Scenes > Build Card Presentation Preview`

The generated `Assets/Scenes/CardPresentationPreview.unity`:

- Has its own camera, lighting, hand, deck, anchors, catalogs, and placeholder fixture using `overlink_creature_00045_basic.png` from `Assets/Cards/Test_art`.
- Starts with five cards and keeps Space/Backspace/drag interaction available.
- Displays an explicit `LOGIN BYPASS` label.
- Contains no `ProtectedSceneGuard`, `AuthFlowController`, or login dependency.
- Is registered as `CardPresentationPreview` by `SceneRegistrar`.

## Verification

- Unity compilation: no errors after the presentation implementation refresh.
- Frame/template validation: 48 frames and 16 templates reported valid.
- Existing gameplay scene was wired to the generated catalogs and fixture.
- Dedicated preview smoke capture completed with five rendered cards, Test_art visible, login bypass active, and zero runtime errors (`Assets/Screenshots/card-presentation-preview-bypass-final.png`).
- Logged-in MainMenu → Play navigation smoke test reached `Gameplay` with the cached profile active and zero runtime errors (`Assets/Screenshots/gameplay-logged-in-final.png`).
- The Unity Test Runner startup was unreliable during the earlier run and timed out before producing totals; this is tracked separately from compilation and runtime smoke validation.

## Legendary layout and resolution pass

Two defects reported after the first pass — "the legendary one still messed up the
layouting" and soft/low-resolution cards — were diagnosed by capturing real Play Mode Game
View pixels over the Unity REST API instead of re-reading the code.

**Legendary was never a full-art frame.** Row/column ink profiling of
`FSTI_Legendary.png` showed a *separate zone map* on the same canvas: art window
x40..792 / y56..719 (753×664 instead of Standard's 700×385), name banner moved to y720..823,
ATK bar to y869..928, ability panel to y949..1130. The previous code contained Legendary art
across the whole canvas and disabled the mask, so the art painted over the name, ATK and
ability panels while Standard anchors were still applied to Legendary text. Both tier zone
tables now live in `CardFaceView.FaceZones` and are the single source of truth for runtime
layout and for generated templates via the shared `CardFaceView.StyleLabel`.

> **Retracted (2026-10-26).** The sentence above is wrong about Legendary not being full-art,
> and the "art window" it measures does not exist. See *Full-art restoration, sorting slices and
> iridescence pass* below. What survived the correction is the second half: Legendary really does
> use a separate zone map, and Standard anchors really were being applied to it.

**Softness was framing plus text rasterisation, not import settings.** The preview camera at
`orthographicSize 3.1` drew a 1216px frame texture into ~190 screen pixels (6.4× downsample);
it is now 1.35 (~585px per card, 2.1×). Separately, every card label was authored at 3–9pt,
so TextMeshPro rasterised a handful of pixels per glyph and then scaled them up. Labels now
render at a fixed 96pt atlas with the transform scaled down — with `isOrthographic = true`
world height is `fontSize × scale`, so apparent size and rasterisation size are independent.
The header labels in the preview scene use the same technique and are positioned from the
camera's actual orthographic bounds rather than hard-coded world Y.

Measured geometry and the full before/after table are recorded in `docs/CARD-FRAME-SPEC.md` §11.

Verification for this pass:

- Compilation clean; Unity console returned zero errors after every rebuild.
- `Overlink/Cards/Validate Template Matrix`: 48 frames, 16 templates valid, no missing scripts.
- 8 s Play Mode capture of `CardPresentationLegendaryPreview.unity`: zero runtime errors, no
  redirect to Main Menu (`Assets/Screenshots/leg_v4.png`).
- 8 s Play Mode capture of `CardPresentationPreview.unity`: zero runtime errors, Standard
  faces unchanged in structure and now crisp (`Assets/Screenshots/std_v4.png`).
- Generated template prefabs carry the atlas-scaled text transforms and the Legendary
  mask/window sizes, confirming prefab mode matches runtime rather than drifting.

## Full-art restoration, sorting slices and iridescence pass (2026-10-26)

Four defects reported after the previous pass, all fixed against the delivered reference sheets
in `Assets/Cards/Example`:

**Legendary is full-art after all.** `Full Art Example.png` shows edge-to-edge art with the
frame's rails and text plates floating *on top* of it. The earlier "art window" was an artefact
of treating `alpha < 24` as open canvas: `FSTI_Legendary.png` is a translucent *overlay*
(alpha ≈1 outside its plates, ≈47 on them), so the region that profiled as a window is simply
where the art is meant to show. `FaceZones.Legendary` now spans the whole 832×1216 canvas with
`FullArt = true`; `ApplyLayout` hides the `SpriteMask` and sets `maskInteraction = None` for
that tier. Legendary art is authored at exactly the canvas aspect, so the cover fit is 1:1 with
zero crop.

**Card stacking is now one sorting slice per card.** Sorting order is global, so with every card
using art −10 / frame 0 / text 20, one card's *labels* drew across its neighbour's *frame* — the
bleed visible when the fan overlapped and when a card was inspected. `CardFaceView` now keeps
those as local offsets added to a per-card `sortBaseline`, and `HandController` assigns slices:
resting `(i+1)×100`, discard 50, hover 10000, drag 10100, inspect 10200, play 10300, preview
header 20000. `LayoutAll` skips the baseline for a focused card so a mid-inspect relayout cannot
drop it back under its neighbours. `ConfigureArtMask` no longer hardcodes the mask's orders.

**The tier shaders enhance rather than replace the art.** They previously added `_AccentColor`
across the whole surface weighted only by alpha, which flattened Legendary to orange. The rail
glow is gated by the sprite's alpha and its own luminance, so only the existing bright rails
catch light. The real cause of the stubborn tint was in the materials, not the shader: all three
had a coloured `_Color` serialized in, and `_Color` is `[PerRendererData]`, so the sprite was
tinted *before* any effect ran. All three are now white.

**Legendary is neutral iridescent foil, not gold — and it composites premultiplied.**
`_AccentColor` is a cool near-white passing through a low-amplitude cosine thin-film palette
driven by sweep position and surface luminance, so the shimmer drifts silver → cyan → violet →
rose; `_Iridescence 0` collapses it to plain silver. The first version of this added its light to
RGB while returning the overlay's original alpha, and `SrcAlpha` blending then multiplied that
light by ~0.2 (plates) or ~0.004 (open canvas) — the effect computed but was invisible. The
shader now accumulates light premultiplied in screen space, raises alpha wherever the foil sweep
or a dust mote lands (`_FoilOpacity`), and divides the source RGB back out by that new alpha, so
the shimmer reads at full strength while pixels the effect never touches remain bit-exact base.
After reviewing the collection gallery, the treatments were swapped: Rare now uses only a restrained
neutral stamped foil band; Epic uses the neutral prismatic-crackle holo pattern instead of the earlier
vertical stripe grating. The crackle is gated to frame ink so no grey shards leak over the artwork;
neither tier stacks both treatments. Glowing dust is two drifting
cell-noise layers of soft twinkling motes. Full details:
`docs/CARD-FRAME-SPEC.md` §13 addendum.

**Faction text removed; Attack label made per-tier.** Every delivered frame bakes its faction
icon into the top-right corner, so the `FactionMark` label duplicated the art and has been
removed from `FaceZones`, `CardFaceView` and the template generator. Conversely Standard's ATK
bar bakes only the arrow icon while Legendary's bakes the word "Attack", so `AtkLabel` is an
*optional* zone — present and left-aligned on Standard, absent (zero-width ⇒ hidden) on
Legendary — and `LabelZone.Align` now carries the per-tier anchoring the sheets show (value
right-anchored on Legendary, HP/Shield values beside their icons on both).

Verification for this pass:

- Compilation clean; all three tier shaders report `hasErrors: false`.
- `Overlink/Cards/Create/Rebuild Clean Template Prefabs` then `Validate Template Matrix`: 48
  frames, 16 templates, zero errors, zero `m_Script: {fileID: 0}`.
- 4 s Play Mode capture of `CardPresentationLegendaryPreview.unity`: zero runtime errors, no
  redirect (`Assets/Screenshots/leg_irid.png`).
- 4 s Play Mode capture of `CardPresentationPreview.unity`: zero runtime errors
  (`Assets/Screenshots/std_attack.png`).

## Follow-up

Final verification should continue with manual pointer interaction in `CardPresentationPreview.unity` if desired; the automated runtime checks already confirmed five faces render, the fixture art binds, login bypass remains active, and the logged-in protected gameplay route reaches `Gameplay`.

The `ArtFocusY` crop bias (0.4) is a heuristic for the current portrait test art. Once real
art arrives it should become an authored per-card field, since the interesting region varies
shot to shot.
