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

The scene has no visible or serialized presentation for:

- Four Nodes
- Three Core Active slots
- Three Core Bench slots
- Node Active and Bench slots
- Protocol slot
- Opponent battlefield
- Opponent hand
- Core durability display
- NP display
- Turn and phase display
- Combat target and blocker presentation
- Node-control indicators
- Card-effect feedback
- Deck/discard counters
- Victory and match-result UI
- Full board-state reconstruction from Core events

The current hand animation is presentation work, but the battlefield itself is not represented.

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

The GDD requires:

- 3 Core Active slots
- 3 Core Bench slots
- 4 Nodes
- 1 Active slot per player at each Node
- 2 Bench slots per player at each Node
- 30–40 card decks
- 5-card starting hands
- No MVP mulligan

The current scene has no battlefield slot objects, and the Core model contains:

```csharp
public const int NodeCount = 3;
```

in `Assets/Scripts/Core/Board.cs`.

**Status:** Failed against the current GDD. The code model and scene both need reconciliation.

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

The current code has Node-control counting, NP gain, Core defeat, and deck operations. But it models only three Nodes, and the complete four-Node victory/multiplier behavior is not verified.

**Status:** Partial and not GDD-complete.

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

The current scene provides hand-card animation and drag/play behavior. It does not provide battlefield views, combat feedback, state-driven board reconstruction, opponent presentation, or victory presentation.

**Status:** Partial.

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
