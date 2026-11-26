# Overlink — Blackbox & Whitebox Testing Plan

**Project:** Overlink (Unity TCG, MVP) — `E:\Games\Overlink`
**Version context:** GDD v0.18 (live doc), 3 factions (fsti/frti/fpb), Unity builds ship via GitHub Actions only.
**Author:** Themis (OpenClaw agent)
**Date:** 2026-11-25
**Review status:** VERIFIED with corrections by Mneme (2026-11-25, child session `agent:mneme:subagent:96f7e6bd-96cc-48b0-8a6c-2bb435ea62ed`)

---

## 1. Goal

Define a concrete testing strategy for Overlink that splits work into:

- **Whitebox** — unit tests against internal C# logic (MatchEngine, MatchState, CardValidator, DeckValidator, EffectInterpreter, PlayerState, Board, CardComposition, CardJson). These run as EditMode NUnit without a Unity build.
- **Blackbox** — end-to-end / build-level tests of the shipped game (deck builder save→load→match, victory conditions through actual play, WebGL/Standalone runtime behaviour). These require a Unity build via CI and/or PlayMode tests.

The plan also identifies what can be verified locally right now versus what must wait for CI/Unity.

---

## 2. Constraints & Facts

| Fact | Source |
|------|--------|
| No Unity installed on the agent host (Tethys) | AGENTS.md §1 |
| Builds ship via GitHub Actions `release-build.yml` (game-ci/unity-builder, Ubuntu) | AGENTS.md §4, `.github/workflows/release-build.yml` |
| Windows (StandaloneWindows64) + WebGL (itch.io) targets | release-build.yml |
| dotnet SDK not available locally on Tethys → `dotnet test` cannot run here right now | runtime check |
| dotnet SDK + Unity available on hyperion (user-reported, not independently verified from this session) | Master, 2026-11-26 |
| Existing EditMode NUnit suite covers Core/Match/Net/Persistence/Editor/Inspection | `Assets\Tests\*` |
| `Overlink.Cards.Tests` (22 tests) is PlayMode-only, never executed | AGENTS.md §4 |
| Three MVP factions locked; `CardValidator` enforces `^card_(fsti\|frti\|fpb)_\d{3}$` | AGENTS.md §2 |
| Deck: 30–40 cards, max 3 copies, single-faction MVP | DeckValidator.cs |
| Card pool: 150 cards (75C/45R/22E/8L), 50 per faction | MvpCardLibrary.cs + CardValidator constants |
| Four victory conditions: Core Destruction, Deck-Out, Concession, Node Domination | MatchState.cs |
| NP: start 20, cap 30 (40 with 3+ Nodes), +4 base +1/Node per Upkeep | MatchState.cs / PlayerState.cs |
| Core durability: 20 starting/max | PlayerState.cs |
| Board: 1 Core Active + 4 Core Bench per player; 4 Nodes × (1 Active + 2 Bench) per player | Board.cs |

---

## 3. Whitebox Testing (unit-level, EditMode NUnit)

### 3.1 What exists (already covered)

From reading `Assets\Tests\Core\*`, current coverage includes:

| Class under test | Test file | Coverage highlights |
|------------------|-----------|---------------------|
| `DeckValidator` | `DeckValidatorTests.cs` | Null/empty deck, 29/30/40/41 card sizes, 4-copy limit, mixed-faction rejection, null entries, unknown IDs, zero/negative counts, legal 30-card counts |
| `CardValidator` | `CardValidatorTests.cs` | Valid unit, flavour length, duplicate ID, missing name, cost 11, negative stats, empty unit, dangling refs, zero amount, target legality (self/commander), repair limits/amounts, GainNP/Draw targeting, keyword support, rarity budget, Relay/Protocol stats, duration bounds, Move/AddKeyword/Summon ReferenceId, ID format (prefix/faction/digits), duplicate keywords, Fast keyword/effect pairing |
| `MatchEngine` | `MatchEngineTests.cs` | Play card (success/insufficient NP/wrong phase/slot occupied/Protocol), declare attack (damage, Core hit for exactly 1, cannot act, position switch, pass phase, end turn, mulligan), FirstStrike, Piercing, concede (both directions, no durability change, result stability after game over); Block/SwitchPosition/Retreat/Mulligan/PassPhase/EndTurn coverage not confirmed |
| `MatchState` | `MatchStateTests.cs` | exists; NP economy partially covered, phase-machine coverage not confirmed |
| `CardComposition` | `CardCompositionTests.cs` | Rarity/faction counting, null handling |
| `CardJson` | `CardJsonTests.cs` | Serialization, roundtrip, escape/control chars, faction tokens, malformed/future-version rejection |
| Plus: BattleDeckSnapshot, BattleEvent, CombatLegality, FeaturedCardPicker, MatchLocalCardId, MatchViewSnapshot, MvpCardLibrary, OpeningLineup, AuthoritativeCommandBoundary |

Match-level tests (Battlefield, Board, HUD, AI, targeting) exist in `Assets\Tests\Match\*`.

Net/Persistence/Editor/Inspection test assemblies exist and cover their domains.

### 3.2 Whitebox gaps to fill (recommended additions)

These are pure C# logic tests that require no Unity build (they'd run in CI or once dotnet is installed):

1. **PlayerState NP economy**
   - `GainNP` clamps at cap — **partially covered** already (`MatchStateTests.cs` has `PlayerState_GainNP_AddsUpToTheCap` and `PlayerState_GainNP_CapsAtThirtyPlusNodes`)
   - `SpendNP` rejects negative/over-cap amounts — **partially covered** already (`PlayerState_SpendNP_DeductsCorrectly`, `PlayerState_SpendNP_FailsWhenInsufficient`)
   - `RepairCore` clamps at `StartingCoreDurability` — **partially covered** already
   - `DrawCard` returns null when deck+discard empty; reshuffles discard when deck empty — **unconfirmed gap**
   - `TakeDamage` clamping — **unconfirmed gap**

2. **MatchState phase machine** — partially covered (`MatchStateTests.cs` exists but phase-machine coverage not confirmed from what was read)
   - `AdvancePhase` cycles Upkeep→Draw→Main→Combat→End→Cleanup→Upkeep (player switch + TurnNumber++ on Cleanup)
   - `OnUpkeepPhase`: readies units, clears CannotBeBlocked, gains NP (+4 + controlled Nodes), checks Node Domination (skipped TurnNumber ≤ 1)
   - `OnDrawPhase`: skips draw on Turn 1; Deck-Out loss only when deck empty AND hand empty
   - `CheckNodeDomination`: 3+ uncontested Nodes → active player wins, "NODE DOMINATION"
   - `CheckVictoryConditions`: both cores dead → active player wins; single core dead → other wins; concession result stable, not overwritten

3. **MatchEngine command matrix** — partially covered (`MatchEngineTests.cs` covers PlayCard success/insufficient NP/wrong phase/slot occupied/Protocol, DeclareAttack damage/Core-hit/cannot-act; Block, SwitchPosition, Retreat, Mulligan, PassPhase, EndTurn coverage not confirmed)
   - `PlayCardCommand` for each card type (Unit/Ability/Protocol/Relay) with correct slot rules
   - `DeclareAttackCommand`: invalid target when attacker not on Core Active and no defender; CannotBeBlocked bypasses blocker; SummoningDelay blocks attacking but not blocking
   - `DeclareBlockCommand`: only non-active player; one block per attacker/blocker; battlefield-group match required (CR-02)
   - `SwitchPositionCommand`: Main phase only; blocked by SummoningDelay; refreshes Node multipliers
   - `RetreatCommand`: Combat phase only; both units exhaust; 1 NP cost; same battlefield group required
   - `MulliganCommand`: Turn 1 only, Upkeep/Draw phase only; returns cards to deck, reshuffles, redraws same count
   - `PassPhaseCommand`/`EndTurnCommand`: clears pending blocks; EndTurn walks phases to hand over

4. **EffectInterpreter** — **entirely untested**: no `EffectInterpreterTests.cs` exists in `Assets\Tests\Core\`
   - DealDamage/Heal with shield-before transfer (10% rounded up to Core when shield-less Core Active)
   - Draw/GainNP (capped by controlled nodes)
   - Buff/Debuff with Amount/Amount2 and duration logging
   - Destroy marks dead; `CollectDeadSlot` announces and applies 1 Core durability loss for Core Active Units
   - Summon requires ReferenceId, Unit/Relay type, free slot
   - Move: Bench→Active or Active→Bench, never opponent's half
   - Repair: capped at MaxRepairAmount (3), restores Core durability
   - AddKeyword logged as unresolved (no keyword payload in schema)

5. **CardValidator edge additions**
   - Pool rarity budget: 75/45/22/8 of 150; warnings when off
   - All 3 factions pass ID format; wrong faction token warns
   - `ValidatePool` with null pool → error

6. **Board** — **entirely untested**: no `BoardTests.cs` exists in `Assets\Tests\Core\`
   - `CanHost`: Core slots accept Unit only; Node slots accept Unit+Relay; Protocol slot accepts Protocol only
   - `TryFindFreeSlot` spreads Units across Core Active → Node Actives → Core Bench → Node Benches
   - `HoldsNode` is per-board occupation (not control — control needs both boards)

7. **MvpCardLibrary**
   - `Register` drops nulls, throws on null input
   - `All()`/`ByFaction()` throw before registration, return after
   - Composition: 50 per faction expected when real pool registered

### 3.3 Where whitebox runs

- **CI (EditMode NUnit):** Add a `test` job to `.github/workflows/release-build.yml` running Unity Test Runner in EditMode batch mode via `game-ci/unity-builder`. This requires **Unity Editor** on the runner (provided by the builder image) — not .NET SDK.
- **Standalone `dotnet test`:** If `Assets\Scripts\Core` has no UnityEngine references (verified for files read: DeckValidator, CardValidator, MatchState, Board, PlayerState, EffectInterpreter, MatchEngine, CardDefinition, CardJson all use only `System.*`), a test project referencing Core can be compiled and run with plain .NET **without Unity**. This needs **.NET SDK installed locally or on the runner**. Claim verified for files read only — not exhaustively for every Core file. Available on hyperion (user-reported).
- **Locally on Tethys:** blocked by missing dotnet SDK and no Unity. Available on hyperion per user report.

---

## 4. Blackbox Testing (build/runtime level)

### 4.1 What blackbox means for Overlink

Blackbox tests treat the built game as opaque: launch it, drive UI or simulate input, observe outcomes. Since Unity isn't local and WebGL/Standalone builds only come from CI, blackbox testing lives in:

- **CI build pipeline** — every release/`workflow_dispatch` produces Windows + WebGL builds. A test step can smoke-test that builds produce non-empty output (already partially done in `release-build.yml` via `ls -la "$ROOT"` and index.html check).
- **PlayMode tests** (Unity Test Runner) — `Overlink.Cards.Tests` (22 tests, PlayMode-only, never executed) is the home for these. They run inside the Unity Editor/player and can exercise UI flows (deck builder, match flow) that EditMode cannot.

### 4.2 Blackbox test scope

| Area | What to verify | How |
|------|----------------|-----|
| Deck builder | Save a 30–40 card single-faction deck; reload it; deck loads with same cards/counts | PlayMode test via DeckbuilderController + DeckLibrary |
| Deck builder rejection | 29-card, 41-card, 4-copy, mixed-faction decks are refused at save | PlayMode test asserting DeckValidator gate in UI |
| Match start | Opening lineup deal (5 cards, cost ≤3), coin flip, first Upkeep NP gain (+4 + nodes) | PlayMode test via GameCoordinator/MatchEngine end-to-end |
| Victory conditions | Core destruction (20→0), Deck-Out (empty deck + empty hand), Concession, Node Domination (3 uncontested) through actual play | PlayMode test driving commands through MatchEngine + MatchState |
| Card play | Play Unit/Ability/Protocol/Relay; effects resolve; Deploy triggers on Units; Protocol occupies single slot | PlayMode test through MatchEngine.PlayCardCommand |
| Combat | Attack/block/damage/simultaneous destruction/FirstStrike/Piercing/Core transfer/battlefield promotion | PlayMode test through MatchEngine.DeclareAttackCommand |
| Build output | Windows zip contains Overlink.exe at root; WebGL channel has index.html at root | CI shell steps (already in release-build.yml) |

### 4.3 Blackbox gaps

- `Overlink.Cards.Tests` exists but has **never been executed** (AGENTS.md §4) — needs a CI PlayMode job or local Unity to run.
- No dedicated CI test job exists today; `release-build.yml` only builds and deploys.
- WebGL playability check (index.html at channel root) is present but only at build time, not as a reusable test.

---

## 5. CI Integration Recommendations

The current `release-build.yml` builds Windows + WebGL on release/`workflow_dispatch`. Recommended additions:

1. **Add a `test` job (EditMode)** — runs before build, fails fast on logic regressions. Runs Unity Test Runner in EditMode batch mode on `Assets\Tests\Core`, `Match`, `Net`, `Persistence`, `Editor`, `Inspection`. Requires Unity Editor on the runner (game-ci/unity-builder image includes it).
2. **Add a `test-playmode` job** — runs `Overlink.Cards.Tests` (22 tests) and any new PlayMode blackbox tests. Requires Unity Editor on the runner.
3. **Add a `smoke-build` step** — assert build output non-empty (already partially present) and, for WebGL, that index.html exists at channel root before pushing to itch.io.
4. **Versioning** — keep `versioning: None` (bundleVersion stability per CI note).

**Missing CI consideration:** A `test` job running EditMode NUnit via Unity Test Runner in batch mode requires a **Unity Editor license** on the runner (UNITY_LICENSE/EMAIL/PASSWORD secrets). This is implicitly handled since the same `game-ci/unity-builder` image is used, but worth stating explicitly.

---

## 6. What Can Be Done Now vs. Later

| Now (Tethys: no Unity, no dotnet) | Now on hyperion (dotnet SDK + Unity per user report) | CI (Unity Editor via game-ci/unity-builder) |
|-----------------------------------|-----------------------------------------------------|---------------------------------------------|
| Review existing tests (done) | Run standalone `dotnet test` on Core test assembly | Add `test` job to release-build.yml (EditMode NUnit) |
| Write/whitebox-test design (this doc) | Add whitebox tests for gaps in §3.2 | Add `test-playmode` job for `Overlink.Cards.Tests` (22 tests) |
| Identify gaps in coverage | Execute `Overlink.Cards.Tests` via local Unity | Add PlayMode blackbox tests (deck builder, victory conditions) |
| Propose test additions | — | Smoke-test build artifacts in CI |

---

## 7. Suggested Next Steps

1. **Correct workspace `AGENTS.md` §3** — Relay and Protocol *are* in `CardType` (code evidence: `CardValidator.cs`, `Board.CanHost`, `EffectInterpreter`, `MatchEngineTests`). Done — AGENTS.md updated 2026-11-25, backup at `AGENTS.md.bak-20261125`.
2. **On hyperion (dotnet SDK + Unity available per user):** run standalone `dotnet test` on Core test assembly; execute `Overlink.Cards.Tests` (22 PlayMode tests never run); add whitebox tests for gaps in §3.2 (`BoardTests.cs`, `EffectInterpreterTests.cs`, `DrawCard` reshuffle/null, phase-machine, command matrix).
3. **Add a `test` job** to `.github/workflows/release-build.yml` running EditMode NUnit via `game-ci/unity-builder` (CI path).
4. **Add a `test-playmode` job** for `Overlink.Cards.Tests` and any new PlayMode blackbox tests.
5. **Add PlayMode blackbox tests** for deck builder save/load and full-match victory conditions.

---

**Remaining unverified areas:** `MvpCardLibrary` "50 per faction" claim (source not read); full phase-machine/command-matrix coverage in existing `MatchStateTests.cs`/`MatchEngineTests.cs` (only partially read); "Core has no Unity dependencies" verified for files read only; hyperion tooling state is user-reported, not independently verified from this session.

**Consequential changes to CI or test infrastructure should go to Mneme for review before applying.**
