using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity.Authentication;

/// <summary>目錄驗證成功的身分；<paramref name="Email"/> 是 AD 的 <c>mail</c> 屬性，未設定時為 null。</summary>
public sealed record AdIdentity(string Sid, string Account, string DisplayName, string? Email = null);

/// <summary>目錄服務（AD）驗證介面。帳密錯誤回傳 <see cref="Error"/>；目錄無法連線或設定錯誤則拋出 <see cref="ExternalServiceException"/>。</summary>
public interface IAdAuthenticator
{
    Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct);
    Task VerifyServiceAsync(CancellationToken ct);
}
