using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Support;

internal static class IdentityApi
{
    internal const string Password = "fixture long password 42!";

    internal static UserAccountRequest LocalAccount(string name = "tester", string[]? roles = null) =>
        new("手動使用者", true, false, true, null, name, roles ?? ["member"], Password);

    internal static HttpClient CookieClient(NexusFactory f, System.Security.Claims.ClaimsPrincipal principal)
    {
        var options = f.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AuthEndpoints.CookieScheme);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, AuthEndpoints.CookieScheme);
        var client = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "Nexus.Session=" + options.TicketDataFormat.Protect(ticket)); return client;
    }

    internal static async Task<Guid> CreateUser(HttpClient admin, UserAccountRequest request)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedUserDto>())!.Id;
    }

    internal static async Task Csrf(HttpClient client)
    {
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF"); client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
    }

    internal static async Task<MeDto> Login(HttpClient client, string account, string method = "ad")
    {
        await Csrf(client); (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest(account, method == "local" ? Password : "fixture-password", method))).EnsureSuccessStatusCode();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF"); client.DefaultRequestHeaders.Add("X-Nexus-CSRF", me.CsrfToken); return me;
    }
}
