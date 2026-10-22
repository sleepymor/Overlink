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
| `Net/PlayerProfile.cs` | The profile record (username, tag, age flag, email, version) |
| `Net/PlayerProfileService.cs` | Cloud Save read/write, draft-then-commit setup |
| `Net/PlayerIdentity.cs` | `Username#Tag` value type and its validators |
| `Net/AuthResult.cs` | Maps UGS error codes and payloads onto player-facing text |
| `Net/PasswordPolicy.cs` | Unity's password rules, checked client-side and quoted to the player |
| `Net/EmailPolicy.cs` | Address shape rules; the address itself is **unverified** — see below |
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

First-time players answer three questions, in order, before reaching MainMenu.

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

**Email.** Asked on the same step as the identity, not a fourth screen. It is stored in the profile
because **Unity cannot hold it**: `SignUpWithUsernamePasswordAsync(username, password)` takes nothing
else, and the package exposes no `UpdateEmailAsync`, no verification endpoint and no password-reset call
— the only `email` fields in `com.unity.services.authentication` are claims inside a Unity-ID `IdToken`
that this project does not use. See *Email is collected, not verified* below before treating the value as
anything other than something the player typed.

Shape is enforced by `Net/EmailPolicy.cs` (RFC 5321 length caps, RFC 5322 `atext` local part, a
two-label domain). Internationalised addresses are refused outright rather than half-supported: an
IDN domain needs Punycode, and handing raw UTF-8 to a mailer bounces it, which would leave a dead
address on file. `"quoted"@local` is refused for the same reason — legal, and never issued by a
consumer provider.

Unlike `PasswordPolicy`, this one publishes no `Requirements` line. Password rules are not guessable;
an address is. What the field does carry is a purpose sentence (`EmailPurposeCopy` in the builder),
because collecting personal data next to a *public* `Username#Tag` without saying which is which is the
part a player reasonably objects to.

Setup writes Cloud Save in a single shot — `BeginSetup()` creates a draft in memory, `CommitSetupAsync()`
writes every key at once — so a half-finished profile can never exist right. The keys are
`profile_username`, `profile_tag`, `profile_age_confirmed`, `profile_email`, `profile_email_verified`,
`profile_completed`, `profile_version`, `profile_created_at`.

`profile_version` went to **2** when the email fields landed, and `IsComplete()` now requires a non-empty
address. The consequence was taken knowingly rather than by accident: **every profile written by an
older build reads as incomplete, so its owner is bounced back to the setup step once** on next launch.
`EnterSetupFlow` prefills username, tag and email from the draft so the interruption is one field rather
than a form, and so a returning player cannot mistake "confirm the name you have" for "pick a new one"
and silently overwrite the identity their friends added them by. `PlayerProfileTests` pins that bounce as
a decision, including the assertion that the age gate is not re-asked.

## Email is collected, not verified

`profile_email` is a string a player typed. Nothing has proved the mailbox exists and nothing has proved
they own it. `profile_email_verified` exists solely so that a later proof has somewhere to land without
another schema change, and this build leaves it `false` for every account.

This is not an omission that a careless reading could blame on the SDK. Everything available was checked,
and none of it sends a message to a player:

- `IAuthenticationService` has **no** email member — no `UpdateEmailAsync`, no verification endpoint, no
  password reset. `SignUpWithUsernamePasswordAsync(username, password)` is the whole credential surface,
  which is why the address cannot ride along with the account it belongs to.
- The only `email` fields in the package are `IdToken.Email` / `EmailVerified` claims under
  *Player Accounts* — the Unity ID browser flow. Those are verified **by Unity**, and they are the
  cheapest possible route to a real address. But `BrowserUtils.CreateBrowserUtils`
  (`Player Accounts/Runtime/Utilities/Browser/BrowserUtils.cs:10`) compiles to `return null` on any
  platform that is not Editor/Standalone/Android/iOS. **WebGL gets null**, and the standalone path needs
  an `HttpListener` on `localhost` plus `Application.OpenURL` — the same two walls that ended Google on
  itch.io. Unity ID therefore cannot be the *only* credential on the web build, for the identical reason.
- `GenerateSignInCodeAsync` / `SignInWithCodeAsync` / `ConfirmCodeAsync` look tantalisingly like an OTP
  API and are not one. Code-link pairs two **devices** (think "enter this code on the other screen"):
  `GenerateSignInCodeAsync` demands the caller be signed out, `ConfirmCodeAsync` demands
  `IsAuthorized` — an already-signed-in session confirms the code. It never touches an inbox, so it
  cannot bootstrap an account from something posted by email.

So a passwordless flow — address → code → address confirmed, no password — needs a sender we do not
have. The realistic shape is a Cloud Code endpoint (package `com.unity.services.cloudcode@2.10.4` is
installed but **entirely unused** in this project) calling an external mailer, with the OTP hashed
server-side and a short expiry. Until that exists, an emailed code cannot be delivered, and the
password stays the only credential that works.

**What the address may and may not be used for meanwhile.** It is safe as a contact handle: support
lookups, "we'll be in touch when purchases arrive", a human-readable record tied to a `playerId`. It is
**not** safe as identity, as a recovery path, or as anything a purchase decision keys off, because the
player controls the value and Cloud Save player data is client-writable. Two properties keep the
collection defensible today, and both break silently if someone "improves" them: the profile is read only
through `Data.Player.LoadAsync`, which is scoped to the owning `playerId`, so no other player can see
it; and nothing logs the address — `EmailPolicy` errors name the *offending character*, never the whole
value, which is why the messages are built from fragments rather than interpolating `email`.

Adding an address to `IsComplete()` means the game now collects personal data from every player at
scale. That was the point — microtransactions need a way to reach the buyer — but it is the reason the
verification debt above is recorded here rather than in a ticket nobody will read before the payments
work starts.

## Scene protection

`ProtectedSceneGuard` sits on a root object in MainMenu, Gameplay, Deckbuilder, Collection, Settings
and NetPresenceDemo. It is reactive: it subscribes to `AuthFlowController.StateChanged` and redirects
only on `SignedOut`, `Error` or `NeedsProfileSetup`. Judging the state itself in `Awake` would race the
profile fetch and bounce legitimately signed-in players back to the login screen, which is the bug this
shape avoids.

In normal play the flow controller has already routed the player and the guard does nothing. The case
it covers is opening a protected scene directly from the Editor. Its `requireLogin` toggle exempts a
scene, which is useful while the net demo scenes are still being iterated on.

## Password rules

Unity's username/password provider owns the policy, and it does not explain itself: a rejected password
comes back as a generic request failure with no rule attached. So the rules are written down once, in
`Net/PasswordPolicy.cs`, and used in three places — the register form shows them, `AccountManager` checks
them before sending, and `AuthResult` quotes them when the service disagrees with the local check.

The policy as transcribed from the SDK's own documentation
(`com.unity.services.authentication`, `SignUpWithUsernamePasswordAsync` remarks): **8–30 characters with at
least one uppercase letter, one lowercase letter, one digit and one special character.** Spaces are
rejected by name rather than being counted as a special character.

Two deliberate choices:

- **Every miss is reported at once.** A password short on length *and* symbols gets one sentence listing
  both. Returning on the first miss turns one correction into four submissions.
- **Only sign-up and password change validate.** Sign-in never re-checks: an account created before these
  rules existed would otherwise become unusable, and the service is the authority on what its own
  accounts hold.

`PasswordPolicyTests` pins a case per clause, including both length boundaries. When Unity changes the
policy text, `PasswordPolicy` is the only file to change with it — the server still refuses what it
refuses, so a stale copy here produces a wrong explanation, not an insecure account.

## Google sign-in

See `Net/GOOGLE_AUTH_SETUP.md` for provider configuration and `docs/GOOGLE-AUTH-SETUP.md` for the
Google Cloud Console walkthrough. Two things are easy to miss:

- the desktop client listens on the **fixed** loopback port `7777` with redirect
  `http://127.0.0.1:7777/callback/`, so that URI must be in the OAuth client's authorized list —
  a dynamic port guarantees `redirect_uri_mismatch`
- the client ID lives in `Assets/Resources/GoogleAuthSettings.asset`. Client IDs are public config;
  client secrets must never ship in the player

**Google is desktop-only right now.** `GoogleTokenProvider.Supported` is false in a WebGL build, so the
Auth screen never draws the button there and web play is username &amp; password. The reason is the rest
of this section: three of the four gates below are properties of itch.io's iframe rather than of the
code, Google is retiring the one opt-out that made the third survivable, and the failure mode is a
popup that authenticates the player and hands nothing back. A button that does that is worse than no
button. The bridge is **not** deleted — it still builds, its harness still runs, and the table below
still describes it — it is simply not offered. Re-enable by flipping `Supported` back on for WebGL and
re-reading every gate first.

Removing the button silently would trade one bug for another, because "where did Google go?" is a
support question with the same shape as "Google does nothing". So the button is *replaced*, not hidden:
`AuthScreenController.ApplyProviderAvailability` keeps exactly one of the Google button (plus its
"— or use your Overlink account —" divider) and a plain-language note on screen, and the note tells the
player to create an account here or download the desktop version for Google. No link in that copy —
itch.io's page has its own download control, and a URL that only means something on one host does not
belong in shipped text. The sentence, so nobody rewords it from memory:

> Google sign-in isn't part of the browser build. Play here with an Overlink account below, or download
> the desktop version to sign in with Google.

The wording is load-bearing in one place: "an Overlink account", not "create an account", because the
line is read just as often by a player who already has one and is hunting for the sign-in they expect.

Both states were verified in play mode against the runtime-built screen: desktop sign-in (button and
divider, 561px of stack), browser sign-in (note in place of both, shorter), and desktop register with the
password hint and an error line (597px, the tallest the screen can reach). `CardHeight` is 660 and the
comment there carries those numbers.

One consequence is worth naming because it is a support ticket waiting to happen: an account created by
Google has no username and password, so a player who signs in with Google on desktop, then opens the web
build and signs out, cannot get back into *that* account from the browser — their Google credential is
unusable there and there is no password to type. This is not a bug in the resume path, which carries a
cached session across fine and still reports `AccountKind.Google`; only a fresh sign-in is platform-bound.
If web play ever needs a real answer it is `AddUsernamePasswordAsync`, attaching credentials to an
existing Google account, not a revival of the bridge.

The two platforms reach that ID token by completely different routes, and only one of them runs inside
somebody else's page:

| | Windows (**shipped**) | WebGL (**built, not offered**) |
| --- | --- | --- |
| Transport | system browser + loopback `HttpListener` | `Assets/Plugins/WebGL/OverlinkGoogleAuth.jslib` |
| Grant | `response_type=id_token`, `form_post` to `127.0.0.1:7777` | Google Identity Services in-page |
| Button credential exchange | n/a — a real browser tab | legacy popup (`use_fedcm_for_button: false`) |
| State check | OAuth `state` + `nonce` | `nonce` only |
| Deadline | 2 min | 3 min |
| Can fail silently | no | no |
| Escape when the embed obstructs | none needed | **Sign in in a new tab** — unreachable, the overlay never opens |
| Offered to the player | yes | **no** — `Supported` is false |

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

That paragraph is the decision, in the end. Gates 1–3 are configuration and transport choices that were
each fixed in turn, and gate 4 — the handback — has never once produced a diagnostic naming its own
cause, because there is nothing on that path left for the bridge to hook. The web build therefore does
not offer Google at all: what a player gets instead is the one path that clears every gate, which is the
form already on the screen. Everything above is kept because it is the evidence for that call and the
starting point for whoever revisits it; `GoogleTokenProvider.Supported` is the single switch.

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

- **EditMode, `Assets/Tests/Net` (43 tests).** `GoogleDeclineMappingTests` pins all four
  `GoogleDecline` outcomes through `AuthResult.FromException`, including the two English substrings
  ("password", "timeout") that the generic path would otherwise win with. `PasswordPolicyTests` pins a
  clause per password rule, both length boundaries, and the two halves of the service's shared
  `InvalidParameters` code — a password rejection has to name the rules, a username rejection must not.
  `NonceVerificationTests` pins
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
