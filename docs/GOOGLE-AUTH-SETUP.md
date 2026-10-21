# Google Authentication Setup Guide

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

**For WebGL builds hosted on itch.io, also add:**
```
https://yourusername.itch.io/overlink
```

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

1. Build for WebGL
2. Upload to itch.io or test locally
3. Click **LOG IN WITH GOOGLE**
4. Google OAuth popup appears
5. Sign in and authorize
6. Popup closes, game continues signed in

---

## Common Issues

### Error: `unsupported_response_type`

**Cause:** Using a Desktop OAuth client instead of Web Application.

**Fix:** Create a new OAuth client with type **Web application** and update the Client ID.

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
Windows: Opens browser to Google OAuth (loopback http://127.0.0.1)
WebGL: JavaScript bridge to Google OAuth popup
    ↓
Google validates user, redirects with ID token
    ↓
Token validated (state + nonce check)
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
- [ ] All redirect URIs added to OAuth client
- [ ] OAuth consent screen configured with test users
- [ ] Client ID added to `GoogleAuthSettings.asset`
- [ ] Same Client ID entered in Unity Dashboard
- [ ] Windows build test: Browser opens, sign-in works, game receives token
- [ ] WebGL build test (if applicable): Popup works, token received
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
