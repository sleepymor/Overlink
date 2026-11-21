# Competitive multiplayer: initial feasibility audit

**Status: blocked for secure or competitive play.** This directory contains a local
.NET 6 portability smoke test, not an authoritative match service. The current
unranked Relay prototype loads each player's selected cloud deck, but runs the
rules on the host's device and accepts the guest's submitted deck. It does not
provide deck secrecy from the host or enforce guest ownership against a modified
client. Online prototype results are not reported to progression.

## Ownership and results

- `Assets/CloudCode/Module~/Project/Module.cs` stores ownership in the `Owned`
  dictionary of the existing progression record, `overlink_progression_v1`.
  Its `SaveDeck` path checks requested counts against that dictionary. The
  client cache in `Assets/Scripts/Net/CloudPlayerState.cs` is not proof of
  ownership; the match service must read the trusted backend record directly.
- The source migrates `Owned` from default player data into Protected Player
  Data. The effective deployed policy and migration have not been verified;
  historically client-writable data cannot establish legitimate ownership.
- `SaveState` unconditionally writes the default record and then writes protected
  ownership separately. `SaveTrustedOwnership` reads a fresh lock immediately
  before writing rather than preserving the lock from the initial state read, so
  concurrent operations can overwrite changes. Cross-record writes are not atomic.
  Do not use this path as competitive proof until concurrency and recovery are fixed.
- `ReportMatch` accepts client claims, uses matching claims for PvP, and writes
  two player records separately. It must not determine competitive results or
  ratings. Keep existing behavior for existing modes until a verified replacement
  is available; do not route competitive results through it.
- Moving an existing default record to protected storage does not establish that
  its historical contents are legitimate. Agree an account migration and trust
  policy before using previously client-writable ownership for competitive play.
  Reuse the existing progression/ownership system; do not create a second
  inventory as an alternative source of truth.

## Rules portability

`CompetitiveSpike.csproj` links the current Unity-independent Core sources and
executes an in-process seeded match and concession twice under .NET 6, the target
of the existing Cloud Code module. This is intentionally a narrow smoke test,
not a full replay or security test. It does not test deployment, AOT, WebGL,
concurrency, serialization, or identical behavior across runtimes.

The engine currently has `System.Random` state and a match-local ID counter in
`MatchState`, and combat `pendingBlocks` inside `MatchEngine`. A complete restore
must account for all of these and every other rules-relevant field; persisting
`MatchState` alone is not sufficient. Match-local IDs and cryptographically
secure, versioned, restorable randomness must be required for competitive games.

`MatchViewSnapshot` now includes public discard identities; no banished-card zone
was found in Core. The existing wire encoder is guest-only and its v2 format is
incompatible with v1 peers.
Do not assume these types implement the approved competitive visibility contract.

## Local smoke test

From the repository root, on a host with the .NET 6 SDK:

```powershell
dotnet build Tools/CompetitiveSpike/CompetitiveSpike.csproj
dotnet Tools/CompetitiveSpike/bin/Debug/net6.0/CompetitiveSpike.dll
```

No Unity Editor is needed. This is not a Unity build and does not replace CI.

## Private Cloud Save probe

`CloudSaveProbe/CloudSaveProbe.csproj` is a separate Cloud Code module source
for the Overlink **testing** environment only. It compiles against the same UGS
API package versions as the existing progression module and does not modify it.
The `ProbePrivateWriteLock` endpoint creates a new random private custom item,
reads its write lock, commits one update, then attempts another with the old
lock. It reports whether the stale write was rejected and the committed value
remained intact. It returns the generated custom ID so a test client can try
direct reads and writes separately. The probe does not handle real match data.

Build without deploying:

```powershell
dotnet build Tools/CompetitiveSpike/CloudSaveProbe/CloudSaveProbe.csproj
```

For an authorized deployment, package this as a **separate temporary module**
in the testing environment, not the progression module. The endpoint checks the
project and environment IDs and requires an authenticated caller. Its private
item contains only test strings. Each invocation creates a persistent item; use
the returned custom ID to remove test items through an authorized Cloud Save
admin workflow after testing. Because any authenticated testing player could
invoke the endpoint and create items, restrict access and remove the temporary
module after the experiment. Do not deploy it to production.

The probe checks a sequential stale-lock case, not simultaneous requests,
response-loss recovery, initial-creation races, membership, or direct client
denial. Run those independent tests before selecting the match storage scheme.
Compiling the module is not evidence it has been deployed or that those checks
have passed. A response other than both flags true fails the stale-lock check.

## Deployment gate (not yet performed)

Use a dedicated testing environment, non-production accounts, and authorized
service credentials. Never publish credentials or player data in logs. Before
enabling competitive play, demonstrate and record:

1. The deployed progression version and effective access policy; attempt direct
   client reads and writes of test ownership and confirm unauthorized writes
   fail. Establish migration and concurrency handling for existing progression.
2. A server-only match record that neither participant can read or write
   directly. Verify that the Cloud Code identity can access it.
3. Conditional, atomic commit of state, revision, deduplication receipt, and
   terminal result in one record, including concurrent requests and response
   loss after commit. Verify service size and write-lock semantics in practice.
4. Module deployment and execution of the shared rules, a pinned card catalog,
   and bounded realistic match payloads within time, quota, and cost limits.
5. Authenticated WebGL and desktop calls, snapshot confidentiality for both
   seats, and server-side deadline enforcement even when neither player polls.
6. Trusted membership for friend invitations and matchmaking, plus idempotent
   lobby cleanup and result publication. Friend matches must not affect rating.

The local source inspection and smoke test are not evidence that any of these
deployed checks passed. No credentials, policies, or live service data were
changed by this spike.
