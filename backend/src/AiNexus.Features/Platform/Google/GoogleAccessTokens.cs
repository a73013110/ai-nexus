using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Google;

/// <summary>
/// 以服務帳戶金鑰簽 JWT（RS256）向 Google 換發 OAuth 存取權杖（<see href="https://developers.google.com/identity/protocols/oauth2/service-account#httprest">JWT bearer flow</see>）。
/// 權杖依（範圍、代為操作的帳號）快取，到期前 <see cref="RefreshMargin"/> 才換新；同一組同時有多個呼叫要換新時只換一次，其他呼叫等它的結果。
/// 沒有用官方 SDK：它依賴 Newtonsoft 與反射序列化（ADR 0030），而這裡只需要簽一個 JWT。
/// </summary>
[Singleton]
public sealed partial class GoogleAccessTokens(GoogleServiceAccount account, IHttpClientFactory clients, IOptions<GoogleOptions> options, TimeProvider clock, ILogger<GoogleAccessTokens> logger)
{
    /// <summary>Google 簽發的權杖最長一小時；提早換新，避免請求送到 Google 時剛好過期。</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<(string Scope, string? Subject), Slot> slots = new();

    /// <summary>取得可用的存取權杖；金鑰未設定、讀不到或被 Google 拒絕時拋出 <see cref="GoogleAuthenticationException"/>，網路錯誤照常拋出。</summary>
    internal async ValueTask<string> GetAsync(string scope, string? subject, CancellationToken ct)
    {
        var slot = slots.GetOrAdd((scope, subject), static _ => new Slot());
        if (slot.Current is { } current && current.RefreshAt > clock.GetUtcNow()) return current.Value;
        await slot.Gate.WaitAsync(ct);
        try
        {
            // 等鎖期間可能已經有人換好了。
            if (slot.Current is { } renewed && renewed.RefreshAt > clock.GetUtcNow()) return renewed.Value;
            var token = await RequestAsync(scope, subject, ct);
            slot.Current = token;
            return token.Value;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary>API 回 401 時呼叫：丟掉這個權杖，下一次呼叫重新換發（只丟同一個，避免把別人剛換好的新權杖也丟掉）。</summary>
    internal void Invalidate(string scope, string? subject, string token)
    {
        if (slots.TryGetValue((scope, subject), out var slot) && slot.Current?.Value == token) slot.Current = null;
    }

    private async Task<Token> RequestAsync(string scope, string? subject, CancellationToken ct)
    {
        var key = account.Key();
        var issuedAt = clock.GetUtcNow();
        using var content = new FormUrlEncodedContent([
            new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new("assertion", Assertion(key, scope, subject, issuedAt)),
        ]);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        using var response = await clients.CreateClient(ControlledHttpClients.Tools).PostAsync(key.TokenUri, content, deadline.Token);
        if (!response.IsSuccessStatusCode)
        {
            var (message, reason) = await GoogleHttp.ErrorOfAsync(response, deadline.Token);
            // 400／401 是憑證或委派設定被拒（invalid_grant、unauthorized_client）；其他狀態碼是暫時性的，交給呼叫端當成無法連線。
            if ((int)response.StatusCode is 400 or 401)
            {
                LogTokenRejected(logger, reason ?? "unknown", (int)response.StatusCode);
                throw new GoogleAuthenticationException(GoogleStatus.CredentialRejected, GoogleHttp.Truncate(message ?? reason ?? "The token request was rejected."));
            }
            throw new HttpRequestException($"Google token endpoint returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(await GoogleHttp.ReadBoundedAsync(response, deadline.Token));
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var value) || value.GetString() is not { Length: > 0 } accessToken)
            throw new HttpRequestException("Google token response has no access_token.");
        var expiresIn = root.TryGetProperty("expires_in", out var seconds) && seconds.TryGetInt32(out var parsed) ? TimeSpan.FromSeconds(parsed) : Lifetime;
        return new(accessToken, issuedAt + expiresIn - RefreshMargin);
    }

    /// <summary>JWT：header 帶金鑰 ID，claims 依 Google 規定；以 <see cref="Utf8JsonWriter"/> 直接寫，不經反射序列化。</summary>
    internal static string Assertion(GoogleServiceAccountKey key, string scope, string? subject, DateTimeOffset issuedAt)
    {
        var header = Json(writer =>
        {
            writer.WriteString("alg", "RS256");
            writer.WriteString("typ", "JWT");
            if (key.KeyId.Length > 0) writer.WriteString("kid", key.KeyId);
        });
        var claims = Json(writer =>
        {
            writer.WriteString("iss", key.ClientEmail);
            writer.WriteString("scope", scope);
            writer.WriteString("aud", key.TokenUri.AbsoluteUri);
            writer.WriteNumber("iat", issuedAt.ToUnixTimeSeconds());
            writer.WriteNumber("exp", (issuedAt + Lifetime).ToUnixTimeSeconds());
            if (subject is not null) writer.WriteString("sub", subject);
        });
        var signingInput = $"{Base64Url.EncodeToString(header)}.{Base64Url.EncodeToString(claims)}";
        var signature = key.Signer.SignData(System.Text.Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private static byte[] Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    private sealed record Token(string Value, DateTimeOffset RefreshAt);

    private sealed class Slot
    {
        private Token? current;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Token? Current
        {
            get => Volatile.Read(ref current);
            set => Volatile.Write(ref current, value);
        }
    }

    [LoggerMessage(EventId = 3700, EventName = "google.token_rejected", Level = LogLevel.Warning, Message = "Google rejected the service account token request ({Reason}, HTTP {StatusCode}); check the key file and domain-wide delegation.")]
    private static partial void LogTokenRejected(ILogger logger, string reason, int statusCode);
}
