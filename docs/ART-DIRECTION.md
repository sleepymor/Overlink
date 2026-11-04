# Overlink Art Direction

Date: 2026-10-30
Status: Locked for the v0.19 visual uplift pass (Phases 0-5).
Supersedes: nothing; consolidates the faction language from `docs/FIGMA-HIGH-FIDELITY-PROMPT.md`
and the multiply-contract from `docs/BOARD-SKIN-PASS-REPORT.md`.

## One-line direction

**A network tabletop floating in a void:** matte dark surfaces, thin emissive circuit inlays,
cool signal light for the player, warm light for the opponent, and nothing on screen brighter
than the things a player must read.

## Palette roles

| Role | Hex | Where |
|---|---|---|
| Void | `#090B10` | camera clear, backdrop base, menu backgrounds |
| Table matte | `#0E1116` → `#161B22` | board slab, panels |
| Signal (player / FSTI accent) | `#6FE3D0` | player glows, focus, hover, emissive rims on player side |
| Primary UI blue | `#2E7AD4` (UiKit `Primary`) | buttons, links |
| Accent UI blue | `#7AB4F8` (UiKit `Accent`) | text accents, highlights |
| Warm (opponent / FRTI accent) | `#E8A33D` | opponent glows, Active socket rims, warnings |
| FPB slate | `#2F3E5C` / accent `#8FB8E8` | FPB faction frames and chips |
| FRTI gunmetal | `#3A4048` / accent `#E8A33D` | FRTI faction frames and chips |
| Danger | `#C84A44` (UiKit `Danger`) | destructive actions, concede |

Value rules:
- Backdrop and void surfaces stay **below 0.2 luminance** so board state and HUD always win the eye.
- Emissive/bloom-eligible surfaces (rims, foils, signal traces) are the **only** things above 1.0 HDR.
- Text never sits below 4.5:1 contrast against its panel (Xbox Accessibility Guideline 101 floors:
  18 px at 1080p PC minimum; HUD numerals SemiBold or heavier).

## Shape language

- **Surfaces:** matte, near-zero specular; detail lives in *inlays* (circuit traces, hex lattice,
  plate seams), never in noisy albedo.
- **Edges:** 1 px accent lines and notched corners on UI; chamfered corners on FRTI, rounded
  double borders on FPB, thin circuit traces on FSTI (per faction table in the Figma doc).
- **Corners:** UI panels use a 6-8 px notch cut on top-left/bottom-right (sci-fi chassis feel);
  buttons keep a 4 px radius so the notch reads as deliberate, not rounded-cute.
- **Motion:** ease-out for settles, BACK-overshoot for pops, 120-220 ms for UI, 80-140 ms for
  combat feedback. Nothing linear except scrolling backdrops.

## Board skin contract (binding for all supplied art)

Skins **multiply** against the 13 serialized albedos on `BattlefieldPresenter`
(`BoardSkinTests` pins the contract). Therefore every board texture must be:

1. **Grayscale / chroma-neutral**, luminance **0.6-1.0** in regions the tint must colour.
2. Free of baked hue, baked vignettes stronger than 10 %, and baked lighting.
3. Authored at the sizes in the spec table below; sRGB, mipmapped, aniso 4.

Exception: `pileSkin` (card backs) may carry full colour; when it does, raise `deckTint` /
`discardTint` toward white so the multiply stops darkening it.

### Supplied-art spec table

| Slot | Pixels | Content |
|---|---|---|
| `tableSkin` | 2048×1024 | matte composite, faint circuit traces, slight edge darkening |
| `socketSkin` | 512×512 | recessed tile, bright corner brackets, per-tile UV (0-1 per face) |
| `plateSkin` | 512×512 | hex/lattice inlay, neutral (tinted per node state at runtime) |
| `pileSkin` | 512×768 | card back; full colour allowed |
| `rimSkin` | material | emissive rim (`_Emission` HDR > 1) so bloom catches it |
| backdrop | 2048×1024 | void nebula + faint grid horizon; keep < 0.2 luminance |
| menu backdrop | 1920×1080 | same family; darkest behind the button column |

Until supplied art lands, `Assets/Shaders/OverlinkBoardFallback.shader` +
`OverlinkBackdrop.shader` generate procedural stand-ins from the same rules; swapping real art
in is a drop onto the five slots (no code change).

## Typography

- **Rajdhani** (OFL): Bold = logo, turn banner, screen titles; SemiBold = HUD numerals, buttons;
  Medium = labels, body where space is tight.
- **LiberationSans** remains the TMP fallback asset only; no new copy sets it deliberately.
- TMP SDF atlases at 1024, static, ASCII + the glyphs used by HUD symbols (◆ ● ▲ / digits).

## What this doc forbids

- Pure-white albedos on board surfaces (kills the tint multiply).
- Drop shadows baked into UI sprites (shadows are code-side, 1 px outline or scrim).
- New hues outside the palette table; factions keep exactly their two colours each.
- Juice at rest: every exaggeration returns to rest (see `game-feel` tiers in the uplift plan).
