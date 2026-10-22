# Google Authentication Setup Guide

> **Status: desktop only.** As of the 2026-10-22 change, WebGL builds do not offer Google sign-in at all
> (`GoogleTokenProvider.Supported` is false under `UNITY_WEBGL`), so the itch.io build shows only the
> username &amp; password form. Everything in this guide about *Authorized JavaScript origins*, the FedCM
> flags and the iframe gates is kept deliberately: it is the record of why that decision was made and the
> checklist for undoing it. Read [`AUTH_FLOW.md`](AUTH_FLOW.md) ▸ *Google sign-in* first.

This guide explains how to properly configure Google Sign-In for Overlink using Unity Gaming Services.
It covers the Google Cloud Console side only. Sign-in is mandatory and there is no guest mode; the
flow itself — states, cached-session resume, profile setup, scene protection — is in
[`AUTH_FLOW.md`](AUTH_FLOW.md).

## Prerequisites

- Unity project with Authentication package installed
- Google Cloud Console account
- Unity Gaming Services account with Authentication enabled

---

## Step 1: Configure Google in Unity Dashboard

1. Go to [Unity Dashboard](https://dashboard.unity3d.com/)
2. Select your project: **Overlink**
3. Navigate to **Authentication** → **Identity Providers**
4. Click **Add Provider** → Select **Google**
5. You'll need the OAuth **Web Application Client ID** from Step 2

---

## Step 2: Create Google OAuth Client (Web Application)

### Why Web Application?

The current code uses the Implicit Flow (`response_type=id_token`) which requires a **Web Application** client, not Desktop. This is because:
- Desktop clients don't support direct ID token responses
- The loopback OAuth flow (`http://127.0.0.1`) is designed for web clients
- Unity Authentication expects tokens from web-compatible flows

### Create the Client

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project or create a new one
3. Navigate to **APIs & Services** → **Credentials**
4. Click **Create Credentials** → **OAuth 2.0 Client ID**
5. Choose **Application type**: **Web application**
6. Name: `Overlink Windows/WebGL Client`

### Configure Authorized Redirect URIs

Add these redirect URIs (one per line):

```
http://127.0.0.1
http://127.0.0.1:7777
http://127.0.0.1:7777/callback/
http://localhost
http://localhost:7777
http://localhost:7777/callback/
```

**For WebGL builds the redirect-URI list above is irrelevant.** Google Identity Services does not use
the authorization-code flow at all — it hands the browser an ID token directly — so it never consults a
redirect URI. What it does check is the **origin of the page it is running in**, and that is a separate
field. Getting this wrong is the single most common reason WebGL Google sign-in fails, and it fails
*silently*: GIS reports the rejection to a callback the game used to throw away.

---

### Configure Authorized JavaScript Origins (required for WebGL)

In the same Web application client: **Credentials → your client → Authorized JavaScript origins → Add URI**.

Rules that are easy to get wrong:

- **Scheme + host only.** No path, no trailing slash, no query. `https://foo.itch.io/overlink` is not an
  origin; `https://foo.itch.io` is.
- Origin means *where the game's own page is served from*, which on itch.io is **not** the URL the player
  visits. Players visit your itch.io page; the game runs inside an iframe whose document comes from a
  different host. Register the iframe's host.
- Everything is exact, including the port. `https://a.itch.zone` does not cover `https://b.itch.zone`.

itch.io serves embedded HTML5 builds from its own CDN, **not** from the page the player is looking at.
The current hosts are:

```
https://html-classic.itch.zone
https://html.itch.zone
```

`html.itch.zone` is the secondary CDN used only when *SharedArrayBuffer support* is enabled in the
project's Embed options; `html-classic.itch.zone` is the default. The older
`https://v6p9d9t4.ssl.hwcdn.net` has been **permanently retired** — do not bother registering it.

Add the user's own page origin too, since a fullscreen "launch" embed and a directly-opened project page
resolve there rather than on the CDN:

```
https://<your-username>.itch.io
```

For local WebGL testing add the exact origin you serve the build folder from, e.g.
`http://localhost:8000`. A relative-path server and a `127.0.0.1` server are *different* origins.

**Don't guess the origin — read it.** Every Google-flow diagnostic the WebGL bridge emits ends with
`[origin: https://whatever]`, in the browser console and in Unity's log. Load the deployed build, open
DevTools (F12) → Console, click **Continue with Google**, and whatever GIS complains about, that suffix is
the exact string to paste into the origins list. If the game never logs anything, right-click the running
game → *View Frame Source* and take the scheme+host of that URL.

Confirmed from a live 403 on the deployed build, whose request headers carried
`origin: https://html-classic.itch.zone` and `referer: https://html-classic.itch.zone/`. Register that
string verbatim.

### Browsers that will resist anyway

The same header dump was `Brave/155`, `Chrome/155`, `Android 16`, `Pixel 10`, `sec-gpc: 1`. Four things
follow, and none of them is fixable from our side:

- **One Tap cannot work in this embed, on any device.** `sec-fetch-storage-access: none` in that request is
  the browser refusing the frame access to first-party cookies, which is what FedCM needs to identify a
  signed-in Google account. The refusal is structural to a cross-origin iframe on itch.io rather than an
  artefact of the phone, so the rendered Google button is the only path that can work here — which is
  what the bridge falls back to.
- **The button's own credential exchange is a separate hazard**, and it is the one that bites in a frame:
  with FedCM for the button, Google finishes the sign-in by calling `navigator.credentials.get()` from
  inside the game's iframe, which the embedder has not delegated. See *the Google popup opens, sits on
  Processing your request, and closes itself* below. The bridge opts out of that transport and offers a
  new tab when even the opt-out is refused.
- **Mobile adds a third risk on top.** The button opens the account chooser as a popup, and mobile and
  in-app browsers eat popups from inside small, gesture-starved frames far more readily than desktop does.
  Expect the button path to be flakier on phones even with the origin registered and FedCM out of the way.
- **Brave Shields can block Google Identity Services as a tracker**, independently of any OAuth
  configuration. It did *not* do so in the observed failure — the script loaded and made its own
  `/gsi/status` request, which is where the 403 came from — so treat this as a standing risk for other
  users rather than the cause here. If a Brave user reports Google sign-in doing nothing at all and no
  `[Account] Google flow:` line, that is the likely explanation, and the answer is Shields-down for the
  site or username and password.

So the practical ranking for web play is: desktop Chrome/Edge/Firefox with the origin registered, then
desktop Brave, then anything on a phone. Username and password is the only path immune to all of it — and
on the WebGL Auth screen it is currently the *secondary* option, beneath a prominent
**Continue with Google**. That ordering is right for Windows, where Google is reliable, and backwards for
an itch.io embed, where Google is the fragile path. Worth revisiting if web play matters: either demote
the button on WebGL builds, or leave it and expect support reports that begin "the Google button does
nothing".

Two caveats worth knowing before you rely on any of this:

- itch.io's own docs advise **against** pointing players at the CDN URL directly, because their sitelock
  can block it. Registering the CDN origin is what makes the *iframe* work; it is not an invitation to
  host the game from there.
- The CDN hostname is controlled by itch.io and has changed before (`hwcdn.net` → `itch.zone`). If Google
  sign-in breaks with no change on our side, suspect a host rotation and re-read the origin from the log.

7. Click **Create**
8. Copy the **Client ID** (looks like `123456789-abc.apps.googleusercontent.com`)

---

## Step 3: Configure OAuth Consent Screen

1. In Google Cloud Console, go to **APIs & Services** → **OAuth consent screen**
2. Choose **External** (unless you have Google Workspace)
3. Fill in required fields:
   - App name: `Overlink`
   - User support email: your email
   - Developer contact: your email
4. Add scopes:
   - `openid`
   - `email`
   - `profile`
5. Add test users (while in testing mode):
   - Add your own Google account email
   - Add any other testers' emails
6. **Save and Continue**

### Publishing the App

While in **Testing** mode, only test users can sign in. To make it public:
1. Go to **OAuth consent screen**
2. Click **Publish App**
3. Submit for verification (required for public apps)

For development/testing, keep it in Testing mode with test users added.

---

## Step 4: Update GoogleAuthSettings Asset

1. In Unity Editor, go to **Project** window
2. Navigate to `Assets/Resources/GoogleAuthSettings.asset`
3. Select the asset
4. In the **Inspector**, paste your **Web Application Client ID** into the `clientId` field
5. Save

**File path:** `Assets/Resources/GoogleAuthSettings.asset`

---

## Step 5: Enter Web Client ID in Unity Dashboard

1. Return to Unity Dashboard → **Authentication** → **Identity Providers**
2. Find the **Google** provider you added
3. Paste the same **Web Application Client ID** into the **Client ID** field
4. Click **Save**

---

## Step 6: Test the Flow

### For Windows Builds

1. Build and run the game (or play in Editor)
2. Click **LOG IN WITH GOOGLE**
3. Your default browser opens with Google sign-in
4. Sign in with a test account
5. Browser shows: "Google sign-in complete. You can close this window and return to Overlink."
6. Game shows: "Signed in with Google."

### For WebGL Builds

1. Build for WebGL and upload to itch.io (or serve the folder over HTTPS locally).
2. Add the serving origin to **Authorized JavaScript origins** *before* testing — see the origins section
   above. This is the step that decides whether the rest works.
3. Open the deployed page with DevTools focused on the **game iframe**, then click
   **CONTINUE WITH GOOGLE**.
4. Expect one of:
   - a white card over the game holding a real **Sign in with Google** button. This is the normal result
     inside an itch.io embed, not a degraded one: One Tap needs the embedder to delegate
     `publickey-credentials-get`, and itch.io does not. Click it and the account chooser opens as a popup.
   - the browser's native account picker (FedCM), which is what the *same build* shows when played from a
     first-party tab or itch.io's *open in new window*.
5. Sign in and authorize. The overlay disappears and the game continues signed in.
6. If the chooser opens and then closes itself on *Processing your request*, that is the credential
   exchange being refused by the frame rather than the origin being rejected — see Common Issues below.
   The overlay keeps the button on screen, explains itself in one line, and offers **Sign in in a new
   tab**; take that, since a top-level tab has no embedder to negotiate with.
7. If anything else fails, the console line `[Account] Google flow: <code>: <detail> [origin: ...]` names
   the reason and the exact origin to register. Nothing in this flow fails without saying why any more.
8. Username and password work regardless of all of the above, and are the fallback when an embed denies
   popups entirely.

**Test on desktop Chrome first.** The steps above assume DevTools, and a phone in Brave adds its own
refusals on top of the structural ones above. Verifying on desktop establishes whether the *origin* is
right before you start chasing a popup or a Shield that only exists on the device you happen to hold.

---

## Common Issues

### Error: `unsupported_response_type`

**Cause:** Using a Desktop OAuth client instead of Web Application.

**Fix:** Create a new OAuth client with type **Web application** and update the Client ID.

---

### Error: `Google sign-in was cancelled.` on itch.io / any embedded build

If the browser console shows **`[GSI_LOGGER]: The given origin is not allowed for the given client ID.`**
(with a 403 on `accounts.google.com/gsi/status` beside it), this is not a code bug and no fallback fixes
it: the origin the game is served from is missing from the OAuth client's **Authorized JavaScript
origins**. GIS checks that before it will draw *anything*, so One Tap and the button both die at the same
gate. Register the origin, exactly as the log reports it, and it works.

The bridge surfaces those rejections instead of hiding them. Look for
`[Account] Google flow: <code>: <detail> [origin: ...]`:

- `unavailable: ... not in the OAuth client's Authorized JavaScript origins ... ; GIS said: [GSI_LOGGER]
  ...` — the origin is wrong or absent. This fires only after the bridge waited five seconds and Google
  drew nothing, so a slow-but-successful render is never mistaken for a rejection. Copy the `[origin: …]`
  value into the Cloud Console.
- `unavailable: accounts.google.com/gsi/client failed to load` — the host page's Content-Security-Policy
  or COEP headers are blocking Google's script. No origin list will help; that is the embedder's policy,
  and the player needs username and password.
- `fallback: One Tap not displayed: opt_out_or_no_session` — normal on itch.io and *not* a failure. One Tap
  cannot draw its floating UI inside a third-party frame, so the bridge renders the button instead. The
  reason string means "no Google session in this frame", not "this player opted out".
- `fallback: FedCM availability in this frame: publickey-credentials-get=denied` — also informational, and
  the reason One Tap is structurally impossible here. `granted` means the embedder delegated it and the
  native picker is available.
- `failed: Google chooser error: <type> (<detail>)` — GIS's own `error_callback`, naming what the button
  flow hit: `popup_closed`, `popup_blocked`, `idp_timeout`, `issued_failed`, `gep_error`. This is
  deliberately **not** fatal: the card stays on screen with a one-line explanation and a **Sign in in a
  new tab** button, because a failed attempt is not a failed session and the button still works.
- `cancelled` — the player actually backed out, or took the new-tab exit. Nothing to fix.

**Fix:** Register the iframe's origin. Then, if the embed still denies the credential exchange, the
new-tab button is the path; if it denies popups entirely, treat web play as username/password-only.

---

### Error: the Google popup opens, sits on *Processing your request*, and closes itself

Distinct from the origin problem, and it only becomes visible once the origin is already registered —
which is why it looked like a popup glitch rather than an identity bug.

**Cause:** FedCM is enabled for the Sign in with Google button. Google's chooser completes the exchange
by calling `navigator.credentials.get()` from GIS's iframe *inside the game page*. In a third-party frame
that call is refused unless the embedder delegates `publickey-credentials-get` via the iframe's `allow`
attribute, and itch.io does not. The promise rejects, the chooser dismisses itself — having authenticated
the player and thrown the result away — and no callback fires.

**Fix:** none on the player's side, and it is already applied: the bridge sets
`use_fedcm_for_button: false`, so the legacy transport finishes the exchange in its own top-level popup
where none of that applies. `use_fedcm_for_prompt` stays `true` on purpose; the two flags are deliberately
opposite, and a tidy-up that equalises them reintroduces this bug. `Tools/WebGL/gsi-bridge-test.mjs`
asserts both.

**When it comes back:** Google is retiring that opt-out. When it goes, this becomes unfixable from JS on
itch.io and the **Sign in in a new tab** button in the overlay is the answer — a top-level tab is
first-party, so there is no embedder to refuse anything. Until then, the `FedCM availability in this
frame: publickey-credentials-get=…` line tells you which world a given build is running in.

**First, confirm the build you are looking at has the fix.** A browser console belongs to whichever build
the tab loaded, and a tab left open across a deploy keeps reporting the previous one — this exact symptom was
diagnosed twice from one stale page before anyone noticed the frame directory had changed. Check the deployed
bridge without playing the game:

```
node Tools/WebGL/check-deployed-build.mjs "https://sleepymor.itch.io/<game>?secret=<unlisted-secret>"
```

It prints the frame directory and `?v=` stamp the page resolves to, the live `use_fedcm_for_prompt` /
`use_fedcm_for_button` values, and which round's diagnostics the shipped bridge contains.

**To reproduce without a build:** `node Tools/WebGL/iframe-probe/serve.mjs`, then open `http://localhost:8001`.
That serves a two-origin page — an embedder on `:8001` framing a GIS test page on `:8000` — which is the same
shape as itch.io, and it prints GIS's raw verdicts (`isNotDisplayed`, `getNotDisplayedReason`, whether the
fallback button actually materialised an iframe, whether `window.open` is permitted) instead of translating
them into a player message. Switch the dropdown between *no allow=*, *delegated FedCM*, *FedCM disabled* and
*not an iframe at all* to see which of those the host is responsible for. Thirty seconds against a browser,
versus a CI cycle per guess.

---

### Error: `redirect_uri_mismatch`

**Cause:** The redirect URI used by the app isn't in your authorized list.

**Fix:** Add all the redirect URIs from Step 2 to your OAuth client configuration.

---

### Error: `invalid_client`

**Cause:** Client ID mismatch between Unity Dashboard and GoogleAuthSettings.

**Fix:** Ensure the same Client ID is in both:
- `Assets/Resources/GoogleAuthSettings.asset`
- Unity Dashboard → Authentication → Google provider

---

### Browser doesn't open on Windows

**Cause:** Firewall or antivirus blocking localhost connections.

**Fix:** 
1. Allow Unity through Windows Firewall
2. Try running as administrator
3. Check antivirus settings

---

## Security Notes

1. **Client ID is public** - It's safe to include in builds and version control
2. **Never commit Client Secret** - Web clients don't use secrets in this flow
3. **State and Nonce validation** - Already implemented in `GoogleTokenProvider.cs`
4. **HTTPS only in production** - Localhost HTTP is fine for desktop, use HTTPS for hosted WebGL

---

## Architecture

### Current Implementation

```
User clicks "LOG IN WITH GOOGLE"
    ↓
AccountManager.SignInWithGoogleAsync()
    ↓
GoogleTokenProvider.RequestIdTokenAsync(clientId)
    ↓
Windows: browser → Google OAuth → form_post redirect to the loopback listener
WebGL:     in-page Google Identity Services → FedCM One Tap, or a rendered Google button in a DOM
           overlay when One Tap can't display (a third-party iframe cannot host One Tap's UI)
    ↓
Google produces an OpenID Connect ID token
    ↓
Token validated (Windows: state + nonce · WebGL: nonce)
    ↓
AuthenticationService.SignInWithGoogleAsync(idToken)
    ↓
Unity Gaming Services validates token with Google
    ↓
User signed in with Unity Player ID + linked Google account
```

### Why This Approach?

- **Unity Authentication requires ID tokens** from external providers
- **Google doesn't allow direct token requests** without OAuth flow
- **Custom OAuth flow** handles Windows (loopback) and WebGL (popup) properly
- **Unity Gaming Services validates** the token server-side for security

---

## Alternative: Unity Player Accounts (Future)

Unity recommends using [Unity Player Accounts](https://docs.unity.com/en-us/authentication/unity-player-accounts) which provides "Sign in with Google" via Unity's web portal. This is simpler but less customizable.

**Pros:**
- No custom OAuth implementation needed
- Unity handles token retrieval
- Cross-platform by default

**Cons:**
- Requires Unity web portal UI
- Less control over branding
- Additional redirect through Unity's system

For now, we're using the custom OAuth flow for better control and native integration.

---

## Testing Checklist

- [ ] Google provider configured in Unity Dashboard
- [ ] Web Application OAuth client created in Google Cloud Console
- [ ] Redirect URIs added for the Windows loopback flow (`http://127.0.0.1:7777/callback/`)
- [ ] **Authorized JavaScript origins** added for every host the WebGL build can be served from
- [ ] OAuth consent screen configured with test users (or the app published) — a consent screen stuck in
      *Testing* refuses every Google account that isn't listed, whatever the origins say
- [ ] Client ID added to `GoogleAuthSettings.asset`
- [ ] Same Client ID entered in Unity Dashboard
- [ ] Windows build test: Browser opens, sign-in works, game receives token
- [ ] WebGL build test on the *deployed* page: One Tap or the fallback button appears, token received
- [ ] WebGL failure is diagnosable: `[Account] Google flow:` names a reason and an origin
- [ ] Google sign-in reaches MainMenu with the new profile's `Username#Tag`
- [ ] Already-linked account rejects second link attempt
- [ ] Sign out and sign back in works, and a restart skips login entirely

---

## Support

If you encounter issues not covered here:

1. Check Unity logs for error messages
2. Check browser console for OAuth errors
3. Verify all redirect URIs are correct
4. Ensure test users are added (if in Testing mode)
5. Try incognito/private browsing to rule out cached tokens

**Unity Authentication Docs:** https://docs.unity.com/en-us/authentication/platform-signin/google
