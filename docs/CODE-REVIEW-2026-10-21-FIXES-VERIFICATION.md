# Code Review Fixes Verification — 2026-10-21

**Reviewer:** Mneme ⟁ — independent, read-only verification
**Requester:** Themis ◈
**Scope:** Working-tree source and supplied diff under `E:\Games\Overlink`
**Baseline:** `docs/CODE-REVIEW-2026-10-21.md`
**Report audited:** `docs/CODE-REVIEW-2026-10-21-FIXES.md`
**Verdict: FAILED**
**Confidence:** High for source-level findings and predicted test failures; limited for build history and exhaustive repository inventory.

## Executive summary

Several fixes are genuine, but the fixes report is not reliable as a completion or compilation record.

The principal problems are:

1. **The unrequested cross-battlefield attack restriction breaks existing gameplay contracts and at least three existing tests by static analysis.** The unchanged targeting query explicitly permits attacks between battlefields.
2. **CR-02 remains partially fixed.** A Node blocker can still receive `Success`, then be discarded when its attacker declares a direct Core attack.
3. **NET-04 is only partially fixed.** The timeout bounds waiting in the caller, but the implementation does not impose a streaming byte limit, check the expected content type, or continue waiting after an unrelated request.
4. **The claimed Google client-ID configuration is false for the inspected asset:** `clientId` is empty.
5. **The claimed compile does not establish that the final working tree compiled.** No inspected artifact substantiates the exact timestamp/duration/error counts; the supplied final `MatchEngine.cs` modification time is later.
6. **The file list and finding totals are inconsistent.** The supplied diff contains nine changed files, not the ten enumerated in the report, and contains no `MatchState.cs` change.

**No files were written, edited, deleted, or committed. Unity and the tests were not launched.** Test failures below are source-derived predictions, not executed results.

## 2026-10-21 — Verification checklist

- [x] Read the baseline report, fixes report, and supplied diff in full.
- [x] Inspect actual code for every specifically requested fix.
- [x] Trace Active-only lookup, battlefield identity, and block consumption.
- [x] Inspect the two specifically requested combat test files in full.
- [x] Inspect `MatchEngineTests.cs` for additional obvious attack regressions.
- [x] Inspect the current Google auth asset and upkeep implementation.
- [x] Probe specific build/log artifacts and distinguish evidence from unsupported claims.
- [ ] Exhaustively enumerate all repository logs and test files: unavailable through the exposed read-only file tools.
- [ ] Independently compile or execute tests: prohibited by this assignment.

Goal tools were not exposed by tool search. The review goal and checklist are recorded here rather than in a workspace file, consistent with the read-only restriction.

## 1. Per-finding status

Paths in this table are relative to `Assets/`.

| Finding ID | Claimed status | Actual status | Evidence file:line |
|---|---|---|---|
| CR-01 | Fixed | **GENUINELY FIXED** for bench targeting | `Scripts/Core/MatchEngine.cs:220–226` resolves defenders with `FindActiveCardInstance`; `:763–785` searches only Core Active and Node Active arrays. Neither bench array is searched. |
| CR-02 | Fixed | **PARTIALLY FIXED** | `Scripts/Core/MatchEngine.cs:336–345` rejects different attacker/blocker battlefields, but `:387–425` still discards an accepted Node block on a direct Core attack. |
| CR-03 | Fixed | **GENUINELY FIXED** | `Scripts/Core/EffectInterpreter.cs:63–66,94–99` obtains the controller's player, counts that controller's Nodes, and passes the cap. `Scripts/Core/PlayerState.cs:36–39,57–63` exposes the matching public static signature and capped gain method. |
| CR-04 | Design decision required | **NOT FIXED**, explicitly deferred | No `PlayerState.cs` change appears in the supplied diff. No reshuffle fix is claimed. The underlying rules decision remains unresolved. |
| CR-05 | Not fixed; design decision mentioned | **NOT FIXED** | `Scripts/Core/EffectInterpreter.cs:365–368` still resolves both enemy Active target kinds through `GetCoreActiveUnits`, excluding Nodes. |
| PR-01 | Fixed | **GENUINELY FIXED** | `Scripts/Match/BattlefieldPresenter.cs:1981–1984` compares with `hoveredTile`; `:1292–1302` accepts/stores the same `Tile` type. Inspected painting callers are instance methods. |
| PR-02 | Fixed | **GENUINELY FIXED** | `Scripts/Match/BattlefieldPresenter.cs:1996–2010` prints ATK/HP/Shield only for Units; other types print their type. `Scripts/Core/CardInstance.cs:11` exposes `CardDefinition Definition`. |
| PR-03 | Fixed | **GENUINELY FIXED** for the reported fallback path | `Scripts/Match/GameCoordinator.cs:929–945` returns `null` after a failed mapped-instance lookup; definition-ID fallback is outside that branch. |
| PR-04 | Documented; no functional change required | **DOCUMENTED-ONLY** | `Scripts/Cards/CardData.cs:7–25` adds documentation but retains the reduced fields. `Scripts/Match/GameCoordinator.cs:451–457` still constructs only ID/name/cost/ability/accent. The missing hand-face information is not restored. |
| PR-05 | Fixed | **GENUINELY FIXED** | `Scripts/Match/GameCoordinator.cs:553–565` rejects a nominated slot that resolves to null. Caller returns `bool` at `:491`; `Reject` returns `bool`/`false` at `:963–968`. |
| CA-01 | Documented | **DOCUMENTED-ONLY** | `Scripts/Match/PrototypeCardPool.cs:71–74` adds a comment but leaves the advertised absent mechanic in the card text. |
| NET-01 | Fixed | **GENUINELY FIXED** for the reported leave/different-lobby race | `Scripts/Net/LobbyManager.cs:478–490` revalidates after the join await and calls `LeaveGame`. `Scripts/Net/NetBootstrap.cs:266–274` implements that disconnect through `mgr.Shutdown()`. |
| NET-02 | Fixed | **GENUINELY FIXED** for silent account-switch success | `Scripts/Net/AccountManager.cs:166–187` rejects an already-linked identity; successful remaining branches await link/sign-in before setting Google kind. |
| NET-03 | Fixed | **GENUINELY FIXED** for the reported Google-to-Guest relabel | `Scripts/Net/AccountManager.cs:127–140` signs in anonymously when needed, otherwise derives kind from the existing Google identity. |
| NET-04 | Fixed | **PARTIALLY FIXED** | `Scripts/Net/GoogleTokenProvider.cs:114–149` adds POST/declared-length checks and a timeout, but reads to completion before checking decoded length. `:83–103` still consumes only the first request. |
| NET-05 | Configuration complete | **NOT FIXED** in the inspected asset | `Resources/GoogleAuthSettings.asset:15` contains an empty `clientId:`. The asset has no entry in the supplied diff. |

### CR-01: both benches are excluded

The actual Active lookup contains only:

- Core Active search: `MatchEngine.cs:765–771`.
- Node Active search: `MatchEngine.cs:773–782`.
- Null return when absent: `MatchEngine.cs:784`.

Therefore, both **CoreBench and NodeBench** are excluded from declared defender resolution. This fix does not require the added cross-battlefield restriction.

### CR-02: accepted blocks can still disappear

`GetBattlefieldIndex` behaves as described:

- Core Active **and Core Bench** → `-1`.
- Node Active **and Node Bench** → `slot.NodeIndex`.
- Missing/unsupported placement → `-2`.

Evidence: `MatchEngine.cs:677–695`.

Bench inclusion in this helper is not itself an acceptance hole: both participants have already been resolved through the Active-only helper before declaration reaches it.

However, this counterexample remains:

1. Ready Unit attacker occupies Node 0.
2. Valid enemy Unit blocker occupies Node 0.
3. `DeclareBlockCommand` returns `Success`: both helper results are `0`.
4. The attacker issues `DeclareAttackCommand` without a positive defender ID.
5. The new same-battlefield attack check is skipped because it is inside the positive-defender branch (`MatchEngine.cs:221–239`).
6. `ConsumePendingBlocker` removes the pending entry, then rejects the Node blocker because a direct Core attack requires a Core Active (`:394,411–417`).
7. The attack proceeds against the Core and returns `Success` (`:248–260`).

Thus the original **successful declaration followed by placement-based discard** remains reachable.

The consumer also still returns null for `CannotBeBlocked`, missing blockers, and no-longer-valid blockers (`:396–407`). Some such invalidations are legitimate state changes, not necessarily defects; they nevertheless contradict an absolute claim that accepted blocks can no longer be ignored.

### PR-01 and PR-02: call-site/type checks

All inspected tint callers are instance methods:

- `PaintPile`: `BattlefieldPresenter.cs:1744–1755`.
- `PaintNodes`: `:1808–1828`.
- Empty and occupied `PaintTile` paths: `:1902–1955`.

The Node plate argument is not inherently non-hoverable: plates are collected as pickables (`:1175–1180`), and `BattlefieldInput.cs:192–211` forwards actionable hits to `SetHover` and treats plates as actionable. There is no demonstrated always-false plate comparison.

`CardText` now excludes both Relay and Protocol combat statistics. Protocol rendering is actually reachable through `PaintCore`, which calls `PaintTile(protocol, p.Board.Protocol, engine)` (`BattlefieldPresenter.cs:1786–1798`).

### NET-02 and NET-03: "always authenticate" needs qualification

- Google sign-in: every **successful** branch now awaits an authentication mutation; an already-linked account intentionally throws instead.
- Guest entry: an already-signed-in account does **not** always call a sign-in method. It inspects the existing account, which is an appropriate fix for the reported mislabeling.

No unconditional Google success assignment remains before link/sign-in completion. The guest method still treats a null Google identity as Guest; SDK identity freshness and other identity-provider semantics were not runtime-tested.

### NET-04: imports are correct; hard limits are incomplete

`System.Text` and `System.Threading.Tasks` are imported at `GoogleTokenProvider.cs:4–5`. The changed method contains real `await` operations. Its task values are used.

I found no basis in this change alone for alleging **CS1998**, **CS4014**, or newly unused local-variable warnings.

The remaining problems are behavioral:

- A request with unknown/chunked length can pass the declared-length check.
- `ReadToEndAsync` buffers the body before `body.Length` is checked.
- `body.Length` measures decoded characters, not input bytes.
- No expected form content-type check is made.
- Non-POST requests return an empty body, then fail downstream state validation. The listener does not resume waiting for the legitimate callback.
- `Task.WhenAny` does not cancel `readTask`. On timeout, exception unwinding disposes the reader and subsequently the listener, which may terminate the pending read, but the method neither explicitly cancels nor awaits/observes its eventual outcome.

The five-second race reduces the original awaited stall. It is not evidence of a fully enforced 16-KB streaming limit or clean read cancellation.

## 2. Unrequested changes and safety

### Cross-battlefield attack restriction — unsafe as submitted

The new rejection at `MatchEngine.cs:231–238` goes beyond CR-01. The baseline defect was **bench targeting**, not permission to attack other Active battlefields.

There is conflicting design commentary:

- `BattlefieldPresenter.cs:18–22` describes own-battlefield-only combat.
- `CombatLegality.cs:58–76` explicitly states the opposite for attackers: any battlefield may be attacked; only blockers are restricted.
- `CombatLegalityTests.cs:130–142` explicitly asserts that an enemy Core remains a target for a Node attacker, with the message **"Any battlefield may be attacked from any other."**

The fix matches the presenter's comment, but **does not match the existing executable targeting contract and tests**. It cannot be accepted as an established design correction without a rule decision and coordinated changes.

Consequences:

- Core attackers cannot attack Node defenders.
- Node attackers cannot attack Core defenders or other Nodes.
- `CombatLegality.AttackTargets` still offers those targets.
- The keyboard fallback can select a forbidden target when the mirror socket is empty: `GameCoordinator.cs:644–663` falls back to any surviving enemy Active and submits it.
- Direct Core attacks remain available to Node attackers because that path skips the new restriction.

This **does not** make all Node combat impossible: same-Node Active combat still passes the equality check. It does over-restrict previously supported cross-battlefield actions and produces UI/engine disagreement.

### Sentinel `-2`

For ordinary valid boards, the attacker and target were just resolved from Active slots on those same boards. Their `FindSlot` lookups should therefore succeed; the `-2` branch is not normally reachable.

The helper is not fail-closed in isolation: two missing/unsupported placements would compare equal at `-2`. Current callers' Active lookups prevent that ordinary case. No new reachable null dereference was established in these helpers.

### CA-01 comment

The comment is useful documentation and a safe nonfunctional change. It does not implement Interdiction Net's advertised mechanic and must not be counted as a functional resolution.

### Upkeep NP patch

Current upkeep genuinely grants:

```csharp
player.GainNP(3 + controlledNodes, cap);
```

Evidence: `MatchState.cs:440–443`.

But **`MatchState.cs` is absent from the supplied working-tree diff**. The present `+3` behavior is confirmed; its attribution to this fix pass is not. No independent live Git status was available.

## 3. Existing tests predicted to fail

**At least four test methods would fail under the inspected source. No tests were run.**

| Test method | Evidence | Predicted failure | Classification |
|---|---|---|---|
| `Destruction_PromotesFromNodeBench` | `Assets/Tests/Core/CombatAndEffectsTests.cs:216–236` | Core attacker → Node 1 defender now returns `InvalidTarget`; defender remains alive, failing `Assert.IsFalse` at `:233`. | Regression from new attack rule |
| `NodeControl_FollowsActiveOccupation` | `Assets/Tests/Core/CombatAndEffectsTests.cs:240–257` | Core attacker → Node 0 defender is rejected; defender stays alive and retains Node control. | Regression from new attack rule |
| `AttackTargets_EveryOfferedTargetIsAcceptedByTheEngine` | `Assets/Tests/Core/CombatLegalityTests.cs:173–208` | Query offers Node 1/3 targets to Core attacker; engine returns `InvalidTarget`, not expected `Success` at `:194`. | Regression/query-engine mismatch |
| `MisplacedBlock_IsOfferedByNowhereYetStillRecordedByTheEngine` | `Assets/Tests/Core/CombatLegalityTests.cs:389–407` | Cross-battlefield block now returns `InvalidCommand`, not expected `Success` at `:406–407`. | Existing test pins original defect; test needs correction |

### Quoted attacker/defender placement evidence

**Node promotion test:**

`CombatAndEffectsTests.cs:220–221`:

```csharp
var doomed = new CardInstance(UnitDef("node_active", 2, 1), PlayerIndex.Opponent) { State = UnitState.Ready };
state.Players[1].Board.NodeActive[1][0].Card = doomed;
```

`:226–227`:

```csharp
var attacker = new CardInstance(UnitDef("attacker", 4, 2), PlayerIndex.Player) { State = UnitState.Ready };
state.Players[0].Board.CoreActive[0].Card = attacker;
```

Attack submission is at `:231`.

**Node-control test:**

`CombatAndEffectsTests.cs:244–245`:

```csharp
var unit = new CardInstance(UnitDef("node_unit", 2, 2), PlayerIndex.Opponent) { State = UnitState.Ready };
state.Players[1].Board.NodeActive[0][0].Card = unit;
```

`:249–250`:

```csharp
var attacker = new CardInstance(UnitDef("attacker", 4, 2), PlayerIndex.Player) { State = UnitState.Ready };
state.Players[0].Board.CoreActive[0].Card = attacker;
```

Attack submission is at `:254`.

**Target-query/engine cross-check:**

`CombatLegalityTests.cs:202–205`:

```csharp
CoreUnit(s, PlayerIndex.Player, "attacker", 3, 5);
CoreUnit(s, PlayerIndex.Opponent, "mirror", 2, 5);
NodeUnit(s, PlayerIndex.Opponent, 1, "node1", 2, 5);
NodeUnit(s, PlayerIndex.Opponent, 3, "node3", 2, 5);
```

The test submits each offered target at `:191–192`.

### Tests that express conflicting expectations but do not themselves newly fail

`AttackTargets_MirrorIsByBattlefieldNotByIndex` and `AttackTargets_ListsEveryLivingEnemyActive_AndLeadsWithTheMirror` still inspect the unchanged query. They expose the incompatible contract but do not execute the new rejection.

Do not report these query-only assertions as newly failing merely because their advertised actions are now rejected.

No Node-A-to-Node-B attack execution was identified in the inspected test files. Such an attack would also be rejected by the new rule.

## 4. False or unsupported claims in the fixes report

### 4.1 "Configuration complete" for NET-05 — false

`Assets/Resources/GoogleAuthSettings.asset:15` contains:

```yaml
clientId:
```

The asset is not populated and is absent from the supplied diff. This directly contradicts the claim that the user has configured the ID **in that asset**. No credential value is reproduced here.

### 4.2 Final-state clean compile — unsupported

The report claims:

- `2026-10-21T07:29:41Z`.
- `3706ms`.
- Zero errors.
- Zero warnings.
- All fixes compiled cleanly.

Artifacts actually found/probed:

| Path | Observation |
|---|---|
| `Logs/AssetImportWorker0.log` | Exists. Inspected beginning identifies an Asset Import Worker; inspected tail shows domain reload/import parameter information. These excerpts do not prove the claimed compile. |
| `Library/LastBuild.buildreport` | Exists as binary serialized build-report data. Not decoded; no verified timestamp/result association with these fixes. |
| `Editor.log` | Not found at repository root. |
| `Library/Editor.log` | Not found. |
| `Logs/Editor.log` | Not found. |
| `Library/Bee/backend.log` | Not found at that exact path. |
| `Library`, `Logs` | Directories exist, but the read tool cannot enumerate them. |

**I cannot conclude that no compile artifact exists anywhere. I did not find inspected evidence substantiating this exact compile claim.**

The assignment supplies a final `MatchEngine.cs` modification time of **08:06:14Z**, which is **36 minutes 33 seconds after** the claimed compile. That filesystem timestamp was not independently accessible through the file reader. Taking it as supplied, the earlier compile cannot establish coverage of the later file state without additional evidence that content was unchanged.

This does not prove compilation would fail. It makes final-state compilation **unverified**.

### 4.3 "All actionable findings addressed" — false as a functional-completion claim

- CR-02 retains a concrete accepted-and-discarded block path.
- NET-04 is incomplete.
- PR-04 changes documentation, not the missing hand-face data.
- CA-01 leaves misleading card text/mechanics unchanged.
- CR-05 remains unfixed.
- NET-05 is still unconfigured in the inspected asset.

Documentation and deferral must not be presented as equivalent to functional repair.

### 4.4 PR-04 problem rewritten — misleading

The baseline finding concerns missing canonical information in the hand face. The fixes report reframes the problem as a missing load-bearing class comment and says no functional change is required.

The documentation is valuable, but it addresses a maintenance warning, not the original presentation defect.

### 4.5 "Invalid blocks … no longer silently ignored" — false as an absolute statement

The Node-block/direct-Core counterexample in §1 still reaches the placement-rejection branch after declaration success.

### 4.6 NET-04 "method validation prevents unrelated requests from consuming the sign-in attempt" — false

The code reads the first context once (`GoogleTokenProvider.cs:83–89`). A non-POST yields empty form data and fails state validation; there is no accept-next-request loop.

### 4.7 "16KB body size limit" — overstated

The header check is not a streaming limit, and the post-read check counts characters after buffering. Unknown-length input can consume more than 16 KB before rejection.

### 4.8 File list/total — inconsistent

The report enumerates **ten** source files but labels the total **nine**.

The supplied diff contains these nine:

1. `Assets/Scripts/Cards/CardData.cs`
2. `Assets/Scripts/Core/EffectInterpreter.cs`
3. `Assets/Scripts/Core/MatchEngine.cs`
4. `Assets/Scripts/Match/BattlefieldPresenter.cs`
5. `Assets/Scripts/Match/GameCoordinator.cs`
6. `Assets/Scripts/Match/PrototypeCardPool.cs`
7. `Assets/Scripts/Net/AccountManager.cs`
8. `Assets/Scripts/Net/GoogleTokenProvider.cs`
9. `Assets/Scripts/Net/LobbyManager.cs`

`MatchState.cs` is the extra enumerated file and is absent from that diff. Two diff headers abbreviate long paths, but their hunks were also checked against the named actual sources.

### 4.9 "13 of 15" — unreconciled

The baseline contains **16 distinct finding IDs**:

- CR-01–05: five.
- PR-01–05: five.
- CA-01: one.
- NET-01–05: five.

The baseline's own summary count is inconsistent with its detailed findings; the fixes report should have reconciled that rather than repeating a denominator of 15.

This verification classifies the 16 IDs as:

- **9 genuinely fixed** within their reported defect scope.
- **2 partially fixed**.
- **2 documented-only**.
- **3 not fixed**.

This accounting is separate from the new attack-rule regression.

### 4.10 "Warnings: 0" — not independently verified, not disproven by task usage

No fresh compiler output was available. Conversely, the changed task code does not inherently justify CS1998/CS4014 accusations: it awaits work and uses the task locals. No unused new local was apparent in the supplied diff.

### 4.11 "No test failures during compilation" — not regression evidence

Compilation is not test execution. The report acknowledges tests were not executed. Its closing safety assessment is premature given the statically predictable failures above.

## 5. Recommended corrections before acceptance

1. **Resolve the attack-rule contradiction.** Remove the unrequested restriction unless authoritative design approval supports it. If changing the rule intentionally, update targeting, keyboard/AI selection, tests, and documentation together.
2. **Finish CR-02 without hiding it behind attack restrictions.** Define how block precommitment relates to the actual intended target, including direct Core attacks from Nodes.
3. **Correct the defect-pinning misplaced-block test** to expect rejection.
4. **Implement bounded incremental OAuth body reading**, expected form validation, and a strategy for unrelated requests and read cancellation.
5. **Correct the fixes report**: exact finding inventory, functional versus documented statuses, nine-file list, upkeep attribution, empty client-ID status, and compilation evidence.
6. After the working tree stops changing, obtain a **fresh final-state compile and test result**, with timestamped artifacts. This reviewer did not run either.

## 6. Not reviewed / limits

- This was static, read-only verification. No Unity interaction, compilation, tests, live authentication, network races, or pointer rendering was performed.
- The supplied diff was read in full and checked against relevant actual source. Its completeness and current Git status were not independently regenerated.
- All of `CombatAndEffectsTests.cs`, `CombatLegalityTests.cs`, and `MatchEngineTests.cs` were read. Other test files were not exhaustively inventoried; **four predicted failures is a minimum, not an exhaustive suite result**.
- Directory enumeration and filesystem metadata inspection were unavailable. Specific missing log paths do not establish repository-wide absence.
- The existing binary build report was not decoded or correlated to this working tree.
- The GDD PDF was not independently parsed. Conflicting source comments cannot establish intended design by themselves.
- Same-lobby-ID leave/rejoin generations, SDK account-identity freshness, transport callback timing, and the exact runtime fate of a disposed pending stream read remain unverified.

**Final verdict: FAILED — genuine fixes are present, but residual defects, an unrequested gameplay regression, predicted existing-test failures, and false/unsupported report claims prevent acceptance of this report as a trustworthy completion record.**
