# Authentication setup

The flow itself — states, cached-session resume, profile setup, scene protection — is in
[`docs/AUTH_FLOW.md`](../../../docs/AUTH_FLOW.md). This file is only about the third-party
configuration the code cannot do for you.

## How this maps to UGS

The whole session layer runs on Unity Gaming Services (UGS), not a custom backend:

- `com.unity.services.authentication` (UGS Authentication) owns the player account,
  session token, and player ID.
- **Sign-in is mandatory.** The supported credentials are Google and a username/password pair;
  there is no guest mode. UGS Anonymous sign-in (`SignInAnonymouslyAsync`) is used for exactly one
  thing here — resuming a session the SDK left cached on disk — see *Resuming a cached session*
  in `docs/AUTH_FLOW.md`.
- **Google** is a UGS *external identity provider*: UGS stores and validates the identity,
  but it never hosts the Google login screen. The game obtains the Google ID token and hands
  it to `SignInWithGoogleAsync` / `LinkWithGoogleAsync`.
- `com.unity.services.relay` and `com.unity.services.lobby` run on UGS for matchmaking.

Google is the only part that needs third-party configuration; everything else is UGS-managed.

## Username/password provider

Enable it in the Unity Dashboard under Authentication → Providers. The client signs up through
`SignUpWithUsernamePasswordAsync`, so there is nothing to provision per account. Unity enforces its
own password policy server-side; a rejected password arrives as a `RequestFailedException` and is
translated to player-facing text by `AuthResult.FromException`.

## Google provider setup

> **Status: desktop only.** WebGL builds do not offer Google sign-in — `GoogleTokenProvider.Supported`
> returns false, so the Auth screen never draws the button and the itch.io build is username &amp;
> password. The reason and the evidence are in [`docs/AUTH_FLOW.md`](../../../docs/AUTH_FLOW.md) ▸ *Google
> sign-in*; the switch is one property. Everything below still describes the bridge, which remains built
> and covered by its harness, because turning Google back on for web is a flip of that property and not
> a rewrite.

1. In the Unity Dashboard, enable Google as an external provider for the project's
   Authentication service.
2. In Google Cloud Console, create an OAuth client ID and put the client ID (not a secret)
   in `Assets/Resources/GoogleAuthSettings.asset`.
3. For WebGL, use a Web application client and add the deployed game's exact origin to the
   **authorized JavaScript origins** — scheme and host only, never a path. On itch.io that is the host the
   game *iframe* is served from, not the page the player visits, and the bridge logs the real value as
   `[origin: …]` on every failure. Redirect URIs do nothing for WebGL: Google Identity Services returns an
   ID token to the page directly and never performs a redirect. The bridge tries One Tap first (with
   FedCM), then falls back to rendering Google's own button in a DOM overlay when One Tap can't display —
   and on itch.io it *can't*, because One Tap in a cross-origin iframe requires the embedder to delegate
   `publickey-credentials-get` and itch.io delegates nothing. Expect the button to be the working path
   there. Every outcome is reported as a coded diagnostic rather than a bare cancellation.
   The button carries its own trap: with FedCM enabled for it, Google completes the exchange by calling
   `navigator.credentials.get()` from inside the game's iframe, and a third-party frame is refused unless
   the embedder delegated that permission. The chooser therefore opens, sits on *Processing your request*,
   and closes itself with no token. `use_fedcm_for_button: false` keeps the button on the legacy
   transport, which finishes the exchange in its own top-level popup instead. The two FedCM flags are
   deliberately opposite; do not tidy them into agreement. When Google removes the opt-out, the overlay's
   **Sign in in a new tab** is the path, because a top-level tab has no embedder to negotiate with.
   See `docs/GOOGLE-AUTH-SETUP.md`.
4. For Windows, add the loopback redirect `http://127.0.0.1:7777/callback/` to the OAuth client's
   authorized redirect URIs. The game opens the system browser and listens on that **fixed** port
   using the OAuth `form_post` callback; a dynamic port guarantees `redirect_uri_mismatch`, which is
   why `GoogleTokenProvider` pins 7777 instead of probing for a free one.
5. Test on the real deployed WebGL origin; `localhost` and production origins are separate
   Google OAuth origins. Before reading any console, confirm the tab loaded the build you think it did:
   `node Tools/WebGL/check-deployed-build.mjs "<game page URL>"` prints the frame directory, the live
   FedCM flags and the diagnostics the *shipped* bridge contains, without a playtest. An embed that
   denies popups cannot do Google sign-in at all — the player needs
   the host's "open in new window" affordance, or the username and password provider.

The Google client ID is committed in `Assets/Resources/GoogleAuthSettings.asset`. A client ID is
public configuration — it travels inside the player either way — but a client secret must never be
shipped, and none is used: the desktop flow is `response_type=id_token` against a web client with no
secret exchange.
