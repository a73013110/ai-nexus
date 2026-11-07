// 呼叫帳密驗證服務確認 AD 帳密（.NET 8 以上，不需要其他套件）。複製到專案後改 namespace 即可使用。
// 網址與金鑰放設定或秘密庫，不寫進程式碼；伺服器憑證保持預設驗證，不要略過。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace YourCompany.Authentication;

/// <summary>AD 驗證通過的使用者。以 <see cref="Sid"/> 對應自己的使用者：帳號改名也不變；<see cref="Email"/> 可能為 null。</summary>
public sealed record VerifiedUser(string Sid, string Account, string DisplayName, string? Email);

/// <summary>使用者造成的結果。金鑰無效、AD 連不上這類系統問題不在這裡，改丟 <see cref="CredentialVerificationException"/>。</summary>
public abstract record CredentialVerificationResult
{
    private CredentialVerificationResult() { }

    /// <summary>帳密正確。</summary>
    public sealed record Verified(VerifiedUser User) : CredentialVerificationResult;

    /// <summary>帳號或密碼錯，或帳號在 AD 已停用；畫面一律顯示「帳號或密碼不正確」。</summary>
    public sealed record Rejected : CredentialVerificationResult;

    /// <summary>同一帳號失敗太多次；過了 <see cref="RetryAfter"/> 再試。</summary>
    public sealed record RateLimited(TimeSpan? RetryAfter) : CredentialVerificationResult;
}

/// <summary>
/// 系統問題，不是密碼錯：401 是金鑰缺少、錯誤或已失效，503 是 AD 連不上。
/// 不要當成登入失敗累計次數；把 <see cref="IssueCode"/> 交給驗證服務的管理員查日誌。
/// </summary>
public sealed class CredentialVerificationException(HttpStatusCode status, string? issueCode)
    : Exception($"Credential verification failed with HTTP {(int)status}{(issueCode is null ? "" : $" (issue {issueCode})")}.")
{
    public HttpStatusCode Status { get; } = status;
    public string? IssueCode { get; } = issueCode;
}

/// <summary>
/// 以 typed HttpClient 註冊（AddHttpClient）：BaseAddress 是驗證服務網址（結尾要有 /），Authorization 帶 Bearer 金鑰。
/// 不保存狀態，可以當 singleton 共用。
/// </summary>
public sealed class CredentialVerificationClient(HttpClient http)
{
    public async Task<CredentialVerificationResult> VerifyAsync(string account, string password, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/credential-verifications", new { account, password }, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var user = await response.Content.ReadFromJsonAsync<VerifiedUser>(cancellationToken);
                return new CredentialVerificationResult.Verified(user ?? throw new CredentialVerificationException(response.StatusCode, null));
            case HttpStatusCode.UnprocessableEntity:
                return new CredentialVerificationResult.Rejected();
            case HttpStatusCode.TooManyRequests:
                return new CredentialVerificationResult.RateLimited(response.Headers.RetryAfter?.Delta);
            default:
                throw new CredentialVerificationException(response.StatusCode, await IssueCodeAsync(response, cancellationToken));
        }
    }

    // 錯誤回應是 problem+json，查證代碼在 issueCode；中間的代理或閘道回 HTML 時讀不到，就不帶代碼。
    private static async Task<string?> IssueCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return problem.RootElement.ValueKind == JsonValueKind.Object && problem.RootElement.TryGetProperty("issueCode", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
