# Card Manager / Frame Template Plan — Placeholder Phase

**Date:** 2026-10-25  
**Status:** Proposed implementation plan; final card roster and final card balance are explicitly out of scope.

## 1. Objective

Revise the Unity card presentation path so the current phase can display the new high-fidelity
card-frame template while continuing to use placeholder card names, art, stats, and ability
text. Establish the runtime seam that can later consume the final card roster without
rewriting hand interaction or gameplay identity handling.

This phase proves **editor workflow, prefab composition, shader treatment, and presentation**,
not final content.

Terminology note: the runtime currently calls the lowest rarity `Common`; Master refers to
that tier as **Basic**. Until the final naming decision is made, use `Common` in enums and
serialized data, display `Basic` in the editor/template UI only if approved, and do not add
a second rarity enum value.

## 2. Current architecture — verified

There is no single `CardManager` class today. Card responsibilities are split as follows:

| Current component | Responsibility | Placeholder-phase rule |
|---|---|---|
| `DeckController` | Queue and discard of `CardData` | Keep; do not replace with final roster |
| `HandController` | Create, reuse, lay out, drag, and remove `CardView` objects | Keep interaction behavior; add face refresh hook |
| `GameCoordinator` | `CardInstance` ↔ load-bearing `CardData` identity map; play routing | Keep identity map and gameplay routing |
| `CardView` | Cube body plus temporary TMP title/cost/description | Replace visual body incrementally; preserve public identity and tween surface |
| `CardData` | Per-instance hand adapter and reference-identity key | **Keep; do not delete or add value equality** |
| `CardDefinition` / `CardInstance` | Authored definition and mutable match state | Future source for real face values; not the placeholder roster |
| `CardAsset` | ScriptableObject authoring path | Extend with template/presentation references; do not author the final set now |
| `CardManagerWindow` | Existing Editor authoring/validation/export window | Extend; keep it as the single card-authoring entry point; fixture mode remains separate from gameplay data |
| `PrototypeCardPool` | 20-card FSTI gameplay test pool | Keep only as a gameplay/test fixture; do not treat as final content |

Verified project version: Unity `2022.3.62f3`. No `Temp\UnityLockfile` is currently
present, but no Unity Editor execution or import verification has been performed in this
plan.

## 3. Target architecture

### 3.1 Keep gameplay identity separate from face presentation

Do not make the new frame system the owner of gameplay identity. `CardData` remains the
reference-identity adapter used by `GameCoordinator.instanceByData` and `HandController`.
The frame system receives a presentation model through an explicit bridge; it must not try
to recover a `CardInstance` by card ID or by searching the hand.

**Required bridge, preserving current call sites:**

```text
GameCoordinator.GetOrMakeData(CardInstance)
  ├─ keeps existing CardData id/title/cost/description/accent values
  ├─ stores the source CardDefinition and CardInstance on that same CardData object
  └─ keeps dataByInstance + instanceByData reference identity unchanged
             │
             ▼
HandController.SetHand(IReadOnlyList<CardData>)
             │
             ▼
CardView.Create(CardData, parent, size)
             │
             ▼
CardFacePresenter.Build(CardData)
  ├─ if CardData has source instance/definition: uses those values
  └─ otherwise: returns explicit placeholder face data
             │
             ▼
        CardFaceView
       ├── CardFrameRenderer
       ├── CardArtSurface
       └── CardTextAndStats
```

Add source references to `CardData` without changing its existing constructor or equality
semantics, for example internal/public read-only `Definition` and `Instance` properties
set by `GameCoordinator` when it creates the adapter. Do not add value-based
`Equals`/`GetHashCode`. Existing manually-created `CardData` remains valid and receives
named placeholder face data until a richer source is supplied.

For this phase, the adapter supplies placeholder values by default. Later, it reads the
final `CardDefinition` and live `CardInstance` values without changing the frame renderer.

### 3.2 Placeholder content is a named mode

Create an explicit `Placeholder` presentation mode rather than silently treating the
current prototype pool as final content.

Placeholder mode may use:

- `Card Name` / `Unit` / `Ability` / `Relay` / `Protocol` labels
- dummy cost values
- dummy ATK / HP / Shield values for Unit frames
- a neutral placeholder art block or a selected test art
- short sample ability text
- FSTI/Common as the default skin, plus an inspector/debug selector for testing all
  type/faction/rarity frame variants

These values are visual fixtures. They must not be used to make balance, roster, or
collection promises.

### 3.3 Template matrix and prefab ownership

Create reusable prefab templates by **card type × rarity tier**:

| Type | Basic/Common | Rare | Epic | Legendary |
|---|---|---|---|---|
| Unit | `Card_Unit_Common.prefab` | `Card_Unit_Rare.prefab` | `Card_Unit_Epic.prefab` | `Card_Unit_Legendary.prefab` |
| Ability | `Card_Ability_Common.prefab` | `Card_Ability_Rare.prefab` | `Card_Ability_Epic.prefab` | `Card_Ability_Legendary.prefab` |
| Relay | `Card_Relay_Common.prefab` | `Card_Relay_Rare.prefab` | `Card_Relay_Epic.prefab` | `Card_Relay_Legendary.prefab` |
| Protocol | `Card_Protocol_Common.prefab` | `Card_Protocol_Rare.prefab` | `Card_Protocol_Epic.prefab` | `Card_Protocol_Legendary.prefab` |

This is **16 base prefabs**, not one prefab per final card. A card instance selects the
prefab by `CardType` and `Rarity`, then supplies its image and values. Faction should be a
skin/variant selected by the frame catalog and material parameters, not a second 48-prefab
matrix. Add faction-specific prefab variants only if the visual design later demonstrates
that a skin cannot provide the needed hierarchy.

Each prefab exposes named, replaceable fields:

- `ArtImage` / art surface
- `FrameImage` / frame overlay
- `CostValue`, `NameValue`, `TypeValue`
- Unit only: `AtkValue`, `HpValue`, `ShieldValue`
- non-Unit only: no combat-stat objects in the hierarchy
- `AbilityValue`, `IdValue`, and optional `FactionMark`
- optional `RarityEffectTarget`

The prefab owns layout and presentation components. Card data supplies values. A prefab
must not contain a final card identity, final artwork, or balance values.

### 3.4 CardTemplateCatalog and runtime prefab selection

Add a serialized runtime `CardTemplateCatalog` ScriptableObject mapping:

```text
CardType × Rarity → Card template prefab
```

It contains exactly 16 entries for the type×tier matrix above, plus one explicit neutral
fallback prefab. Each entry stores the prefab reference and its key; the editor validates
one-and-only-one entry per key and validates the prefab's declared type/tier metadata.
Faction is not part of this lookup because faction is supplied to the frame catalog and
skin/material parameters.

The runtime creation path is explicit:

```text
CardView.Create(CardData, parent, size, frameCatalog, templateCatalog)
  → CardFacePresenter.Build(CardData)
  → templateCatalog.Get(CardFaceData.Type, CardFaceData.Rarity)
  → Object.Instantiate(templatePrefab, parent)
  → bind ArtImage, FrameImage, and value fields
  → attach/preserve CardView interaction root, collider, and tween shell
```

The compatibility overload without catalogs uses the documented placeholder template and
frame fallbacks. The normal `HandController` path carries serialized references to both
catalogs. `CardView` must not reconstruct a prefab from a string path or directly select a
hard-coded type layout. Missing template keys log the exact `CardType` and `Rarity`, then use
the neutral fallback; they never silently revert to the old cube while template mode is on.

The instantiated prefab is a presentation instance only. It receives image and value data
from `CardFacePresenter`; it does not write those values back into `CardAsset`, gameplay
`CardDefinition`, or `CardInstance`.

### 3.5 Placeholder fixture storage

Keep placeholder values in a dedicated `CardPresentationFixture` ScriptableObject or an
explicitly marked presentation-fixture asset, separate from `CardAsset` gameplay data. It
contains only temporary display fields (sample name, cost, ability text, sample Unit stats,
art sprite, faction, rarity, and type). It must live under a clearly named placeholder or
editor-fixtures folder and must not be included in the final card roster/export path.

`CardManagerWindow` edits fixture values only when its selected mode is `Placeholder`; it
must show a visible `PLACEHOLDER FIXTURE` indicator and must not serialize those edits into
`CardAsset` gameplay properties. The runtime placeholder adapter reads this fixture by an
explicit serialized reference. If no fixture is assigned, it uses immutable code defaults
and reports the missing reference. Tests must prove fixture edits do not mutate a CardAsset.

### 3.6 Tier shader and material ownership

Basic/Common uses the plain frame material with **no custom tier shader**. Create one
custom shader/material treatment for each non-Basic tier:

| Tier | Shader/material | Intended treatment |
|---|---|---|
| Rare | `Overlink/CardTier/Rare` | restrained accent line or bounded glow |
| Epic | `Overlink/CardTier/Epic` | stronger edge energy/ornament, bounded glow |
| Legendary | `Overlink/CardTier/Legendary` | premium frame treatment, controlled bloom/iridescence |

Use ShaderLab or Shader Graph according to the project's installed package support; do not
add a package solely for this placeholder phase. Keep inputs explicit and bounded:
`_AccentColor`, `_Intensity`, `_Speed`, `_NoiseScale`, `_EmissionStrength`, and an enable
flag. The shader operates on the frame overlay/material, not the readable text or art.

Rare/Epic/Legendary materials are separate assets, not runtime-created hidden materials.
Each has a static fallback for low-end/WebGL and disabled-motion settings. `CardTierMaterialSet`
(or equivalent serialized configuration) must expose the custom material and its static
fallback per tier, plus the selection rule: use the custom material only when the renderer
supports the shader and tier effects are enabled; otherwise use that tier's static fallback.
No shader encodes card identity or gameplay state, and effects must not bleed outside the
832×1216 card bounds.

### 3.7 Proposed component responsibilities

#### `CardFaceView` — runtime MonoBehaviour

Owns the visual hierarchy for one card face:

- frame overlay sprite
- art surface behind the frame
- placeholder/live name, cost, type, stats, ability, and ID text
- frame selection refresh
- sorting order between art, frame, and text

It must not own play legality, deck identity, or mutable combat state.

#### `CardFrameCatalog` — serialized runtime lookup

Maps:

```text
CardType × Faction × Rarity → frame Sprite
```

The PNG folder remains the authoring/import location:

```text
Assets/Cards/Frame/{Type}/{FACTION}_{Rarity}.png
```

Runtime must **not** call `Resources.Load` on `Assets/...` paths and must not assume an
arbitrary asset path is loadable. Use a serialized `CardFrameCatalog` ScriptableObject
containing 48 entries (`CardType`, `Faction`, `Rarity`, `Sprite`) plus one neutral fallback
Sprite. An editor-only menu action scans the folder, normalizes the filename faction token
(`FSTI`/`FRTI`/`FPB` → `Faction.Fsti`/`Frti`/`Fpb`), assigns Sprite references, validates
one-and-only-one entry per key, and saves the catalog asset.

Pass the catalog explicitly into the runtime-created face: `HandController` holds a
serialized `CardFrameCatalog` reference and passes it through the `CardView.Create` path;
retain the existing overload as a compatibility fallback that uses a documented placeholder
catalog. Do not use a hidden singleton or a string-only lookup.

The catalog must fail visibly and safely when a frame is missing: log the exact key and use
the neutral fallback, never silently produce an invisible card.

#### `CardFacePresenter` — adapter/presentation model

Converts either:

- placeholder fixture data in this phase, or
- `CardInstance` + `CardDefinition` in the final-content phase

into a face model. It owns no Unity hierarchy and is testable without the Editor.

#### Existing `CardView`

Remain the interaction/tween shell initially. It hosts `CardFaceView` as a child and
forwards refresh calls to it. Preserve:

- `CardView.Data`
- `CardView.Create(CardData, Transform, Vector3)` compatibility overload
- `CardTween`
- root collider and hand drag/drop behavior
- reference identity semantics

The current `CardView.Setup(CardData)` label path must be changed explicitly: remove or
stop calling its `MakeLabel("Title"/"Cost"/"Desc")` calls once `CardFaceView` owns those
fields. Do not leave both paths active. Keep the old cube body only as a temporary fallback
behind a named placeholder flag; it must not render underneath an opaque frame and produce
duplicate visuals.

## 4. Card Manager Editor workflow

`CardManagerWindow` remains the single authoring workflow for `CardAsset` files. Its existing
filters, serialized-property editing, Undo behavior, validation, composition summary, and
export flow remain intact.

Add a **Presentation Template** section to the selected-card detail:

- preview type, tier, and faction skin
- select/replace art image
- select/replace frame sprite through the catalog
- edit placeholder display values without changing final-roster assumptions
- select the resolved type×tier prefab
- show the resolved tier material/shader
- warn when a Unit has no stat zones or a non-Unit has combat-stat objects
- warn when Basic/Common has a custom tier shader
- preview the selected card in a four-tier comparison panel

Add a **Template Matrix** panel:

- show the 16 required type×tier prefabs and their status
- show the 48 frame-catalog entries and their status
- show four tier materials: Basic/Common plain, Rare custom, Epic custom, Legendary custom
- create/update only missing or explicitly selected templates
- never overwrite a user-edited prefab silently; require explicit update confirmation
- report duplicate keys, missing sprites, missing materials, missing shader references, and
  wrong type hierarchy

Add menu/window actions named `Create/Update Template Prefabs`, `Validate Template Matrix`,
`Preview Tier Materials`, and `Refresh Frame Catalog`. Editor code belongs under the existing
Editor assembly; runtime catalog, template metadata, and face components stay in runtime
assemblies. Assign references through Unity APIs and save through `AssetDatabase`; do not
hand-edit prefab YAML or fabricate `.meta` files.

## 5. Placeholder-phase scope

### In scope

1. Extend the existing `CardManagerWindow`; do not create a parallel card-authoring window.
2. Add its template-matrix controls and non-destructive prefab generation/validation actions.
3. Generate and validate the 16 type×tier prefabs; Basic/Common uses the plain material,
   while Rare/Epic/Legendary use their explicit tier materials.
4. Import the 48 frame PNGs as 2D/UI sprites with explicit importer settings.
5. Add a serialized `CardFrameCatalog` asset and an editor scanner/populator; do not use
   arbitrary `Assets/...` runtime paths or an unassigned string lookup.
6. Extend `CardData` with source references without changing its constructor, identity, or
   equality behavior.
7. Update `GameCoordinator.GetOrMakeData` to attach the existing `CardDefinition` and
   `CardInstance` to the same cached `CardData` object.
8. Add a placeholder face model and presenter with an explicit fallback for old/manual
   `CardData` objects.
9. Pass the catalog through `HandController.CreateView` → `CardView.Create`.
10. Render the frame, placeholder art surface, and placeholder text in the existing hand.
11. Remove/disable the old `CardView.MakeLabel` path when `CardFaceView` owns those fields.
12. Preserve existing hand interaction, tweening, colliders, and card identity.
13. Add a debug/editor selector to preview all four types, three factions, and four rarities.
14. Verify Unit frames structurally show stats and Ability/Relay/Protocol frames do not.
15. Add tests for lookup normalization, missing-frame fallback, template lookup/instantiation,
    bridge binding, prefab matrix completeness, Basic/Common no-shader behavior, placeholder
    fixture isolation, and placeholder field binding.

### Explicitly out of scope

- final card list, final names, final stats, final abilities, final faction distribution
- final rarity distribution or collection economy
- replacing `CardData`
- deleting `PrototypeCardPool`
- changing gameplay rules or card legality
- keyword-chip design or final keyword presentation
- full-art card implementation beyond keeping the art surface as a replaceable layer
- network/database card synchronization
- final art assets
- release/build claims

## 6. Data contract for the placeholder face

The presentation model should contain only fields the frame needs:

```text
CardFaceData
  Id                 string
  DisplayName        string
  Type               CardType
  Faction            Faction
  Rarity             Rarity
  Cost               int
  ArtSprite          Sprite?      // placeholder art in this phase
  Atk                int?         // present only for Unit
  Hp                 int?          // present only for Unit
  Shield             int?          // present only for Unit
  AbilityText        string
```

Rules:

- Non-Unit frames must have structurally absent combat-stat fields, not zeroed fake stats.
- The placeholder adapter may provide sample Unit stats only when `Type == Unit`.
- Final live stats must later come from `CardInstance.CurrentAtk`, `CurrentHp`, and
  `CurrentShield`; printed definition stats are not sufficient after buffs/damage.
- `CardData` remains the identity key and is not the final face data contract.

## 7. Import, prefab, and compositing contract

### Import settings

For every frame and placeholder art PNG:

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single`
- Alpha is Transparency: enabled
- Mip Maps: disabled
- Compression: `None` for frame overlays
- Filter Mode: `Bilinear` unless pixel-art testing requires Point
- Wrap Mode: `Clamp`
- Pixels Per Unit: one documented project value, used consistently

Unity-generated `.meta` files are expected after import. They must be reviewed as editor
outputs, not hand-authored.

### Draw order

The intended order is:

```text
1. placeholder/live art surface
2. frame PNG overlay (alpha preserved)
3. runtime text and live stats
4. interaction/selection highlight
```

The frame must be above art because the delivered frame geometry includes intentional
chrome overlapping the top-left of the art window. Text must remain separate from the PNG
so it can change, localize, and later bind to live values.

## 8. Implementation sequence

### Step A — Editor/import setup and manager extension

- Extend `CardManagerWindow`; verify its existing list, filters, validation, save, and export
  behavior remains intact.
- Add the non-destructive Template Matrix panel before generating any prefabs.
- Confirm the Editor is not already holding the project before any batchmode action.
- Import the frame folder through the Editor or a one-shot explicit editor utility.
- Verify all 48 files are present as sprites and have the required settings.
- Do not hand-edit Unity YAML or fabricate `.meta` files.

### Step B — Presentation model, catalogs, fixture, and tier materials

- Add the placeholder face model and adapter under the existing runtime assembly that owns
  card presentation (`Overlink.Cards` unless inspection proves another owner is better).
- Add serialized `CardFrameCatalog` and `CardTemplateCatalog` lookup with neutral fallbacks.
- Add the isolated `CardPresentationFixture` asset and an explicit serialized reference to
  the runtime placeholder adapter.
- Keep editor-only preview utilities under `Overlink.Editor`.

### Step C — Prefab templates, face hierarchy, and call-site migration

- Add serialized `CardFrameCatalog`, `CardTemplateCatalog`, and placeholder-fixture
  references to `HandController` or its presentation configuration, and pass them from
  `HandController.CreateView` into a new `CardView.Create` overload; retain the old overload
  for callers/tests that do not provide catalogs.
- In `CardView.Create`, preserve the interaction root/collider/tween shell, ask
  `CardTemplateCatalog` for the type×rarity prefab, instantiate it, and create/bind
  `CardFaceView` with the `CardData` bridge and both catalogs.
- Preserve the existing root collider and tween shell.
- Remove/disable the old `CardView.Setup` calls to `MakeLabel("Title"/"Cost"/"Desc")`;
  `CardFaceView` becomes the only owner of those visual fields.
- Replace the cube body only after the face renders correctly; if retained temporarily,
  disable its renderer when `CardFaceView` is active.
- Keep art surface, frame, and runtime labels as separate children.

**Call-site checklist:**

1. `GameCoordinator.GetOrMakeData(CardInstance)` attaches the source definition/instance
   to the existing identity-stable `CardData`.
2. `HandController.SetHand` remains unchanged in its identity behavior.
3. `HandController.CreateView` passes the serialized frame catalog, template catalog, and
   fixture/config reference to `CardView.Create`.
4. `CardView.Create` resolves and instantiates the type×rarity prefab, then keeps the old
   overload compatible through documented fallbacks.
5. `CardView.Setup` no longer creates a second set of Title/Cost/Desc labels.
6. `CardFacePresenter.Build(CardData)` uses live instance stats when a source exists and
   reads the isolated fixture only for explicit placeholder mode.

### Step D — Placeholder preview and editor preview

- Default to one neutral test card, not the final pool.
- Add a debug selector for all frame keys.
- Confirm Unit and non-Unit structural differences.
- Confirm long placeholder text uses a visible bounded policy: auto-fit with a minimum
  readable size, then a warning; never silently ellipsize during this phase.

### Step E — Tests, validation, and handoff

- Unit-test frame-key normalization and fallback.
- Unit-test that non-Unit placeholder faces expose no combat-stat zones.
- Verify existing CardData identity and hand reuse tests remain valid.
- Run Editor compilation and play-mode/manual checks only after the user authorizes the
  state-changing Unity step; do not claim them from static inspection.

## 9. Acceptance criteria

The placeholder phase is complete when:

- A hand card displays a real frame sprite instead of the cube.
- Placeholder art appears below the frame and does not cover frame chrome.
- Placeholder text appears above the frame and does not require baked text in PNGs.
- A frame can be selected by Type/Faction/Rarity without changing gameplay identity.
- All 48 frame keys resolve, or missing keys produce the documented fallback/log.
- The Template Matrix reports all 16 type×tier prefabs and required references, and the
  runtime `CardTemplateCatalog` resolves each key to the corresponding prefab.
- Basic/Common prefabs use no custom tier shader.
- Rare, Epic, and Legendary prefabs each resolve their own explicit custom material/shader,
  with the documented static fallback selection path.
- Prefab fields allow image/value replacement without changing prefab hierarchy.
- Placeholder fixture edits cannot mutate `CardAsset` gameplay data or enter final export.
- Unit and non-Unit templates have structurally different stat zones.
- Existing drag, hover, tween, collider, play, and duplicate-card identity behavior is
  preserved.
- The implementation contains no claim that the current pool is the final game roster.
- The final card-data adapter seam is documented and ready for later replacement.

## 10. Decisions still needed before implementation

1. **Tier naming:** display the lowest tier as `Basic` while serializing it as
   `Rarity.Common`, or display `Common` everywhere?
2. **Prefab scope:** approve 16 base prefabs (type×tier) with faction as a skin/catalog
   selection, rather than 48 faction-specific prefab variants.
3. **Preview control:** default FSTI/Common/Unit only, or expose a visible debug selector in
   the scene and Card Manager?
4. **Placeholder art:** use a flat neutral fill, one supplied test illustration, or a
   generated placeholder sprite?
5. **Frame size:** preserve the current hand `CardSize` during the first integration, or
   change the physical card ratio to 832:1216 (`1.0 × 1.4615`) immediately?
6. **Shader style:** static Rare/Epic/Legendary effects first, or bounded animation in the
   placeholder phase?
7. **Text ownership:** confirm labels/stat values remain runtime text and are not duplicated
   by baked PNG labels.
8. **Implementation permission:** approve the editor import, prefab generation, shader/material
   assets, and runtime code changes as a separate execution step after this plan.

Until these are answered, this document is the implementation boundary — not a claim that
the frame system is already wired or that Unity has compiled it.
