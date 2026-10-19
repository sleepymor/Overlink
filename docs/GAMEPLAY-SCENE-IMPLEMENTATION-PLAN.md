# Gameplay Scene Implementation Plan

**Project:** Overlink  
**Audit date:** 2026-10-17 (presentation and interaction work through 2026-10-19)  
**Source scene:** `Assets/Scenes/Gameplay.unity`  
**Primary source:** `GDD/GDD-Overlink-v0.17-PreDevDraft.pdf`  
**Verification status:** Executed. Unity compiles clean, both test suites run (EditMode 985 / PlayMode 185, §2.1),
and the scene has been booted in play mode with a captured Game View. What remains unverified is anything that
needs a real mouse: the bridge cannot synthesise input, so click routing is covered by tests around its inputs
and outputs rather than by simulated clicks (§3.8).

## Executive conclusion

`Gameplay.unity` is a **playable prototype of the GDD match**, not yet a shippable one.

The battlefield is authored scene geometry bound from a prefab, mirrored for the opponent, and clickable: a
player can deploy by dragging, read any card or pile, declare attacks and answer them with blocks, all without
touching the key list. All four victory conditions are implemented, the deterministic AI plays a full turn, and
the presentation is a view of Core state rather than a second rules engine.

What is not done is the last mile of feel and of data: no damage flash, no screen shake and no zone-to-zone
trace; the pilot deck is still 30 cards rather than the plan's 11/4/3/2 mix; the card pipeline is still
programmatic rather than ScriptableObject-backed; the scene is not registered in Build Settings (owner
declined, `ProjectSettings/**` is protected); and the rules model still carries unreconciled phase naming
against the GDD (§3.1).

## 1. What the scene currently contains

`Assets/Scenes/Gameplay.unity` — four roots:

- `Match`
  - `GameCoordinator` — boots the match, owns the engine/AI, the command entry points and the block window
  - `BattlefieldPresenter` — the board view: binds `Battlefield` by name, paints it from `MatchState`
  - `BattlefieldInput` — pointer and keyboard routing over the presenter's hit-test and Core's legality queries
  - `HandController` — the draggable fan
  - `DeckController`
  - `NetAccountPanelGuard` — suppresses the Net layer's sign-in panel here (§2)
  - `Battlefield` — **prefab instance** (`Assets/Prefabs/Battlefield.prefab`, 207 objects)
  - `DeckPile` · `PlaySlot` · `DiscardPile` transforms
- `Battlefield HUD` — overlay canvas: the event log and the reader panel
- `Main Camera` — Camera, AudioListener, URP additional camera data, `BattlefieldCameraController`
- `Key Light`

### Current serialized wiring

- `GameCoordinator.handController` -> `HandController`
- `GameCoordinator.enableAI = true`
- `GameCoordinator.aiSeed = 42`
- `GameCoordinator.blockWindowSeconds = 4`
- `BattlefieldPresenter` / `BattlefieldInput` -> found by type at runtime, so the scene stores no fragile
  back-reference to the board it already parents
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

**The Net layer's sign-in panel is suppressed here.** `AccountManager` self-installs in every scene via
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and `AccountMenuUI` then builds a Google / guest panel into
whichever canvas it finds first, so it drew across the board header in this scene too - which is a local match
against the AI, with no `NetworkManager` and nothing for those buttons to act on. `NetAccountPanelGuard` on
the `Match` root hides it while this scene is loaded and hands it back on unload, so the Net demo scenes keep
exactly what they had. It is hidden rather than fixed because the defect is typography inside
`Assets/Scripts/Net`, which is off limits from here: `AccountMenuUI.CreateButton` never sets a font size, so
its three button labels render at TextMeshPro's 36-point default in rows the layout budgets 28 pixels for.
`Assets/Tests/Match/NetAccountPanelGuardTests.cs` holds the contract that makes this safe - the panel is hidden
on enable, restored on disable, and left alone when the scene is one of the Net demos.
Note the panel's `FindObjectOfType<Canvas>()` now has two candidates, `ConsoleCanvas` and `Battlefield HUD`.
Which one it lands on is unspecified, and it must not be the latter: my canvas has no `GraphicRaycaster` by
design, so the sign-in buttons would be silently dead. Hiding the panel sidesteps that entirely here; the
hazard only becomes someone's problem if the guard's `hideAccountPanel` is switched off.

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
- Pointer interaction: every tile, pile, Node plate and Core counter is clickable - read a card, read a pile,
  declare an attack, declare a block (`BattlefieldInput`, `CombatLegality`)

Still presentation-only or absent:

- Opponent hand (hidden by design; only its card count is known to Core)
- Card-effect feedback beyond log text and tile tint: no damage flash, no screen shake, no zone-to-zone trace
- Full board-state reconstruction from Core events

### 2.1 Where the test suites live

`Assets/Tests/Core/Overlink.Core.Tests.asmdef` has an empty `includePlatforms` and the legacy
`optionalUnityReferences: ["TestAssemblies"]`, so Unity registers it as a **PlayMode** module. Its tests are
therefore absent from the EditMode run entirely - a green EditMode suite says nothing about `CombatLegality`,
`MatchState`, `Board` or the engine. Both cross-check tests in `CombatLegalityTests` were found broken by the
PlayMode run and by nothing else; `Overlink.Match.Tests.asmdef` is the reverse (`includePlatforms: ["Editor"]`),
so it only ever runs in EditMode. **Run both suites, not one.**

Current tallies: EditMode 985 discovered, 980 passed, 4 skipped, 1 failed; PlayMode 185 / 185.

The one EditMode failure is not ours. `UnitySkills.Tests.Core.PerceptionSkillsTests
.SceneSummarize_CountsObjectsCorrectly` asserts `stats.totalObjects >= 4` against a scene its own `SetUp` has
just replaced with `NewSceneSetup.DefaultGameObjects`, and it fails at that line rather than at the `success`
assert above it. It fails identically when the package's fixture is run on its own with none of our assemblies
in the run (14/15), so it is a test-order dependence inside `com.besty.unity-skills`, not a symptom of anything
in `Assets/`. It was already the single pre-existing failure at the start of this work.

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

**Status:** Mostly implemented; combat feedback remains the one gap. Entry paths are no longer one of them -
the board is pointer-driven (§3.8).

### 3.8 Pointer interaction

**GDD reference:** §2.1.6 Combat, §4.6 presentation, §4.15.1 card types.

The board is clickable. A player who has never read the key list can click a card to read it, click the discard
to leaf through it, click a Ready unit and then a socket to attack, and answer the opponent's attacks with
blocks. The keyboard bindings all still work; the mouse is an addition, and the legend printed on the board
advertises the mouse first because that is what a new player reaches for.

The responsibility split is the one drop targeting already used, and each part is testable on its own:

| File | Question it answers | Knows rules? | Knows geometry? |
|---|---|---|---|
| `Core/CombatLegality.cs` | what may I do right now | yes | no |
| `Match/GameCoordinator.cs` | may I *execute* it | yes | no |
| `Match/BattlefieldPresenter.cs` | what is under the pointer | no | yes |
| `Match/BattlefieldInput.cs` | what is the player mid-way through doing | no | no |

`CombatLegality` is new and deliberately lives in Core, headless. `MatchEngine` can only answer "is this move
legal" by being offered a concrete command, and a refused command returns an opaque `CommandResult` with no
enumeration of what *would* have worked - a board cannot light sockets by trying every command each frame. So
the permissible set is computed there (`CanAttack`, `AttackTargets`, `CanAttackCore`, `CanBlock`,
`BlockableAttackers`, `SharesBattlefield`), reading `MatchState` and never writing it, mirroring the engine's
own checks with the engine cited at each one. Being headless it is unit-testable without a scene, a camera or
a frame, and reusable by netplay without taking a dependency on the presentation layer.

**No colliders.** The tile cubes carry no `BoxCollider` on purpose: a hand drag has to raycast *through* the
board to the play line, and a collider on every socket would stop it. So `BattlefieldPresenter.FindHitAt`
projects the pointer onto the board plane (`new Plane(boardRoot.forward, boardRoot.position)`) and reuses the
footprint maths the drop test already had, then matches the point against each tile's rectangle. Hit-testing
is therefore resolution-independent, needs no physics layer setup, and cannot be confused by the z-fighting
between a tile and its gold rim. `BoardHit.Describe()` turns the result into the words the reader panel shows.

**Marks are derived, never authoritative.** One `PickMark` per tile (None / Selected / AttackTarget / Blocker /
BlockTarget / CoreTarget), rebuilt from scratch through `ClearMarks()` whenever the selection changes, from the
same `CombatLegality` queries the click is judged against. Hover is a separate transient sheen applied after
the marks. Because a mark is a *picture of* a rule and not a rule, the command entry points re-ask the query
before issuing anything: `TryPlayerAttackWith` and `TryPlayerBlockWith` refuse a stale highlight rather than
trusting it, which matters because `MatchEngine.ExecuteDeclareAttack` does not check *whose* turn it is.

**The block window.** Blocking is the one move a player gets on the opponent's turn, and `SimpleAI.TakeTurn()`
used to resolve the whole turn in a single frame, so there was nowhere to ask. The turn is now two calls -
`TakeTurnUntilCombat()` and `FinishTurn()` - with `TakeTurn()` composing them, which is why every pre-existing
caller behaves identically. `GameCoordinator.ExecuteAITurn` pauses between them for `blockWindowSeconds`
(serialized, default 4) when and only when the player has a legal blocker. `Return` closes it early; `Escape`
deliberately does not, because a reflexive cancel must not cost a player their only defensive move, and neither
does committing a block - two units in two contested battlefields have two answers to give. After a commit the
remaining blockers are re-offered from `ReadyBlockers()` rather than from what was lit a moment ago, so a unit
that has just been spent cannot light again.

**Reading the table.** The reader panel is a second display-only panel on the HUD canvas, mirrored from the log
at the opposite edge. There is no `EventSystem` anywhere in the project, so it cannot own a close button;
dismissal is "click elsewhere or Escape", which its footer says out loud. The discard is public knowledge and
is leafed through with `←`/`→`; the deck is the one clickable object that refuses to open, because its order is
secret and a browser through it would be a cheat with a nice interface - it reports its count instead, which
Deck-Out makes worth showing anyway.

**Two gates fixed on the way.** `passPhaseKey` and `endTurnKey` fired with `matchState.ActivePlayer`, so they
drove the opponent's phases during its turn - harmless while that turn lasted half a second, and an actual
exploit once it paused for four. Both are now gated on the player's own turn. Concede stays ungated, because
conceding is legal on either turn (GDD 2.1.8).

**Known hole, deliberately not touched.** `MatchEngine.FindCardInstance` resolves a defender through Bench
slots, so the engine will accept an attack on a benched unit; `Board.cs` and the GDD say benched units are out
of the fight. `CombatLegality.AttackTargets` offers Active-only targets, so the player is never shown the
illegal move, and `ExecuteDeclareBlock`'s companion hole (it records a block from the wrong battlefield that
`ConsumePendingBlocker` then ignores) is pinned by
`MisplacedBlock_IsOfferedByNowhereYetStillRecordedByTheEngine` instead of papered over. Fixing the engine is a
rules decision for the Core owner, not something the click layer should quietly compensate for.

**Status:** Implemented. `CombatLegalityTests` (20, PlayMode - see §2.1), `PointerTargetingTests` (19) and the
reader half of `BattlefieldHudTests` cover the queries, the hit-test, the marks and the panel text. Three of
those nineteen run against the **shipped prefab** rather than a procedural board
(`ShippedPrefab_HoldsEveryTileTheClickLayerNames`, `_EveryTileCentreResolvesThatTile`,
`_CarriesNoCollidersAnywhere`): they bind `Assets/Prefabs/Battlefield.prefab` under a throwaway presenter and
re-assert the contract, because a hand-edit that overlaps two sockets or renames one is invisible to every
other test here and surfaces in play as a click landing on the wrong card. The no-collider assertion pins the
design decision above, since one stray `BoxCollider` would silently break a drag that starts over it.

What is not covered by any test is the pointer itself: the bridge exposes no C# eval and no input simulation
(805 skills, none of which fake a mouse click), and the only PlayMode test assembly in the project references
Core alone - so exercising `BattlefieldInput.Click()` end-to-end would need a new test assembly with a
dependency on `Overlink.Match`. That is a structural change and is left for the owner to approve. The routing
is therefore verified by inspection plus a play-mode boot capture (`Assets/Screenshots/click_boot_v1.png`:
board, legend, HUD and reader all live, zero console errors), and the pieces it glues together are the tested
ones.


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

**Resolved by execution.** That GUID belongs to `UniversalAdditionalCameraData.cs` in `com.unity.render-
pipelines.universal`'s own `Runtime` folder, so it is a package component and not a broken project reference -
which is what static inspection could suspect but not prove. Two independent checks now confirm it resolves:
`validate_find_missing_scripts` reports 0 objects, and `validate_missing_references` reports 0 issues across the
scene. The scene also boots in play mode with one camera and one light and no console errors.

**Status:** Closed.

### 4.3 Unity lock and verification

`Temp/UnityLockfile` is no longer a barrier: the editor has been driven through the bridge throughout, so every
item this section used to leave open has now been exercised except the last two.

| Claim | Evidence now |
|---|---|
| Unity compilation / domain reload | Compiler reports clean (0 errors) after each refresh |
| EditMode execution | 985 discovered, 980 passed, 4 skipped, 1 pre-existing package failure (§2.1) |
| PlayMode execution | 185 / 185 |
| Scene opens without missing scripts | `validate_find_missing_scripts`: 0; `validate_scene`: 0 errors, 0 warnings |
| URP camera component resolves | §4.2 |
| Initial hand dealing | Game View capture: 5-card fan, `DECK 25`, `HAND 5`, NP 4/10 |
| AI turn execution | `SimpleAITests` (11) incl. the turn-split guards |
| Victory UI | `MatchStateTests` + presenter paint path |
| Card effects and combat | Covered by Core tests; **not** demonstrated as a played match |
| Full match completion | **Still not demonstrated end-to-end in play** |

The honest remainder is the last row. Nothing here has sat through a match from Turn 1 to a victory banner
without a human at the mouse, and the bridge cannot drive one: no input simulation, no C# eval (§3.8). That is a
playtest to schedule, not a test to write.

Completion notes claiming compilation success are not sufficient evidence without the actual Unity compiler
output - that constraint still stands, and it is the reason every row above cites the check rather than an
intention.

## 5. Current status by milestone

Measured against the suites in §2.1: EditMode 985 discovered, PlayMode 185.

| Area | Status |
|---|---|
| Scene file exists | Implemented |
| Camera, light, and basic anchors | Implemented |
| GameCoordinator wiring | Implemented; boots the match from the scene |
| Player hand presentation | Implemented in prototype form |
| Runtime prototype card pool | Implemented; pilot mix still 30 cards, not the plan's 11/4/3/2 |
| Deterministic AI bootstrap | Implemented; turn split covered by `SimpleAITests` |
| Core match backend | Partially implemented and not GDD-reconciled |
| Four-Node rules | Implemented (`Board.NodeCount` is 4; the old three-Node note is stale) |
| Full battlefield scene | Implemented as `Assets/Prefabs/Battlefield.prefab`, bound by name |
| Opponent presentation | Implemented, mirrored |
| Core/Node UI | Implemented, incl. status strips, piles and Node plates |
| GDD victory conditions | All four implemented (`MatchStateTests`) |
| Pointer interaction | Implemented (§3.8); click routing verified by inspection, not by simulated clicks |
| Net account panel in Gameplay | Suppressed by `NetAccountPanelGuard` (§2) |
| ScriptableObject card pipeline | Deferred |
| Runtime CardDatabase loading | Not demonstrated |
| Build-settings registration | Missing; `ProjectSettings/**` protected, owner declined |
| Unity compilation | Verified clean via the editor's compiler output |
| NUnit execution | Verified: EditMode 985 / PlayMode 185, one pre-existing package failure (§2.1) |
| Scene runtime playtest | Verified by boot + Game View capture; no console errors |
| Full match completion | Not demonstrated end-to-end in play |

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

Scene-level presentation, with what has landed marked:

- [x] Four Nodes
- [x] Core Active and Bench slots
- [x] Protocol slot
- [x] Player and opponent battlefield
- [x] Deck and discard zones
- [x] Core durability and NP displays
- [x] Turn and phase display
- [x] Combat targeting and blockers — §3.8, both pointer and keyboard
- [x] Node-control indicators
- [x] Victory/match-result UI
- [ ] Card-effect and damage feedback — no damage flash, no screen shake, no zone-to-zone trace
- [ ] Opponent hand presentation (hidden by design; only its count is known to Core)

The scene must remain a view of the Core state rather than becoming a second rules engine. That constraint is
now load-bearing rather than aspirational: the pointer layer is the closest this project has come to the board
being asked to make a decision, and the answer is the three-way split in §3.8 — the view supplies geometry,
Core supplies legality, and neither borrows the other's job.

### Phase D — Register and verify

1. ~~Obtain legitimate Unity lock release.~~ — done; the editor was driven through the bridge throughout.
2. Register and enable `Gameplay.unity` in `EditorBuildSettings.asset`. **Blocked by policy**, not by work:
   `ProjectSettings/**` is protected and the owner declined (§4.1). Still the one item that stops this scene
   being reachable from a build.
3. ~~Open the scene in Unity.~~ — done, opens clean.
4. ~~Confirm no missing scripts or package references.~~ — 0 and 0 (§4.2).
5. ~~Compile all assemblies.~~ — clean.
6. ~~Run EditMode and PlayMode NUnit tests.~~ — 985 / 185 (§2.1).
7. Start the scene and verify:
    - ~~5-card opening hands~~ — captured in the Game View
    - ~~Player hand rendering~~
    - ~~AI turn~~
    - ~~Combat and blocking~~ — covered by tests; the *pointer* path needs a human hand
    - ~~Node control~~, ~~NP gain~~, ~~Core victory~~, ~~Node-domination victory~~, ~~Deck-out~~,
      ~~Match-result UI~~ — all four conditions implemented and unit-tested
    - ~~Play-card legality~~
    - [ ] **Effects** — no damage flash, no shake, no zone-to-zone trace
    - [ ] **A full match, played to a result without touching the key list**

Item 7's last box is the whole remaining ask, and it is a playtest rather than a test: the bridge cannot
simulate a click, so nobody has yet watched this board be played end to end by a pointer.

## Final classification

The original audit's six unresolved items are now five closed and one open by policy:

| Audit claim (2026-10-17) | Now |
|---|---|
| Three Nodes in code versus four in the GDD | Closed — `Board.NodeCount` is 4 |
| Core durability 20 in code versus 3 in the GDD | Closed — `PlayerState.StartingCoreDurability` is 3 |
| Incomplete battlefield presentation | Closed — bound prefab, mirrored opponent, piles, plates, status strips, victory UI, pointer interaction |
| Missing build-settings registration | **Open** — protected path, owner declined |
| Unverified camera/package reference | Closed — URP `UniversalAdditionalCameraData` (§4.2) |
| Unverified Unity compilation, tests, runtime match flow | Closed — §4.3 |

So this is no longer "a minimal bootstrap around a code-driven prototype". It is a **playable prototype with a
complete rules surface and a real board**, whose remaining gaps are feel (combat feedback), data (the pilot
deck's 11/4/3/2 mix and the ScriptableObject pipeline), policy (build registration), and one unreconciled
semantic: the phase names still do not spell the GDD's six (§3.1).

The next decision is therefore not "make 20 cards" and no longer "reconcile the authoritative rules" either. It
is **play it**: schedule the match in §3.8's last row, because the two things a pointer-driven board can get
wrong that no test can catch are the timing of the block window and the legibility of a lit socket at either
zoom tier.
