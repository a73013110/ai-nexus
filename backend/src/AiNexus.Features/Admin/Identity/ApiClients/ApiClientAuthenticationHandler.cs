using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AiNexus.Data.App;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>
/// 以 <c>Authorization: Bearer</c> 的金鑰辨識外部系統：雜湊後以唯一索引查詢，一次查詢、不比對明碼。
/// 刪除或重新產生金鑰後下一個請求就失效，所以不快取。
/// </summary>
internal sealed class ApiClientAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Bearer = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var key = header[Bearer.Length..].Trim();
        // 格式不對的值不必雜湊、也不查資料庫；失敗原因只進框架的除錯日誌，回應一律相同。
        if (!ApiClientKeys.WellFormed(key)) return AuthenticateResult.Fail("Malformed API key.");
        var hash = ApiClientKeys.Hash(key);
        var client = await db.ApiClients.AsNoTracking().Where(x => x.KeyHash == hash).Select(x => new { x.Id, x.Name }).SingleOrDefaultAsync(Context.RequestAborted);
        if (client is null) return AuthenticateResult.Fail("Unknown API key.");
        // 刻意不帶 PrimarySid、Name 這類使用者 claim：CurrentUser 只認得使用者身分，外部系統不會被當成登入者建立帳號。
        var identity = new ClaimsIdentity(
            [new(ApiClientAccess.ClientIdClaim, client.Id.ToString("D", CultureInfo.InvariantCulture)), new(ApiClientAccess.ClientNameClaim, client.Name)],
            ApiClientAccess.Scheme, ApiClientAccess.ClientNameClaim, null);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ApiClientAccess.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return Problems.WriteAsync(Context, HttpErrors.AuthenticationRequired);
    }
}
