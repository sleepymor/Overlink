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

1. In the Unity Dashboard, enable Google as an external provider for the project's
   Authentication service.
2. In Google Cloud Console, create an OAuth client ID and put the client ID (not a secret)
   in `Assets/Resources/GoogleAuthSettings.asset`.
3. For WebGL, use a Web application client and add the deployed game's exact origin to the
   authorized JavaScript origins. The WebGL bridge loads Google Identity Services from the
   browser and receives its OpenID Connect ID token.
4. For Windows, add the loopback redirect `http://127.0.0.1:7777/callback/` to the OAuth client's
   authorized redirect URIs. The game opens the system browser and listens on that **fixed** port
   using the OAuth `form_post` callback; a dynamic port guarantees `redirect_uri_mismatch`, which is
   why `GoogleTokenProvider` pins 7777 instead of probing for a free one.
5. Test on the real deployed WebGL origin; `localhost` and production origins are separate
   Google OAuth origins.

The Google client ID is committed in `Assets/Resources/GoogleAuthSettings.asset`. A client ID is
public configuration — it travels inside the player either way — but a client secret must never be
shipped, and none is used: the desktop flow is `response_type=id_token` against a web client with no
secret exchange.
