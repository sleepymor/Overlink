using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace OverlinkProgression;

public sealed class EmailVerificationModule
{
    private const string OtpKeyPrefix = "email_otp_";
    private const string OtpKey = "pending_code";
    private const string VerifiedKey = "overlink_email_verified_v1";
    private const string ResendKeySecret = "RESEND_KEY";
    private const string ResendFromSecret = "RESEND_FROM";
    private const string GoogleClientId = "368956467694-isuifa9vd7u9guimip7hr61uf5o83jdt.apps.googleusercontent.com";
    private const int CodeLifetimeSeconds = 600;
    private const int MaxAttemptsPerCode = 5;
    private const int MaxSendsPerWindow = 5;
    private const int SendWindowSeconds = 3600;

    private static readonly HttpClient Http = new();
    private static readonly Dictionary<string, RSA> GoogleKeys = new(StringComparer.Ordinal);
    private static readonly object GoogleKeysLock = new();

    [CloudCodeFunction("GetEmailVerified")]
    public async Task<EmailVerificationResponse> GetEmailVerified(IExecutionContext context, IGameApiClient api)
    {
        RequirePlayer(context);
        var result = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken!,
            context.ProjectId!, context.PlayerId!, new List<string> { VerifiedKey });
        var item = result.Data.Results.FirstOrDefault(entry => entry.Key == VerifiedKey);
        string json = item?.Value?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(json)) return new EmailVerificationResponse();

        VerifiedEmail? record = JsonSerializer.Deserialize<VerifiedEmail>(json);
        return new EmailVerificationResponse
        {
            Verified = !string.IsNullOrEmpty(record?.Email),
            Email = record?.Email ?? ""
        };
    }

    // Progression endpoints call this first. Only server code writes the protected verified record, so a
    // modified client cannot pass it by sending its own claim.
    internal static async Task RequireVerifiedAsync(IExecutionContext context, IGameApiClient api)
    {
        RequirePlayer(context);
        var result = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken!,
            context.ProjectId!, context.PlayerId!, new List<string> { VerifiedKey });
        var item = result.Data.Results.FirstOrDefault(entry => entry.Key == VerifiedKey);
        string json = item?.Value?.ToString() ?? "";
        VerifiedEmail? record = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<VerifiedEmail>(json);
        if (string.IsNullOrEmpty(record?.Email))
            throw new InvalidOperationException("Verify your email before continuing.");
    }

    [CloudCodeFunction("RequestEmailCode")]
    public async Task<EmailVerificationResponse> RequestEmailCode(IExecutionContext context, IGameApiClient api,
        string email)
    {
        RequirePlayer(context);
        string address = NormalizeEmail(email);
        long now = UnixNow();

        var (pending, writeLock) = await LoadPending(context, api, address);
        pending.SentAt.RemoveAll(sent => now - sent >= SendWindowSeconds);
        if (pending.SentAt.Count >= MaxSendsPerWindow)
            throw new InvalidOperationException("Too many codes were requested for this email. Try again later.");

        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000");
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var updated = new PendingCode
        {
            Email = address,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(CodeHash(salt, code)),
            ExpiresAt = now + CodeLifetimeSeconds,
            Attempts = 0,
            SentAt = pending.SentAt.Append(now).ToList()
        };
        await SavePending(context, api, address, updated, writeLock);
        await SendCode(context, api, address, code);

        return new EmailVerificationResponse { Verified = false, Email = address };
    }

    [CloudCodeFunction("VerifyEmailCode")]
    public async Task<EmailVerificationResponse> VerifyEmailCode(IExecutionContext context, IGameApiClient api,
        string email, string code)
    {
        RequirePlayer(context);
        string address = NormalizeEmail(email);
        long now = UnixNow();

        var (pending, writeLock) = await LoadPending(context, api, address);
        if (string.IsNullOrEmpty(pending.Hash) || now >= pending.ExpiresAt)
            throw new InvalidOperationException("That code has expired. Request a new one.");
        if (pending.Attempts >= MaxAttemptsPerCode)
            throw new InvalidOperationException("Too many wrong codes. Request a new one.");

        byte[] salt = Convert.FromBase64String(pending.Salt);
        byte[] expected = Convert.FromBase64String(pending.Hash);
        byte[] actual = CodeHash(salt, (code ?? "").Trim());
        bool match = CryptographicOperations.FixedTimeEquals(expected, actual);

        if (!match)
        {
            pending.Attempts++;
            await SavePending(context, api, address, pending, writeLock);
            int left = Math.Max(0, MaxAttemptsPerCode - pending.Attempts);
            throw new InvalidOperationException(left == 0
                ? "Too many wrong codes. Request a new one."
                : $"That code is not right. {left} attempt(s) left.");
        }

        await SaveVerified(context, api, address);
        pending.Hash = "";
        pending.Salt = "";
        pending.Attempts = 0;
        await SavePending(context, api, address, pending, writeLock);

        return new EmailVerificationResponse { Verified = true, Email = address };
    }

    private static async Task<(PendingCode Pending, string? WriteLock)> LoadPending(IExecutionContext context,
        IGameApiClient api, string address)
    {
        var result = await api.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken!,
            context.ProjectId!, OtpCustomId(address), new List<string> { OtpKey });
        var item = result.Data.Results.FirstOrDefault(entry => entry.Key == OtpKey);
        if (item == null) return (new PendingCode(), null);

        string json = item.Value?.ToString() ?? "";
        PendingCode? pending = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PendingCode>(json);
        return (pending ?? new PendingCode(), item.WriteLock);
    }

    private static async Task SavePending(IExecutionContext context, IGameApiClient api, string address,
        PendingCode pending, string? writeLock)
    {
        string value = JsonSerializer.Serialize(pending);
        var body = writeLock == null
            ? new SetItemBody(OtpKey, value)
            : new SetItemBody(OtpKey, value, writeLock);
        await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken!, context.ProjectId!,
            OtpCustomId(address), body);
    }

    private static async Task SendCode(IExecutionContext context, IGameApiClient api, string address, string code)
    {
        var apiKey = await api.SecretManager.GetSecret(context, ResendKeySecret);
        var from = await api.SecretManager.GetSecret(context, ResendFromSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Value);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            from = from.Value,
            to = new[] { address },
            subject = "Your Overlink verification code",
            text = $"Your Overlink verification code is {code}. It expires in 10 minutes. " +
                   "If you did not try to sign in, you can ignore this email."
        }), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            // Log the status only. The body and the key must not reach the logs.
            throw new InvalidOperationException($"Email delivery failed (HTTP {(int)response.StatusCode}).");
        }
    }

    private static async Task SaveVerified(IExecutionContext context, IGameApiClient api, string address)
    {
        var result = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken!,
            context.ProjectId!, context.PlayerId!, new List<string> { VerifiedKey });
        var item = result.Data.Results.FirstOrDefault(entry => entry.Key == VerifiedKey);
        string value = JsonSerializer.Serialize(new VerifiedEmail { Email = address });
        var body = item == null
            ? new SetItemBody(VerifiedKey, value)
            : new SetItemBody(VerifiedKey, value, item.WriteLock);
        await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken!, context.ProjectId!,
            context.PlayerId!, body);
    }

    private static byte[] CodeHash(byte[] salt, string code)
    {
        byte[] codeBytes = Encoding.UTF8.GetBytes(code);
        byte[] buffer = new byte[salt.Length + codeBytes.Length];
        Buffer.BlockCopy(salt, 0, buffer, 0, salt.Length);
        Buffer.BlockCopy(codeBytes, 0, buffer, salt.Length, codeBytes.Length);
        return SHA256.HashData(buffer);
    }

    private static string OtpCustomId(string address)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(address));
        return OtpKeyPrefix + Convert.ToHexString(digest, 0, 16).ToLowerInvariant();
    }

    private static string NormalizeEmail(string email)
    {
        string value = (email ?? "").Trim().ToLowerInvariant();
        if (value.Length < 3 || value.Length > 254 || !value.Contains('@') || value.Contains(' '))
            throw new InvalidOperationException("Enter a valid email address.");
        return value;
    }

    private static void RequirePlayer(IExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerId) || string.IsNullOrWhiteSpace(context.ServiceToken))
            throw new InvalidOperationException("An authenticated player is required.");
    }

    private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Google sign-in skips the code. The client sends the Google ID token, and the server checks
    // its signature and claims itself. The client-supplied email_verified value is never trusted.
    [CloudCodeFunction("VerifyGoogleEmail")]
    public async Task<EmailVerificationResponse> VerifyGoogleEmail(IExecutionContext context, IGameApiClient api,
        string idToken)
    {
        RequirePlayer(context);
        GoogleClaims claims = await ValidateGoogleToken(idToken ?? "");
        if (!claims.EmailVerified || string.IsNullOrEmpty(claims.Email))
            throw new InvalidOperationException("Google has not verified this email address.");

        string address = NormalizeEmail(claims.Email);
        await SaveVerified(context, api, address);
        return new EmailVerificationResponse { Verified = true, Email = address };
    }

    private static async Task<GoogleClaims> ValidateGoogleToken(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3) throw new InvalidOperationException("Google sign-in token is malformed.");

        JsonElement header = ParseJsonPart(parts[0]);
        string kid = header.TryGetProperty("kid", out JsonElement kidValue) ? kidValue.GetString() ?? "" : "";
        string alg = header.TryGetProperty("alg", out JsonElement algValue) ? algValue.GetString() ?? "" : "";
        if (alg != "RS256" || string.IsNullOrEmpty(kid))
            throw new InvalidOperationException("Google sign-in token uses an unsupported signature.");

        RSA key = await GoogleKey(kid)
            ?? throw new InvalidOperationException("Google signing key is unknown.");
        byte[] signed = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        byte[] signature = Base64UrlDecode(parts[2]);
        if (!key.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new InvalidOperationException("Google sign-in token signature is invalid.");

        JsonElement payload = ParseJsonPart(parts[1]);
        string issuer = payload.TryGetProperty("iss", out JsonElement iss) ? iss.GetString() ?? "" : "";
        if (issuer != "accounts.google.com" && issuer != "https://accounts.google.com")
            throw new InvalidOperationException("Google sign-in token has the wrong issuer.");

        if (!payload.TryGetProperty("aud", out JsonElement aud) || aud.GetString() != GoogleClientId)
            throw new InvalidOperationException("Google sign-in token is for a different app.");

        long exp = payload.TryGetProperty("exp", out JsonElement expValue) ? expValue.GetInt64() : 0;
        if (exp <= UnixNow()) throw new InvalidOperationException("Google sign-in token has expired.");

        return new GoogleClaims
        {
            Email = payload.TryGetProperty("email", out JsonElement email) ? email.GetString() ?? "" : "",
            EmailVerified = payload.TryGetProperty("email_verified", out JsonElement verified)
                && verified.ValueKind == JsonValueKind.True
        };
    }

    private static async Task<RSA?> GoogleKey(string kid)
    {
        lock (GoogleKeysLock)
        {
            if (GoogleKeys.TryGetValue(kid, out RSA? cached)) return cached;
        }

        using HttpResponseMessage response = await Http.GetAsync("https://www.googleapis.com/oauth2/v3/certs");
        response.EnsureSuccessStatusCode();
        using JsonDocument jwks = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        lock (GoogleKeysLock)
        {
            foreach (JsonElement jwk in jwks.RootElement.GetProperty("keys").EnumerateArray())
            {
                string keyId = jwk.GetProperty("kid").GetString() ?? "";
                var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = Base64UrlDecode(jwk.GetProperty("n").GetString() ?? ""),
                    Exponent = Base64UrlDecode(jwk.GetProperty("e").GetString() ?? "")
                });
                GoogleKeys[keyId] = rsa;
            }
            return GoogleKeys.TryGetValue(kid, out RSA? fetched) ? fetched : null;
        }
    }

    private static JsonElement ParseJsonPart(string part) =>
        JsonDocument.Parse(Base64UrlDecode(part)).RootElement.Clone();

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        while (padded.Length % 4 != 0) padded += "=";
        return Convert.FromBase64String(padded);
    }

}

public sealed class EmailVerificationResponse
{
    public bool Verified { get; set; }
    public string Email { get; set; } = "";
}

public sealed class PendingCode
{
    public string Email { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Hash { get; set; } = "";
    public long ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public List<long> SentAt { get; set; } = new();
}

public sealed class VerifiedEmail
{
    public string Email { get; set; } = "";
}

public sealed class GoogleClaims
{
    public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
}
