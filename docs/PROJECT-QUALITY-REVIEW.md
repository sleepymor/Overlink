# Overlink — Project Quality Review (2026-11-19)

**Purpose:** Give a reviewing agent an accurate, evidence-based snapshot of the project's overall state and quality. All claims are grounded in the actual code at `E:\Games\Overlink` — not in memory or stale docs.

**Branch:** `master` (latest commit `cf33750` — "release v2026.11.7")

**Source of truth:** `AGENTS.md` (grounded in verified code and live GDD v0.18 as of 8 Nov 2026).

---

## 1. What this project is

**Overlink** — a Unity TCG (trading card game). Three MVP factions, card-driven combat, Active–Bench formations, four victory conditions.

| Attribute | Value |
|---|---|
| Unity version | 2022.3.62f3 (per AGENTS.md §1 / stale PLAN-v0.17) |
| Build system | GitHub Actions only (`release-build.yml`); Unity is **not** installed on the agent host |
| Build targets | Windows (`StandaloneWindows64`) + WebGL (itch.io via butler) |
| Tagging | Calendar-versioned alpha: `v2026.11.0` → `v2026.11.7` |
| Live GDD | v0.18 (docId `1R_HfV94lUSCt3Q41sDyHepGx7yrC--slO7Sb71Doosc`) |

**Recent commit themes** (last 15): battle records, socket card-face rendering, board skins, resting-hand sink, HUD status layer, per-key buttons + retreat, opening lineup drag, lineup flow. The project is actively in the Gameplay/HUD polish phase.

---

## 2. Architecture assessment

### 2.1 Assembly boundaries (good)

Five clean assembly definitions, no circular dependencies observed:

| Assembly | Location | Purpose |
|---|---|---|
| `Overlink.Core` | `Assets/Scripts/Core/Overlink.Core.asmdef` | Rules model — board, cards, effects, validation, match state, engine |
| `Overlink.Match` | `Assets/Scripts/Match/Overlink.Match.asmdef` | Presentation + gameplay scene wiring (GameCoordinator, presenters, input) |
| `Overlink.Cards` | `Assets/Scripts/Cards/` | CardAsset ScriptableObject bridge to Core definitions |
| `Overlink.Net` | `Assets/Scripts/Net/Overlink.Net.asmdef` | Auth, lobby, Google token, profile, email/password policies |
| `Overlink.UI` | `Assets/Scripts/UI/Overlink.UI.asmdef` | Main menu + placeholder stubs (zero game-logic dependency) |

**Positive:** `Overlink.UI` explicitly does not reference Core/Match/Cards/Net — a clean dependency boundary. `CombatLegality.cs` lives in Core (headless, unit-testable without a scene/camera). The board binds to geometry by name index rather than fragile child paths.

### 2.2 Card data model

- `CardDefinition` + `CardJson` + `CardValidator` + `Enums.cs` — all four card types (Unit, Ability, Relay, Protocol) present in code.
- `CardAsset` (ScriptableObject) ↔ `CardDefinition` round-trip via `ToDefinition()` / `FromDefinition()`.
- `CardCatalog.asset` at `Assets\Resources\Cards\CardCatalog.asset` collects CardAsset files.
- 150-card pool (75C/45R/22E/8L, 50 per faction fsti/frti/fpb) at `Tools\mneme-review\card_pool.json` + `Archive\cards\card_pool.json`.
- Card ID format `^card_(fsti|frti|fpb)_\d{3}$` enforced by `CardValidator.cs` (line 19).
- `DeckValidator.cs` (MinDeckSize=30, MaxDeckSize=40, MaxCopiesPerCard=3) at `Assets\Scripts\Core\DeckValidator.cs`.

**Concern:** AGENTS.md §6 still listed `Assets\Cards\card_pool.json` (non-existent) — fixed in this review pass. The canonical pool path should be `Tools\mneme-review\card_pool.json`.

### 2.3 Board / match model

`Board.cs` constants (verified in code):
- `CoreActiveSlots = 1`, `CoreBenchSlots = 4` (1 Active + 4 Bench per player at Core — deliberate deviation from GDD's 3+3)
- `NodeCount = 4`, `NodeActiveSlots = 1`, `NodeBenchSlots = 2`, `NodesForDomination = 3`
- `StartingCoreDurability = 20` in `PlayerState.cs` (GDD says 3 — an open GDD-vs-code discrepancy noted in the gameplay plan)

Node control is computed from two boards (`Board.HoldsNode` per side + `MatchState.ControlsNode` across both), avoiding the "both players control all 4 Nodes at setup" bug. All four victory conditions implemented (Node Domination, Core Destruction, Deck-Out, Concession) with `MatchState.ResultReason` strings.

**Open decision:** Deck-Out is currently unreachable because `PlayerState.DrawCard()` reshuffles discard into deck when empty (no reshuffle rule in GDD). `MatchStateTests.PlayerState_DrawCard_ReshufflesWhenDeckEmpty` asserts current behaviour.

---

## 3. Testing

### 3.1 Test assemblies

Six test assemblies, all EditMode except Cards (PlayMode):

| Assembly | Location | Notes |
|---|---|---|
| `Overlink.Core.Tests` | `Assets\Tests\Core\` | 13 files — CardValidator, MatchState, MatchEngine, CombatLegality, DeckValidator, etc. |
| `Overlink.Match.Tests` | `Assets\Tests\Match\` | 11 files — board, HUD, camera, drop targets, AI, input, idle hand sink |
| `Overlink.Cards.Tests` | `Assets\Tests\Cards\` | 2 files (CardAssetTests, CardHandTests) — **PlayMode-only, never executed** per AGENTS.md |
| `Overlink.Net.Tests` | `Assets\Tests\Net\` | 7 files — password/email policies, nonce, Google decline mapping |
| `Overlink.Persistence.Tests` | `Assets\Tests\Persistence\` | 1 file — DeckLibraryTests |
| `Overlink.Deckbuilder.Editor.Tests` / `Overlink.Inspection.Tests` | Editor/Inspection | Scene tests + inspection runtime tests |

### 3.2 Test health (per AGENTS.md §4)

- Full EditMode suite: **1372 leaves — 1367 passed, 4 skipped, 1 standing failure**
- The 1 failure is **pre-existing and not yours**: `UnitySkills.Tests.Core.PerceptionSkillsTests.SceneSummarize_CountsObjectsCorrectly` (test-order dependence inside `com.besty.unity-skills`; fails identically with none of the project's assemblies).
- `Overlink.Cards.Tests` (22 tests) is PlayMode-only and **has never been executed** — do not treat as verified.
- `Overlink.Net.Tests`: 43/43 (password), 90/90 (email), then full EditMode 1061/1067 (2 pre-existing failures).

### 3.3 Gaps

- `Assets\Tests\Cards\` has only 2 test files but AGENTS.md cites 22 Cards tests — discrepancy to verify.
- No PlayMode suite runs in CI (build pipeline has no test job per AGENTS.md TODO 2026-11-01).
- Unity is not available locally, so tests cannot be run on the agent host — verification depends on CI.

---

## 4. Build & release pipeline

| Item | State |
|---|---|
| Workflow | `.github\workflows\release-build.yml` (game-ci/unity-builder, Ubuntu runners) |
| Triggers | Release publish + `workflow_dispatch` |
| Tags | `v0.18.1`, `v2026.11.0` … `v2026.11.7` |
| CI status | v2026.11.0: run 41303478866 completed successfully for Windows + WebGL + itch.io pushes |
| WebGL | Content-root fix (commit `7140a00`) — flattened index.html; itch.io page still 404 publicly (visibility blocked) |
| Google on WebGL | Disabled behind `GoogleTokenProvider.Supported` (desktop-only) |
| `Builds/` | Zero tracked files (cleaned up per earlier lesson) |

**Concern:** No test job in the workflow — CI verifies build only, not test pass/fail.

---

## 5. Scenes (verified present in `Assets\Scenes\`)

| Scene | State |
|---|---|
| `MainMenu.unity` | Fully implemented (v0.18 scope complete) |
| `Gameplay.unity` | Implemented — full battlefield, HUD, lineup, AI, victory UI |
| `Deckbuilder.unity` | Placeholder stub |
| `Collection.unity` | Placeholder stub |
| `Settings.unity` | Placeholder stub |
| `Gacha.unity` | Shop + split from RevealGacha (v2026.11.1 pass) |
| `RevealGacha.unity` | Pack reveal |
| `Auth.unity` | Auth flow |
| `NetPresenceDemo.unity` | Enabled build scene (existing) |
| `SampleScene.unity` | Disabled (not in build) |
| Many `Card*Preview.unity` | Card frame/presentation preview scenes |

`Gameplay.unity` is generated by `Assets\Editor\BuildGameplayScene.cs` (menu: Overlink > Scenes > Build Gameplay Scene) — not hand-written YAML.

**Concern:** `Gameplay.unity` is **not registered in Build Settings** — `ProjectSettings/**` is protected; the owner declined registration. `BuildGameplayScene.cs` intentionally refuses to add a build-settings entry. This is a known open item.

---

## 6. Documentation accuracy

### 6.1 AGENTS.md (source of truth)

Accurate on: factions (3 only), card types (all 4 in enum), Board constants, DeckValidator path, card ID format, build/test conventions, guardrails.

**Fixed in this review:** §6 card pool path (`Assets\Cards\card_pool.json` → `Tools\mneme-review\card_pool.json`).

### 6.2 Archived docs (moved to `docs\Archive\`)

| Doc | Why obsolete |
|---|---|
| `PLAN-v0.17.md` | Locked v0.17 pre-development plan; describes match systems as "Absent" |
| `PLAN-v0.17.lock.json` | Locks milestones M1–M5 all pending; baseline commit `c9e8d4b` |
| `GAMEPLAY-SCENE-IMPLEMENTATION-PLAN - Copy.md` | Stale duplicate claiming Gameplay is unimplemented (NodeCount=3, durability 20) |

### 6.3 Remaining docs

`PLAN-v0.18-main-menu.md` marked complete. `GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md` (non-Copy) is the canonical gameplay plan — documents Gameplay as implemented with known gaps (combat feedback, deck-out reshuffle decision, build registration).

---

## 7. Known risks & open decisions

| # | Risk / open decision | Evidence |
|---|---|---|
| 1 | **Core durability mismatch**: GDD says 3, code uses 20 | `PlayerState.cs` line with `StartingCoreDurability = 20`; flagged in gameplay plan §3.4 |
| 2 | **Deck-Out unreachable**: reshuffle-on-empty contradicts GDD | `MatchStateTests.PlayerState_DrawCard_ReshufflesWhenDeckEmpty`; gameplay plan §3.3 |
| 3 | **Gameplay.unity not in Build Settings** | `ProjectSettings/**` protected; `BuildGameplayScene.cs` refuses registration; gameplay plan §4.1 |
| 4 | **Camera GUID unresolved**: `a79441f348de89743a2939f4d699eac1` no matching `.meta` under `Assets` | Gameplay - Copy plan §4.2; requires Unity confirmation |
| 5 | **Overlink.Cards.Tests never executed** (PlayMode-only) | AGENTS.md §4 |
| 6 | **No test job in CI** | AGENTS.md TODO 2026-11-01: "workflow has no test job" |
| 7 | **itch.io page 404 publicly** | Visibility not public; "played in browser" not ticked (Master-side, UI only) |
| 8 | **v0.18.6 tag mismatch**: `GoogleTokenProvider.Supported` not in tagged commit | AGENTS.md TODO 2026-10-22: uncommitted work, release state unknowable |
| 9 | **Card pool path inconsistency** | AGENTS.md §6 listed non-existent `Assets\Cards\card_pool.json` — fixed |
| 10 | **No local Unity** — all verification must go through GitHub Actions | AGENTS.md §4 |

---

## 8. What the reviewing agent should verify

### Code correctness
- [ ] `Overlink.Core` compiles headless (no Unity dependency) — Core tests pass
- [ ] `CardValidator.IdPattern` = `^card_(fsti|frti|fpb)_\d{3}$` matches `Enums.Faction` values (Fsti/Frti/Fpb → `CardJson.FactionToken` maps correctly)
- [ ] `DeckValidator.MinDeckSize=30, MaxDeckSize=40, MaxCopiesPerCard=3` enforced at save
- [ ] `Board` constants match `CombatLegality` and `MatchEngine` assumptions (1 Core Active, 4 Nodes, etc.)

### Build / CI
- [ ] `release-build.yml` triggers on release publish + workflow_dispatch
- [ ] Windows + WebGL jobs both reach "Build with Unity"
- [ ] Confirm no test job exists; decide whether to add one

### Documentation
- [ ] AGENTS.md §6 card pool path = `Tools\mneme-review\card_pool.json`
- [ ] `docs/Archive/` contains exactly the 3 archived files; `docs/` contains none of them
- [ ] `docs/PLAN-v0.18-main-menu.md` has `Status: ✅ COMPLETE` header
- [ ] `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md` (non-Copy) is the only gameplay plan

### Gaps to decide
- [ ] Core durability: 3 (GDD) vs 20 (code) — explicit decision needed
- [ ] Deck-Out reshuffle: keep house rule or go GDD-literal
- [ ] Gameplay.unity build registration: owner decision on `ProjectSettings/` exception
- [ ] Add CI test job

---

## 9. Bottom line

The project is **mid-MVP polish**: Core rules are implemented and unit-tested, Gameplay scene is built and playable-prototype, main menu + placeholder stubs exist, card pool of 150 exists across 3 factions, and releases ship via GitHub Actions to itch.io. The code shows good assembly boundaries and headless-testable rules. The main quality risks are the GDD-vs-code durability discrepancy, the unreachable Deck-Out condition, missing build-settings registration for Gameplay, and no CI test execution. Documentation has been aligned with code in this pass (AGENTS.md path fix + obsolete plan archival).