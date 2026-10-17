using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity.Authentication;

public sealed record AdIdentity(string Sid, string Account, string DisplayName);

/// <summary>目錄服務（AD）驗證介面。帳密錯誤回傳 <see cref="Error"/>；目錄無法連線或設定錯誤則拋出 <see cref="ExternalServiceException"/>。</summary>
public interface IAdAuthenticator
{
    Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct);
    Task VerifyServiceAsync(CancellationToken ct);
}
