using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Overlink.Net
{
    /// <summary>
    /// Obtains a Google OpenID Connect ID token without placing a client secret in the game.
    /// Windows uses a loopback browser callback; WebGL delegates to the browser through the
    /// .jslib bridge in Plugins/WebGL.
    /// </summary>
    public static class GoogleTokenProvider
    {
        const string ReceiverName = "OverlinkGoogleAuthReceiver";
        static GoogleTokenReceiver receiver;

        public static Task<string> RequestIdTokenAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Google OAuth client ID is not configured.");

#if UNITY_WEBGL && !UNITY_EDITOR
            return RequestWebGlAsync(clientId);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return RequestWindowsAsync(clientId);
#else
            throw new PlatformNotSupportedException("Google sign-in is configured for Windows and WebGL only.");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void OverlinkGoogleSignIn(string clientId, string nonce, string receiverName, string callbackMethod);

        // The bridge is fire-and-forget, so nothing on this side can observe a GIS flow that never
        // answers: a popup the browser blocked, an overlay the player walked away from, a CDN that
        // served a CSP that killed the GIS script. Without a deadline the await below hangs forever,
        // which latches AccountManager.IsBusy true and wedges the Auth screen for the rest of the
        // session — username and password included. Three minutes covers a slow trip through the
        // account chooser; the Windows loopback path uses the same shape at two minutes.
        static readonly TimeSpan InteractionTimeout = TimeSpan.FromMinutes(3);

        static Task<string> RequestWebGlAsync(string clientId)
        {
            EnsureReceiver();
            var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            receiver.Begin(source);
            string nonce = Guid.NewGuid().ToString("N");
            OverlinkGoogleSignIn(clientId, nonce, ReceiverName, nameof(GoogleTokenReceiver.ReceiveToken));
            return ValidateNonceAsync(source.Task, nonce);
        }

        static async Task<string> ValidateNonceAsync(Task<string> tokenTask, string nonce)
        {
            Task winner = await Task.WhenAny(tokenTask, Task.Delay(InteractionTimeout));
            if (winner != tokenTask)
                throw Declined(GoogleDecline.TimedOut,
                    $"the bridge sent nothing within {(int)InteractionTimeout.TotalSeconds} seconds");
            string token = await tokenTask;
            if (!string.Equals(ReadJwtClaim(token), nonce, StringComparison.Ordinal))
                throw Declined(GoogleDecline.Failed, "Google returned an ID token with the wrong nonce");
            return token;
        }
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static async Task<string> RequestWindowsAsync(string clientId)
        {
            // Use a fixed port instead of dynamic to avoid redirect_uri_mismatch.
            // Google OAuth requires exact redirect URIs to be whitelisted.
            const int port = 7777;
            string redirect = $"http://127.0.0.1:{port}/callback/";
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(redirect);
                listener.Start();

                string state = Guid.NewGuid().ToString("N");
                string nonce = Guid.NewGuid().ToString("N");
                string url = "https://accounts.google.com/o/oauth2/v2/auth" +
                    $"?client_id={Uri.EscapeDataString(clientId)}" +
                    $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
                    "&response_type=id_token" +
                    "&response_mode=form_post" +
                    "&scope=openid%20email%20profile" +
                    $"&state={Uri.EscapeDataString(state)}" +
                    $"&nonce={Uri.EscapeDataString(nonce)}";
                Application.OpenURL(url);

                Task<HttpListenerContext> callbackTask = listener.GetContextAsync();
                Task completed = await Task.WhenAny(callbackTask, Task.Delay(TimeSpan.FromMinutes(2)));
                if (completed != callbackTask)
                    throw Declined(GoogleDecline.TimedOut, "the loopback callback never arrived");
                HttpListenerContext context = await callbackTask;
                Dictionary<string, string> values = ParseForm(await ReadBodyAsync(context.Request));
                string responseState = values.TryGetValue("state", out var returnedState) ? returnedState : "";
                string token = values.TryGetValue("id_token", out var returnedToken) ? returnedToken : "";

                WriteResponse(context.Response, string.IsNullOrEmpty(token)
                    ? "Google sign-in did not return a token. You can close this window."
                    : "Google sign-in complete. You can close this window and return to Overlink.");

                if (!string.Equals(state, responseState, StringComparison.Ordinal))
                    throw Declined(GoogleDecline.Failed, "Google returned a mismatched OAuth state");
                if (string.IsNullOrWhiteSpace(token))
                    throw Declined(GoogleDecline.Cancelled, "the loopback callback carried no ID token");
                if (!string.Equals(ReadJwtClaim(token), nonce, StringComparison.Ordinal))
                    throw Declined(GoogleDecline.Failed, "Google returned an ID token with the wrong nonce");
                return token;
            }
        }

        static async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            // NET-04: Bound request size, check method, and add a read timeout to prevent local stall/abuse.
            const int maxBodySize = 16384; // 16KB is more than enough for OAuth callback form data
            
            if (request.HttpMethod != "POST")
            {
                return string.Empty;
            }
            
            if (request.ContentLength64 > maxBodySize)
            {
                throw new InvalidOperationException($"OAuth callback body exceeds {maxBodySize} bytes.");
            }
            
            using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            {
                var readTask = reader.ReadToEndAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                var completed = await Task.WhenAny(readTask, timeoutTask);
                
                if (completed == timeoutTask)
                {
                    throw new TimeoutException("OAuth callback body read timed out.");
                }
                
                string body = await readTask;
                if (body.Length > maxBodySize)
                {
                    throw new InvalidOperationException($"OAuth callback body exceeds {maxBodySize} bytes.");
                }
                
                return body;
            }
        }

        static Dictionary<string, string> ParseForm(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in (body ?? "").Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                string[] parts = pair.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(parts[0].Replace("+", " "));
                string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace("+", " ")) : "";
                result[key] = value;
            }
            return result;
        }

        static void WriteResponse(HttpListenerResponse response, string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes($"<html><body><p>{message}</p></body></html>");
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            using (var stream = response.OutputStream) stream.Write(bytes, 0, bytes.Length);
        }
#endif

        static string ReadJwtClaim(string token)
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return "";
            string encoded = parts[1].Replace('-', '+').Replace('_', '/');
            while (encoded.Length % 4 != 0) encoded += "=";
            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonUtility.FromJson<JwtClaims>(json)?.nonce ?? "";
            }
            catch
            {
                return "";
            }
        }

        [Serializable]
        sealed class JwtClaims
        {
            public string nonce;
        }

        /// <summary>How a Google sign-in attempt ended without producing a usable ID token.</summary>
        public enum GoogleDecline
        {
            /// <summary>The player backed out of Google's own UI.</summary>
            Cancelled,

            /// <summary>Google's UI could not open in this browser context at all.</summary>
            Unavailable,

            /// <summary>Google's UI opened but returned nothing usable.</summary>
            Failed,

            /// <summary>Neither Google nor the player ever answered.</summary>
            TimedOut,
        }

        /// <summary>
        /// Thrown by this provider when the provider flow itself fails, on every platform.
        /// <para>
        /// A distinct type rather than a well-worded <see cref="InvalidOperationException"/> because
        /// <see cref="AuthResult.FromException"/> falls back to substring-matching English message text:
        /// a Google sentence containing "password" arrives as a weak-password error and one containing
        /// "timeout" arrives as a network error. Structuring the outcome keeps the sentence the player
        /// reads independent of how that sentence happens to scan.
        /// </para>
        /// </summary>
        public sealed class GoogleDeclineException : Exception
        {
            public GoogleDecline Decline { get; }

            public GoogleDeclineException(GoogleDecline decline, string detail)
                : base("Google sign-in declined (" + decline + "): " + detail)
            {
                Decline = decline;
            }
        }

        static GoogleDeclineException Declined(GoogleDecline decline, string detail)
        {
            return new GoogleDeclineException(decline, detail);
        }

        /// <summary>
        /// Translates the bridge's failure code into a <see cref="GoogleDecline"/>.
        /// <para>
        /// "fallback" is deliberately absent: the bridge emits it when One Tap gives way to the Google
        /// button, which is a handoff mid-flow rather than an outcome, and a later code always supersedes
        /// it. An unrecognised code lands on Cancelled, which is the one that costs the player least to
        /// read, so a bridge and a build that disagree about vocabulary still produce a sane message.
        /// </para>
        /// </summary>
        public static GoogleDecline DeclineFromCode(string code)
        {
            switch (code)
            {
                case "unavailable": return GoogleDecline.Unavailable;
                case "failed": return GoogleDecline.Failed;
                default: return GoogleDecline.Cancelled;
            }
        }

        static void EnsureReceiver()
        {
            if (receiver != null) return;
            var go = new GameObject(ReceiverName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            receiver = go.AddComponent<GoogleTokenReceiver>();
        }

        sealed class GoogleTokenReceiver : MonoBehaviour
        {
            TaskCompletionSource<string> pending;
            GoogleDecline decline = GoogleDecline.Cancelled;
            string detail = "";

            public void Begin(TaskCompletionSource<string> source)
            {
                pending = source;
                decline = GoogleDecline.Cancelled;
                detail = "";
            }

            /// <summary>
            /// Called by the jslib with "&lt;code&gt;|&lt;detail&gt;" immediately before it sends an empty
            /// token. Unity dispatches SendMessage calls in order, so the reason is always in hand by
            /// the time <see cref="ReceiveToken"/> runs. The code selects the player's sentence; the
            /// detail is GIS's own wording and exists only for the log.
            /// </summary>
            public void ReceiveDiagnostic(string payload)
            {
                if (string.IsNullOrEmpty(payload)) return;
                int separator = payload.IndexOf('|');
                string code = separator < 0 ? payload : payload.Substring(0, separator);
                detail = separator < 0 ? "" : payload.Substring(separator + 1);
                decline = DeclineFromCode(code);
                Debug.Log($"[Account] Google flow: {code}: {detail}");
            }

            public void ReceiveToken(string token)
            {
                var source = pending;
                pending = null;
                if (source == null) return;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    source.TrySetResult(token);
                    return;
                }
                source.TrySetException(Declined(decline, detail));
            }
        }
    }
}
