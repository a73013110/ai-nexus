using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Reviews;

namespace AiNexus.IntegrationTests.Support;

internal static class IdentityApi
{
    internal const string Password = "fixture long password 42!";

    internal static UserAccountRequest LocalAccount(string name = "tester", string[]? roles = null) =>
        new("手動使用者", true, false, true, null, name, roles ?? ["member"], Password);

    internal static HttpClient CookieClient(AppFactory f, System.Security.Claims.ClaimsPrincipal principal)
    {
        var options = f.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AuthEndpoints.CookieScheme);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, AuthEndpoints.CookieScheme);
        var client = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "AiNexus.Session=" + options.TicketDataFormat.Protect(ticket)); return client;
    }

    internal static async Task<Guid> CreateUser(HttpClient admin, UserAccountRequest request)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedUserDto>())!.Id;
    }

    /// <summary>直接把帳號設為停用（離職），略過管理端點：測試的是其他功能怎麼看待停用者，不是停用本身。</summary>
    internal static async Task DisableAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Enabled, false));
    }

    internal static async Task<AccountReviewsDto> ReviewsAsync(HttpClient admin) => (await admin.GetFromJsonAsync<AccountReviewsDto>("/api/v1/admin/account-reviews"))!;

    /// <summary>某位使用者的待辦代碼；沒有待辦時為空。</summary>
    internal static async Task<string[]> ReviewCodesAsync(HttpClient admin, Guid user)
        => [.. (await ReviewsAsync(admin)).Users.SingleOrDefault(x => x.Id == user)?.Items.Select(x => x.Code) ?? []];

    internal static async Task Csrf(HttpClient client)
    {
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", session.CsrfToken);
    }

    internal static async Task<MeDto> Login(HttpClient client, string account, string method = "ad", string? password = null)
    {
        await Csrf(client); (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest(account, password ?? (method == "local" ? Password : "fixture-password"), method))).EnsureSuccessStatusCode();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", me.CsrfToken); return me;
    }
}
