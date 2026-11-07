using AiNexus.Features.Admin.Identity.ApiClients;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiNexus.Features.Admin.Identity.Authentication;

[ValidatedInHandler("Empty or oversized credentials answer credentials_rejected like wrong ones, so callers cannot tell input format from wrong passwords.")]
public sealed record CredentialVerificationRequest(string Account, string Password);

/// <summary>
/// 通過 AD 驗證的帳號。<see cref="Sid"/> 是 AD 的 objectSid，帳號改名也不變，呼叫端應以它對應自己的使用者；
/// <see cref="Account"/> 是不含網域的 sAMAccountName，大小寫依目錄；<see cref="Email"/> 是目錄的 mail，未設定時為 null。
/// </summary>
public sealed record VerifiedCredentialDto(string Sid, string Account, string DisplayName, string? Email);

/// <summary>
/// 外部系統（公文系統等）以 AD 驗證使用者的帳密。只回答「這組帳密是誰」：不建立 AiNexus 帳號、不看 AiNexus 的帳號狀態與授權，
/// 登入後的授權由呼叫端決定。帳號的失敗計數與網站登入共用；每次呼叫都寫登入稽核，對象是呼叫的系統。
/// </summary>
internal sealed class VerifyCredentials(DirectoryCredentials directory, AuthenticationAudit audit, Issues issues) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder api) => api
        .MapPost("/credential-verifications", (CredentialVerificationRequest body, HttpContext http, VerifyCredentials handler, CancellationToken ct) =>
            handler.HandleAsync(http, body, ct))
        .RequireApiClient()
        .WithRequestBodyLimit(4096)
        .NotAudited("驗證結果由登入稽核記錄（identity.credentials.verified）")
        .WithName("VerifyCredentials");

    public async Task<Results<Ok<VerifiedCredentialDto>, ProblemHttpResult>> HandleAsync(HttpContext http, CredentialVerificationRequest body, CancellationToken ct)
    {
        var client = http.User.ApiClientId();
        var name = http.User.ApiClientName();
        Result<AdIdentity> identity;
        try
        {
            identity = body.Account is { Length: > 0 and <= 256 } && body.Password is { Length: > 0 and <= 1024 }
                ? await directory.VerifyAsync(http.Response, body.Account, body.Password, ct)
                : IdentityErrors.CredentialsRejected;
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            // AD 連不上、服務帳號未設定：回 503 與查證代碼，稽核記同一個代碼，呼叫端不可把它當成密碼錯誤。
            var problem = issues.Problem(error);
            await audit.WriteAsync(IdentityAudit.CredentialsVerified, null, body.Account, "ad", ct, problem.Code, problem.IssueCode, client, name);
            throw;
        }
        if (!identity.IsSuccess)
        {
            // 帳密錯誤、帳號停用、目錄缺 SID 一律同一個代碼，不透露是哪一種；被限流則照實回 429 與 Retry-After。
            var error = identity.Error.Kind == ErrorKind.RateLimited ? identity.Error : IdentityErrors.CredentialsRejected;
            var issue = issues.Report(error);
            await audit.WriteAsync(IdentityAudit.CredentialsVerified, null, body.Account, "ad", ct, error.Code, issue, client, name);
            return error.ToProblem(issue);
        }
        var verified = identity.Value;
        var account = UserAccounts.AccountName(verified.Account);
        await audit.WriteAsync(IdentityAudit.CredentialsVerified, null, account, "ad", ct, resource: client, client: name);
        return TypedResults.Ok(new VerifiedCredentialDto(verified.Sid, account, verified.DisplayName, verified.Email));
    }
}
