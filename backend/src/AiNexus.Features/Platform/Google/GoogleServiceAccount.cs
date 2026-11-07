using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Google;

/// <summary>換發權杖或呼叫 API 前就知道會失敗：沒有設定金鑰、金鑰讀不到，或 Google 拒絕這組憑證。</summary>
internal sealed class GoogleAuthenticationException(GoogleStatus status, string message) : Exception(message)
{
    public GoogleStatus Status { get; } = status;
}

/// <summary>服務帳戶金鑰裡簽 JWT 需要的部分；<see cref="Signer"/> 由 <see cref="GoogleServiceAccount"/> 持有，整個程序共用。</summary>
internal sealed record GoogleServiceAccountKey(string ClientEmail, string KeyId, Uri TokenUri, RSA Signer);

/// <summary>
/// 讀取 <see cref="GoogleOptions.ServiceAccountKeyFile"/> 的服務帳戶金鑰。第一次用到才讀，成功後快取到程序結束；
/// 讀失敗不快取，管理員補上或修正金鑰檔後下一次呼叫就會生效，不必回收集區。
/// </summary>
[Singleton]
public sealed class GoogleServiceAccount(IOptions<GoogleOptions> options, IHostEnvironment environment) : IDisposable
{
    private readonly Lock gate = new();
    private GoogleServiceAccountKey? key;

    public bool Configured => options.Value.ServiceAccountKeyFile.Length > 0;

    /// <summary>服務帳戶的信箱，管理頁提示「日曆要分享給誰」用；沒有設定或讀不到時為 null。</summary>
    public string? ClientEmail
    {
        get
        {
            try
            {
                return Configured ? Key().ClientEmail : null;
            }
            catch (GoogleAuthenticationException)
            {
                return null;
            }
        }
    }

    /// <summary>啟動檢查用：讀不到或格式錯誤時拋出，訊息說明原因（不含金鑰內容）。</summary>
    public void Verify() => _ = Key();

    internal GoogleServiceAccountKey Key()
    {
        if (Volatile.Read(ref key) is { } loaded) return loaded;
        lock (gate)
        {
            return key ??= Load();
        }
    }

    private GoogleServiceAccountKey Load()
    {
        var setting = options.Value.ServiceAccountKeyFile;
        if (setting.Length == 0) throw new GoogleAuthenticationException(GoogleStatus.NotConfigured, "Google:ServiceAccountKeyFile is not set.");
        var path = Path.GetFullPath(setting, environment.ContentRootPath);
        byte[] json;
        try
        {
            json = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new GoogleAuthenticationException(GoogleStatus.NotConfigured, $"The service account key file cannot be read ({ex.GetType().Name}).");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (Text(root, "type") != "service_account")
                throw new GoogleAuthenticationException(GoogleStatus.NotConfigured, "The key file is not a service account key.");
            var email = Text(root, "client_email");
            var privateKey = Text(root, "private_key");
            var tokenUri = Text(root, "token_uri") ?? "https://oauth2.googleapis.com/token";
            if (email is null || privateKey is null || !Uri.TryCreate(tokenUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new GoogleAuthenticationException(GoogleStatus.NotConfigured, "The service account key file is missing client_email, private_key or a valid token_uri.");
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(privateKey);
            }
            catch (ArgumentException)
            {
                rsa.Dispose();
                throw new GoogleAuthenticationException(GoogleStatus.NotConfigured, "The private_key in the service account key file is not a valid PEM key.");
            }
            return new(email, Text(root, "private_key_id") ?? "", uri, rsa);
        }
        catch (JsonException)
        {
            throw new GoogleAuthenticationException(GoogleStatus.NotConfigured, "The service account key file is not valid JSON.");
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    public void Dispose() => key?.Signer.Dispose();
}
