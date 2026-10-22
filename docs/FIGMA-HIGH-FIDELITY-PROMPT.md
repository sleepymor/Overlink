# Figma Agent Prompt — Overlink Card Frames, Low-Fi → High-Fi

**Deliverable:** 12 high-fidelity card frame components (4 card types × 3 factions)
**Source material:** `D:\OverlinkLowFidFrame\Card - Unit.png`, `Card - Protocol-Abilty-Relay.png`
**Author:** Themis ◈ for Master
**Date:** 2026-10-22

---

## PROMPT — copy everything below this line

---

You are designing high-fidelity card frames for **Overlink**, a sci-fi tactical card
battler. You are given two low-fidelity wireframes. Your job is to turn them into finished,
production-ready frame components — 12 variants total — without moving a single zone.

### Non-negotiable geometry

These values come from pixel measurement of the source wireframes. **Do not change them.**
The Unity runtime will composite card art, text and numbers into these exact boxes. If you
move a box, the game breaks.

**Canvas:** 832 × 1216 px (ratio 0.6842). Use this exact size for every frame.

| Zone | Box (x, y, w, h) | Source |
|---|---|---|
| Outer border | 0,0 → 832,1216 | measured |
| White rail | 25,25 → 782,1166 | measured |
| Content box | 66,66 → 766,1184 | measured |
| Cost orb | 26,26 → 106,106 | measured — 80 × 80 circle, breaks the rail |
| Faction mark | top-right, ~720,40 → 766,86 | **proposed** — wireframe has a text label only |
| Art window | 66,66 → 766,451 | measured — 700 × 385 |
| Name banner | 244,451 → 587,577 | measured — 343 × 126, centred |
| Type label | 304,618 → 528,646 | measured 224 × 17; **28px tall proposed** |
| Stat bar A | 99,661 → 733,744 | measured — 634 × 83 |
| Stat bar B | 99,774 → 733,857 | measured — 634 × 83 |
| Ability panel | 99,887 → 733,1097 | measured — 634 × 210 |
| ID + rarity | 99,1152 → 339,1179 | measured 157 × 17; **240 × 28 proposed** |

Everything marked *measured* is fixed — do not move it. The two *proposed* rows are
widened because at hand size (card renders ~220px tall) a 17px box scales to 3px and becomes
illegible. Confirm with Master before applying the proposed sizes.

Two layouts share everything above except the block between **y=649 and y=1096**:

- **Unit** — two stat bars (A: HP + Shield, B: ATK) plus a 210px ability panel
- **Ability / Relay / Protocol** — one merged panel, 99,649 → 733,1097 (634 × 448)

### The 12 variants

Build each frame as a Figma component. Naming: `Card/Frame/{Type}/{Faction}`.

| | FSTI | FRTI | FPB |
|---|---|---|---|
| **Unit** | `Card/Frame/Unit/FSTI` | `…/Unit/FRTI` | `…/Unit/FPB` |
| **Ability** | `…/Ability/FSTI` | `…/Ability/FRTI` | `…/Ability/FPB` |
| **Relay** | `…/Relay/FSTI` | `…/Relay/FRTI` | `…/Relay/FPB` |
| **Protocol** | `…/Protocol/FSTI` | `…/Protocol/FRTI` | `…/Protocol/FPB` |

Unit frames carry the stat-bar layout. The other nine carry the merged-panel layout.

### Faction visual language

Each faction must be identifiable at a glance **without reading text** — at hand size the
card renders around 220px tall, so a 17px label becomes 3px and is unreadable. Faction
identity therefore lives in colour, material, and silhouette.

| Faction | Full name | Gameplay identity | Visual direction |
|---|---|---|---|
| **FSTI** | Frontier Science and Technology Institute | Signal and data control — card draw, NP generation, Protocols, effect chaining | Clean, precise, clinical. Thin circuit-trace inlays, hexagonal lattice, cool light. Suggests instrumentation and data flow. |
| **FRTI** | Frontier Research and Technology Institute | Industrial combat — high ATK, durable frames, Relays as structures | Heavy, angular, engineered. Visible bolts, plate seams, chamfered corners, machined metal. Suggests load-bearing structure. |
| **FPB** | Frontier Planning Bureau | Attrition and defence — Shield, HP, healing, Repair, Node holding | Rounded, layered, civic. Soft double-border, embossed geometry, matte finish. Suggests a fortified institution. |

**Proposed faction palette — confirm with Master before locking:**

- FSTI — deep teal `#1B6E6E` rail, `#6FE3D0` accent glow
- FRTI — gunmetal `#3A4048` rail, `#E8A33D` amber accent
- FPB — slate blue `#2F3E5C` rail, `#8FB8E8` accent

Keep the underlying structure identical across factions. Faction is a *skin*, not a
different layout — a player must recognise "that's a Unit" before "that's FSTI".

### Type visual language

Type is conveyed by **layout**, not by a label:

- **Unit** — stat bars present. The presence of ATK/HP is the signal.
- **Ability** — merged panel, no stats. Reads as an instant/event.
- **Relay** — merged panel, no stats. Give it a distinct corner treatment (it occupies a Node slot persistently).
- **Protocol** — merged panel, no stats. Give it a distinct header band (only one Protocol can be active per player).

Type accent colours already exist in the game code — use them so the frame matches runtime
tint:

| Type | RGB (0–1) | Hex |
|---|---|---|
| Ability | 0.80, 0.55, 0.30 | `#CC8C4C` |
| Relay | 0.30, 0.65, 0.50 | `#4CA680` |
| Protocol | 0.55, 0.40, 0.75 | `#8C66BF` |
| Unit | 0.50, 0.55, 0.70 | `#808CB2` |

### Rarity treatment

Rarity must **not** be a text string. A 17px text box becomes 3px in hand — invisible. Encode
rarity in the frame itself:

- **Common** — plain rail, no ornament
- **Rare** — single accent line on the rail
- **Epic** — accent line plus corner filigree
- **Legendary** — full frame treatment: gold or faction-tinted metal rail, gem inset, heavier
  border

Build these as a Figma variant property `Rarity = Common | Rare | Epic | Legendary` on the
frame component, so all 12 components get 4 states each (48 total).

Rarity is also the link to the **full-art** variant: a Legendary card drops the art window
entirely and lets the illustration bleed to the canvas edge, with all text overlaid on a
translucent scrim. Do not build full-art yet — just make sure the rarity system can express it.

### Typography

| Element | Size | Weight | Notes |
|---|---|---|---|
| Cost | 44px | Bold | Must fit two digits (`10` is the max cost) |
| Name | 34px | Bold | Longest names are `Interdiction Net`, `Bulwark Protocol`, `Piercing Lancer` |
| Type | 16px | Medium | Small; layout carries the meaning, this is confirmation |
| Stat numbers | 36px | Bold | ATK / HP / Shield |
| Ability text | 22px | Regular | Must handle up to ~140 characters |
| ID + rarity | 13px | Regular | Format `card_fsti_003` |

Font: match the project's existing UI font if available, otherwise a clean geometric sans
(Inter, Roboto, or similar). Do not use a serif.

### Layer structure — mandatory

Name layers exactly. Unity will slice this.

```
Card / Frame / {Type} / {Faction}
├── Border
├── Rail
├── Art Window          ← must be an empty rectangle with a mask, no fill image
├── Cost Orb
│   └── Cost Value      ← text layer, empty
├── Faction Crest
├── Name Banner
│   └── Name Value      ← text layer, empty
├── Type Label
│   └── Type Value      ← text layer, empty
├── Stat Bar A          ← Unit only
│   ├── HP Value        ← text layer, empty
│   └── Shield Value    ← text layer, empty
├── Stat Bar B          ← Unit only
│   └── ATK Value       ← text layer, empty
├── Ability Panel
│   └── Ability Value   ← text layer, empty
└── ID + Rarity
    └── ID Value        ← text layer, empty
```

All value layers start **empty**. This is a frame, not a filled card. Do not populate with
sample card data except on one example instance per type for review.

### Do not

- Do not change any zone coordinate or size. They are measured, not arbitrary.
- Do not add a keyword chip row. Keywords were deliberately excluded from the card face on
  2026-10-22 — they are conveyed through ability text prose instead.
- Do not add a type wordmark beyond the small Type label. Layout already encodes type.
- Do not make the art window opaque or add a background image to it. It must stay an empty
  masked rectangle — the game composites illustration into it at runtime.
- Do not make the name banner a different width. It is 343px centred by design.
- Do not use pure `#FF0000` for the name banner. That was a placeholder in the wireframe. Use
  a faction-appropriate dark bar that reads against artwork.
- Do not add drop shadows or glows outside the canvas bounds.

### Deliver

1. 12 frame components (4 types × 3 factions), each with 4 rarity variants
2. One filled example per type showing real data from the pool below
3. A colour token page listing every hex used
4. The components set to **clip content** on the art window, so overflow is visible if a
   future art asset does not match the box ratio

### Sample data for the example instances

Use these real cards so the examples are honest:

- **Unit / FSTI** — `card_fsti_003`, "Vanguard", cost 2, ATK 3, HP 2, Shield 0, "First Strike. Wins the first exchange."
- **Ability / FSTI** — `card_fsti_012`, "Volley", cost 2, "Deal 2 damage to every enemy active unit."
- **Relay / FSTI** — `card_fsti_016`, "Signal Relay", cost 2, "Occupy a Node slot. Its Active slot controls that Node for you."
- **Protocol / FSTI** — `card_fsti_018`, "Field Repairs", cost 3, "Repair 3 Core durability when played."

For FRTI and FPB examples, reuse the same four cards with the faction skin applied — the
goal is to show the skin difference, not to invent new cards.

### One known mismatch — flag it, don't fix it

The art window is **700 × 385 = 1.818:1**, but the game generates art at **1024 × 640 =
1.600:1**. Fitting 1.6 into 1.818 crops about 12% of the illustration vertically.

Two clean fixes, for Master to choose:

- Widen the art window to **700 × 437** (true 16:10) — costs 52px, taken from the ability panel
- Generate art at **1024 × 563** (1.818) — no layout change

Build to the 700 × 385 box as specified. Raise the mismatch in your handoff notes rather
than silently resizing it.

### Acceptance checklist

- [ ] Canvas exactly 832 × 1216 on every frame
- [ ] All 12 components exist with correct naming
- [ ] Unit frames have two stat bars; the other nine have one merged panel
- [ ] Art window is empty and masked on every frame
- [ ] Cost orb accommodates two digits without clipping
- [ ] Every zone matches the coordinate table exactly
- [ ] Rarity is a frame treatment, not a text string
- [ ] Faction is distinguishable at 220px card height
- [ ] Type is distinguishable at 220px card height
- [ ] All value text layers are empty except on the four review examples
- [ ] No keyword chip row anywhere

---

## Notes for Master (not part of the prompt)

**Where the faction descriptions come from:** `docs/PLAN-v0.17.md` §0 "Factions — LOCKED"
gives the full names and gameplay identities verbatim. The visual directions are my reading
of those gameplay identities — they are a proposal, not canon.

**Where the type colours come from:** `GameCoordinator.AccentFor(CardType)` in the runtime,
so the frame tint and the code tint agree. If you change one, change both.

**Why rarity moved out of the text box:** at hand size the card renders ~220px tall. The
17px ID+rarity box scales to ~3px. A text rarity is unreadable; a border treatment is not.

**The 12-component count:** 4 types × 3 factions. If you also want 4 rarity states per
component, that is 48 visual states from 12 components — Figma variant properties handle
this without extra components.
