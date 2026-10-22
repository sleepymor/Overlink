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
| Button credential exchange | n/a — a real browser tab | legacy popup (`use_fedcm_for_button: false`) |
| State check | OAuth `state` + `nonce` | `nonce` only |
| Deadline | 2 min | 3 min |
| Can fail silently | no | no |
| Escape when the embed obstructs | none needed | **Sign in in a new tab** |

WebGL is the harder case because an itch.io web play embeds the build in a cross-origin iframe, and One
Tap is precisely the GIS feature that assumes it can paint over the top-level page. So the bridge falls
back to rendering Google's own button in a DOM overlay when One Tap declines, keeps that button on the
legacy credential transport, offers a top-level tab when even that is refused, and reports a coded
diagnostic (`cancelled` / `unavailable` / `failed` / `fallback`) with GIS's raw reason and the iframe's
own origin. `GoogleTokenProvider` turns those into a `GoogleDeclineException` rather than a message
string, because `AuthResult.FromException`'s generic path matches English substrings and Google's
sentences contain the words "password" and "timeout". `GoogleDeclineMappingTests` pins that down,
including the two collisions it exists to prevent.

What to actually expect on itch.io, stated plainly so nobody re-derives it. There are three independent
gates, they fail differently, and each one only becomes visible after the previous one is passed — which
is why this took three rounds and why the log lines below are worth keeping.

1. **The origin gate.** GIS checks the calling page's origin against the OAuth client's **Authorized
   JavaScript origins** before it will draw *anything*. If the itch.io CDN host is missing there, One Tap
   *and* the fallback button both die at the same moment, and the browser console says
   `[GSI_LOGGER]: The given origin is not allowed for the given client ID.` alongside a 403 on
   `accounts.google.com/gsi/status`. This is configuration, not code, and no amount of fallback logic
   touches it. It is the first failure that actually happened here, with the frame's origin confirmed as
   `https://html-classic.itch.zone` from the rejected request's own headers. The bridge detects it by
   waiting five seconds for `renderButton` to produce a DOM node and reporting `unavailable` when nothing
   appears, quoting GIS's own console line — that rejection is otherwise invisible to Unity's log, because
   it is printed to the browser console and never handed to a callback. **Confirmed passed:** after the
   origin was registered, the same build logged `fallback: One Tap not displayed: opt_out_or_no_session
   [origin: html-classic.itch.zone]` and drew a real button.
2. **The One Tap gate.** Once the origin is allowed, One Tap *still* declines inside a cross-origin frame.
   Observed directly: the rejected `/gsi/status` request carried `sec-fetch-storage-access: none`, so the
   browser grants the frame no first-party cookie access. The reason string that comes back is
   `opt_out_or_no_session` — no session in that frame, not a player who opted out. This is structural
   rather than something more fallback logic can code around, and the rendered Google button is the
   answer: the player clicks it themselves, so it needs no permission to paint over the page.
3. **The credential-exchange gate.** The button being *clicked* is not the end of it. With FedCM enabled
   for the button, Google's chooser completes the exchange by calling `navigator.credentials.get()` from
   GIS's iframe inside the game page, and in a third-party frame that call is refused unless the embedder
   delegates `publickey-credentials-get` via the iframe's `allow` attribute — which itch.io does not. The
   promise rejects, and the chooser dismisses itself **after authenticating the player and throwing the
   result away**. The visible symptom is distinctive and easy to misread as a popup glitch: the window
   opens, sits on *Processing your request*, and closes, with no error anywhere. So the bridge sets
   `use_fedcm_for_button: false` and lets the legacy transport finish the exchange in its own top-level
   popup, where none of the above applies. `use_fedcm_for_prompt` stays `true` — the opposite choice, on
   purpose, because One Tap has no popup of its own and the flag is what makes the first-party case work.

Google is retiring the button's FedCM opt-out. When that lands, gate 3 stops being codeable around on this
host and the bridge's **Sign in in a new tab** escape hatch becomes the primary web path: a top-level tab
is first-party, so all three gates are simply absent. It appears in the overlay only after GIS reports a
failure, because offering a player a way out of the embed alongside a working button invites them to take
it for no reason. `probeFedCM` logs `publickey-credentials-get=denied|granted|prompt` in the meantime, so
a future build says which of those worlds it is in.

A fourth point is now watched, though it has never been *seen* failing: the **handback**, where Google's
popup completes the authentication and has to return the ID token to the frame that opened it. Nothing on
that path reaches `error_callback`, so it fails as silence and C# burns its deadline. The bridge therefore
wraps `window.open` and taps `postMessage` for the flow's duration: a popup that closes without delivering
a token is a named `failed` diagnostic carrying its URL (single-use secrets redacted, payload shape only —
a bearer credential never enters a log line), and an event with the opaque `origin: null` that a sandboxed
popup produces is visible at all. Both are non-terminal, and both are uninstalled when the flow ends,
because a wrapped `window.open` left in a shipped page is worse than the diagnostic it buys.

**Check which build a console belongs to before reading it.** A browser console belongs to whichever build
the tab loaded, and a tab left open across a deploy keeps reporting the previous one. Every symptom in this
section was observed on itch, so this is worth more than a footnote: one full round here was spent on a
console showing only `fallback: One Tap not displayed`, which read as the FedCM opt-out having failed and
was in fact the pre-opt-out build. The deployed bridge is a public static file, so the question "is my fix
live?" never needs a playtest:

```
node Tools/WebGL/check-deployed-build.mjs "https://sleepymor.itch.io/<game>?secret=<unlisted-secret>"
```

It resolves the frame directory and `?v=` stamp the page points at, decodes `Build/Overlink.framework.js.br`,
prints the live `use_fedcm_for_prompt` / `use_fedcm_for_button` values, and reports which round's markers the
shipped bridge contains — exiting non-zero when the button still uses FedCM, or when neither flag is present
and the deployment predates the fix outright. Pass a frame directory instead of a page URL to check a build
you already identified. itch's CDN sends no `Permissions-Policy`, `X-Frame-Options` or COOP/COEP header on the
game frame either, which is worth knowing: gate 3 depends entirely on the embedder's `allow` attribute and
nothing else, so `probeFedCM`'s one line is the whole verdict.

Every gate above reports through GIS's `error_callback` or the prompt notification rather than guessing,
and a recoverable error is deliberately **not** terminal: the overlay survives a failed attempt so the
player can retry, since tearing down the card they are looking at turns a retry into a relaunch.

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

Coverage comes in three layers, because the auth flow spans three runtimes and no single one of them can
see the others:

- **EditMode, `Assets/Tests/Net` (26 tests).** `GoogleDeclineMappingTests` pins all four
  `GoogleDecline` outcomes through `AuthResult.FromException`, including the two English substrings
  ("password", "timeout") that the generic path would otherwise win with. `NonceVerificationTests` pins
  the ID-token gate: a matching nonce passes at every payload length, an absent claim is distinguished
  from a wrong value, a malformed token never matches, and the raw token never leaks into a player
  message. `VerifyNonce` deliberately lives below the platform `#if`s so the check the WebGL path depends
  on is one the editor can actually execute.
- **`node Tools/WebGL/gsi-bridge-test.mjs` (108 assertions).** A `.jslib` belongs to no assembly, so
  nothing in `Assets/Tests` can reference it and Unity's runner cannot see it at all. This harness stubs
  `mergeInto`, `UTF8ToString`, `SendMessage`, the DOM, fake timers and `google.accounts.id`, and covers
  what the bridge does with each GIS outcome: the One Tap → button handoff, the coded diagnostics, the
  five-second render verification, the console tap's install and restore, the non-terminal `error_callback`,
  the hidden-until-failed new-tab exit, the FedCM probe, the two transport flags being opposite, and the
  handback spies - a closed popup named once and non-terminally, secrets redacted from its URL, message
  payloads reduced to their shape, host `postMessage` filtered out, and `window.open` plus the listener
  given back when the flow ends. Every guard in it has been mutation-verified: deleting the guard must fail
  an assertion, not just reduce the count.
- **`node Tools/WebGL/check-deployed-build.mjs`.** Answers "is my fix actually live?" against the public
  static files itch is serving, without a playtest, and exits non-zero when the deployed bridge still has
  the button on FedCM. It is what identified the stale console above.
- **`Tools/WebGL/iframe-probe/serve.mjs`.** A real two-origin embed, in a browser, in thirty seconds,
  printing GIS's raw verdicts instead of translating them into a player message. Use it before theorising
  about any iframe regression; it is how gate 3 above was confirmed to be about `allow=` rather than the
  origin list.

Verified end to end in play mode: register → age → username#tag → MainMenu with the name showing;
restart → cached session resumed and setup skipped; sign out → back to Auth; a protected scene opened
directly while signed out → redirected to Auth; wrong password and under-13 and short-username → the
matching player-facing error with no state change. The Google path itself can only be verified against a
deployed WebGL build on its real origin, which is why every layer above exists.
