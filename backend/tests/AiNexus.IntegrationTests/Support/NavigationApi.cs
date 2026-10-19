using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>選單管理 API 的測試捷徑。允許清單預設是空的（不允許任何外部連結），測試外部連結時用 <see cref="AllowLinks"/> 開放測試主機。</summary>
internal static class NavigationApi
{
    internal const string Items = "/api/v1/admin/navigation-items";
    internal const string LinkHost = "intranet.example.com";

    internal static Action<IServiceCollection> AllowLinks(params string[] hosts)
        => services => services.PostConfigure<AccessControlOptions>(options => options.NavigationLinkHosts = hosts.Length == 0 ? [LinkHost] : hosts);

    internal static NavigationItemCreateRequest Group(string name, string? parent = null) => new("group", parent, null, name, null, true, null, null);
    internal static NavigationItemCreateRequest Page(string feature, string? parent = null) => new("page", parent, feature, null, null, true, null, null);
    internal static NavigationItemCreateRequest Link(string name, string url, string? parent = null, params string[] audiences) => new("link", parent, null, name, url, true, null, audiences);

    internal static async Task<NavigationItemDto> CreateAsync(HttpClient client, NavigationItemCreateRequest request)
    {
        var response = await client.PostAsJsonAsync(Items, request);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<NavigationItemDto>())!;
    }

    internal static async Task<NavigationItemDto[]> ListAsync(HttpClient client) => (await client.GetFromJsonAsync<NavigationItemDto[]>(Items))!;
}
