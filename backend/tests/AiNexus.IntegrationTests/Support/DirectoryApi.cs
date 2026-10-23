using System.Net.Http.Json;
using AiNexus.Features.Admin.Identity.Provisioning;

namespace AiNexus.IntegrationTests.Support;

/// <summary>從 AD 匯入與更新使用者的 API；成功才回傳逐人的結果，失敗的狀態碼由測試自己斷言。</summary>
internal static class DirectoryApi
{
    internal const string Imports = "/api/v1/admin/directory-imports";
    internal const string Syncs = "/api/v1/admin/directory-syncs";

    internal const string Nodes = "/api/v1/admin/directory-nodes";
    internal const string People = "/api/v1/admin/directory-people";

    internal static async Task<DirectoryNodesDto> Children(HttpClient admin, string? parent = null)
        => (await admin.GetFromJsonAsync<DirectoryNodesDto>(parent is null ? Nodes : $"{Nodes}?parent={Uri.EscapeDataString(parent)}"))!;

    internal static async Task<DirectoryPeopleDto> List(HttpClient admin, string node, bool subtree = false)
        => (await admin.GetFromJsonAsync<DirectoryPeopleDto>($"{People}?node={Uri.EscapeDataString(node)}&subtree={(subtree ? "true" : "false")}"))!;

    internal static async Task<DirectoryPersonDetailDto> Detail(HttpClient admin, string sid)
        => (await admin.GetFromJsonAsync<DirectoryPersonDetailDto>($"{People}/{Uri.EscapeDataString(sid)}"))!;

    internal static Task<DirectoryOutcomeDto[]> Import(HttpClient admin, params string[] sids) => Import(admin, new DirectoryImportRequest(sids));

    internal static Task<DirectoryOutcomeDto[]> Import(HttpClient admin, DirectoryImportRequest request) => Post(admin, Imports, request);

    internal static Task<DirectoryOutcomeDto[]> Preview(HttpClient admin, DirectoryImportRequest request) => Post(admin, Imports + "/preview", request);

    internal static Task<DirectoryOutcomeDto[]> Sync(HttpClient admin, params Guid[] ids) => Post(admin, Syncs, new DirectorySyncRequest(ids));

    private static async Task<DirectoryOutcomeDto[]> Post<T>(HttpClient client, string path, T body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return [.. (await response.Content.ReadFromJsonAsync<DirectoryOutcomesDto>())!.Items];
    }
}
