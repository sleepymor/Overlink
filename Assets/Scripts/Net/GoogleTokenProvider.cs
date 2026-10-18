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
            string token = await tokenTask;
            if (!string.Equals(ReadJwtClaim(token), nonce, StringComparison.Ordinal))
                throw new InvalidOperationException("Google sign-in nonce validation failed.");
            return token;
        }
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static async Task<string> RequestWindowsAsync(string clientId)
        {
            int port = FindFreePort();
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
                    throw new TimeoutException("Google sign-in timed out.");
                HttpListenerContext context = await callbackTask;
                Dictionary<string, string> values = ParseForm(await ReadBodyAsync(context.Request));
                string responseState = values.TryGetValue("state", out var returnedState) ? returnedState : "";
                string token = values.TryGetValue("id_token", out var returnedToken) ? returnedToken : "";

                WriteResponse(context.Response, string.IsNullOrEmpty(token)
                    ? "Google sign-in did not return a token. You can close this window."
                    : "Google sign-in complete. You can close this window and return to Overlink.");

                if (!string.Equals(state, responseState, StringComparison.Ordinal))
                    throw new InvalidOperationException("Google sign-in state validation failed.");
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("Google sign-in was cancelled or returned no ID token.");
                if (!string.Equals(ReadJwtClaim(token), nonce, StringComparison.Ordinal))
                    throw new InvalidOperationException("Google sign-in nonce validation failed.");
                return token;
            }
        }

        static int FindFreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        static async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                return await reader.ReadToEndAsync();
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

            public void Begin(TaskCompletionSource<string> source) => pending = source;

            public void ReceiveToken(string token)
            {
                var source = pending;
                pending = null;
                if (source == null) return;
                if (string.IsNullOrWhiteSpace(token))
                    source.TrySetException(new InvalidOperationException("Google sign-in was cancelled."));
                else
                    source.TrySetResult(token);
            }
        }
    }
}
