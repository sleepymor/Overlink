# Gameplay Scene Implementation Plan

**Project:** Overlink  
**Audit date:** 2026-10-17  
**Source scene:** `Assets/Scenes/Gameplay.unity`  
**Primary source:** `GDD/GDD-Overlink-v0.17-PreDevDraft.pdf`  
**Verification status:** Static inspection only; Unity execution remains unverified while `Temp/UnityLockfile` exists.

## Executive conclusion

`Gameplay.unity` is currently a **minimal hand-and-match bootstrap scene**, not a complete GDD-compliant gameplay scene.

The scene exists and wires the player hand to a code-driven match coordinator and deterministic AI. However, the battlefield presentation, build registration, Unity validation, and several rules-model alignments remain incomplete.

The remaining work is **not limited to creating 20 cards**. A 20-card programmatic pilot pool already exists in `Assets/Scripts/Match/PrototypeCardPool.cs`. The scene and rules implementation still require reconciliation and verification.

## 1. What the scene currently contains

`Assets/Scenes/Gameplay.unity` contains:

- `Match`
  - `GameCoordinator`
  - `HandController`
  - `DeckController`
  - `DeckPile` transform
  - `PlaySlot` transform
  - `DiscardPile` transform
- `Main Camera`
  - Camera
  - AudioListener
  - URP additional camera data reference
- `Key Light`

### Current serialized wiring

- `GameCoordinator.handController` -> `HandController`
- `GameCoordinator.enableAI = true`
- `GameCoordinator.aiSeed = 42`
- `HandController.deck` -> `DeckController`
- `HandController.deckPile` -> `DeckPile`
- `HandController.playSlot` -> `PlaySlot`
- `HandController.discardPile` -> `DiscardPile`
- `HandController.autoDraw = false`
- `HandController.handSizeStart = 5`

`GameCoordinator` creates the runtime match in code using:

- `PrototypeCardPool.All()`
- `PrototypeCardPool.BuildDeck()`
- `MatchState`
- `MatchEngine`
- `SimpleAI`
- `HandController.SetHand()`

This is a valid **runtime bootstrap skeleton**.

## 2. What is missing from the scene

The battlefield is now authored geometry rather than a blank slate. `Gameplay.unity` ships a `Battlefield`
**prefab instance** under the `Match` root (`Assets/Prefabs/Battlefield.prefab`, 207 objects), built
deterministically by `Overlink > Battlefields > Build Battlefield Prefab`. At runtime
`BattlefieldPresenter.BuildBoard()` finds that child by name and **binds** to it; if the child is ever
missing it falls back to constructing the same hierarchy procedurally, so nothing regresses.

The scene's fourth root, `Battlefield HUD`, is **not** part of that prefab: it is an overlay `Canvas`
(`BattlefieldHud`) that owns the event log. Board geometry lives on the tilted `Match` root and is framed by
the camera; the log is pinned to the screen, so it stays legible at either zoom tier and the tabletop keeps
the column it used to reserve for it. `BuildGameplayScene` builds it eagerly so it is an editable object, and
the presenter binds to it, creating one lazily only in play mode (creating it during the editor prefab build
would leave a stray root object behind).

**Grouped hierarchy.** The board is nested for editing rather than laid out flat, so a whole region is one
selectable object in the Scene view:

```
Battlefield
├── Table · Header · ControlsHint · Victory          (board furniture, flat by design)
├── Core
│   ├── Player  → Active / Protocol / Bench          (each rank its own group)
│   ├── Opponent → Active / Protocol / Bench
│   └── CoreCaption
├── Nodes → Node 1 … Node 4                          (group carries the centre offset)
│            └── Player / Opponent → Active / Bench
├── Piles → DeckPile · DiscardPile                   (stacked cubes, not sockets)
└── Status → OStatus · PStatus

Battlefield HUD (separate scene root, overlay Canvas)
└── LogPanel → LogHeader · LogText                   (the event log, as UI)
```

Only the *tile* names stay contractual (`PlayCoreBench3`, `NodePlayBench2_1`, …); the groups above them are
free to rename, reparent or move. Binding resolves tiles through a name index over the whole subtree rather
than a child path, and the drop hit-test converts each tile through its world transform and divides the
body's lossy scale by the board root's, so moving or scaling a `Bench` or a whole `Node 3` group in the
editor moves its drop footprint with it. Each socket is also one object now — its cube, its gold rim and its
caption are children of it — so dragging a single socket never strands its text. A duplicated tile name is
treated as ambiguity and fails the bind (rebuilding deterministically) rather than resolving first-wins.

Represented today:

- Four Nodes, each with Active and Bench slots
- Core battlefield with 1 Active + 4 Bench slot per player, plus the Protocol slot
- Opponent battlefield, mirroring the player's so both Actives face each other across the table centre
- Core durability display
- NP display
- Turn and phase display
- Node-control indicators
- Match log panel (screen-space UI, not tabletop geometry)
- Deck and discard piles with live counts (`Piles` group; not sockets, never drop targets)
- Victory and match-result UI

Still presentation-only or absent:

- Opponent hand (hidden by design; only its card count is known to Core)
- Combat target and blocker presentation
- Card-effect feedback beyond log text
- Full board-state reconstruction from Core events

## 3. GDD requirements mapped to implementation

### 3.1 Turn structure

**GDD reference:** §2.1.2, pages 13–15.

The GDD defines six phases:

1. Upkeep
2. Draw
3. Main
4. Combat
5. End
6. Cleanup

The current code uses:

```text
Draw -> Standby -> Main -> Combat -> End -> Opponent
```

`Standby` appears to perform the GDD Upkeep work, while `Opponent` performs turn handoff. The behavior is not documented as an explicit mapping and there is no explicit `Cleanup` phase name.

**Status:** Partial. The transition framework exists, but the semantic mapping must be reconciled with the GDD before completion is claimed.

### 3.2 Battlefield structure

**GDD reference:** §2.1.3, page 15; §4.16 battlefield presentation.

The GDD PDF lists:

- 3 Core Active slots
- 3 Core Bench slots
- 4 Nodes
- 1 Active slot per player at each Node
- 2 Bench slots per player at each Node
- 30–40 card decks
- 5-card starting hands
- No MVP mulligan

**Deviation, deliberate:** the 3+3 Core in the PDF is treated as stale and is superseded by the
owner's stated layout — **1 Core Active + 4 Core Bench** per player. `Board.cs` is the authority:

```csharp
public const int CoreActiveSlots = 1;
public const int CoreBenchSlots = 4;
public const int NodeCount = 4;
public const int NodeActiveSlots = 1;
public const int NodeBenchSlots = 2;
public const int NodesForDomination = 3;
```

All of the above are now met. Decks (30 cards) and 5-card starting hands are in place; the mulligan
entry point `MatchEngine.ExecuteMulligan` exists as a no-op, which satisfies "no MVP mulligan".

**Board geometry, screen order.** Every battlefield fields two ranks per player and the two halves are
**mirrored** about the table centre: the Active is always the *inner* rank, facing the opponent and
drawn larger (5:7 portrait, like a card), with the Bench rank behind it toward its owner's own edge.
Bottom of screen to top: **player Bench → player Active → opponent Active → opponent Bench**. The node
plates use the same shape at a smaller scale. This was verified by pixel probe of a runtime screenshot
(`Assets/Screenshots/prefab_v2.png`).

On the Core the Active is the focal socket, so it is the largest tile on the table (1.10 × 1.54) and sits on
the board's centre line (`x = 0`) rather than being pushed aside for the Protocol, which keeps its own
0.72 × 1.008 footprint beside it at `x = 1.05`. The Bench rank is a touch tighter (0.66 × 0.924) and sits at
`y = ±2.5`: being the furthest row from the centre line it is also the furthest from the camera, and the
smaller footprint is what leaves the status strip a clear band before the hand fan. Because the hit-test
resolves each tile through its own world transform and effective scale, enlarging a socket widens its drop
target for free. (`Assets/Screenshots/layout_final_tilt.png`)

**Deck and discard piles.** `BattlefieldPresenter` also prints the two piles onto the table (`Piles` group,
`DeckPile` / `DiscardPile` at `x = ±3, y = -4.35`, flanking the hand fan). Each is a top card plus two cubes
stacked behind it and nudged off the corner, painted with a card-back tint deliberately cooler than any empty
socket, and it carries the live count (`DECK 25`, `DISCARD 0`) so a deck draining towards the Deck-Out
condition is legible. They are **not** sockets: they parse to no slot identity, are never probed as drop
targets, and `PaintPile` is separate from `PaintTile`. Previously the hand's draw and discard anchors were
invisible markers, so cards appeared from nowhere and vanished into nowhere; `SyncPileAnchors()` moves those
anchors onto the piles once the board exists, which also means dragging the `Piles` group in the editor takes
the card flight path with it.

**The tabletop shrank when the log left it.** `TableWidth` / `TableDepth` are public constants on the
presenter (16.5 × 11.4) because the camera's overhead tier is defined by them - its test reads them rather
than copying the numbers. The width used to be 19.5 units, roughly four of which belonged to the log panel on
the right, so keeping it would have framed the board with a column of empty felt.

**Socket targeting.** Each tile pins itself to the Core slot it renders, keyed off its name
(`BattlefieldPresenter.TryParseSlotIdentity`), so a dragged Unit or Relay can be dropped on a chosen socket
instead of landing wherever `Board.TryFindFreeSlot` happened to prefer. The hit-test
(`BattlefieldPresenter.FindDropTarget`) is geometry alone — it probes only the player's own hosting sockets,
ignores depth because a dragged card floats above the table on the board's local Z, and judges no rules.
`GameCoordinator.ResolveDropTarget` keeps the highlight solely when the play would actually be legal there
(card type via `BoardSlot.CanHost`, vacancy, own turn, Main phase, enough NP), so a dark board is itself the
signal that the move is not currently allowed. A plain fling past the play line still auto-spreads, which
keeps the old fast path and the standalone hand working. Every refusal now also prints its reason on the
board through `GameCoordinator.Reject` rather than hiding it in the Editor console.

Two Core rules bound what a lit socket can promise, and neither is a presentation bug:

- Plays are legal in the **Main** phase only (GDD §2.1.2) and a turn opens in **Upkeep**
  (`MatchState.InitializeMatch`), so the first drag of a match is refused until the phase has been passed
  twice.
- `MatchState.ResolveDestructionAndPromotion` runs after every command and never leaves an Active socket
  vacant. A Bench drop made while your Active is empty therefore still ends with the unit in Active: the
  Bench only holds a unit once its Active is occupied. The play is accepted rather than refused, because
  blocking it would cost the player their action for no gain, and the promotion narrates itself in the
  match log.

**Status:** Implemented and runtime-verified as a view of Core. The tile-name to slot contract, the
socket hit-test against a built board, and the engine honouring a nominated socket are covered by
`Assets/Tests/Match/DropTargetTests.cs`, including the promotion behaviour above.

### 3.3 Node control and victory

**GDD references:** §2.1.7, pages 21–23; §2.1.8, page 24.

Required behavior:

- Neutral, Contested, and Controlled Node states
- Immediate recomputation after actions and destruction batches
- +1 NP per controlled Node
- Node-specific Core multipliers
- Node domination victory by controlling 3 of 4 Nodes at Upkeep
- Core destruction victory
- Deck-out victory
- Concession victory

The current code has Node-control counting, NP gain, Core defeat, and deck operations, and now models
four Nodes with the four multipliers (Node 1 +1 ATK, Node 2 +1 HP, Node 3 First Strike, Node 4
draw-on-kill) and `NodesForDomination = 3`. Simultaneous both-Cores-down resolves to the active player.

All four GDD §2.1.8 conditions are now implemented, and each victory records a human-readable
`MatchState.ResultReason` (`BOTH CORES DESTROYED`, `CORE DESTRUCTION`, `NODE DOMINATION`, `DECK-OUT`,
`YOU CONCEDED` / `OPPONENT CONCEDED`) which the victory banner prints under the result word:

- **Node Domination** — `CheckNodeDomination()` at Upkeep, 3 of 4 Nodes.
- **Core Destruction** — `CheckVictoryConditions()`, both players checked.
- **Deck-Out** — `OnDrawPhase()` fails the match when a player cannot draw **and** holds no cards,
  matching the GDD wording ("cannot draw a card during their Draw phase and has no cards in hand").
- **Concession** — `CommandType.Concede` (appended after `EndTurn`, so no existing command ordinals
  shift), `ConcedeCommand`, `MatchState.Concede(PlayerIndex)`, dispatched by `ExecuteConcede`. The
  engine's active-player gate exempts it alongside `DeclareBlock`, so the losing player can resign on
  the opponent's turn. Bound to **Q** in `GameCoordinator` and shown in the controls hint.

One caveat blocks the Deck-Out condition from ever firing in practice: `PlayerState.DrawCard()`
reshuffles the discard into the deck when the deck runs dry. The GDD contains no reshuffle rule, and
a legal 30–40 card deck cannot run out against at most 18 board slots, so Deck-Out is currently
unreachable. Removing the reshuffle (GDD-literal) versus keeping it as an explicit house rule is an
open decision; `MatchStateTests.PlayerState_DrawCard_ReshufflesWhenDeckEmpty` asserts the current
behaviour and would have to be inverted if the reshuffle goes.

**Status:** All four victory conditions implemented and unit-tested; Deck-Out is gated behind the
open reshuffle decision above.

### 3.4 Core durability

**GDD reference:** §2.1.7, pages 22–23.

The GDD states that Core durability starts at **3**.

The current code contains:

```csharp
public const int StartingCoreDurability = 20;
```

in `Assets/Scripts/Core/PlayerState.cs`.

The implementation also needs verification for:

- Direct Core attack damage
- Core Active destruction loss
- Damage transfer from Core Actives
- Simultaneous damage/destruction batches
- Victory resolution ordering

**Status:** Failed until the GDD-versus-plan discrepancy is explicitly resolved.

### 3.5 Card and deck data

**GDD references:** §2.1.9, page 24; §4.5, page 43.

The GDD requires:

- 30–40 card decks
- Maximum 3 copies of a card
- Single faction in MVP
- Card definitions authored as ScriptableObjects
- Export to JSON
- Runtime CardDatabase loading and validation

The current runtime prototype provides:

- 20 programmatic card definitions
- IDs `card_fsti_001` through `card_fsti_020`
- Single FSTI faction
- A 30-card generated deck
- Maximum two copies per card
- No Unity CardAsset instances
- No demonstrated CardDatabase startup load

Because the current direction explicitly avoids Unity assets for now, the absence of `.asset` cards is not itself a defect. It does mean the production ScriptableObject-to-JSON pipeline is deferred.

The actual prototype distribution is:

```text
11 Units
4 Abilities
2 Relays
3 Protocols
```

The earlier M4 plan target was:

```text
11 Units
4 Abilities
3 Relays
2 Protocols
```

The GDD's 30-card reference composition is:

```text
16 Units
6 Abilities
4 Relays
4 Protocols
```

The GDD identifies that final composition as a balancing reference, not a strict legality rule. The pilot distribution therefore needs an explicit design decision rather than an assumed correction.

**Status:** Prototype-complete; production data pipeline incomplete.

### 3.6 Card and effect architecture

**GDD references:** §4.4, page 40; §4.5, page 43.

The GDD requires:

- Data-driven card definitions
- Ordered effect descriptors
- A single EffectInterpreter over the authoritative EffectType vocabulary
- Runtime validation at database startup
- Presentation that observes Core events without mutating Core state

The current code has the data-driven Core and an EffectInterpreter path, but the scene does not expose the full battlefield or event-driven presentation required by the GDD.

**Status:** Core direction is present; scene/presentation integration is incomplete.

### 3.7 Presentation layer

**GDD reference:** §4.6, page 45.

The presentation layer must:

- Subscribe to Core events
- Never mutate Core state
- Animate cards entering zones
- Show damage feedback and screen shake
- Rebuild views from GameState snapshots

The scene now provides hand-card animation, drag-to-socket play, a state-driven battlefield view
(`BattlefieldPresenter`, bound from `Assets/Prefabs/Battlefield.prefab`), mirrored opponent presentation,
per-player status strips, victory/result presentation, and - as screen-space UI on its own overlay canvas
rather than as board geometry - an event log. What it still lacks is combat
feedback: no damage flash, no screen shake on a Core hit, and no zone-to-zone trace for a card entering a
socket, which simply repaints on the next refresh.

**Two-tier zoom** (`BattlefieldCameraController`). The wheel has one continuous travel with two legs. The
tilted leg zooms between `minDistance` and `maxDistance` at the authored 52-degree pitch, and **the opening
framing is the ceiling** - `BuildGameplayScene` seeds `maxDistance` from the same `distance` constant it
writes to the camera, so the match begins at the deepest tabletop zoom-out and the first scroll out is never
a wasted notch. Past that ceiling the same input cranes the camera up instead of pulling it back: distance
grows toward `birdDistance` over `birdTravelNotches` notches while `CurrentPitch` smoothsteps from `pitch` to
`birdPitch` (89 rather than 90, which keeps the ground-plane raycast that drives drag-panning
non-degenerate). The blend is read off the *eased* distance, so pitch and position arrive together, and the
legs clamp against each other so the camera lands exactly on the ceiling from either direction.
`birdDistance = 18` is chosen to **fit the tabletop rather than fill the screen**: at the authored 38-degree
field of view the overhead frame is about 12.4 x 18.6 board units at the narrowest 1.5:1 aspect the game is
played at, against the 16.5 x 11.4 tabletop, so the table's depth is the tight axis and the board fills the
frame with roughly half a unit of margin top and bottom. `BirdView_OverheadFrameFitsTheTabletopSnugly`
locks both halves of that - it must fit, and it must not be wasteful - reading the table's dimensions from
`BattlefieldPresenter.TableWidth/TableDepth` so retuning the table retunes the camera's contract with it.

**Status:** Mostly implemented; combat feedback and explicit entry paths remain.

## 4. Build and scene integrity issues

### 4.1 Build-settings registration

`ProjectSettings/EditorBuildSettings.asset` currently lists only:

- `Assets/Scenes/SampleScene.unity` — disabled
- `Assets/Scenes/NetPresenceDemo.unity` — enabled

`Assets/Scenes/Gameplay.unity` is absent.

`Assets/Editor/BuildGameplayScene.cs` intentionally refuses to add a new build-settings entry because `ProjectSettings/**` is protected for this milestone. Therefore the scene builder can create the scene but cannot make it build-registered under the current policy.

**Status:** Still open.

### 4.2 Camera reference

The scene contains camera script GUID:

```text
a79441f348de89743a2939f4d699eac1
```

No matching project `.meta` file was found under `Assets`. This may be a URP package component rather than a broken project reference, but static inspection cannot prove package resolution.

**Status:** Requires Unity confirmation.

### 4.3 Unity lock and verification

`Temp/UnityLockfile` is present. Therefore the following have not been proven:

- Unity compilation/domain reload
- NUnit EditMode execution
- NUnit PlayMode execution
- Scene opening without missing scripts
- URP camera component resolution
- Initial hand dealing
- AI turn execution
- Card effects and combat
- Full match completion
- Victory UI

Completion notes claiming compilation success are not sufficient evidence without the actual Unity compiler output.

## 5. Current status by milestone

| Area | Status |
|---|---|
| Scene file exists | Implemented |
| Camera, light, and basic anchors | Implemented |
| GameCoordinator wiring | Implemented statically |
| Player hand presentation | Implemented in prototype form |
| Runtime prototype card pool | Implemented |
| Deterministic AI bootstrap | Implemented in source; runtime unverified |
| Core match backend | Partially implemented and not GDD-reconciled |
| Four-Node rules | Not implemented; current code has three Nodes |
| Full battlefield scene | Not implemented |
| Opponent presentation | Not implemented |
| Core/Node UI | Not implemented |
| GDD victory conditions | Partial/unverified |
| ScriptableObject card pipeline | Deferred |
| Runtime CardDatabase loading | Not demonstrated |
| Build-settings registration | Missing |
| Unity compilation | Unverified |
| NUnit execution | Unverified |
| Scene runtime playtest | Unverified |
| Full match completion | Unverified |

## 6. Recommended implementation order

### Phase A — Resolve authoritative rules

1. Decide whether the GDD or the current plan controls Core durability:
   - GDD: 3
   - Current code: 20
2. Change the Core model to four Nodes if the GDD is authoritative.
3. Reconcile phase names and semantics:
   - Upkeep versus Standby
   - Cleanup versus Opponent handoff
4. Verify Node-domination victory is exactly 3 of 4 Nodes.
5. Verify Node 1–4 Core multipliers.
6. Verify Core damage transfer and simultaneous victory resolution.

Do this before marking M3 or M4 complete.

### Phase B — Lock the pilot card design

1. Keep the programmatic card path temporarily, since Unity assets are deferred.
2. Confirm the intended 20-card distribution.
3. Decide whether the plan's 11/4/3/2 target is authoritative.
4. Keep Basic and Exclusive/full-art presentation metadata separate from gameplay identity.
5. Validate the complete pool with zero errors.
6. Serialize the pilot pool through the agreed runtime path.

### Phase C — Build the real Gameplay presentation

Add scene-level presentation for:

- Four Nodes
- Core Active and Bench slots
- Protocol slot
- Player and opponent battlefield
- Deck and discard zones
- Core durability and NP displays
- Turn and phase display
- Combat targeting and blockers
- Node-control indicators
- Victory/match-result UI
- Card-effect and damage feedback

The scene must remain a view of the Core state rather than becoming a second rules engine.

### Phase D — Register and verify

1. Obtain legitimate Unity lock release.
2. Register and enable `Gameplay.unity` in `EditorBuildSettings.asset`.
3. Open the scene in Unity.
4. Confirm no missing scripts or package references.
5. Compile all assemblies.
6. Run EditMode and PlayMode NUnit tests.
7. Start the scene and verify:
   - 5-card opening hands
   - Player hand rendering
   - AI turn
   - Play-card legality
   - NP gain
   - Node control
   - Combat and blocking
   - Effects
   - Deck-out
   - Core victory
   - Node-domination victory
   - Match-result UI

## Final classification

OpenCode created a **minimal scene bootstrap around a code-driven match prototype**. It did not yet create a complete GDD-compliant Gameplay scene.

The most important unresolved standards issues are:

- Three Nodes in code versus four in the GDD
- Core durability 20 in code versus 3 in the GDD
- Incomplete battlefield presentation
- Missing build-settings registration
- Unverified camera/package reference
- Unverified Unity compilation, tests, and runtime match flow

The next decision is therefore not simply “make 20 cards.” The next decision is to reconcile the authoritative rules, then finish and verify the scene against those rules.
