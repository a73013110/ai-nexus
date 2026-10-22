using System.Net.Http.Json;
using AiNexus.Features.Admin.Identity.Provisioning;

namespace AiNexus.IntegrationTests.Support;

/// <summary>從 AD 匯入與更新使用者的 API；成功才回傳逐人的結果，失敗的狀態碼由測試自己斷言。</summary>
internal static class DirectoryApi
{
    internal const string Imports = "/api/v1/admin/directory-imports";
    internal const string Syncs = "/api/v1/admin/directory-syncs";

    internal static async Task<DirectoryPeopleDto> Search(HttpClient admin, string search)
        => (await admin.GetFromJsonAsync<DirectoryPeopleDto>($"/api/v1/admin/directory-people?search={Uri.EscapeDataString(search)}"))!;

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
