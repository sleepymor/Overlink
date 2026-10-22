# Card Frame Spec — measured geometry

**Source:** `D:\OverlinkLowFidFrame\Card - Unit.png` and `Card - Protocol-Abilty-Relay.png`
**Measured:** 2026-10-22, pixel-exact (see method note at end)
**Canvas:** 832 × 1216 px, ratio 0.6842
**Status:** Frame design settled. Not yet imported into Unity.

---

## 1. Canvas and rails

| Element | Value | Notes |
|---|---|---|
| Canvas | 832 × 1216 | ratio 0.6842 |
| Outer black border | 25 px all sides | `#000000` |
| White frame rail | 41 px | `#FFFFFF`, inside the black border |
| Content box | x66..765, y66..1183 | 700 × 1118 |

The content box is where every zone lives. Nothing bleeds past x765 or y1183 except the
cost orb, which sits in the top-left rail.

---

## 2. Zone map — Unit frame

Coordinates are absolute pixels on the 832 × 1216 canvas.

| # | Zone | Label text | Geometry | Position |
|---|---|---|---|---|
| 1 | Cost | `Cost` | 80 × 80 circle, x26..105 y26..105 | top-left rail |
| 2 | Factions | `Factions` | text only, no box | top-right |
| 3 | Art | `16:10 Image` | 700 × 385, x66..765 y66..450 | top-centre |
| 4 | Name | `Name` | 343 × 126, x244..586 y451..576 | centred |
| 5 | Type | `Type` | 224 × 17 | centred, below Name |
| 6 | HP + Shield | `HP + Shield` | 634 × 83, x99..732 y661..743 | panel 1 |
| 7 | ATK | `ATK` | 634 × 83, x99..732 y774..856 | panel 2 |
| 8 | Ability | `Ability` | 634 × 210, x99..732 y887..1096 | panel 3 |
| 9 | ID + Rarity | `ID + Rarity` | 157 × 17, x99..255 y1152..1168 | bottom-left |

### Vertical rhythm

```
y26   ┌ ⬤Cost 80×80                    Factions ┐
y66   ├──────────────────────────────────────────┤
y450  │        Art 700×385 (1.818)               │
y451  ├──────────────────────────────────────────┤
y576  │        Name 343×126 (red bar)            │
      │        Type 224×17                       │
y661  ├──────────────────────────────────────────┤
y743  │        HP + Shield 634×83                │
y774  ├──────────────────────────────────────────┤
y856  │        ATK 634×83                        │
y887  ├──────────────────────────────────────────┤
y1096 │        Ability 634×210                   │
y1152 ├──────────────────────────────────────────┤
y1168 │  ID + Rarity 157×17                      │
y1183 └──────────────────────────────────────────┘
```

---

## 3. The two frame variants

Both files are 832 × 1216 and **94% identical**. The only difference is the text
architecture below the name banner:

| Variant | Zones below Name | Region |
|---|---|---|
| **Unit** | 3 stacked panels — HP+Shield, ATK, Ability | y649..1096 |
| **Ability / Relay / Protocol** | 1 merged block | y649..1096 |

Everything above y649 and below y1096 is byte-identical between the two files.

---

## 4. Field coverage

| `CardDefinition` field | Zone | Covered |
|---|---|---|
| `Cost` | Cost | ✅ |
| `Faction` | Factions | ✅ |
| `Name` | Name | ✅ |
| `Type` | Type | ✅ |
| `Atk` | ATK | ✅ |
| `Hp` | HP + Shield | ✅ |
| `Shield` | HP + Shield | ✅ |
| `Rarity` | ID + Rarity | ✅ |
| `Id` | ID + Rarity | ✅ |
| `AbilityText` | Ability | ✅ |
| `ArtPath` | 16:10 Image | ✅ |
| `Keywords` | — | **excluded by design (2026-10-22)** |
| `DesignNote` | — | correctly absent |
| `Effects` | — | correctly absent |

**Decision 2026-10-22:** keyword chips are not part of the card face. Every pool card still
carries `Keywords` data — it is simply not rendered. Keyword meaning is conveyed through
`AbilityText` prose (e.g. `"Piercing. Ignores Shield when dealing damage."`).

Consequence to keep in mind: 6 of the 15 keywords change combat maths in ways the stat
numbers do not show — `Piercing`, `FirstStrike`, `Lifesteal`, `CannotBlock`,
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
(`BattlefieldPresenter.cs:1387–1452`) prints `CurrentHp + "/" + def.Hp`, and shows
`NodeAtkBonus` / `NodeHpBonus` as parenthetical modifiers when non-zero.

---

## 6. Art window ratio — 1.818 vs 1.600

| Thing | Ratio |
|---|---|
| Art window (700 × 385) | **1.818** |
| Generation target (1024 × 640) | **1.600** |

The window is wider than the generated art, so fitting 1.6 into 1.818 crops roughly **12%**
of the art vertically.

Two clean options:

- **Change the window** to 700 × 437 (true 16:10) — costs 52px, must come out of the
  Name/Type/panel stack.
- **Change generation** to 1024 × 563 (1.818) — no layout change, matches the window exactly.

The label `16:10 Image` inside the window is currently inaccurate; the window is not 16:10.

---

## 7. Space warnings

**ID + Rarity is 157 × 17 px.** Rendering `card_fsti_001` plus a rarity marker in that box
is tight — roughly 5pt at 832px width. Consider widening to ~220px (there is free space to
the right, the band is empty from x256 to x765) or raising to ~24px tall.

**The bottom band y1097..1190 (700 × 94) is empty** except the ID+Rarity box. That is
available space if any zone needs to grow.

---

## 8. Unity integration — nothing is wired

The frame exists only as two PNGs in `D:\OverlinkLowFidFrame\`. Verified state:

| Check | Result |
|---|---|
| Frame PNG under `Assets\` | **absent** — zero textures in the project outside screenshots |
| `SpriteRenderer` in Battlefield.prefab | **0** |
| `UI.Image` in Battlefield.prefab | **0** |
| `m_Sprite` / `m_Texture` refs | **0** |
| Anything reading `ArtPath` | **nothing** — all 4 pool factories set `ArtPath = string.Empty` |
| `CardView` visual | a coloured **cube**; `Setup` builds only Title / Cost / Desc labels |

`CardView.CardSize` is currently `1.0 × 1.4` (0.714) — close to the frame's 0.684 but not
equal. Set it from the frame: 832/1216 gives `1.0 × 1.4615`.

Steps required, none done:

1. Copy both PNGs to `Assets\Art\Cards\`, Texture Type = **Sprite (2D and UI)**
2. Add a sprite surface to `CardView` (replace or overlay the cube body)
3. Map `ArtPath` → sprite at runtime; populate the field in `PrototypeCardPool`
4. Rewire `CardView` from the 5-field `CardData` to `CardDefinition` + `CardInstance`

`CardData` is load-bearing (identity key in `GameCoordinator.instanceByData`, keeps
duplicate copies distinct) — migrate, do not delete.

---

## 9. Method note

Geometry was measured by scanning the PNGs pixel-by-pixel: exact colour histograms, per-row
run-length segmentation, and connected-component analysis of non-fill regions. Zone labels
were confirmed with a vision pass (`view_image`).

An earlier pass sampled rows at 16px stride and mis-read the three stat/ability panels as
blank text boxes, concluding stats were absent. That was wrong — the panels are HP+Shield,
ATK, and Ability. The corrected reading is above.
