## 2026-10-20 — Verify v0.18 main-menu implementation plan
- [x] Read the applicable workflow instructions and inspect the current plan
- [x] Verify scene, package, assembly, API, and build-settings assumptions against the repository
- [x] Identify plan defects that would block OpenCode or Unity execution
- [x] Perform a final consistency check and report verified findings

**Outcome:** Plan verification completed; corrections remain pending because this was a read-only verification request.

## 2026-10-20 — Revise v0.18 main-menu implementation plan
- [x] Read the repair/edit workflow and inspect the current plan state
- [x] Resolve the assembly-definition contradiction and correct Unity API guidance
- [x] Make scene registration deterministic and validated without direct ProjectSettings edits
- [x] Complete menu navigation and back-navigation requirements for the enabled scene inventory
- [x] Add serialized-reference validation and stronger acceptance checks
- [x] Re-audit the revised plan and report the final status

**Outcome:** Revised English plan independently reviewed by Mneme; prior blockers resolved and remaining documentation issues corrected.

## 2026-10-21 — Commit and release Unity CI workflow as v0.18.1
- [x] Record the current repository state and identify the intended Unity CI workflow patch
- [x] Verify the workflow syntax/content and run applicable repository checks
- [x] Stage the intended changes and reconcile the commit diff (scope expanded to "all change" by Master)
- [x] Commit the patch, create tag `v0.18.1`, and push the commit and tag
- [x] Verify the remote branch and tag point to the expected commit
- [x] Create the GitHub release to trigger the release-build workflow
- [x] Confirm the workflow started and both build jobs reached the Unity build step

**Outcome:** Commit `2b6658d` pushed to `origin/master`; tag `v0.18.1` pushed and verified on the remote; GitHub release published at https://github.com/sleepymor/Overlink/releases/tag/v0.18.1. CI run 39531362808 started via the `release: published` trigger; both `Build StandaloneWindows64` and `Build WebGL` passed setup, checkout, cache, and license steps and were executing `Build with Unity` when this pass ended. Final build success is unverified; a follow-up check is scheduled at 02:30Z. Secret scan on staged docs/memory returned no hits; `GoogleAuthSettings.asset` has an empty `clientId`.

## 2026-10-21 - Make WebGL play in-browser on itch.io
- [x] Diagnose why the WebGL channel was a download instead of a playable game
- [x] Inspect the shipped WebGL artifact to confirm the real cause (not compression)
- [x] Add a content-root resolver so index.html lands at the channel root
- [x] Stop publishing WebGL as a zip asset; keep Windows as the only download
- [x] Commit and push the workflow fix (7140a00) and verify the remote head
- [x] Run CI and confirm both jobs succeed with the correct content root
- [x] Confirm butler pushed 18 files to the web channel (build processing)
- [ ] Confirm itch.io page visibility is public (blocked: page returns 404 publicly)
- [ ] Confirm the web channel is ticked "played in browser" (Master-side, UI only)

**Outcome:** Root cause was a nested index.html (build/WebGL/Overlink/index.html), not Brotli compression. Commit 7140a00 resolved the content root and pushed the flattened directory; run 39538577177 is fully green. Master set page kind to HTML. Remaining: the page is still 404 publicly, so browser play cannot be verified until visibility is public and the web channel is marked playable.

## 2026-10-22 — Stop offering Google sign-in on WebGL, and name the password rules
- [x] Confirm the itch.io iframe gates are structural, not a code defect (three gates + the silent handback)
- [x] Disable Google for the web build behind one switch (`GoogleTokenProvider.Supported`)
- [x] Keep the jslib bridge and its harness; mark it dormant rather than delete it
- [x] Hide the Auth screen's Google button and its divider where the provider is unavailable
- [x] Replace them with a note that says why, and points at the desktop download
- [x] Transcribe Unity's password policy into `Net/PasswordPolicy.cs` and check it before signup / password change
- [x] Make `AuthResult` quote the rules instead of "does not meet the requirements", on both the substring path and the shared 10002 code
- [x] Show the requirement line on the register form, next to the password field
- [x] Add `PasswordPolicyTests` (17 cases) and extend the Net test asmdef to reach the UGS error codes
- [x] Compile clean in the Editor; EditMode `Overlink.Net.Tests` 43/43; jslib harness 128/128
- [x] Verify the one EditMode failure outside the net layer is pre-existing by re-running with the diff stashed
- [ ] Commit, tag and cut the release (needs Master's go-ahead — publishing is an external side effect)

## 2026-10-22 — Email becomes part of the account (for future microtransactions)
- [x] Establish whether Unity can store an email — **it cannot** (no email member on
      `IAuthenticationService`; the only `email` fields are Unity-ID `IdToken` claims)
- [x] Decide placement with Master: the identity step of profile setup, which every account crosses,
      rather than the register form Google sign-ins never see
- [x] `Net/EmailPolicy.cs` — RFC 5321 caps and RFC 5322 `atext`, first-fault errors, no `Requirements`
      string (an address is guessable; a password is not)
- [x] `profile_email` + `profile_email_verified` in the profile, `profile_version` 1 → 2
- [x] `IsComplete()` requires an address → existing accounts are bounced through setup **once**
- [x] `EnterSetupFlow` prefills name/tag/email so the bounce is one field, not a blank form that
      invites a returning player to overwrite the identity their friends added them by
- [x] Editing an address revokes `emailVerified` (proof attaches to the old value otherwise)
- [x] Purpose line on the field: contact about account and future purchases, never shown to other players
- [x] `EmailPolicyTests` + `PlayerProfileTests`; EditMode `Overlink.Net.Tests` **90/90**; full EditMode
      **1061/1067**, the 2 failures being the pre-existing pair
- [ ] **Blocked, needs a decision:** passwordless email-OTP sign-in (Master's stated design) requires an
      outbound mail sender. Cloud Code is installed and unused; no Unity API sends mail. Code-link is
      device pairing, not email — `ConfirmCodeAsync` needs an already-authorized session.
- [ ] **Debt:** every address collected is self-reported. `profile_email_verified` is `false` for all of
      them until a sender exists. Nothing security-relevant may key off the field before then.

**Release state, checked while answering "is this releasable?":** `v0.18.6` is **already tagged and pushed
to origin** (annotated tag `469a4e4` → commit `b0a79cc`), and `b0a79cc` does **not** contain
`GoogleTokenProvider.Supported` — verified with `git show HEAD:...GoogleTokenProvider.cs`, which still has
the bare `#if UNITY_WEBGL` with no `return false`. So whatever itch.io is serving under v0.18.6 still shows
the Google button on web. All of today's work is uncommitted. Whether the GitHub Release actually published
(and therefore whether the workflow deployed) is **unknowable from here**: the repo is private and there is
no `gh` or token on this machine, so the releases API 404s and butler is not installed.

**Both players build.** WebGL → 7 scenes, `webgl-check.data.br` 4.3 MB / `.wasm.br` 7.4 MB, and the new
strings (`browser build`, `Overlink account below`, `Email address`, `profile_email`) are in the shipped
bundle after Brotli-decoding. Windows → exe + `_Data` (177 files), and `Overlink.UI.dll` /
`Overlink.Net.dll` carry the same strings plus `Continue with Google`, which is the desktop-keeps-Google
claim verified from the artifact rather than from the Editor. `Builds/` has zero tracked files
(`git ls-files Builds` was checked **before** deleting, per the earlier lesson) and was cleaned up.

**Not verified:** the WebGL build has never been *run* in a browser. Headless Edge renders the page but
never grants a WebGL context (blank canvas, only the `Overlink` label, 5 KB PNG, four attempts with
isolated profiles and swiftshader). That is a limitation of the harness, not evidence about the build —
but it does mean "the web build boots" is still an assumption.

**Outcome:** An email is now mandatory at signup, stored in Cloud Save next to the age gate, and named in
the profile schema as version 2. Unity cannot hold it — verified, not assumed — so the profile is the only
place it could go, and the setup step is the only screen every account passes through. The passwordless
OTP flow Master described is recorded as blocked rather than half-built: it needs a mail sender, and
shipping a "check your email" field that can never receive one would be worse than asking for a password.

**Outcome:** Google is desktop-only. The web build's Auth screen shows username &amp; password plus a line
saying where Google went and how to get it (download the desktop version), so no itch.io player can spend
three minutes waiting on a popup that cannot hand anything back, and none has to guess why the button
disappeared. The bridge is untouched and still tested; `Supported` is the single line that reverses this.
Separately, a password that
misses a rule is now told which rule — on the form, on submit, and in the service's own rejection. Known
consequence, documented in `docs/AUTH_FLOW.md`: a Google-created account has no password, so signing out
on the web build locks that account out of web play; the answer is `AddUsernamePasswordAsync`, not the
bridge.
