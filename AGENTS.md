# AGENTS.md — Overlink (Unity TCG)

**Purpose:** keep any agent (including future turns of Themis) from making repo-wide assumptions that contradict how this project actually works. Read before editing.

## 1. What this repo is

A Unity TCG called Overlink. Three MVP factions, card-driven combat, Active–Bench formations, four victory conditions. Builds ship through GitHub Actions only — Unity is **not** installed on the agent host.

**Source of truth:** the actual code at `E:\Games\Overlink`. The path `C:\Users\...\unity\overlink\...` is a stale stub with different faction names and a different deck data model — **do not read or write there.**

**The live GDD** is `GDD - Overlink` (docId `1R_HfV94lUSCt3Q41sDyHepGx7yrC--slO7Sb71Doosc`, account `nemorzgm.05@gmail.com`), version v0.18 as of 8 Nov 2026. The PDF at `GDD\GDD-Overlink-v0.17-PreDevDraft.pdf` is a stale v0.17 export — **do not treat it as current.** Where code and GDD disagree, code wins. Where neither covers something, ask.

## 2. Factions (locked, 3 only — verified in code, `CardValidator.cs` line 13)

| Token | Name | Identity |
|---|---|---|
| `fsti` | Frontier Science and Technology Institute | Signal and data control — card draw, NP generation, Protocols, effect chaining |
| `frti` | Frontier Research and Technology Institute | Industrial combat — high ATK, durable frames, Relays as structures |
| `fpb` | Frontier Planning Bureau | Attrition and defence — Shield, HP, healing, Repair, Node holding |

**Rules:**
- Card IDs match `^card_(fsti\|frti\|fpb)_\d{3}$` (enforced by `CardValidator.cs`).
- MVP decks are single-faction only — mixed factions deferred to post-MVP.
- `CardValidator.KnownFactions` = `{ Nexus, Vanguard, Cipher, Warden }` exists **only** in the stale C: stub — **not** in the E: repo. Do not cite or rely on it.
- GDD v0.18 renamed F1/F2/F3 placeholders to fsti/frti/fpb — those are the canonical names.

## 3. Card types (implemented vs designed)

| Type | In code? | Notes |
|---|---|---|
| Unit | ✅ Yes | `CardType` enum: Active/Bench/Ability. Units map to Active/Bench board positions |
| Ability | ✅ Yes | One-shot effect, no board slot |
| Relay | ❌ No | Designed in GDD §2.1.5, not yet in `CardType` enum — deferred to post-MVP |
| Protocol | ❌ No | Designed in GDD §2.1.5, not yet in `CardType` enum — deferred to post-MVP |

Deck type-share ranges (Relay 10–15%, Protocol 10–15%) are aspirational until those types are implemented.

## 4. Build & test conventions

**Builds:** GitHub Actions `release-build.yml` (`game-ci/unity-builder`, Ubuntu runners). Triggered by publishing a release or `workflow_dispatch`. Windows (`StandaloneWindows64`) + WebGL (itch.io via butler). Unity is not available locally — never attempt a local build.

**Tests:** EditMode NUnit. Full suite was 1372 leaves (1367 passed, 4 skipped, 1 standing UnitySkills-package failure — not yours). `Overlink.Cards.Tests` (22 tests) is PlayMode-only and has never been executed — do not treat it as verified.

**Do not move tags.** Tags are never moved. A stray tag named `master` collides with the `master` branch — use explicit refspecs (`refs/heads/master:refs/heads/master`) if pushing the branch.

## 5. Guardrails (what NOT to do without explicit approval)

- **Do not build a deck-builder UI.** `Deckbuilder.unity` is a `PlaceholderSceneController` only — no functional deck-builder editor window exists.
- **No `DeckValidator` class exists.** The GDD describes one (MinDeckSize=30, MaxDeckSize=40, MaxCopiesPerCard=3, single-faction), but it is not implemented. Deck legality is currently enforced in `MatchState.ValidateDecks` (null/empty check only) and `CommandValidator`.
- **Do not push tags or move existing tags.**
- **Do not add a fourth faction.** Three factions for MVP.
- **Do not cite `Assets\Overlink\Core\Cards\DeckValidator.cs`** — it does not exist. Deck-related C# files live at `Assets\Scripts\Core\` or not at all.

## 6. Where things live

```
E:\Games\Overlink\
  Assets\Scripts\Core\        CardDefinition.cs, CardJson.cs, CardValidator.cs, MatchState.cs, EffectInterpreter.cs, Board.cs
  Assets\Scripts\Match\       GameCoordinator.cs, MatchEngine.cs, SimpleAI.cs, BattlefieldPresenter.cs
  Assets\Scripts\UI\          MainMenuController.cs, OfflineCollectionController.cs, AuthFlowController.cs
  Assets\Scripts\Cards\       CardAsset.cs, CardView.cs, DeckController.cs, HandController.cs (presentation only)
  Assets\Scripts\Net\         AuthFlowController.cs, LobbyManager.cs, GoogleTokenProvider.cs
  Assets\Scripts\VFX\         UiBar.cs, UiCounter.cs, UiFx*.cs, UiTween.cs, shaders
  Assets\Scenes\              Gameplay.unity, MainMenu.unity, Deckbuilder.unity (placeholder), Collection.unity, Gacha.unity
  Assets\Tests\Core\ / Match\ / Net\   EditMode NUnit tests
  Assets\Tests\Cards\         PlayMode-only — 22 tests, never executed
  GDD\GDD-Overlink-v0.17-PreDevDraft.pdf   Stale v0.17 export — do not treat as current
  .github\workflows\release-build.yml   Build pipeline
```

## 7. Deck construction rules (from GDD v0.18, matches code where implemented)

- Deck: 30–40 cards, max 3 copies of any card, single-faction MVP
- Cost range: 0–10 NP (`CardValidator.MaxCost = 10`)
- Starting hand: 5 cards
- Card ID format: `card_<faction>_<nnn>`
- MVP library: exactly 100 card definitions (50 Common / 30 Rare / 15 Epic / 5 Legendary)
- Reference 30-card deck: 16 Units / 6 Abilities / 4 Relays / 4 Protocols (balancing reference, not enforced)
