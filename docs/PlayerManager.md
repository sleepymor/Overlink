# Testing Player Manager

Open **Overlink → Tools → Player Manager** in the Unity Editor. This is an
Editor-only administrative tool, not a shipped debug menu or a player-callable
currency endpoint. It is hard locked to:

- Project: `467bcece-28d8-4091-9b3c-8138c7a02dca`
- Environment: **testing**, `ff807509-790a-4ab0-90aa-0b2a9aac70dc`
- Cloud Save default player-data key: `overlink_progression_v1`

## One-time administrative setup

1. In Unity Dashboard, open **Manage organization → Administration → Service
   Accounts**. An organization owner/manager may need to do this.
2. Create a dedicated testing service account and key. Assign project-level
   roles that grant **Cloud Save player-data read and write** access for Overlink.
   Review the permissions shown in Dashboard; do not grant organization-wide
   Owner/Admin access just to use this tool. Use environment restrictions where
   supported. The tool's Testing lock does not itself restrict a leaked key.
3. Outside the repository, configure these environment variables for the process
   that launches Unity:
   - `OVERLINK_TESTING_SERVICE_KEY_ID`: the service-account key ID.
   - `OVERLINK_TESTING_SERVICE_SECRET`: its secret key.
4. On Windows, the tool reads saved **user environment variables directly**, so
   restarting Unity/Hub is not required. Click **Recheck credentials** in the
   window. On other platforms, restart Unity/Hub so the Editor inherits them.

Do not put credentials in scripts, assets, PlayerPrefs, EditorPrefs, tracked
configuration, screenshots, or chat. The window has no credential input/storage.
The client exchanges credentials for an in-memory, project-and-environment-scoped
bearer token and discards it on window close/domain reload. Revoke unused keys.
It selects a complete pair from the process environment first, then the Windows
user environment. It never mixes values from different sources or displays them.
The window shows the source, not the credentials. If rotating a saved user key
while Unity still has an older complete process pair, restart Unity/Hub to drop
that older inherited pair.

## Inspect and adjust a test player

1. Select **Testing** in Unity Dashboard and copy the player's ID from
   Authentication / Player Management. The player must already have signed in
   and initialized progression (normally by selecting their starter).
2. Open Player Manager, paste the ID, and click **Load / Refresh from Testing**.
3. **Owned cards** shows server-owned IDs and counts, with names and rarities
   resolved from editable local `Assets/Cards/Data/*.asset` files. Missing local
   cards remain visible by ID. The **Asset** button pings the local definition;
   it does not edit ownership.
4. **Decks & progression** shows saved decks, active/battle deck IDs, starter,
   level, XP, match totals and purchase/loyalty counters, all read-only.
5. **Sign the target player out on every client.** Leave Editor Play Mode.
6. Under **Balances → Add to balance**, select the balance, enter a positive
   **Amount to add**, and click **Add amount to pending total**. For example,
   adding `1000` to `2400` stages a new total of `3400`. This does not save yet.
   Alternatively edit the **Pending totals** directly to set absolute balances.
   Enter a reason and acknowledge that the player is not playing.
7. Click **Review and save to Testing…**, inspect the player ID and before/after
   totals, then confirm. A successful operation refreshes from Cloud Save.
8. Sign in again in-game to reload the authoritative state. Administrative pack
   grants do not increase `PurchasedPacks` or advance paid-purchase loyalty.
   Free-loyalty opening still needs its dedicated in-game action wired up.

**Zero-credit testing caveat:** the current module's starter-credit backfill can
restore `2400` credits when `GetState` loads an otherwise untouched starter
account set to zero (no purchases, standard packs, XP or match totals). Use an
already-progressed test player for zero-credit tests. The manager does not
falsify progression counters to bypass this behavior.

The tool does not create progression records, modify card ownership/decks, edit
schema versions, or expose Production editing. Unknown JSON fields and receipt
dictionaries are preserved; only changed balance properties are replaced.
Unsupported future schemas and missing write locks are refused on save.
Use a Testing deployment of the latest progression module before testing new
entitlement fields: an older module may ignore/drop fields it does not know.

## Conflicts, failures and audit

- Every update includes the write lock returned on load. HTTP 409 means the
  record changed: refresh and review again. There is no force overwrite.
- Writes are never automatically retried. A timeout/cancellation can occur
  **after** a server commit. Refresh before deciding whether another adjustment
  is necessary. Interrupted saves invalidate the snapshot.
- The current progression module uses unconditional Cloud Save writes. A lock
  on this tool cannot prevent a gameplay operation that already loaded an older
  snapshot from overwriting a later admin edit. **Keep the player signed out.**
- Attempts and outcomes are appended outside the project to
  `%LOCALAPPDATA%/Overlink/PlayerManager/testing-adjustments.jsonl` on Windows
  (the OS LocalApplicationData folder elsewhere). Records include player ID,
  reason, UTC timestamp and before/after balances; no credentials or full player
  payload. Treat player IDs and reasons as private information. This is a local
  diagnostic trail, not a tamper-proof or centralized server audit.
- If the local audit cannot be written, the write is aborted before sending. If
  an outcome audit fails after sending, refresh: the server may have committed.
- HTTP 401/403: check key validity and project Cloud Save data permissions.
  HTTP 429/5xx: wait and refresh. No response bodies or tokens are logged.

## Existing security limitation

The module currently stores progression in **default** player data, which Unity
documents as player-readable **and player-writable**. Calling Cloud Code from
the normal UI does not secure that storage against a modified client. Protected
player data or an appropriate access policy, plus concurrency-safe module saves,
still needs to be addressed separately before claiming a secure authoritative
economy. This Editor tool does not change the access class or migrate data.

## API references

- [Unity service-account authentication](https://services.docs.unity.com/docs/service-account-auth/)
- [Token exchange (v1)](https://services.docs.unity.com/auth/v1/)
- [Cloud Save REST API and write locks](https://services.docs.unity.com/cloud-save/v1/)

The v1 exchange is called with explicit `projectId` and `environmentId` query
parameters. Cloud Save requests use the resulting scoped bearer token; they do
not use the current player's authentication session or a default environment.
