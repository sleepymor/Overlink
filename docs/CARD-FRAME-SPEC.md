# Card Frame Spec â€” measured geometry

**Source:** `D:\OverlinkLowFidFrame\Card - Unit.png` and `Card - Protocol-Abilty-Relay.png`
**Measured:** 2026-10-22, pixel-exact (see method note at end)
**Canvas:** 832 Ã— 1216 px, ratio 0.6842
**Status:** Frame design imported into Unity as editable presentation templates; geometry remains portrait 832 Ã— 1216.

---

## 1. Canvas and rails

| Element | Value | Notes |
|---|---|---|
| Canvas | 832 Ã— 1216 | ratio 0.6842 |
| Outer black border | 25 px all sides | `#000000` |
| White frame rail | 41 px | `#FFFFFF`, inside the black border |
| Content box | x66..765, y66..1183 | 700 Ã— 1118 |

The content box is where every zone lives. Nothing bleeds past x765 or y1183 except the
cost orb, which sits in the top-left rail.

---

## 2. Zone map â€” Unit frame

Coordinates are absolute pixels on the 832 Ã— 1216 canvas.

| # | Zone | Label text | Geometry | Position |
|---|---|---|---|---|
| 1 | Cost | `Cost` | 80 Ã— 80 circle, x26..105 y26..105 | top-left rail |
| 2 | Factions | `Factions` | text only, no box | top-right |
| 3 | Art | `16:10 Image` | 700 Ã— 385, x66..765 y66..450 | top-centre |
| 4 | Name | `Name` | 343 Ã— 126, x244..586 y451..576 | centred |
| 5 | Type | `Type` | 224 Ã— 17 | centred, below Name |
| 6 | HP + Shield | `HP + Shield` | 634 Ã— 83, x99..732 y661..743 | panel 1 |
| 7 | ATK | `ATK` | 634 Ã— 83, x99..732 y774..856 | panel 2 |
| 8 | Ability | `Ability` | 634 Ã— 210, x99..732 y887..1096 | panel 3 |
| 9 | ID + Rarity | `ID + Rarity` | 157 Ã— 17, x99..255 y1152..1168 | bottom-left |

### Vertical rhythm

```
y26   â”Œ â¬¤Cost 80Ã—80                    Factions â”
y66   â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
y450  â”‚        Art 700Ã—385 (1.818)               â”‚
y451  â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
y576  â”‚        Name 343Ã—126 (red bar)            â”‚
      â”‚        Type 224Ã—17                       â”‚
y661  â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
y743  â”‚        HP + Shield 634Ã—83                â”‚
y774  â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
y856  â”‚        ATK 634Ã—83                        â”‚
y887  â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
y1096 â”‚        Ability 634Ã—210                   â”‚
y1152 â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
y1168 â”‚  ID + Rarity 157Ã—17                      â”‚
y1183 â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

---

## 3. The two frame variants

Both files are 832 Ã— 1216 and **94% identical**. The only difference is the text
architecture below the name banner:

| Variant | Zones below Name | Region |
|---|---|---|
| **Unit** | 3 stacked panels â€” HP+Shield, ATK, Ability | y649..1096 |
| **Ability / Relay / Protocol** | 1 merged block | y649..1096 |

Everything above y649 and below y1096 is byte-identical between the two files.

---

## 4. Field coverage

| `CardDefinition` field | Zone | Covered |
|---|---|---|
| `Cost` | Cost | âœ… |
| `Faction` | Factions | âœ… |
| `Name` | Name | âœ… |
| `Type` | Type | âœ… |
| `Atk` | ATK | âœ… |
| `Hp` | HP + Shield | âœ… |
| `Shield` | HP + Shield | âœ… |
| `Rarity` | ID + Rarity | âœ… |
| `Id` | ID + Rarity | âœ… |
| `AbilityText` | Ability | âœ… |
| `ArtPath` | 16:10 Image | âœ… |
| `Keywords` | â€” | **excluded by design (2026-10-22)** |
| `DesignNote` | â€” | correctly absent |
| `Effects` | â€” | correctly absent |

**Decision 2026-10-22:** keyword chips are not part of the card face. Every pool card still
carries `Keywords` data â€” it is simply not rendered. Keyword meaning is conveyed through
`AbilityText` prose (e.g. `"Piercing. Ignores Shield when dealing damage."`).

Consequence to keep in mind: 6 of the 15 keywords change combat maths in ways the stat
numbers do not show â€” `Piercing`, `FirstStrike`, `Lifesteal`, `CannotBlock`,
`SummoningDelay`, `Shield`. With no chips, a player reading only ATK/HP cannot see them.
If that becomes a comprehension problem in playtest, the cheapest fix is an icon strip
overlaid on the art window's bottom edge, which costs no layout space.

---

## 5. Live values, not printed values

The frame must bind to `CardInstance`, not `CardDefinition`, wherever the value can change
during a match.

| Zone | Bind to | Why |
|---|---|---|
| ATK | `card.CurrentAtk` | buffs, debuffs, node bonus |
| HP + Shield | `card.CurrentHp`, `card.CurrentShield` | damage, healing, shields |
| Name / Cost / Type / Rarity / Id | `card.Definition.*` | static |
| Ability | `card.Definition.AbilityText` | static |
| Art | `card.Definition.ArtPath` | static |

Reference implementation for the HP display convention: `BattlefieldPresenter.CardDetails`
(`BattlefieldPresenter.cs:1387â€“1452`) prints `CurrentHp + "/" + def.Hp`, and shows
`NodeAtkBonus` / `NodeHpBonus` as parenthetical modifiers when non-zero.

---

## 6. Art window ratio â€” 1.818 vs 1.600

| Thing | Ratio |
|---|---|
| Art window (700 Ã— 385) | **1.818** |
| Generation target (1024 Ã— 640) | **1.600** |

The window is wider than the generated art, so fitting 1.6 into 1.818 crops roughly **12%**
of the art vertically.

Two clean options:

- **Change the window** to 700 Ã— 437 (true 16:10) â€” costs 52px, must come out of the
  Name/Type/panel stack.
- **Change generation** to 1024 Ã— 563 (1.818) â€” no layout change, matches the window exactly.

The label `16:10 Image` inside the window is currently inaccurate; the window is not 16:10.

---

## 7. Space warnings

**ID + Rarity is 157 Ã— 17 px.** Rendering `card_fsti_001` plus a rarity marker in that box
is tight â€” roughly 5pt at 832px width. Consider widening to ~220px (there is free space to
the right, the band is empty from x256 to x765) or raising to ~24px tall.

**The bottom band y1097..1190 (700 Ã— 94) is empty** except the ID+Rarity box. That is
available space if any zone needs to grow.

---

## 8. Unity integration â€” nothing is wired

The frame exists only as two PNGs in `D:\OverlinkLowFidFrame\`. Verified state:

| Check | Result |
|---|---|
| Frame PNG under `Assets\` | **absent** â€” zero textures in the project outside screenshots |
| `SpriteRenderer` in Battlefield.prefab | **0** |
| `UI.Image` in Battlefield.prefab | **0** |
| `m_Sprite` / `m_Texture` refs | **0** |
| Anything reading `ArtPath` | **nothing** â€” all 4 pool factories set `ArtPath = string.Empty` |
| `CardView` visual | a coloured **cube**; `Setup` builds only Title / Cost / Desc labels |

`CardView.CardSize` is currently `1.0 Ã— 1.4` (0.714) â€” close to the frame's 0.684 but not
equal. Set it from the frame: 832/1216 gives `1.0 Ã— 1.4615`.

Steps required, none done:

1. Copy both PNGs to `Assets\Art\Cards\`, Texture Type = **Sprite (2D and UI)**
2. Add a sprite surface to `CardView` (replace or overlay the cube body)
3. Map `ArtPath` â†’ sprite at runtime; populate the field in `PrototypeCardPool`
4. Rewire `CardView` from the 5-field `CardData` to `CardDefinition` + `CardInstance`

`CardData` is load-bearing (identity key in `GameCoordinator.instanceByData`, keeps
duplicate copies distinct) â€” migrate, do not delete.

---

## 9. Method note

Geometry was measured by scanning the PNGs pixel-by-pixel: exact colour histograms, per-row
run-length segmentation, and connected-component analysis of non-fill regions. Zone labels
were confirmed with a vision pass (`view_image`).

An earlier pass sampled rows at 16px stride and mis-read the three stat/ability panels as
blank text boxes, concluding stats were absent. That was wrong â€” the panels are HP+Shield,
ATK, and Ability. The corrected reading is above.

---

## 10. Delivered hi-fi geometry â€” measured 2026-10-25 (PROVISIONAL)

> **Status: provisional test asset.** Master confirmed on 2026-10-25 that all cards and frames
> created to this point are tests and placeholders. The measurements below are accurate for
> the test assets that shipped, but they are **not** an implementation contract and must not
> be treated as final geometry. Re-measure against the real assets when they exist.
>
> This section therefore does **not** supersede Â§2. Â§2 describes design intent; this section
> describes one test build. Where they disagree, neither is authoritative yet.

Measured from `Assets\Cards\Example` (the four review examples, Common rarity). The
wireframe described intent; these are the pixels that shipped in the test build.

| Element | Measured | Notes |
|---|---|---|
| Canvas | 832 Ã— 1216 | confirmed on every frame and every example |
| Art window | x66..765, y66..450 (700 Ã— 385) | placeholder fill was exactly `R68 G68 B68` |
| Name row | y451..~576 | name text occupies y483..550 |
| **HP number** | **x115..142, y485..510** | **left of the name banner** |
| **Shield number** | **x619..699, y485..510** | **right of the name banner** |
| Name text | x252..493, y500..525 | "Vanguard" |
| Type label | y618..640 | "Unit" |
| **ATK bar** | **y800..829** | number + arrow icon, x129..198 |
| Ability panel | y887..1097 (210 tall) | text block y934..1076 |
| ID string | y1154..1164, x102..178 | `card_fsti_003` |

### Correction to Â§2

Â§2 places HP + Shield in a dedicated panel at **y661..743**, below the name banner. The
delivered frame does not do this. HP and Shield sit in **boxes flanking the name banner**,
on the same row (y485..510). Only ATK gets its own bar (y800..829).

Practical consequence: the name banner is **narrower than 343px** in the shipped design
because HP and Shield boxes consume the row's left and right ends. Do not build to 343px
centred if you are matching the delivered assets.

### Rarity visibility â€” measured (test build only)

Pixel diff between rarity variants of the same frame:

| Pair | Differing pixels | Region |
|---|---|---|
| Common â†’ Rare | ~1,806 | x34..796, y34..1180 |
| Rare â†’ Epic | ~1,171 | x40..792, y42..1176 |
| Epic â†’ Legendary | ~187,406 | full frame |

Common, Rare and Epic differ by roughly **0.2â€“0.3%** of frame pixels, spread thinly across
the whole border. At hand size (~220px tall) that is not perceptible. Legendary is a
complete redesign and is instantly obvious.

So in the test build there are **two** visible rarities, not four. Worth carrying into the
real assets: if rarity should matter in play, Rare and Epic need real contrast.

### Ability text fit â€” measured (test build only)

In the delivered example the ability text spans y934..1076 â€” **143px of the 210px panel
(68%)** â€” for a 39-character string over 3 lines.

The longest string in `PrototypeCardPool` is 88 characters:

> Occupy a Node slot. While present, enemy units deployed to that Node cannot be blocked.

That is ~2.3Ã— the example. At the same line height it needs roughly 7 lines, which exceeds
the panel. Either the ability panel needs auto-shrink, or long-text cards need a shorter
prose pass.

## 11. Standard and Legendary are different layouts â€” measured 2026-10-25, corrected 2026-10-26

The Epic â†’ Legendary pixel diff in Â§10 was not a recolour. Legendary is a **separate zone
map on the same 832 Ã— 1216 canvas**, and the reason the Legendary card kept looking broken
while Standard looked fine is that the code applied Standard anchors to a Legendary frame.

An earlier revision of this section concluded "Legendary is not a full-art frame". **That
conclusion was wrong and is retracted here.** The profiling that produced it counted pixels
with `alpha < 24` as "open canvas", so it measured the frame's large *transparent* region and
reported it as an art window. In fact `FSTI_Legendary.png` is a **translucent overlay**: its
alpha is â‰ˆ1 outside the plates and â‰ˆ47 on them, so there is no window at all â€” the art runs
edge-to-edge underneath and the overlay's panels are scrimmed so text stays legible on top of
it. `docs/FIGMA-HIGH-FIDELITY-PROMPT.md` ("a Legendary card drops the art window â€¦ translucent
scrim") and `Assets/Cards/Example/Full Art Example.png` both say the same.

Corrected measurement, taken from the frame's **opaque plate geometry** and the baked ink
inside it, then cross-checked against both reference sheets:

| Element | Standard | Legendary |
|---|---|---|
| Art | window x66..765, y66..450 (700 Ã— 385), masked | **full canvas, no mask** |
| Frame alpha | 255 below y460 (opaque body) | â‰ˆ1 open / â‰ˆ47 on plates (overlay) |
| Cost gem | x20..165, y16..165 | x28..153, y14..160 |
| Health plate | x101..226, y453..536 | x99..228, y720..794 |
| Name plate | x257..573, y454..566 | x244..586, y723..823 |
| Shield plate | x605..730, y453..536 | x603..732, y720..794 |
| Type label | own band, y~638 | below the plate, on the art, y~846 |
| ATK bar | x108..730, y776..854 â€” **icon only** | x99..732, y869..928 â€” **icon + baked "Attack"** |
| Ability | x108..723, y896..1087 â€” no caption | x99..732, y949..1130 â€” baked "Ability" caption |
| ID | on the rule, x341..490 y1164..1168 | bottom-left, y~1152 |

Four consequences worth keeping:

1. **Legendary is the full-art tier.** Art fills the canvas and the overlay draws on top; no
   crop window, no `SpriteMask`. Painting art *under* the panels is the design, not a bug.
2. **The stat plates are icon-over-caption, so values sit beside the icon.** Both tiers bake
   the icon at y-centre ~497 (Standard) / ~755 (Legendary) with the "Health"/"Shield" caption
   underneath, leaving no room below. The value goes on the icon row, to the icon's right.
3. **Only Standard needs a text "Attack" label.** Standard's bar bakes the arrow icon alone, so
   the face spells the word out; Legendary's bar already contains it. This is why `AtkLabel` is
   an optional zone (empty â‡’ hidden) rather than a label on both tiers.
4. **The ATK value is right-anchored on Legendary, left-anchored text on Standard.** The
   full-art sheet puts the number against the far rail; the non-full-art sheet puts it beside
   the icon. `LabelZone.Align` carries that per-tier.

Portrait art in the Standard window must be **cover**-fit with the crop biased upward
(`ArtFocusY` 0.4) so a face is not cut off. Legendary needs neither: its art is authored at
exactly 832 Ã— 1216, so the cover fit is 1:1 with zero excess.

### Implementation note

`CardFaceView.FaceZones.Standard` / `.Legendary` now hold both tables as the single source
of truth. `CardFaceView.StyleLabel` is shared with `CardPresentationEditor`, so generated
templates are laid out by the same code that runs at play time.

### Resolution was a framing bug, not an import bug

Cards looked pixelated because the preview camera at `orthographicSize 3.1` drew a 1216px
frame texture into ~190 screen pixels â€” a 6.4Ã— downsample. Import settings were already
correct. Two changes fixed it:

- Preview camera to `orthographicSize 1.35` (~585px per card at 1080p, a 2.1Ã— downsample).
- Text now renders its TMP atlas at a **fixed 96pt** and shrinks the transform, instead of
  being authored at 3â€“9pt. With `isOrthographic = true` a glyph's world height is
  `fontSize Ã— scale`, so apparent size and rasterisation size are independent. The old
  3â€“9pt labels rasterised a handful of pixels tall and then got scaled up â€” that was the
  actual smearing, and it was invisible while the cards were too small to notice.

**Correction, 2026-10-27:** "Import settings were already correct" was true only of the *preview*
scenes this section was written about, and it held because raising the orthographic camera to 1.35
took the downsample to 2.1Ã—, where bilinear is merely soft. It is **not** true of the shipped hand,
which cannot escape a ~5â€“6Ã— downsample however the lens is set, because a card that small on screen
is the whole point of a hand of five. There the missing mip chain *was* the bug: `mipmapCount == 1`
at 6.4:1 shatters the frame rails into dashes. Both card importers now enable mips and `aniso: 4`.
See the addendum at the end of Â§13. The lesson generalises: a texture whose minification is fixed by
moving the camera is not evidence the import settings are right, only that the artifact was hidden.

## 12. One sorting slice per card - the stacking contract

`SpriteRenderer.sortingOrder` and TMP's `sortingOrder` are **global** within a sorting
layer, so a hand of overlapping cards cannot rely on a fixed per-child order. With art at -10,
frame at 0 and text at 20, card A's *labels* drew across card B's *frame*: ability text and
name strings bled over the neighbour's art and rails. Depth does not rescue this either - the
tier shaders use `ZTest Always`, matching `Sprites/Default`, because 2D sprites are ordered
by sorting rather than by z.

The fix is a **per-card baseline**. `CardFaceView` keeps the child offsets local
(`ArtOffset` -10, `FrameOffset` 0, `MaskFrontOffset` -9, `MaskBackOffset` -11,
`TextOffset` 20) and adds them to one `sortBaseline`, set through
`CardFaceView.SetSortBaseline`. `HandController` owns the policy:

| State | Slice |
|---|---|
| Resting slot *i* | `(i + 1) * 100` |
| Sending to discard | 50 (behind the whole hand) |
| Hovered | 10000 |
| Dragged | 10100 |
| Held-zoom / inspected | 10200 |
| Playing / resolving | 10300 |
| Preview scene header text | 20000 |

Stride 100 leaves room for the local offsets; the focus slices sit far above the hand so a
lifted card clears every neighbour. `LayoutAll` deliberately skips the baseline for a focused
card, otherwise a relayout mid-inspect would drop it back under its neighbours.

Two rules for anything that touches a card's renderers: never hardcode a child's order
(`ConfigureArtMask` used to, which let a neighbour's art leak through), and keep the preview
header above 10300 because the play-slot card can reach into the header band. `CreateText` carried a
literal `20` for years and survived only because `ApplyRenderLayers` overwrites every child on each
bind - it now derives `sortBaseline + TextOffset` like its siblings, because a label created after
that pass would otherwise inherit the constant and draw beneath every neighbouring card.

## 13. Tier shaders enhance the art; they never replace it

The first tier shaders added `_AccentColor` across the whole surface weighted only by alpha,
which turned a Legendary card flat orange and destroyed the artwork. The contract now is:

- **The sprite's own colour is the image.** Effects only *add* light.
- **The rail glow is gated by the sprite's alpha and its own luminance**
  (`smoothstep(_GlowThreshold, 1, lum)`), so the existing bright rails are what catch light
  and the dark plates stay dark.
- **Foil is the exception, and it is honest about it.** A stamped-foil sweep covers the art too,
  so the Legendary facet and dust layers are *not* alpha-gated - they buy their own opacity
  where they land (`_FoilOpacity`) instead of hiding behind the overlay's ~0.2 alpha. See the
  addendum below for why anything less is literally invisible.
- **`_Color` stays white.** It is `[PerRendererData]`, and all three materials previously had
  a tinted `_Color` serialized in - that tinted the sprite before any effect ran, which is why
  the accent looked unfixable from the shader alone.

Legendary is deliberately **neutral and iridescent**, not gold: `_AccentColor` is a cool
near-white (0.86, 0.88, 0.94) passing through a low-amplitude cosine thin-film palette, so the
shimmer drifts silver -> cyan -> violet -> rose with sweep position and surface luminance.
`_Iridescence 0` collapses it to plain silver. The tier signatures are deliberately separated:
Rare uses a restrained iridescent stamped foil band with no crackle layer; Epic uses the
**prismatic** crackle holo - saturated spectrum on the shard faces and crack rims, neutral bloom;
Legendary uses the strongest neutral foil sweep plus glowing dust over full-bleed art. None of the
three recolours the card: hue lives in the additive light, never in `_Color`.

### Addendum: why the first iridescent pass was invisible - measured 2026-10-26

The rewrite that made Legendary neutral and iridescent still produced no visible shimmer. The
bug was in the compositing maths, not the palette: the frame is a translucent overlay (alpha ~47
on its plates, ~1 on the open canvas), and with `Blend SrcAlpha OneMinusSrcAlpha` the GPU
multiplies fragment RGB by fragment **alpha** before it reaches the screen. Light added to RGB
and returned with the original alpha is attenuated ~250x over the art - present in the buffer,
gone to the eye. Two rules follow:

1. **Accumulate light premultiplied, then divide back out.** Compute the screen-space
   contribution (`base.rgb * a + accent * amount`), raise alpha wherever foil or dust lands
   (`outA = a + facet * _FoilOpacity + dust * 0.85`), and emit `screen / outA`. Where
   nothing is added the result is exactly the base pixel, so the art is still untouched.
2. **Foil is stamped over the art, so it must not be alpha-gated.** The facet sweep and the
   dust layers cover the whole card; only the rail glow keeps the alpha/luminance gate.

Dust is two cell-noise layers (density `_NoiseScale`, finer layer at 2.3x) with a soft disc
around a random point per cell and a per-cell twinkle clock, so it reads as glinting motes
rather than a checkerboard. Verified by 5s Play Mode capture of
`Assets/Scenes/CardPresentationLegendaryPreview.unity` with zero runtime errors:
`Assets/Screenshots/final_legendary.png`.

### Addendum: the orange relapse and the invisible Rare/Epic foil - measured 2026-10-26

Both defects came back the moment `Overlink/Cards/Preview Tier Materials` was run again, and
the cause was the editor helper, not the shaders. `FindOrFallbackMaterial` deleted and
recreated `Rare.mat` / `Epic.mat` / `Legendary.mat` from scratch, seeding the new material with
`color = fallback.color` - i.e. the *static fallback's* flat tier swatch (`(1, 0.55, 0.12)` for
Legendary). That lands in `_Color`, which multiplies the sprite before a single effect term
runs, so the Legendary frame went flat orange again and every tuned property on the custom
materials was reset to shader defaults: Rare and Epic came back with `_Iridescence 0` and
`_HoloOpacity/_FoilOpacity` at 0.02-0.06, which is why their shimmer was literally gone rather
than merely weak.

Two rules now hold:

1. **`_Color` on a custom tier material is white, unconditionally.** It is
   `[PerRendererData]` sprite-tint plumbing, not a tier identity. `FindOrFallbackMaterial` no
   longer recreates the asset: it reuses the existing material, swaps the shader only when it
   differs, and forces `_Color` white and `_EffectEnabled` 1. Re-running the menu refreshes the
   wiring without flattening the finish.
2. **The flat swatch colour belongs to the fallback only.** `CardTier_*_Static.mat` keeps its
   tinted `_Color` on purpose - that is the whole point of a no-shader fallback. The moment it
   is copied onto a tier shader it becomes a wash over the art.

Tier signature values as shipped (all three `_Color = white`):

| Tier | Shader file | Declared name | Finish | The terms that must be visible |
|---|---|---|---|---|
| Rare | `Assets/Shaders/CardTierRare.shader` | `Overlink/CardTier/Rare` | stamped iridescent foil band | `_FacetStrength 0.4`, `_FoilOpacity 0.18`, `_Iridescence 0.7`, `_Intensity 0.75` |
| Epic | `Assets/Shaders/CardTierEpic.shader` | `Overlink/CardTier/Epic` | **prismatic** facet-mesh holo | `_HoloStrength 0.45`, `_HoloOpacity 0.14`, `_PrismStrength 0.68`, `_PrismSat 0.62`, `_FacetSharp 6.5`, `_CrackleScale 14`, `_Intensity 0.64` |
| Legendary | `Assets/Shaders/CardTierLegendary.shader` | `Overlink/CardTier/Legendary` | foil sweep + glinting dust | `_FacetStrength 0.9`, `_FoilOpacity 0.5`, `_SparkleStrength 1.4`, `_Iridescence 0.85` |

The alpha each tier buys for its light (`_HoloOpacity` / `_FoilOpacity`) is what makes the
shimmer survive `SrcAlpha` over the translucent plates: at 0.02 the added light is attenuated
~50x and the effect is present in the buffer but invisible on screen.

### Naming trap - RESOLVED 2026-10-26 (was: the tier shaders were cross-wired)

`CardTier.shader` declared `Overlink/CardTier/Rare` while holding the crackle holo and being
referenced by **Epic.mat**, and `CardTierEpic.shader` declared `Overlink/CardTier/Epic` while
holding the foil band and being referenced by **Rare.mat**. It cost two debugging passes: one
tuning table was written from file names with Rare and Epic swapped, and the pass after that
"tuned" Epic through `_FacetStrength`, a property its shader never declared.

Fixed by renaming each file to the tier it actually serves and correcting the declaration
string. The `.meta` files moved with them, so the guids are unchanged and the material
references survived untouched:

| Now | guid | was |
|---|---|---|
| `CardTierEpic.shader` / `Overlink/CardTier/Epic` | `68600cce46102e1498872a78200c4a97` | `CardTier.shader` / `Overlink/CardTier/Rare` |
| `CardTierRare.shader` / `Overlink/CardTier/Rare` | `4950968b847b94745b48169a9e5922d1` | `CardTierEpic.shader` / `Overlink/CardTier/Epic` |
| `CardTierLegendary.shader` / `Overlink/CardTier/Legendary` | `2b1b5cebd93acf34d8d37657fbef8f8d` | unchanged |

Nothing referenced these by asset path (checked: no `.shader` path appears in any scene,
prefab, material or `ProjectSettings/`), and `CardFaceView.ApplyCardMaterial` only uses
`Shader.Find("Overlink/CardTier/" + rarity)` as a *capability probe*, assigning the material
asset from `CardTierMaterialSet.Resolve` - so the rename is inert at runtime. The durable rule
survives the fix: **to find out how a tier looks, follow `CardTierMaterialSet.asset` → material
→ `m_Shader` guid → shader file**, and verify that the material's serialized properties are
actually declared by that shader before trusting a tune.

### Prismatic Epic - added 2026-10-26, facet mesh the same day

Epic splits light into a spectrum. First pass used the jittered-Voronoi shard tessellation and
full-amplitude `Spectrum()`; it was reported as *too smooth, too intense, too big, and not
triangular enough*, which turned out to be three separate causes:

1. **Smooth.** `facing` was computed from the per-pixel `light` ramp, so brightness varied
   *inside* every shard - a gradient sweeping across a continuous field, i.e. an oil slick. Real
   prismatic foil is a surface of discrete mirrors: each one is a single flat colour that
   switches as a unit. Fixed by sampling everything per facet and driving the flash from a
   narrow Gaussian in the wrapped distance to the peak (`_FacetSharp` is the inverse width), so
   a facet sits dark, crosses a threshold, and snaps on.
2. **Not triangular.** Voronoi cells are convex blobs with rounded boundaries. Replaced with
   `SampleFacets`: a sheared quad grid, each quad split by a per-cell randomly chosen diagonal,
   corners jittered off-grid from *integer lattice ids* so adjacent quads agree on every shared
   vertex and the mesh stays watertight. Result: irregular triangles of varying size and angle.
3. **Too intense / too big.** `Spectrum()` is a full-amplitude palettogram, so `_PrismStrength`
   alone was a saturation control and 0.85 read as Christmas foil. Split into two knobs -
   `_PrismSat` desaturates the film toward white *before* it is mixed, `_PrismStrength` blends
   the result against the neutral `ThinFilm` tint - and `_CrackleScale` 9 -> 14 for roughly half
   the facet area. `key = 0.35 + 0.65 * saturate(lum * 1.6)` weights added light by the pixel
   underneath, so a flash on dark frame ink stays dim instead of floating as a coloured sticker.
4. **Then: more colour, less crackle.** Reported as still too much, but the hue had been tamed
   too far in the same pass. Amount and chroma are independent axes, so this cut the amount and
   raised the saturation: `_Intensity` 0.8 -> 0.64 is a straight 80% on every term at once (it
   feeds `on`, which gates modulation, facet glints, crack rims and bloom together),
   `_CrackleContrast` 0.3 -> 0.22 cuts the structural modulation a further ~27% on top of that,
   and `_PrismSat` / `_PrismStrength` 0.4 / 0.5 -> 0.62 / 0.68 push the surviving light back
   toward the spectrum. Net: a quieter mesh carrying richer hue.

Rule for future tuning: **change the amount in one place.** `_Intensity` scales every term
simultaneously, so trimming `_HoloStrength`, `_EdgeGlow` and `_EmissionStrength` alongside it
compounds - three 20% cuts read as a 50% cut and the finish vanishes. Keep those three at their
relative weights and move `_Intensity` for loudness, `_CrackleContrast` for how much the mesh
shows in the print, `_PrismSat` for chroma, `_FacetSharp` for how many facets are lit at once,
and `_CrackleScale` for facet size. Beware `_CrackleScale` above ~18: crack rims approach one
pixel at hand size and start to alias.

Constraints kept from §13: hue rides on the additive terms only (the mean-preserving `modulation`
stays achromatic, so a facet cannot recolour the art beneath it); crack rims, facet faces and the
rail bloom take three separated spectrum phases (`hue`, `hue + 0.22`, `hue + 0.11` at 0.55 mix);
everything remains gated by `smoothstep(0.02, 0.22, a)` so the transparent art window takes no
facet silhouettes; alpha lifts only where light lands.

`Epic.mat` was also stripped of five orphan properties (`_FacetWidth`, `_FacetStrength`,
`_FoilOpacity`, `_NoiseScale`, `_SheenWidth`) that this shader does not declare - silently
ignored by Unity, which is how the previous pass ended up tuning Epic with values that never
reached a fragment. `Rare.mat` lost its seven crackle orphans the same way. Both materials now
serialize exactly the properties their shader declares.

### Frame matrix previews: every faction and card type - added 2026-10-26

The frame catalog holds 48 sprites (4 card types x 3 factions x 4 rarities), but every preview
scene was built from a fixture hardcoded to `Faction.Fsti` / `CardType.Unit`, so 36 of those 48
frames were only ever visible inside a match. `CardPresentationEditor.CreateTierFixture` now takes
the full rarity / faction / type triple, and `FixturePathFor` addresses fixtures by that triple
while preserving the historical spellings for FSTI Unit (`CardPresentationFixture.asset`,
`..._Rare.asset`, ...) so existing scene and template references survive untouched; other axes
append in a fixed order - rarity, then faction, then type - e.g.
`..._Epic_FRTI_Relay.asset`.

Scene menu, all under `Overlink > Scenes`:

| Item | Writes | Shows |
|---|---|---|
| Build Card Collection Preview | `CardCollectionPreview.unity` | FSTI, 4 tiers, one row |
| Build All Faction Collections | + `CardCollectionPreview_FRTI/_FPB.unity` | one 4-tier row per faction |
| Build Faction Matrix Preview | `CardFrameMatrixPreview_Unit.unity` | 3 faction rows x 4 tiers, Unit |
| Build All Frame Matrix Previews | + `_Ability`, `_Relay`, `_Protocol` | the same grid per type |

All 48 frames do not fit one viewport legibly - 12 cards at readable size is already the limit at
ortho 2.05 - so the type axis became a **scene** axis rather than two more rows of postage stamps.
The matrix uses cells 0.80 x 1.22 with 0.66-wide cards; the per-faction rows keep the original
1.16 x 1.7 cells at full size, so a shader can be judged there and frame identity compared in the
matrix. Column order is Common, Rare, Epic, Legendary in both, so a tier reads at the same x
either way.

`PreviewFraming` carries the per-scene camera/gallery numbers (defaults reproduce the original
full-size four-tier row exactly, so existing callers are unaffected), and
`CardPresentationPreviewScene` derives its header text and per-card captions from the fixtures the
gallery actually holds rather than a serialized label - the header cannot then disagree with the
contents, and a shared type/faction is inferred from `galleryFixtures[0]`.

`SceneRegistrar` gained an `optional` flag on its scene table. The generated previews are absent
on a fresh clone, and the old code aborted the whole build-settings registration on any missing
scene, so a missing preview blocked unrelated builds. Optional entries now warn and are skipped.

### Addendum: the Gallery refactor took the fan down with it - fixed 2026-10-26

Adding `HandLayoutMode.Gallery` meant every placement had to become mode-aware, so the twelve
inline `ComputeSlot(...)` calls in `HandController` were folded into one dispatcher,
`SlotFor(i, out pos, out rot)`. The fan branch of that dispatcher was written as `SlotFor(i, ...)`
instead of `ComputeSlot(i, cards.Count, fanSpacing, arcHeight, maxYaw, handY, ...)` - it called
itself.

This compiles clean and is invisible to every static check, because a method may recurse. It is
also not a visual glitch: .NET cannot recover from a stack overflow, so any fan layout - drawing a
card, hovering, releasing a drag, `LayoutAll` on scene start - killed the player or editor process
outright rather than mispositioning a card.

The rule this is a reminder of: when a dispatcher replaces direct calls to a target, the branch for
the *existing* behaviour must call the target, not the dispatcher. Mechanical call-site rewrites
need a grep of the dispatcher body for its own name before they are trusted.

### Addendum: a per-tier size override is a layout lie - fixed 2026-10-26

`CardView.SizeFor(data, fixture, fallback)` returned `LegendaryCardSize` whenever the rarity was
Legendary and the caller's `cardSize` otherwise. That was defensible when Legendary was genuinely
a different aspect; once every frame moved to the shared 832x1216 canvas the constant was set equal
to `CardSize` and the function became a branch that does nothing except *ignore the caller*.

It stayed invisible for two reasons: the fan scenes all set `cardSize` to exactly `CardSize`, so the
branch returned the same vector either way. The frame matrix was the first caller that laid out at a
smaller footprint, and there one of the twelve cards refused to shrink. Because
`ComputeGallerySlot` spaces cells by `galleryCellWidth`, the oversized Legendary did not merely look
wrong, it sat on top of its own caption and both of its neighbours.

Removed rather than patched: a per-tier size that is not actually a different size has no reason to
shadow the caller's footprint, and `HandController.CreateView` now passes `cardSize` straight
through. The general hazard is the equality itself - a constant aliasing another constant looks
like intent and reads as harmless in review, so the branch that consumes it survives long after the
reason for it went away.

### Addendum: captions must not read a tweening transform - fixed 2026-10-26

`CreateGalleryCaptions` placed each label under `card.transform.position`. `PopulateGallery` calls
`LayoutAll()` first, which starts a tween from each card's spawn origin, and only then snaps the
transforms to their slots - so the live tween keeps moving the cards afterwards and the caption was
a snapshot of flight progress rather than of the layout.

The signature is what made this worth writing down: the top row's captions sat too *low*, the bottom
row's too *high*, and the middle row's looked correct. A row whose slot coincides with the origin is
right by luck, so a partial screenshot reads as a sporadic font problem rather than a systematic
race, and the bug survives review of the frame it was captured in.

Captions now recompute their cell through `HandController.ComputeGallerySlot`, the same pure
function the layout uses, so the label is anchored to the grid rather than to whatever the tween is
doing. Any decoration derived from a card's position must do the same.

### Addendum: the auth bypass list redirected every generated preview - fixed 2026-10-26

`AuthFlowController` keeps a list of standalone presentation scenes and skips the authentication
flow for them, because the boot hook would otherwise bounce an Editor-opened preview to the main
menu. It was an exact-match array of three names, with a comment saying to add new previews to it.

Six scenes were generated after that comment was written and none were added, so all six bounced:
the capture came back as a 35 KB screenshot of the main menu instead of the ~550 KB of a rendered
matrix. The failure was only visible in the image, since Play mode itself reported no error, no
warning, and a clean exit - the guard did exactly what it is designed to do.

Now matched by prefix (`CardPresentation`, `CardCollectionPreview`, `CardFrameMatrixPreview`) so a
generated family cannot be forgotten by construction. All five call sites funnel through
`IsCardPreviewScene`, and `ProtectedSceneGuard` holds no list of its own - it only reacts to
`AuthFlowController` state - so this remains the single place to look. Screenshot *size* is a useful
cheap canary for exactly this class of silent redirect.

### Addendum: TMP auto-sizing deletes the width you just set - fixed 2026-10-26

Every preview label comes from one `CreateLabel` in `CardPresentationPreviewScene`. It set a rect
width from the cell, enabled word wrapping, and also enabled `enableAutoSizing`. Those two fight:
auto-sizing resizes the RectTransform to the text's *preferred* width, so the `sizeDelta` is thrown
away and wrapping has nothing left to wrap against. A caption like `FRTI • LEGENDARY` simply drew at
whatever width it wanted and ran into the neighbouring column. In the matrix all twelve captions sat
in a 0.80-wide cell, so the bleed was continuous and worst in the right-hand columns.

Auto-sizing was also the only reason the font ever looked right. `scale = worldEm / atlasSize` at
`fontSize = atlasSize` already renders the exact intended em, so auto-sizing could only ever shrink
it toward `fontSizeMin` - it was quietly rendering the captions smaller than designed, which is why
the matrix read as uniform postage stamps rather than as three tiers of type.

Removing it exposed a second, independent bug that auto-sizing had been hiding. The rect was a fixed
`worldEm * 3.2` with `TextAlignmentOptions.Center`, so *every* label reserved 3.2 em of vertical
space regardless of how many lines it actually held: the two-line header and the one-line subtitle
overlapped each other, and the two-line caption under the last grid row had no reserved room at all
and hung off the bottom of the viewport. Labels are now **top-anchored and sized by their own line
count** - `CreateLabel` takes `lines`, aligns top-centre, and places the block's centre at
`topEdge - blockHeight / 2`, so a label's vertical footprint is computable from its arguments instead
of emergent from TMP's layout pass.

That made the framing constants wrong rather than the labels, and they had to be re-derived from the
same arithmetic the code now uses. The matrix went `orthoSize 2.05 -> 2.30`, `topY 1.02 -> 1.05`,
`subtitleLift 0.865 -> 0.76`; the shared default `subtitleLift` went `0.72 -> 0.68`, which also fixes
the two fan scenes that never set it. At 2.30 ortho the budget closes: header foot 1.783, subtitle
foot 1.615, top row crown 1.532, last caption foot -2.132 against a -2.30 edge.

Two lessons that generalise past this file. A layout constant tuned against wrongly-rendered text is
not a measurement, it is a coincidence - fixing the renderer invalidates every number downstream of
it, so re-verify the whole family of scenes rather than the one you were looking at. And a runtime
audit of the *actual* sort values was what separated the real defect from the imagined one: the
suspected cross-card slice leak and an inverted depth ramp both measured clean (slot *i* paints
90/100/120 + 100*i, and `z = -0.002*i` agrees with it because the camera sits at -z, the same
convention `Hover` at -0.4 and `BeginDrag` at -1.0 already used), while the apparent size variance in
a fan was only tweens still in flight - the same root cause as the caption misplacement above.

### Addendum: the fan is riveted, and the depth is the smallest one that keeps it in frame — measured 2026-10-26

> **Superseded on the depth only (2026-10-27).** The `z ≥ 7.73` bound below is the depth at which the row
> loses *nothing*; it is not the only legal depth. The shipped hand is now **6.7**, which accepts a
> quarter of a card hanging off the bottom edge in exchange for an 18% bigger, sharper card — see the
> addendum that follows. The rivet, the congruence invariant and the arc/roll coupling all still stand.

The hand read as *drifting*: no two neighbouring overlaps matched, so a name banner was sliced at a
different angle from pair to pair. Three theories were tested and killed before the real one survived.

* **Radial size falloff.** For a plane parallel to the image plane, `ndc = (X/Z, Y/Z) / tan`, so
  magnification is `f/Z` and uniform across the whole frame. Lateral position changes nothing; only
  depth does. Any future "the cards shrink towards the edges" theory has to clear this first — a
  compensation for it would have *invented* an 8% size change on side-drags.
* **The camera arcs.** `PlaceCamera` builds rotation from `Euler(pitch, yaw, 0)`, so a lateral pan is
  a pure translation and a camera-parented hand cannot change shape under it.
* **Cards yawed away from the lens.** `ComputeSlot` writes `Quaternion.Euler(0, 0, -t * yawDeg)` — a
  pure Z roll, never a yaw.

What survived: each card rotated about **its own centre** while the centres sat on a flat row, so
card *i+1* is card *i* rotated about *i*'s middle. The edge two neighbours share therefore opens out at
one corner and pinches shut at the other, differently for every pair and differently between the top
and bottom of the same pair — the slanted slice, worst at the row ends where the roll is largest.
`fanPivotsOnRivet` folds the row onto one rivet, which makes every neighbouring pair an exact rotation
of the one before it: congruent by construction, so the overlap is identical along the whole length of
the row at every height, while the cards still roll outward into a fan. That is why the roll could go
up to 14° without the drift returning — a bigger roll is safe once the pairs match. Gap uniformity is
a weaker proxy and passes on a row that still slices banners.

Arc and roll are not independent dials either: `r = halfRow / sin(yaw)`, and the rise is
`r(1 − cos yaw) = halfRow · tan(yaw/2)`. In pivot mode `arcHeight` is derived and ignored; it stays
authored at 0.18 so the scene documents the shape it actually has.

Then the framing. The rivet drops the ends of the row, and the row's lowest corner lands at
camera-space `y = −1.65 − 0.182 (arc) − 0.830 (half a card, rolled 14°) = −2.662`. That number does not
move with depth, while the frame's bottom edge is only `−tan(fov/2) · z`, so the shipped depth has to
satisfy `z ≥ 2.662 / tan(19°) = 7.73`. At the previous 7.0 the outer cards lost 37px — 17% of a card —
below the bottom edge. **7.9** clears it with ~9px of margin and takes the card from 30.3% to 26.9% of
screen height in the same move, which is also what the size complaint asked for. Raising the holder
instead would have unclipped the row by pushing 40px of fan up into the player's own ranks. Sizing the
hand is done with depth, never with scale: a scaled holder would silently rescale the drag, hover and
drop rays, all of which are measured in this plane.

Both numbers were taken live through the real Play path (MainMenu → `Btn_Play`'s `onClick`, never a
direct or additive scene load), with the hover neutralised at runtime first: a physically resting
pointer lifts the card under it, which is exactly the contamination a judgement of shape must not be
made on.

### Addendum: the rivet fixed the geometry, and the drift was still there — two raster faults, measured 2026-10-27

The row became congruent and the player still saw the layout slide along the fan, worst toward the
ends, and saw the card as pixelated. Those are one complaint, and they are not geometry.

A fourth theory had to die first. `StyleLabel` sets `text.isOrthographic = true`, which looks like it
would screen-align the glyphs and leave a rolled card's type flat. In TMP 3.0.7 it is a **unit-scaling
flag**, not an alignment flag — every use is `fontSize * 0.01 * (m_isOrthographic ? 1 : 0.1)`, converting
pixel-space margins and advances into world units. Glyph vertices are still generated in the label's own
rect plane and transformed by the renderer, so type *does* follow the roll. Theory dead; do not reopen it.

What the live audit found instead, walking the generated glyph buffer and expressing every label in the
card's own 832×1216 canvas so the roll, the lens and the holder scale all cancel:

* **The name plates disagreed with each other.** Shrink-to-fit sized each name against its own text, so
  one hand set its five plates at **71.0, 76.7, 85.5, 96.0 and 96.0 point** — a 35% spread — with the
  glyph block tops at canvas y **490, 493, 495, 496, 490**. The type changed size *and* moved along the
  row at the same time, growing toward the ends. The name plate is the one element that straddles the
  overlap with its neighbour, so this broke the congruence the rivet had just established, one layer up.
  Single-line zones now keep one em and are squeezed sideways (`CondenseToFitWidth`): all five come back
  at 96.0 pt, top 490, and the long names land at exactly the 300px plate. A horizontal squeeze leaves the
  em box, the baseline and the plate height identical, and name length was already visible as a width
  change, so it costs nothing the eye is tracking. The scale is applied in the label's own frame, so the
  card's roll composes outside it and the glyphs stay square-on-the-card rather than sheared. Auto-sizing
  stays on for the multi-line ability panel, where shrinking is the right response to more text.
* **The frame had no mip chain at all.** The delivered frames are 832×1216 at `spritePixelsToUnits: 832`,
  so a card is exactly 1.0 × 1.4615 units, and `faceSize` matches — no stretch. At depth 7.9 the hand drew
  that card **190px tall on a 705px screen: 6.4:1 minification with `mipmapCount == 1`**. Bilinear cannot
  average a 6:1 reduction, so the teal rails shattered into dashes, the filigree became noise, and the
  baked captions turned to mush — and two neighbours rolled to different angles sample that aliased
  pattern at different sub-pixel phases, which is literally "the layout shifting, worse to the side".
  Mipmaps on (`enableMipMap: 1`, `aniso: 4`, now `mipmapCount == 11`) across all 56 card textures, plus the
  same flip in `ConfigureArtImporter` / `ConfigureFrameImporter` so a catalog rebuild cannot undo it. The
  tier shaders sample `_MainTex` through `SAMPLE_TEXTURE2D` with derivatives, so they pick mip levels
  correctly and the holo passes were untouched.

The two faults feed each other, which is why the depth moved too. A sharper card invites a closer one, and
a closer one is the cheaper half of the resolution fix: **6.7** instead of 7.9. The constraint is no longer
"in frame at all costs" but "lose at most a quarter of a card", which the player signed off on. The riveted
row's lowest corner still lands at camera-space y = −2.662 whatever the depth, the frame reaches only
−tan(19°)·z, so the clipped amount is `2.662 − 0.3443·z`, and asking that to stay under
`0.25 × 1.4615 = 0.365` puts **z ≥ 6.67**. At 6.7 the outer cards hang 24.2% and the centre card 5%, the
card grows from 26.9% to **31.7%** of screen height, and the minification drops from 6.4:1 to 5.4:1.

Closing the lens does not cost board space here, because the whole fan hangs *below* the axis: magnifying
it about the centre pushes its top edge *down*, from 223px to 199px of 705 measured live. The bigger cards
cover **less** of the ranks than the small ones did. `maxHandSpan` is derived from the same lens and
follows to 6.96.

`Overlink.Cards.Tests` gained `Unity.TextMeshPro` in `references` — the test now names `TextMeshPro` and
`TMP_TextInfo`, and a test assembly cannot see a type its runtime assembly merely references. That is
additive: `includePlatforms: []` and `optionalUnityReferences` are untouched, so the assembly stays
PlayMode and discovery still reports 1068 EditMode / **197** PlayMode. Full runs: **197/197 PlayMode**,
**1068 / 1062 / 2 failed / 4 skipped** EditMode — the same two pre-existing failures, unchanged.

### Addendum: the face was not flat — depth offsets inside a card, and why only a perspective lens shows them — measured 2026-10-27

The player's report, in their words: on the perspective camera, "when the card moves to the side the text
is no longer lined up with the frame … it's normal on ortho camera, but on perspective camera we use it's
messed up." Same for the art. That second clause is not a workaround observation, it is the diagnosis.

For a plane parallel to the image plane, magnification is uniform: everything at depth `Z` scales by
`f/Z`, so a face drawn flat **cannot** desynchronise, wherever the card sits. Push one layer `dz` in front
of its plate and that layer is at `Z + dz` and scales by `f/(Z+dz)` instead. The slip, in pixels:

```
Δ = (f_px / Z) · x · dz / Z
```

`f_px / Z` is the lens's px-per-unit at the card and `x` is the card's camera-space lateral offset from the
axis. Two things follow, and both were seen. The error grows with `|x|`, so it is nil at the middle of the
fan and worst at the ends. And it **changes sign with `x`**, so the type leans one way on the left half of
the screen and the other way on the right. No ordinary layout bug does that — a mis-set rect is wrong the
same amount everywhere. Sign reversal across the centre line is the fingerprint of depth.

At the shipped hand: a 705 px screen at 38° vertical field of view gives `f_px = 352.5 / tan(19°) =
1023.8` px, and `Z = 6.7` gives **152.8 px per unit**. The name plate's old `dz = -0.04` on the outer card
at `x = ±3.48` is therefore `152.8 · 3.48 · 0.04 / 6.7 ≈` **3.2 px**, in opposite directions either side
of centre. Orthography has no `Z` in the denominator, so `Δ ≡ 0` at every `x` and every `dz` — which is
why nine preview scenes never showed it and why the same cards looked perfect on a different camera.

The fix is to make the face flat, and four offsets went:

* `CardFaceView.PixelPosition` z **-0.04 → 0** (the name rail and its labels),
* `ArtImage` **0.02 → 0**,
* `ArtMask` **0.01 → 0**,
* `HandController.ComputeSlot`'s per-card stair **-0.002f · i → 0**, in the riveted *and* the flat layout.

Draw order was never actually these offsets' job. Every tier shader is `Cull Off ZWrite Off ZTest Always`,
so nothing depth-tests and `sortingOrder` is the only thing that has ever decided which layer is on top —
which is precisely why the `dz` values were free to go and why they bought nothing but this bug.

One layer did need them. The old `-0.04` existed to keep the type in front of `LegacyBody`'s front face, the
placeholder cube. Only the legacy path carries that cube, so `CardView.PlaceLegacyBody(size, body)` now
holds it behind the face plane at `z = size.z * 0.5 + 0.01`, and the face stays flat whether or not a cube
is standing behind it. Moving the cube rather than the type is the right direction of the fix: the cube is
temporary scaffolding and the face is the deliverable.

Not worth flattening: the board tile labels at `LabelZ = 0.02`. The same maths at `x = ±5.5` and
`Z ≈ 16` (64.0 px per unit) gives **0.44 px**. Only the hand is both close enough and wide enough to be
measurable, which is why a whole class of cards looked fine on the table and broken in the fan.

So there are now two surfaces for reading a card and they are not interchangeable. The preview family is
orthographic at roughly **585 px per card** and is for *content* — it cannot exhibit this defect class at
all. The live Gameplay lens is perspective at roughly **228 px per card** and is for *alignment*. Flipping
the previews to perspective was considered and refused: it would trade away the only thing they are for in
order to duplicate a check the game scene already gives. Documented in place instead.

`Overlink.Cards.Tests` gained three PlayMode tests: `FaceLayers_AreCoPlanar_SoTheLensCannotPullThemApart`,
`FanSlots_AllRideOnePlane_BothLayouts`, and `DeletedDepthOffsets_ReallyDidSeparateTextFromFrame`. The last
is the one that earns its keep — it gauges the shift against a real bound name plate and asserts three
things: zero shift at `dz = 0`, opposite signs either side of centre, **and the sign reversal itself**. A
test that only measured magnitude would pass for a shear, a bad pivot or a mis-set rect. Full runs at this
point: **200/200 PlayMode** (20 `CardHandTests` + 179 `Overlink.*` + 1 UnitySkills).

### Addendum: the fan is camera-parented, so the board is what has to move — measured 2026-10-27

A different fault from the one above, found on the same lens. At the framing Stage 1 shipped with, the resting
fan covered **29.2 px of the player's own four Core Bench sockets** at 1280×720. Nothing was mis-rendered; the
composition simply had two occupants in one rectangle. It matters because retreat makes that rank a
destination, and a socket the resting fan sits on is a socket the player cannot click — the hand canvas has a
raycaster, unlike the status layer, which is the whole difference between this and the side-panel overlap at
the end of this section.

Why the rig cannot zoom or pitch its way out: the fan hangs in the **camera's own plane** at depth 6.7, so its
screen footprint is `f_px/6.7` times its geometry — a constant of the lens. Pitch and distance move the board
and the fan together. Measured, both levers make it worse or trade it for a bigger problem:

| lever | worst socket under the fan |
|---|---|
| pitch 52 → 56 | Core Bench, **−36.0 px** — worse than the 52 it replaced |
| pitch 52 → 60 | Core Bench, **−41.7 px** |
| distance 15 → 17 | Core Bench −6.8 px at the old bias, and at any deeper bias `N1PlayBench0` — a *node* bench — goes **72.8 px** behind a card |
| distance 15 → 18 | `N0PlayBench1` at **−35.3 px** even with the bias applied; 34 sockets now share the fan's x-span |

Raising pitch pushes the near rows *down* the screen — it does not tuck them under the fan. And a shallower
pitch shrinks on-screen card height by `sin(p)`, which is the legibility the frame was built for. Pulling back
is worse for a subtler reason: shrinking the board toward the centre of the frame drags the **outer** near
corners horizontally *into* the fan's span, so a socket that was merely low becomes one that is behind a card.

**The one thing that can move is the board, and the dial for that is the aim point.** Ground projection at
the shipped yaw is closed-form, with `Δ = worldZ − focusZ` (the board root is rotated +90° about X, so
board-local *y* is world *z*):

```
depth     = d + Δ·cos p
camera Y  = Δ·sin p
screen    = 0.5 + (cameraY / depth) / (2·tan(fov/2))        fraction of frame height, 0 = bottom
```

The fan uses the same expression, and here the geometry bites. `ComputeSlot`'s pivoted form **ignores
`arcHeight`**: the rise is a consequence of the rivet radius `r = halfRow / sin(|yaw|) = 1.48 / sin 14° =
6.1177`, so a card at roll `phi` sits at `−r(1 − cos phi)`. The centre card is lowest on that arc but unrolled,
and the two cards beside it sit only `0.0456` lower while rolling `7°`, which lifts their outer corners by
`0.5·sin 7°` more than an unrolled corner gains. Net: **the row is topped by the mid cards at camera-space
`−0.90936`, not by the centre card at `−0.91925`** — 1.5 px higher on screen. The outer pair roll to
`−1.00167`. Depth 6.7 puts the fan's top at **0.30292 of the frame = 218.1 px of 720**. (A tuner who reaches
for `arcHeight` to lower the fan will find the Inspector accepts it and nothing moves; that is what
`CardHandTests.FanSlots_NeighbourPairsAreCongruent` exists to say out loud.) The bench's near edge is
`Δ = (−2.5 − 0.462) − focusZ`. Seam:

| focus.z | seam |
|---|---|
| −0.2 (the table's own centre) | **−29.2 px**, four sockets behind cards |
| −0.6 | −1.8 px, still touching |
| **−1.0** | **+24.7 px**, nothing behind anything |
| −1.4 | +50.2 px, but the tabletop's far edge climbs to 637 and passes *under* the legend's glyph floor at 629 |

**A false alarm worth recording, because it is the easy mistake to repeat.** An earlier pass over this
concluded *no* focus.z is feasible — that the socket band, the fan and the HUD's top band sum to more than
720 px, so the composition was over-subscribed and something had to be cut. It was wrong, and the error was
method: it took a **global minimum over all sockets of one axis**, demanding the outer node benches (screen
`x 117..234`) clear the fan's top edge — when the fan spans `x 305.8..974.2` and those sockets are nowhere
near a card. Judging rectangles by their shadows on one axis invents a conflict that does not exist. The
correct test is 2-D: project every tile's four corners, take its screen AABB, and intersect that against the
fan's and against each HUD box. Run that way, the top band is clean at every focus.z in the table and
`−1.0` has always been available.

What `−1.0` buys, measured per tile at 1280×720:

* **no socket under the fan.** The nearest socket clears the fan's top edge by 24.7 px. Only the two near node
  plates' outer corners (59 px wide) sit behind cards, and a plate is a marker whose corner under a card costs
  nothing — the sockets on it are clear.
* **no socket in the banner or the legend.** The tallest socket anywhere on the table — a far node bench — tops
  out at 603.4 against the legend's glyph floor at 629, 25.6 px of air. The tallest one *inside* the legend's
  x-span, the opponent's Core Bench, has 81.8 px.
* the tabletop spans **y 69..623 of 720**, so the fan owns the bottom 218 px, the status layer owns the top 97,
  and the board is the band between them instead of bleeding under all three.

What it does **not** fix, stated so it is not rediscovered as a surprise: ten sockets still sit behind the two
side slabs — `NodeList` (470×236 reference, left middle) and the log panel (320×240, right middle) — about
**12.7k px² at 720p**, worst case ~60% of a tile. That is visual only. The status canvas carries no
`GraphicRaycaster` and every `Graphic` on it sets `raycastTarget = false`, which is precisely the asymmetry
that makes the fan a hard constraint and the slabs a soft one: a socket behind a slab is dim, a socket behind
a card is dead. It is left to Stage 3, because Stage 3 is what reflows those two columns to seat the button
rail, and tuning the panels twice is waste.

**The two pile tiles are dead at every bias, and the live check is what found it.** `DeckPile` and
`DiscardPile` are not decoration — `BattlefieldInput.cs:409-414` turns a click on them into the deck browser
and the discard viewer. They sit at `|x| = 3`, board `y = −4.35`, and at the opening framing that puts them
*entirely* behind the resting fan: 73.1 × 80.5 px of overlap for a 73 × 81 px tile at the old bias, 69.9 ×
74.5 for a 70 × 74 px tile at the authored one. The bias lifts them 65 px up the screen and changes nothing
about the occlusion, which is why the socket-only sweep never flagged it — the piles are `TileRole.Pile`, not
sockets, and the seam contract was written about the ranks a player has to click to place a unit.

There is no board-space answer this time, so it must not be "fixed" by nudging a constant. Clearing the fan's
x-span at the piles' depth needs `|x| ≥ 4.5`, and the node plates own `x −6.9..−4.1` at exactly that depth —
the pile would land on NODE 1.

**The decision, taken: the piles stay on the table and the resting hand gets out of their way.** The fan
sinks while the pointer is away from it and rises back when the pointer comes to read a card, so the piles are
reachable in the state a player is not looking at their hand, and the hand is whole in the state they are.
Depth of sink: three quarters of the fan's own screen height, falling back to a half when three quarters would
push it off the bottom of the frame. This is Stage 3 work and it lands with the button rail, not here.

Two things Stage 3 has to measure rather than assume. First, the required sink is set by the *piles'* screen
rect, not by the fan's: uncovering a tile that sits inside the fan's band means dropping the fan's top edge
below the tile's top edge, so the number to hit is measured against the pile rects at the authored framing
(70×74 px at `fz −1.0`, 73×81 at `fz −0.2`) and the ¾/½ rule is a budget, not a guarantee. If the honest
measurement says a half is not enough, that is a finding to bring back rather than a reason to slide the
constant past the floor. Second, **the trigger region must be the fan's resting footprint — a fixed band — and
not its current position.** A sink that watches where the fan actually is oscillates on the boundary in one
frame: the fan moves away, the pointer is outside, the fan rises, the pointer is inside, and the player
watches it flutter. Hysteresis on a fixed band is the whole fix, and it is the kind of thing that reads as
polish and is actually correctness.

The pan origin moves with the bias **on purpose**. `boardCenter` is captured from the authored focus, so the
clamp is now `[−1.55, −0.45]` at the opening framing (slack 0.55, because the table already fits the frame).
Four camera tests asserted `TableCenterY ± panSlackZ` and were only ever green because the two numbers
coincided; they now assert against the rig's own `BoardCenter`, newly exposed for exactly that reason. A test
that cannot see the thing a clamp is built around will guess it, and a guessed constant is how `0.35 / 16`
survived in `DropTargetTests` long after the game stopped rendering it.

Two tests pin the result: `RestFraming_LeavesThePlayersOwnBenchClearOfTheRestingFan` derives the fan's top edge
from `HandController.ComputeSlot` and the shipped fan dials, projects both, and asserts the seam in
**fractions of frame height** — so it reads identically at 720p and 4K — plus a companion bound that the same
bias must not push the tabletop's far edge past 0.95 of the frame. `RestFraming_PanOriginIsTheAuthoredRestBias`
locks the origin and says out loud that it is not the table centre.

Three places have to agree, and one of them is the file nobody regenerates: the controller's serialized
default, `BuildGameplayScene.CreateCamera`, and the hand-authored `focus:` at `Gameplay.unity:882`. The scene
is the tuning surface and the builder is bootstrap only. While there: the scene's saved camera transform was
stale too — `y 12.608` against the pose the rig derives, `11.820` — a leftover from an editor drag. Harmless
at runtime, since `LateUpdate` rewrites the pose every frame, but it means the Scene and Game views had been
showing a shot the game never renders. Corrected to the derived pose.

### Two traps this section walked into first

**The contract test was wrong before it was right, and only asserted loudly enough to notice.** Its first
form took the face's *height* as a lateral offset — `new Vector2(CardFaceHalfHeight * 2f, CardFaceHalfHeight)`
— which is invisible on the centre card (roll 0) and inflates the wing cards, so the computed row top sat
1.55 px too high and the seam came out at 0.004 of the frame against a 0.02 bound. Had that arithmetic been a
comment instead of an assertion it would have gone into this document and stayed there, correct-looking,
forever. The contract paid for itself on its first run by failing on itself, and the failure message quoted
the seam back at me, which is what made the diagnosis take a minute instead of a session.

**A verdict is only worth the compile state it was produced in.** `Overlink.Match.Tests` did not reference
`Overlink.Cards`, so the `using` this test needs was `CS0234` and the assembly kept the DLL from its last
successful compile. A full-suite run launched in that window reported 1086 tests with the new `RestFraming`
cases *Passed* — impossible, since the code behind those names had never compiled — and a class run in the
same window reported 22 tests for a file that contains 16. Both numbers were stale. The rule this buys:

* `debug_get_errors` immediately before a run, not after. It is the only reliable compile check there is, and
  a live error freezes the domain reload, which freezes discovery, which is how a phantom-green suite and a
  "Unity did not enter Play Mode" timeout end up looking like two unrelated problems.
* Count the tests against the file. Sixteen `[Test]` methods is sixteen; anything else means the manifest and
  the binary disagree.
* When the bridge's dispatcher stalls (requests queued, `mainThreadIdleMs` climbing, `Responding` still
  true), a forced recompile plus a focus on the Editor window clears it. Do not queue more work behind the
  stall — three of the play-mode timeouts above were requests waiting on a queue that had stopped draining,
  and each one that eventually landed flipped the Editor in or out of play mode underneath me.

## 14. Verifying the build

### Use the REST bridge - do not invoke csc yourself

When the Unity Editor is open, the UnitySkills server answers on `127.0.0.1:8090`-`8100` (read
`projectName` from `GET /health` and confirm it is Overlink before writing - ports are first-come
across open Editors). It is strictly better than any out-of-process compile, because it verifies the
thing that actually ships:

| Question | Call |
|---|---|
| Did the C# compile? | `POST /skill/debug_get_errors` |
| Are the shaders clean? | `POST /skill/validate_shader_errors` (project-wide, `count: 0`) |
| Build the previews / fixtures | `POST /skill/editor_execute_menu {"menuPath":"Overlink/..."}` |
| Does the fan actually run? | `POST /skill/editor_play_capture {"durationSeconds":10,"captureScreenshot":true}` |
| Where is this card really? | `POST /skill/component_get_properties` on the `SpriteRenderer` -> `bounds` |

Play-mode jobs are async: `editor_play_capture` returns a `jobId`, then poll `GET /jobs/<id>` until
`terminal: true`. Runtime-spawned objects (gallery cards) are absent from the hierarchy in edit
mode, so inspect them while playing. `scene_get_hierarchy` gives names and ids only, no values.

**Do not shell out to `csc` with Unity's response files while the Editor is open.** This was tried
on 2026-10-26 and it broke the project. `Library\Bee\artifacts\1900b0aE.dag\<Asm>.rsp` carries both
`-out:` and `-refout:` into that directory, and sets no `-assemblyname`, so Roslyn derives the
assembly *identity* from the `-out:` filename. Overriding only `-out:` to a temp path still wrote
the reference assembly into `Library\Bee\` under the name `verify_Overlink.X`; every downstream
assembly then referenced a nonexistent `verify_Overlink.Core`, and the Editor refused to load
`Overlink.Editor`, `Overlink.Match`, `Overlink.UI` and friends - eight console errors and a dead
toolchain, all from a "read-only" check.

If a compile must be reproduced out of process (Editor closed), rewrite **both** outputs:

```powershell
$csc = "D:\APPS\UnityEditor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll"
$rt  = "D:\APPS\UnityEditor\2022.3.62f3\Editor\Data\NetCoreRuntime\dotnet.exe"
& $rt exec $csc "@Library\Bee\artifacts\1900b0aE.dag\Overlink.Editor.rsp" `
  "/out:$env:TEMP\opencode\Overlink.Editor.dll" "/refout:$env:TEMP\opencode\Overlink.Editor.ref.dll"
```

Keep the temp filenames identical to the real assembly names, or the identity drift reappears.

### Recovering from a corrupted script pipeline

Symptoms: the Console floods with `Unable to resolve reference '<something>'`, and
`EditorApplication.isCompiling` sticks true while `mainThreadIdleMs` stays low - the flag is a
stale leftover from an interrupted build, no backend is running, and no write request clears it
(`editor_get_state` returns 503). Deleting the bad DLLs does not help: Unity will not rebuild them
because no source mtime changed, and `debug_force_recompile` cannot attach to the stuck flag.

The fix is a restart, not a refresh:

1. Kill the Unity processes and delete `Library\Bee`, `Library\ScriptAssemblies` and `Temp`.
   Leave `Library\ArtifactDB` alone - the asset import cache is unaffected, and rebuilding it is
   slow for no reason.
2. Relaunch `Unity.exe -projectPath <proj>`. Source under `Assets/` is never at risk here; only
   regenerable build output is removed.
3. The bridge comes back on the same port within seconds and every assembly rebuilds clean.

### Shader log archaeology

`Logs\shadercompiler-*.log` records every compilation with its `insize=`. Grep for
`file=Assets/Shaders/<Name>.shader` and confirm an `ok=1` line whose `insize` equals the file's
current byte length; a smaller `insize` is an older revision of the same file, and stale `error:`
lines from pre-rename states are common, so match on the **current** filename *and* size before
concluding anything broke. This is a weak substitute for `validate_shader_errors` - prefer the
bridge whenever the Editor is up.

### Running the test suites

The bridge drives the real Test Runner, so a smoke test needs no separate harness:

| Question | Call |
|---|---|
| Run everything | `POST /skill/test_run {"testMode":"EditMode"\|"PlayMode"}` -> `jobId` |
| One test or class | `POST /skill/test_run_by_name {"testName":"CardHandTests.DrawAndDiscard_UpdatesHand","testMode":"PlayMode"}` |
| Subset by assembly | `POST /skill/test_run {"testMode":"PlayMode","filter":"Overlink.Cards.Tests"}` - a plain assembly-or-class name only; regex alternation returns `status: failed` with `total: 0`, which reads as "no failures" rather than "nothing ran" |
| Poll | `POST /skill/test_get_result {"jobId":...}` until `status` is neither `running` nor `pending` |
| Why did it fail? | same call, `result.failedTestDetails[].message` / `.stackTrace` |
| History | `POST /skill/test_get_summary` - `allFailedTests` is deduplicated across *all* runs and capped, so absence from it proves nothing about a specific test |

Current baseline: **EditMode 1068** (1062 pass, 2 pre-existing failures, 4 skipped) and **PlayMode
197 cases** (197 results, all pass).

Two EditMode failures are not ours and predate any presentation work:

* `UnitySkills.Tests.Core.PerceptionSkillsTests.SceneSummarize_CountsObjectsCorrectly` - the
  preceding test in that plugin's own suite calls `NewScene(EmptyScene, Single)` and never restores
  the scene, so the assertion "default scene has Camera + Light, so the floor is 4" runs against an
  empty one. Third-party code in `Library\PackageCache`; it also means the Editor can be left on a
  scratch scene after a run, so check `scene_get_loaded` before trusting the next capture.
* `Overlink.Match.Tests.SimpleAITests.SimpleAI_MakesLegalMoves` - see below.

`Overlink.Match.Tests` references only `Overlink.Core` and `Overlink.Match`, never `Overlink.Cards`
or `Overlink.Editor`, so no shader or hand-layout change can reach it; confirming the asmdef graph
before investigating a failure is what separates "mine" from "pre-existing" in one command.

### The hand tests are PlayMode, and discovery cannot see them

`Overlink.Cards.Tests` and `Overlink.Core.Tests` declare `includePlatforms: []` with
`optionalUnityReferences: ["TestAssemblies"]`, which classifies them as **PlayMode** assemblies. So
`test_list` omits both from the EditMode list (890 UnitySkills + 88 Match + 90 Net = the 1068 above).
Grepping the EditMode output for `CardHandTests` therefore finds no match and reads as *"my new test
was never compiled"* — it was, and the dll says so.

The PlayMode list *is* reachable, and this is the cheap route: pass `{"testMode":"PlayMode",
"limit":5000}`. The default page is 100 items and 197 > 100, so an unbounded call comes back looking
empty, and `test_discover_get_result` offers no cursor to page past it — which is what made an earlier
reading of this call conclude that PlayMode discovery returns nothing at all. With the limit set it
reports 197 cases and `truncated: false`, so adding a test is confirmable in one call by watching that
total move and grepping the returned `fullName`s. A PlayMode **run** remains the only proof it *passed*:
197 cases = 17 `CardHandTests` + 179 Core/Card-asset + 1 UnitySkills.

The trap has a tempting false cure. Rewriting those two asmdefs into the Match/Net shape
(`includePlatforms: ["Editor"]`, explicit `UnityEngine.TestRunner` / `UnityEditor.TestRunner`
references, `overrideReferences` with `nunit.framework.dll`) really does make them appear in EditMode
discovery — and then thirteen of the sixteen `CardHandTests` that existed at the time failed with
`DontDestroyOnLoad can only be used in play mode`, because the rigs build a pitched table and a camera
and coroutine through frames. (The class is at seventeen now; the shape is what was measured, not the
count.) The A/B, both measured against Unity's own `TestRunnerApi`:

| asmdef shape | EditMode | PlayMode |
|---|---|---|
| `includePlatforms: []` (shipped) | 1068, no `CardHandTests` | 197, all pass |
| `includePlatforms: ["Editor"]` | 1263, 13 mode failures | — |

Leave the asmdefs alone — with one exception that is *not* a shape change: `Overlink.Cards.Tests` now
also lists `Unity.TextMeshPro` in `references`, because a test that names `TextMeshPro` or
`TMP_TextInfo` cannot see a type its runtime assembly merely references. Adding a reference leaves
`includePlatforms` and `optionalUnityReferences` alone, so the assembly stays PlayMode and the totals
above are unaffected. When a test must be confirmed to have run and the bridge's totals disagree
with the source, the decisive move is a throwaway Editor script calling
`TestRunnerApi.RetrieveTestList` / `Execute` directly — and for PlayMode it must re-register its
callbacks from an `[InitializeOnLoad]` static constructor, because entering play mode reloads the
domain and wipes the registration along with every other static.

### A PlayMode test that passes alone and fails in a batch

PlayMode tests run inside whatever scene the Editor entered Play mode from - here `MainMenu`, with
`AuthFlowController` live. About a second in, the auth flow completes and calls `LoadScene`, and a
`Single`-mode load destroys every GameObject the test created dynamically. Whether that lands
inside a given test's `WaitForSeconds` window is pure scheduling, so the same test is green in
isolation and red in a suite.

Two rules follow. Pin rigs with `Object.DontDestroyOnLoad(go)` so ambient startup cannot vaporise
them, and assert against your own subtree (`go.GetComponentInChildren<CardView>()`) rather than
`FindObjectOfType`, which additionally picks up objects leaked by earlier tests and misses inactive
ones. Note that `HandController.CardCount` counts the internal view list, so it can read 1 while the
scene object is gone - the two disagreeing is precisely the signal that something destroyed it.

### Deterministic tests must not consume `System.Random`

`MatchEngineTests.MatchEngine_ExecuteMulligan_ReshufflesAndRedraws` asserted the mulliganed card is
absent from the hand afterwards. `MatchEngine` returns it to the deck, calls `ShuffleDeck`, then
draws a replacement - and `PlayerState.ShuffleDeck` seeds `new Random()` from the clock, so with 26
cards in the deck the same card comes straight back about one run in twenty-six. It passed in
isolation, failed in the suite, and had never been observed failing because nothing ran the suite
often enough.

The assertion was replaced with the three facts that carry the same intent without the gamble: the
deck is back to its opening size (so a card went out and one came in), the hand is whole, and the
card is still in play somewhere. Product-side seeding is the other fix and remains open - any test
that touches a shuffle is only as reproducible as that clock.
