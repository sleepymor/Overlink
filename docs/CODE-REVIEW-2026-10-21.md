# Overlink code review — 2026-10-21

**Reviewer:** Mneme ⟁ (independent review, read-only)
**Requester:** Themis ◈
**Scope:** Read-only static review of the accessible Unity C# sources and tests under `E:\Games\Overlink\Assets`, against `docs/GAMEPLAY-SCENE-IMPLEMENTATION-PLAN.md`. No files were changed, Unity was not run, and the repository revision was not independently checked.
**Repo revision at review:** `master` @ `7140a00`

## Executive summary

**Verdict: FAILED.** The authoritative engine accepts attacks against benched cards and records blocks it later ignores. The card-face adapter also drops most canonical card metadata. These are not merely style concerns.

**Findings:** 3 High, 10 Medium, 2 Low; 0 Critical. Counts include two design/configuration risks identified separately from proven runtime defects. Confidence is high for the cited control-flow defects, moderate for repository-wide absence and test-coverage claims because a complete file inventory and test run were unavailable.

## Review checklist

### 2026-10-21 — Full code review

- [x] Examine the accessible implementation plan and identify stale or unresolved design claims.
- [x] Trace attack, block, effect, resource, and draw paths in Core.
- [x] Inspect all 20 definitions in `PrototypeCardPool.All()` against the relevant validator checks.
- [x] Trace canonical card data into the hand face, board captions, and reader.
- [x] Inspect the named account, token, console, lobby, and auth-settings sources.
- [x] Examine accessible Core tests as evidence, without treating historical test results as a fresh run.
- [ ] Independently parse the GDD PDF, enumerate and inspect every one of the claimed 48 C# files and 12 test files, inspect every Editor/UI source, or execute either Unity test suite.

## Findings — Core rules

### CR-01 · High · Benched defenders are legal engine attack targets

**Location:** `Assets/Scripts/Core/MatchEngine.cs:219–225, 674–703`.

**Evidence:** Attack execution uses `target = FindCardInstance(opponent.Board, command.DefenderInstanceId);` and only rejects a missing or dead target. `FindCardInstance` explicitly searches both `board.CoreBench` and `board.NodeBench`.

**Impact:** A direct command caller can attack a benched card, although the board's combat set and `CombatLegality.AttackTargets` exclude bench occupants. UI filtering does not secure the authoritative command boundary.

**Smallest fix:** Resolve declared defenders from occupied **Active** slots only; test a submitted `DeclareAttackCommand` against each bench type and assert rejection.

### CR-02 · High · Illegal cross-battlefield blocks are recorded and then discarded

**Location:** `Assets/Scripts/Core/MatchEngine.cs:307–329, 576–603`.

**Evidence:** `ExecuteDeclareBlock` validates the Active blocker and attacker, then writes `pendingBlocks[attacker.InstanceId] = blocker.InstanceId` without checking placement. At consumption, the engine logs that a non-Core-Active cannot block a Core attack or that a blocker is not in the target's battlefield, and returns `null`.

**Impact:** A successful block command can spend the defender's opportunity without blocking anything. The implementation plan §3.8 and the test named `MisplacedBlock_IsOfferedByNowhereYetStillRecordedByTheEngine` document this discrepancy; that test preserves the defect rather than asserting rejection.

**Smallest fix:** Validate the block's intended target/battlefield before accepting a declaration. If the intended target is not yet known, change the command or pre-commitment model so an impossible block cannot return `Success`.

### CR-03 · Medium · Effect-driven NP gain ignores the Node-adjusted cap

**Location:** `Assets/Scripts/Core/EffectInterpreter.cs:94–97`; `Assets/Scripts/Core/PlayerState.cs:55–67`.

**Evidence:** `GainNP` effects call `own.GainNP(effect.Amount)`. That overload calls `GainNP(amount, MaxNP)` and describes itself as "clamped to the base 10 cap when the caller has no Node context." Upkeep uses a Node-dependent cap.

**Impact:** Controlled-Node bonus capacity applies to turn income but not to an NP-gain card such as Overclock; at 10 NP, its gain is discarded even when the player's applicable cap exceeds 10.

**Smallest fix:** Pass the active player's Node-adjusted cap to effect gain; cover the boundary at 10 NP with one or more controlled Nodes.

### CR-04 · Medium, design decision required · Automatic reshuffle obstructs the specified deck-out condition

**Location:** `Assets/Scripts/Core/PlayerState.cs:97–105`; implementation plan §3.3.

**Evidence:** When `Deck.Count == 0`, `DrawCard()` calls `ReshuffleDiscard()` unless discard is also empty. The plan says the GDD has no reshuffle rule and identifies `PlayerState_DrawCard_ReshufflesWhenDeckEmpty` as a test of the current behavior.

**Impact:** Deck-out is much harder to trigger than the stated "cannot draw during Draw phase and has no cards in hand" condition. This is a **rules discrepancy**, not proof that every possible match can never deck out.

**Smallest fix:** Decide explicitly whether reshuffle is a house rule. For literal GDD behavior, remove it and invert the reshuffle test; cover empty deck/nonempty discard and the hand-condition boundary.

### CR-05 · Medium · "Every enemy active unit" effect resolves only against Core Actives

**Location:** `Assets/Scripts/Match/PrototypeCardPool.cs:53–55`; `Assets/Scripts/Core/EffectInterpreter.cs:359–367`.

**Evidence:** Volley says `"Deal 2 damage to every enemy active unit."` and uses `DealAoEDamage, TargetKind.EnemyActive`. The resolver maps both `EnemyActive` and `AllEnemyActive` to `foe.Board.GetCoreActiveUnits(foe.Index)`, excluding Node Actives.

**Impact:** The printed unqualified "every enemy active unit" does not describe the executed effect across the four Nodes. The two target enum values also resolve identically here.

**Smallest fix:** If Volley is Core-only, say so on the card; otherwise introduce/use an all-battlefields Active target resolution and test a Core and Node occupant together.

## Findings — presentation and card data

### PR-01 · Medium · Hover tint is applied to every painted tile

**Location:** `Assets/Scripts/Match/BattlefieldPresenter.cs:1916–1955, 1978–1984`.

**Evidence:** Both empty and occupied tile paint paths call `TintForHover(tile, ...)`, while that function returns `tile == null ? tint : Color.Lerp(tint, Color.white, 0.22f);`. It does not compare the tile to the stored hovered tile.

**Impact:** Every non-null tile gets the hover brightening, reducing the distinction between a hovered socket and the rest of the board.

**Smallest fix:** Make hover tint conditional on `tile == hoveredTile`; add a two-tile paint test.

### PR-02 · Medium · Relay board captions show combat stats they do not have

**Location:** `Assets/Scripts/Match/BattlefieldPresenter.cs:1987–2001, 1399–1416`.

**Evidence:** `CardText` unconditionally appends `card.CurrentAtk`/`card.CurrentHp`. By contrast, the reader prints combat stats only under `if (def.Type == CardType.Unit)`. A Relay can occupy a Node socket (`Board.cs:23–36`); a Protocol uses its separate slot and is not established to pass through this tile-caption path.

**Impact:** Relay tiles can misleadingly show `0/0`.

**Smallest fix:** Apply the Unit type gate to the tile caption and use a Relay-specific concise label.

### PR-03 · Medium · A stale hand-view identity may play a different physical copy

**Location:** `Assets/Scripts/Match/GameCoordinator.cs:912–930`.

**Evidence:** After `instanceByData.TryGetValue` finds a known `CardData`, `FindHandCard` returns its exact instance only if still in hand; otherwise it falls through to `player.Hand.Find(c => c.Definition.Id == cardData.id)`.

**Impact:** If a cached view/action survives removal of its physical card while another copy remains, the stale action can select the other copy. This is a code-path risk; an end-to-end input reproduction was not performed.

**Smallest fix:** For a **known** cached `CardData` whose instance is gone, return `null`. Keep ID fallback only for data not present in the identity map, if external views require it.

### PR-04 · Medium · The hand card face drops canonical identity and combat information

**Location:** `Assets/Scripts/Match/GameCoordinator.cs:449–464`; `Assets/Scripts/Cards/CardData.cs:8–20`; `Assets/Scripts/Cards/CardView.cs:68–82`.

**Evidence:** `GetOrMakeData` constructs `CardData(def.Id, def.Name, def.Cost, def.AbilityText, AccentFor(def.Type))`. `CardView.Setup` creates only `"Title"`, `"Cost"`, and `"Desc"` labels and colors a cube. The projection has no Faction, Rarity, Type label, ATK, HP, Shield, Keywords, or ArtPath.

**Impact:** The proposed card frame cannot obtain those fields from the current hand-face API. `CardData` is **load-bearing as a hand-view adapter and instance-identity key**, not a safe-to-delete dead class; its `StarterDeck()` data is a separate legacy sample, not the match's canonical pool. The battlefield reader *does* show type, stats, ability text, and keywords (`BattlefieldPresenter.cs:1381–1451`), though its faction/rarity line is conditional; that reader is not the hand card face.

**Smallest fix:** Design the new face around `CardDefinition` plus a distinct `CardInstance` identity and current stats, or extend the explicit view model and its mapping. Migrate callers/tests before removing `CardData`.

### PR-05 · Medium · Invalid nominated placement silently becomes automatic placement

**Location:** `Assets/Scripts/Match/GameCoordinator.cs:550–565`.

**Evidence:** A non-null `chosenSlot` is resolved via `player.Board.GetSlot(...)`, but when that returns `null`, the same `if (slot == null && !TryChooseSlot(...))` path used for a free-form fling chooses another slot.

**Impact:** An invalid/stale nominated socket can deploy a card elsewhere despite the API's "honoured exactly" contract. Normal highlighted targets should generally be valid; this is an API-edge defect, not a demonstrated ordinary drag failure.

**Smallest fix:** Reject when `chosenSlot != null` and resolution returns `null`; auto-place only when no slot was nominated.

## Findings — prototype cards and validation

### CA-01 · Medium · Interdiction Net promises an absent mechanic

**Location:** `Assets/Scripts/Match/PrototypeCardPool.cs:69–72, 169–183`.

**Evidence:** The Relay text says `"While present, enemy units deployed to that Node cannot be blocked."` The `Relay` factory gives it `Effects = new EffectDescriptor[0]` and supplies no keywords. The pool entry passes no mechanic-bearing arguments.

**Impact:** Deployment and combat have no effect/keyword implementing this promise. Merely adding `Keyword.CannotBlock` to a Relay would not be correct: the validator treats it as Unit-only (`CardValidator.cs:352–370`) and that keyword describes the bearer rather than enemy units conditionally deployed to the Node.

**Smallest fix:** Implement an explicit persistent Node-scoped rule with tests, or change the text to describe the Relay's actual occupancy behavior.

### Validator answer for all 20 cards

By inspection of `PrototypeCardPool.All()` and its `Unit`, `DeployUnit`, `Ability`, `Relay`, and `Protocol` factories (`PrototypeCardPool.cs:22–86, 112–202`), **all 20 appear to produce zero `Severity.Error` validator issues**: IDs are distinct and faction-conforming; non-Units leave combat stats at zero; Shield Units have positive Shield; Deploy/Draw, Activate, and Repair keywords have supporting effects; effect magnitudes and Repair's amount/target are within the shown constraints. In particular, Debilitator's `Keyword.Activate` plus Debuff effect meets the validator's requirement for at least one effect (`CardValidator.cs:342–349`).

This is **static evaluation, not an executed `ValidatePool(All())` assertion**. All 20 set empty `ArtPath`, so the validator produces informational ArtPath issues (`CardValidator.cs:458–461`); "zero errors" does not mean "zero issues." The validator does **not** check that prose such as Interdiction Net's matches implemented mechanics. The supplied premise that Unit-only keywords are *enforced* as errors is slightly too strong: wrong-type use produces a **Warning** at `CardValidator.cs:352–356`, while non-Unit combat stats produce an Error at `CardValidator.cs:112–115`.

## Findings — account, networking, and security

### NET-01 · High · A late Relay join can revive a lobby after Leave

**Location:** `Assets/Scripts/Net/LobbyManager.cs:455–485`; `LeaveLobby()` at `:252–272`.

**Evidence:** `PollOnceAsync` checks `CurrentLobby` and `leaving` before `await net.JoinRelayAsync(relayCode)`, then unconditionally calls `SetState(LobbyState.InGame)` on successful completion. `LeaveLobby` clears the lobby while asynchronous work can still be outstanding.

**Impact:** A successful late join can put the client in an abandoned session or show an `InGame` state after leaving.

**Smallest fix:** Capture the lobby ID/operation version, revalidate both after the join await, and leave the network session if the result is stale.

### NET-02 · Medium · Selecting a new Google identity can report success while retaining the old one

**Location:** `Assets/Scripts/Net/AccountManager.cs:131–162`.

**Evidence:** The method obtains `idToken`, but if `auth.IsSignedIn` and `auth.PlayerInfo?.GetGoogleId()` is nonempty, it calls neither `LinkWithGoogleAsync(idToken)` nor `SignInWithGoogleAsync(idToken)`; it still sets `Kind = AccountKind.Google` and `"Signed in with Google."`

**Impact:** An attempted account change can silently leave the original Unity account active.

**Smallest fix:** Explicitly reject switching an already-linked account, or implement sign-out/confirm/switch semantics; never claim the newly selected identity was used without checking it.

### NET-03 · Medium · "Play as Guest" can misclassify an existing Google session

**Location:** `Assets/Scripts/Net/AccountManager.cs:116–128`.

**Evidence:** `if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync();` is followed unconditionally by `Kind = AccountKind.Guest`.

**Impact:** Calling this public method on a signed-in Google account changes the local account label without changing authentication. UI availability may reduce exposure, but does not repair the API contract.

**Smallest fix:** Refuse while already Google-signed-in or derive `Kind` from the actual authenticated account.

### NET-04 · Low · Windows loopback OAuth callback has an unbounded first-request body

**Location:** `Assets/Scripts/Net/GoogleTokenProvider.cs:81–89, 114–119`.

**Evidence:** The first `listener.GetContextAsync()` result is sent to `ReadBodyAsync`, which calls `reader.ReadToEndAsync()` without checking method, content type, or length first.

**Impact:** A local process could consume or stall the pending callback; this is a local availability concern, **not evidence of token forgery**. The code does check state and nonce before returning a token.

**Smallest fix:** Bound request size and read time, require the expected POST form, and reject unrelated requests without consuming the sign-in attempt.

### NET-05 · Low, configuration · Empty Google client ID intentionally disables Google sign-in

**Location:** `Assets/Resources/GoogleAuthSettings.asset:17`; `Assets/Scripts/Net/AccountManager.cs:138–140`.

**Evidence:** The asset contains `clientId:` with no value; the account method throws `"Configure Resources/GoogleAuthSettings with a Google client ID first."` before requesting a token.

**Impact:** Google sign-in is currently nonfunctional until configured; guest sign-in is not shown to depend on that value. A client ID is not a client secret. This is a setup blocker, not a leaked credential.

**Smallest fix:** Supply the appropriate public OAuth client ID in the intended configuration and verify each supported platform's flow.

## Explicit answers to A–G

**A — Correctness.** Proven defects include bench attack acceptance (CR-01), accepted/ignored blocks (CR-02), wrong NP cap (CR-03), board hover and caption defects (PR-01/02), and asynchronous account/lobby state errors (NET-01–03). Volley and Interdiction Net have demonstrated text/mechanic mismatches. The interpreter's `Resolve` processes descriptors in source order and settles destruction once after the batch (`EffectInterpreter.cs:42–59`); callers also settle afterward (`MatchEngine.cs:169–190, 276–282`). That is redundant on inspection, but **no double application of the same effect or double Core-destruction charge was established** from those calls alone.

**B — Architecture.** `CardData` is a live adapter for `HandController`/`CardView` and the coordinator's instance map, while `CardDefinition` is the Core model. The hand face renders Title, Cost, Description, and type-derived accent only. Battlefield tile captions render name, current ATK/HP, Shield, and state; the expanded reader renders additional canonical fields, including keywords. No inspected hand-face code renders Faction, Rarity, printed ATK/HP, Shield, Keywords, or artwork. Do not delete `CardData` without migrating its callers and physical-copy identity handling.

**C — Validator.** All 20 prototype definitions appear to have zero validation **errors** by static inspection, not by running the validator. Empty art produces informational issues. Debilitator is valid by the validator's Activate/effect rule. Interdiction Net's advertised block prevention is not implemented. Unit-only keyword misuse is a warning, not invariably an error.

**D — Security.** `GoogleTokenProvider` requests Google over `https://accounts.google.com/...`, redirects to loopback `127.0.0.1`, checks returned state and nonce, and passes the ID token to Unity Authentication (`GoogleTokenProvider.cs:64–103`; `AccountManager.cs:142–156`). `DebugConsole.cs:48–81` captures the **first line of every Unity log message** and shows it in an in-game overlay; it does not itself fetch or print a credential. No hardcoded client secret or explicit ID-token log was found in the inspected sources. Nevertheless, `AccountManager.cs:190–202` logs full exceptions, so whether an SDK exception might contain sensitive response details cannot be guaranteed statically; the console will mirror such logs. The empty client ID is a functional configuration blocker, not a secret-handling safeguard. WebGL bridge implementation, token signature/audience enforcement by the receiving SDK, and all possible logs were not verified.

**E — Test gaps.** The accessible tests and plan provide coverage of many happy paths, but the known bench-target and misplaced-block cases need engine-level **rejection** assertions; the existing misplaced-block test explicitly pins acceptance. Add tests for Node-adjusted effect NP cap, Volley against Core **and** Node Actives, an empty deck with nonempty discard after a deck-out rule decision, hover specificity, Relay captions, stale duplicate-card view identity, invalid nominated slots, and lobby Leave during a pending Relay join. These are specific unverified paths, **not a claim that no test anywhere in the unenumerated 12 files covers them**. The plan §2.1 places Core tests in PlayMode; its historical test tallies are not results of this review.

**F — Dead code/duplication.** `CardData.StarterDeck()` (`CardData.cs:29–43`) is a legacy sample deck, separate from the match's `PrototypeCardPool.BuildDeck()` (`PrototypeCardPool.cs:94–113`); repository-wide absence of other callers was not established. `CardData` and `CardDefinition` are parallel representations, but the former is load-bearing. `PickMark.CoreTarget` and the separate `coreMarked`/`MarkCoreTarget` representation in `BattlefieldPresenter` are a local duplication candidate, not proven removable dead code. The `GainNP(int)` overload is live but loses the Node context. No broader symbol-deadness claim is warranted without a search inventory.

**G — Top five risks, ranked.**

| Rank | Severity | Location | Risk | Smallest correct fix |
|---|---|---|---|---|
| 1 | High | `MatchEngine.cs:219–225, 674–703` | Authoritative commands attack protected Bench cards | Resolve defenders from Active slots only |
| 2 | High | `MatchEngine.cs:307–329, 576–603` | Successful blocks silently fail | Reject impossible declarations at submission |
| 3 | High | `LobbyManager.cs:455–485` | Late join revives an abandoned lobby/session | Recheck version after await; disconnect stale join |
| 4 | Medium | `AccountManager.cs:142–162` | Google account switch falsely reports success | Reject or explicitly implement switching |
| 5 | Medium | `GameCoordinator.cs:449–464`; `CardView.cs:68–82` | Current card-frame path discards gameplay metadata | Use/extend a canonical-instance view model |

## Verified clean or appropriately bounded

- `Board.cs:43–59` defines four Nodes, 3-of-4 domination, one Core Active and four Core Bench slots. `PlayerState` uses a starting Core durability of three. Older contrary passages in the implementation plan are stale; its final classification acknowledges the update.
- `MatchState.AdvancePhase()` currently enumerates **Upkeep → Draw → Main → Combat → End → Cleanup** (`MatchState.cs:429–463`), not the older "Draw → Standby … Opponent" sequence still described in plan §3.1.
- `MatchEngine.cs:203–214` requires an actionable Active **Unit** to attack. This check does not repair its defender lookup.
- `EffectInterpreter.Resolve()` iterates descriptors in order and settles the board after the batch (`EffectInterpreter.cs:42–59`).
- `CardValidator.cs:109–115, 352–370` checks non-Unit stats and flags Unit-only keywords on other types; the latter is explicitly a warning.
- The Windows Google flow uses an HTTPS authorization endpoint plus random state and nonce checks (`GoogleTokenProvider.cs:69–103`). The code includes no client secret. These observations do not constitute an end-to-end OAuth security certification.
- The built-in debug overlay escapes angle brackets before displaying captured log text (`DebugConsole.cs:61–81, 126–134`), reducing rich-text injection through ordinary log messages.

## Not reviewed / limits

The GDD source is a PDF (`GDD/GDD-Overlink-v0.17-PreDevDraft.pdf`); the available `read` tool identified it as binary and supplied no extracted text. Design comparisons therefore rely on the readable implementation plan and code, not independent PDF quotation. The plan contains historical claims and internally stale passages; none of its historical compile, screenshot, or test results was independently reproduced.

The available tools did not enumerate directories or run a repository-wide symbol search. Consequently, inspection was **not exhaustive across all 48 C# files, all 12 test files, Editor tooling, UI, WebGL JavaScript bridge, scene/prefab serialization, or external packages**. No Unity compile, automated tests, live authentication, network race reproduction, pointer interaction, or full match was performed. These gaps limit claims of absence—including "no other caller," "no other secret," and "no test coverage"—but do not negate the cited source-level defects.

**Overall confidence:** High for the concrete quoted control-flow findings; moderate for pool validation by static inspection; limited for repository-wide completeness and runtime/security behavior.
