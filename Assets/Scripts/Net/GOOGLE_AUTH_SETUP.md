# Authentication setup

## How this maps to UGS

The whole session layer runs on Unity Gaming Services (UGS), not a custom backend:

- `com.unity.services.authentication` (UGS Authentication) owns the player account,
  session token, and player ID.
- **Guest** is UGS Anonymous sign-in (`SignInAnonymouslyAsync`).
- **Google** is a UGS *external identity provider*: UGS stores and validates the identity,
  but it never hosts the Google login screen. The game obtains the Google ID token and hands
  it to `SignInWithGoogleAsync` / `LinkWithGoogleAsync`.
- `com.unity.services.relay` and `com.unity.services.lobby` run on UGS for matchmaking.

Google is the only part that needs third-party configuration; everything else is UGS-managed.

## Google provider setup

1. In the Unity Dashboard, enable Google as an external provider for the project's
   Authentication service.
2. In Google Cloud Console, create an OAuth client ID and put the client ID (not a secret)
   in `Assets/Resources/GoogleAuthSettings.asset`.
3. For WebGL, use a Web application client and add the deployed game's exact origin to the
   authorized JavaScript origins. The WebGL bridge loads Google Identity Services from the
   browser and receives its OpenID Connect ID token.
4. For Windows, add the loopback redirect pattern used by the desktop client to the OAuth
   client configuration. The game opens the system browser and listens on a temporary
   `127.0.0.1` port using the OAuth `form_post` callback.
5. Test on the real deployed WebGL origin; `localhost` and production origins are separate
   Google OAuth origins.

The Google client ID is intentionally blank in the committed asset. OAuth client IDs are
public client configuration, but client secrets must never be shipped in the Unity player.
