# Authentication flow

Overlink requires an account. There is no guest mode, no "continue without signing in", and no way to
reach MainMenu or any gameplay scene from a cold boot without passing through this flow first.

Everything below is implemented in code that builds its own UI at runtime. Nothing here needs manual
Inspector wiring: the Auth scene contains a camera and an `AuthScreenController`, and the controller
constructs the rest of its widgets in `Awake`. Designer overrides stay possible — the serialized
references are still there, and a widget assigned in the Inspector replaces the runtime-built one.

## The three outcomes of a cold boot

The build settings put `Auth` at index 0, so the game always starts there. `AuthFlowController` runs
the flow and owns every scene transition it causes:

| Situation | Result |
| --- | --- |
| Cached session, complete profile | straight to **MainMenu** |
| Cached session, no profile yet | **Auth** stops on profile setup |
| No usable session | **Auth** shows sign-in |
| Legacy anonymous session | discarded on sight, then sign-in |

UI controllers never load a scene. They call `OnPlayerSignedIn`, `OnPlayerSetupComplete` or
`OnPlayerSignedOut` on `AuthFlowController`, so there is exactly one place to look when wondering why
a transition happened. States are `Initializing → CheckingCachedSession → SignedOut | LoadingProfile →
(NeedsProfileSetup | Ready)`, plus `Error`.

## Files

| File | Role |
| --- | --- |
| `Net/AccountManager.cs` | Owns the UGS session: sign-in, sign-up, sign-out, account classification |
| `Net/AuthFlowController.cs` | State machine and the only source of auth scene transitions |
| `Net/PlayerProfile.cs` | The profile record (username, tag, age flag, version) |
| `Net/PlayerProfileService.cs` | Cloud Save read/write, draft-then-commit setup |
| `Net/PlayerIdentity.cs` | `Username#Tag` value type and its validators |
| `Net/AuthResult.cs` | Maps UGS error codes and payloads onto player-facing text |
| `UI/AuthScreenController.cs` (+ `.Build.cs`) | Sign-in and profile-setup screens |
| `UI/UiKit.cs` | The UGUI/TMP primitives both self-building screens share |
| `UI/ProtectedSceneGuard.cs` | Redirects a directly-opened protected scene back to Auth |
| `UI/MainMenuController.Account.cs` | Name display, Player ID dialog, sign-out |
| `Net/AccountResetUtility.cs` | Editor menu items for wiping local account state |

## Resuming a cached session

The Unity Authentication SDK does **not** resume a session on its own. `UnityServices.InitializeAsync()`
loads the cached token from PlayerPrefs into `SessionToken` but leaves `IsSignedIn` false — the only
internal caller of that resume path is the refresh scheduler, which refuses to run until a session is
already signed in. So `AccountManager.RestoreCachedSessionAsync` asks for the resume explicitly, and
the only public call that performs it is `SignInAnonymouslyAsync`: when a cached token exists it takes
a "continuing existing session with cached token" branch and never touches the anonymous provider.
`CreateAccount = false` makes it fail rather than mint an anonymous account when there is no token to
resume, which is what keeps this compatible with the no-guest-accounts rule.

The resumed account is then inspected and anything that is not a real login is discarded:

1. a Google link → `AccountKind.Google`
2. a non-null `PlayerInfo.Username` → `AccountKind.UsernamePassword`
3. otherwise the identity list is scanned; a non-anonymous provider means `UsernamePassword`
4. nothing recognised → `AccountKind.Guest`, signed out on sight with `SignOut(true)` so the stale
   token is cleared and the next boot does not repeat it

Step 4 is what catches the anonymous sessions the removed guest flow left on developer machines.

## Profile setup

First-time players answer two questions, in order, before reaching MainMenu.

**Age.** Minimum 13, editable on the `AuthScreenController` Inspector. Tied to the account, not the
device, so it is asked once.

**Username#Tag.** Riot-style, and deliberately *not* UGS's own player-name API:
`GetPlayerNameAsync(autoGenerate: true)` invents names for players who never chose one, and
`UpdatePlayerNameAsync` rejects whitespace, so neither can carry a `Username#Tag`. Cloud Save is the
single source of truth; `AccountManager.PlayerName`/`PlayerTag` are a cache fed by
`ApplyIdentity(username, tag)`.

- username: 3–16 characters, letters, digits, spaces, `_`, `-`; no leading or trailing space
- tag: 3–5 characters, forced to upper case as typed, digits allowed
- the pair is copyable from the MainMenu Player ID dialog

Uniqueness is **not** enforced locally. Two players can pick the same `Username#Tag`; the collision is
logged once by `WarnIfUniquenessUnchecked` and waits on a Cloud Code reservation endpoint.

Setup writes Cloud Save in a single shot — `BeginSetup()` creates a draft in memory, `CommitSetupAsync()`
writes every key at once — so a half-finished profile can never exist server-side. The keys are
`profile_username`, `profile_tag`, `profile_age_confirmed`, `profile_completed`, `profile_version`,
`profile_created_at`.

## Scene protection

`ProtectedSceneGuard` sits on a root object in MainMenu, Gameplay, Deckbuilder, Collection, Settings
and NetPresenceDemo. It is reactive: it subscribes to `AuthFlowController.StateChanged` and redirects
only on `SignedOut`, `Error` or `NeedsProfileSetup`. Judging the state itself in `Awake` would race the
profile fetch and bounce legitimately signed-in players back to the login screen, which is the bug this
shape avoids.

In normal play the flow controller has already routed the player and the guard does nothing. The case
it covers is opening a protected scene directly from the Editor. Its `requireLogin` toggle exempts a
scene, which is useful while the net demo scenes are still being iterated on.

## Google sign-in

See `Net/GOOGLE_AUTH_SETUP.md` for provider configuration and `docs/GOOGLE-AUTH-SETUP.md` for the
Google Cloud Console walkthrough. Two things are easy to miss:

- the desktop client listens on the **fixed** loopback port `7777` with redirect
  `http://127.0.0.1:7777/callback/`, so that URI must be in the OAuth client's authorized list —
  a dynamic port guarantees `redirect_uri_mismatch`
- the client ID lives in `Assets/Resources/GoogleAuthSettings.asset`. Client IDs are public config;
  client secrets must never ship in the player

The two platforms reach that ID token by completely different routes, and only one of them runs inside
somebody else's page:

| | Windows | WebGL |
| --- | --- | --- |
| Transport | system browser + loopback `HttpListener` | `Assets/Plugins/WebGL/OverlinkGoogleAuth.jslib` |
| Grant | `response_type=id_token`, `form_post` to `127.0.0.1:7777` | Google Identity Services in-page |
| State check | OAuth `state` + `nonce` | `nonce` only |
| Deadline | 2 min | 3 min |
| Can fail silently | no | no |

WebGL is the harder case because an itch.io web play embeds the build in a cross-origin iframe, and
One Tap is precisely the GIS feature that assumes it can paint over the top-level page. So the bridge
opts into FedCM, falls back to rendering Google's own button in a DOM overlay when One Tap declines,
and reports a coded diagnostic (`cancelled` / `unavailable` / `failed` / `fallback`) with GIS's raw
reason and the iframe's own origin. `GoogleTokenProvider` turns those into a `GoogleDeclineException`
rather than a message string, because `AuthResult.FromException`'s generic path matches English
substrings and Google's sentences contain the words "password" and "timeout".
`GoogleDeclineMappingTests` pins that down, including the two collisions it exists to prevent.

What to actually expect on itch.io, stated plainly so nobody re-derives it. There are two independent
gates, and they fail differently:

1. **The origin gate.** GIS checks the calling page's origin against the OAuth client's **Authorized
   JavaScript origins** before it will draw *anything*. If the itch.io CDN host is missing there, One Tap
   *and* the fallback button both die at the same moment, and the browser console says
   `[GSI_LOGGER]: The given origin is not allowed for the given client ID.` alongside a 403 on
   `accounts.google.com/gsi/status`. This is configuration, not code, and no amount of fallback logic
   touches it. It is the failure that actually happened here, with the frame's origin confirmed as
   `https://html-classic.itch.zone` from the rejected request's own headers. The bridge now detects it by
   waiting five seconds for `renderButton` to produce a DOM node and reporting `unavailable` when nothing
   appears, quoting GIS's own console line — that rejection is otherwise invisible to Unity's log, because
   it is printed to the browser console and never handed to a callback.
2. **The iframe gate.** Once the origin is allowed, One Tap *still* declines inside a cross-origin frame.
   Observed directly: the rejected `/gsi/status` request carried `sec-fetch-storage-access: none`, so the
   browser grants the frame no first-party cookie access, and FedCM needs exactly that to identify a
   signed-in Google account. (It separately requires the embedder to delegate `publickey-credentials-get`
   via the iframe's `allow` attribute, which itch.io does not do.) Either refusal alone is sufficient, so
   this is structural rather than something more fallback logic can code around. The rendered Google
   button is therefore the working path on itch.io, not a degraded fallback. FedCM is opted in regardless,
   because the identical build played from a first-party tab gets the native picker with no overlay, and
   without the flag GIS uses a transport Google is retiring.

If an embed denies popups as well, Google sign-in is simply unavailable on that host and the player uses
username and password — which is why the `unavailable` message leads with that rather than promising a
retry. Note also that itch.io's CDN host is theirs to change: it already went from `hwcdn.net` to
`itch.zone` once, so a Google sign-in regression with no change on our side is worth re-reading the
origin before anything else.

`Tools/WebGL/iframe-probe/serve.mjs` reproduces the two-origin setup in thirty seconds, without a build,
and prints GIS's raw verdicts. Use it before theorising about any future iframe regression.

## Testing

`AccountResetUtility`'s menu items (`Overlink ▸ Account`) reset local state only:
*Reset Account (Full)* signs out, deletes all PlayerPrefs and clears the auth cache files;
*Sign Out (Keep PlayerPrefs)* drops just the session; *Show Account Info* reports the live state.
Whatever is in Cloud Save belongs to the account and stays on the service, so a corrupt server-side
profile needs the Unity Dashboard to clear.

Verified end to end in play mode: register → age → username#tag → MainMenu with the name showing;
restart → cached session resumed and setup skipped; sign out → back to Auth; a protected scene opened
directly while signed out → redirected to Auth; wrong password and under-13 and short-username → the
matching player-facing error with no state change.
