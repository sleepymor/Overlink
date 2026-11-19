# Review: Planning docs cleanup (2026-11-19)

**What:** Archived obsolete planning docs, fixed one path inaccuracy in AGENTS.md, and marked the v0.18 main-menu plan as complete.

**Scope:** Documentation only. No code, scene, Unity asset, or JSON card data was changed.

---

## 1. Changes made

### 1.1 Archived obsolete planning docs (moved via `git mv`, git history preserved)

| Original path | Archived path | Reason |
|---|---|---|
| `docs/PLAN-v0.17.md` | `docs/Archive/PLAN-v0.17.md` | Locked v0.17 plan (2026-10-17). Describes pre-development state: "Card data: Placeholder", "Match systems: Absent", scenes `SampleScene` / `NetPresenceDemo` only. Superseded by AGENTS.md (GDD v0.18 live as of 8 Nov 2026) and the built repo. |
| `docs/PLAN-v0.17.lock.json` | `docs/Archive/PLAN-v0.17.lock.json` | Lock digest for the v0.17 plan (baseline commit `c9e8d4b`, milestones M1–M5 all pending). Locks a plan that no longer reflects reality. |
| `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN - Copy.md` | `docs/Archive/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN - Copy.md` | Stale duplicate of the gameplay plan. Claims Gameplay.unity is a minimal unimplemented bootstrap (NodeCount=3 in code, Core durability=20, no battlefield, build registration missing). Superseded by `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md` (no Copy), which documents the scene as implemented (NodeCount=4, full battlefield present). |

**Why archive instead of delete:** Preserves git history and audit trail while making it unambiguous that these are not current. The non-archived `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md` remains the canonical gameplay plan.

### 1.2 AGENTS.md path fix

| File | Change |
|---|---|
| `AGENTS.md` §6 | `Assets\Cards\card_pool.json` → `Tools\mneme-review\card_pool.json` (backup noted at `Archive\cards\card_pool.json`) |

**Why:** `Assets\Cards\card_pool.json` does not exist. The live 150-card pool (75C/45R/22E/8L, 50 per faction fsti/frti/fpb) lives at `Tools\mneme-review\card_pool.json` and `Archive\cards\card_pool.json`. CardAsset sources live under `Assets\Cards\Data\`, collected in `Assets\Resources\Cards\CardCatalog.asset`.

### 1.3 Marked v0.18 main-menu plan as complete

| File | Change |
|---|---|
| `docs/PLAN-v0.18-main-menu.md` | Added `Status: ✅ COMPLETE` header noting all scoped scenes (`MainMenu`, `Deckbuilder`, `Collection`, `Settings`) exist in `Assets/Scenes/`; plan is now a record of completed work, not active guidance; source of truth is `AGENTS.md`. |

**Why:** All scoped scenes already exist. Verified against `Assets/Scenes/`: `MainMenu.unity`, `Deckbuilder.unity`, `Collection.unity`, `Settings.unity`, `Gameplay.unity`, `Gacha.unity`, `Auth.unity`, `NetPresenceDemo.unity`, `SampleScene.unity` are all present.

---

## 2. What the reviewing agent should verify

### 2.1 Docs folder is clean and non-contradictory

- [ ] `docs/` contains no `PLAN-v0.17.md`, no `PLAN-v0.17.lock.json`, and no `GAMEPLAY-SCENE-IMPLEMENTATION-PLAN - Copy.md`.
- [ ] `docs/Archive/` contains exactly those three archived files.
- [ ] `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md` (non-Copy) remains and is the canonical gameplay plan.
- [ ] `docs/PLAN-v0.18-main-menu.md` begins with the `Status: ✅ COMPLETE` header.

### 2.2 AGENTS.md accuracy

- [ ] AGENTS.md §6 references `Tools\mneme-review\card_pool.json`, not `Assets\Cards\card_pool.json`.
- [ ] AGENTS.md §2 factions table matches code: `fsti`, `frti`, `fpb` only (3 factions).
- [ ] AGENTS.md §3 card types: Unit, Ability, Relay, Protocol all present in `Enums.cs`.
- [ ] AGENTS.md §6 `Board.cs` constants match code: `CoreActiveSlots=1`, `CoreBenchSlots=4`, `NodeCount=4`, `NodeActiveSlots=1`, `NodeBenchSlots=2`, `NodesForDomination=3`.
- [ ] AGENTS.md does not cite `Assets\Overlink\Core\Cards\DeckValidator.cs` (that path does not exist; DeckValidator is at `Assets\Scripts\Core\DeckValidator.cs`).

### 2.3 Source-of-truth alignment

- [ ] Code wins where it disagrees with any doc (per AGENTS.md §1).
- [ ] The live GDD is v0.18 (docId `1R_HfV94lUSCt3Q41sDyHepGx7yrC--slO7Sb71Doosc`); the PDF `GDD\GDD-Overlink-v0.17-PreDevDraft.pdf` is stale and must not be treated as current.
- [ ] Card ID format `^card_(fsti|frti|fpb)_\d{3}$` enforced by `CardValidator.cs`.
- [ ] Card pool is 150 cards (75C/45R/22E/8L, 50 per faction) at `Tools\mneme-review\card_pool.json`.

---

## 3. Files not touched (pre-existing dirty tree, unrelated to this task)

The working tree already had ~64 modified files and 16 untracked files before this cleanup (Editor scripts, Materials, Battlefield, SignalBay, Gacha work, etc.). None of those were modified by this cleanup. The only files changed by this task:

- Staged: 3 archive moves (`git mv`)
- Modified: `AGENTS.md`, `docs/PLAN-v0.18-main-menu.md`
- New: `docs/REVIEW-PLANNING-CLEANUP.md` (this file)

---

## 4. Suggested next step for the reviewing agent

If everything above checks out, the reviewing agent can treat `AGENTS.md` + `docs/Archive/` + remaining `docs/*.md` as the consistent planning baseline. Any further planning work should update `AGENTS.md` as the source of truth and avoid creating new top-level `PLAN-v*.md` files without superseding or archiving the previous one.