using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("Overlink.PlayerManager.Editor.Tests")]

namespace Overlink.EditorTools
{
    /// <summary>Editor-only admin client, hard locked to Overlink Testing. No runtime auth reuse.</summary>
    public sealed class TestingPlayerManagerClient : IDisposable
    {
        public const string ProjectId = "467bcece-28d8-4091-9b3c-8138c7a02dca";
        public const string EnvironmentId = "ff807509-790a-4ab0-90aa-0b2a9aac70dc";
        public const string StateKey = "overlink_progression_v1";
        public const string KeyIdVariable = "OVERLINK_TESTING_SERVICE_KEY_ID";
        public const string SecretVariable = "OVERLINK_TESTING_SERVICE_SECRET";
        public static string AuditDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Overlink", "PlayerManager");

        readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        string token;
        DateTime refreshAt;

        public static bool HasCredentials => TryGetCredentials(out _, out _, out _);
        public static string CredentialSource
        {
            get
            {
                TryGetCredentials(out _, out _, out string source);
                return source;
            }
        }

        static bool TryGetCredentials(out string keyId, out string secret, out string source) =>
            ResolveCredentials((name, target) => Environment.GetEnvironmentVariable(name, target),
                Environment.OSVersion.Platform == PlatformID.Win32NT, out keyId, out secret, out source);

        // Select an entire pair from one source. Never combine unrelated process/user keys.
        internal static bool ResolveCredentials(Func<string, EnvironmentVariableTarget, string> read, bool windows,
            out string keyId, out string secret, out string source)
        {
            keyId = read(KeyIdVariable, EnvironmentVariableTarget.Process);
            secret = read(SecretVariable, EnvironmentVariableTarget.Process);
            if (!string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(secret))
            {
                source = "Process environment";
                return true;
            }
            if (windows)
            {
                keyId = read(KeyIdVariable, EnvironmentVariableTarget.User);
                secret = read(SecretVariable, EnvironmentVariableTarget.User);
                if (!string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(secret))
                {
                    source = "Windows user environment";
                    return true;
                }
            }
            keyId = secret = null;
            source = "Missing a complete credential pair";
            return false;
        }

        public void ResetAuthentication()
        {
            token = null;
            refreshAt = default;
        }

        static string ItemsUrl(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Enter a Player ID.");
            return "https://cloud-save.services.api.unity.com/v1/data/projects/" + ProjectId
                + "/players/" + Uri.EscapeDataString(playerId) + "/items";
        }

        async Task<string> GetTokenAsync(CancellationToken cancellation)
        {
            if (token != null && DateTime.UtcNow < refreshAt) return token;
            if (!TryGetCredentials(out string keyId, out string secret, out _))
                throw new InvalidOperationException("Testing credentials are missing. Set both variables in the same process environment or Windows user environment. See docs/PlayerManager.md.");
            string credentials = keyId + ":" + secret;
            using (var request = new HttpRequestMessage(HttpMethod.Post,
                "https://services.api.unity.com/auth/v1/token-exchange?projectId=" + ProjectId + "&environmentId=" + EnvironmentId))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                string response = await SendAsync(request, cancellation);
                token = (string)PlayerManagerState.ParseObject(response)["accessToken"];
                if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Unity token exchange returned no access token.");
                refreshAt = DateTime.UtcNow.AddMinutes(50); // Documented token lifetime is one hour.
                return token;
            }
        }

        async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            using (HttpResponseMessage response = await http.SendAsync(request, cancellation))
            {
                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    if (code == 401) { token = null; refreshAt = default; }
                    // Do not log response bodies, credentials, bearer tokens or player JSON.
                    throw new InvalidOperationException(code == 409
                        ? "Cloud Save conflict (409): the player changed since loading. Refresh and review again; nothing was force-written."
                        : "Unity request failed (HTTP " + code + "). Check service-account permissions (401/403), Player ID, or retry later (429/5xx). No automatic write retries are performed.");
                }
                return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<PlayerManagerState> LoadAsync(string playerId, CancellationToken cancellation)
        {
            string bearer = await GetTokenAsync(cancellation);
            using (var request = new HttpRequestMessage(HttpMethod.Get, ItemsUrl(playerId) + "?keys=" + StateKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                string response = await SendAsync(request, cancellation);
                cancellation.ThrowIfCancellationRequested();
                return PlayerManagerState.FromResponse(playerId, response);
            }
        }

        public async Task SaveAsync(PlayerManagerState state, int[] balances, string reason, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 512)
                throw new ArgumentException("A reason of 1–512 characters is required.");
            JObject body = state.BuildSaveRequest(balances);
            string bearer = await GetTokenAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            string operation = Guid.NewGuid().ToString("N");
            var audit = new JObject
            {
                ["operation"] = operation, ["projectId"] = ProjectId, ["environmentId"] = EnvironmentId,
                ["playerId"] = state.PlayerId, ["reason"] = reason.Trim(),
                ["before"] = new JObject(), ["after"] = new JObject()
            };
            for (int i = 0; i < balances.Length; i++)
            {
                audit["before"][PlayerManagerState.BalanceFields[i]] = state.Balance(i);
                audit["after"][PlayerManagerState.BalanceFields[i]] = balances[i];
            }
            AppendAudit(audit, "attempt"); // Abort before sending if local audit cannot be written.
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, ItemsUrl(state.PlayerId)))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    await SendAsync(request, cancellation);
                }
            }
            catch
            {
                // Transport failures can occur after a successful server write. Never assume failure.
                AppendAudit(audit, "not-confirmed-refresh-required");
                throw;
            }
            AppendAudit(audit, "server-confirmed");
        }

        static void AppendAudit(JObject audit, string outcome)
        {
            Directory.CreateDirectory(AuditDirectory);
            audit["utc"] = DateTime.UtcNow.ToString("O");
            audit["outcome"] = outcome;
            File.AppendAllText(Path.Combine(AuditDirectory, "testing-adjustments.jsonl"),
                audit.ToString(Formatting.None) + Environment.NewLine, Encoding.UTF8);
        }

        public void Dispose()
        {
            token = null;
            http.Dispose();
        }
    }
}
