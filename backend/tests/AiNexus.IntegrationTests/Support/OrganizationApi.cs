using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Admin.Organization.Imports;
using AiNexus.Features.Platform.Account;

namespace AiNexus.IntegrationTests.Support;

/// <summary>組織管理 API 的測試捷徑。</summary>
internal static class OrganizationApi
{
    internal const string Units = "/api/v1/admin/organization-units";
    internal const string Import = Units + "/import";

    // 匯入的預覽與結果帶列舉，API 以字串輸出。
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    internal static async Task<UnitDto> CreateUnitAsync(HttpClient client, string name, Guid? parent = null, bool enabled = true)
    {
        var response = await client.PostAsJsonAsync(Units, new UnitCreateRequest(parent, name, enabled));
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<UnitDto>())!;
    }

    internal static async Task<UnitDto[]> ListUnitsAsync(HttpClient client) => (await client.GetFromJsonAsync<UnitDto[]>(Units))!;

    internal static Task<HttpResponseMessage> SaveMemberAsync(HttpClient client, Guid unit, Guid user, bool primary = false, bool manager = false)
        => client.PutAsJsonAsync($"{Units}/{unit}/members/{user}", new UnitMemberRequest(primary, manager));

    internal static async Task<UnitMemberDto[]> MembersAsync(HttpClient client, Guid unit) => (await client.GetFromJsonAsync<UnitMemberDto[]>($"{Units}/{unit}/members"))!;

    internal static async Task<UnitImportPreviewDto> PreviewImportAsync(HttpClient client)
    {
        var response = await client.GetAsync(Import);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<UnitImportPreviewDto>(Json))!;
    }

    internal static Task<HttpResponseMessage> ApplyImportAsync(HttpClient client, string fingerprint)
        => client.PostAsJsonAsync(Import, new UnitImportRequest(fingerprint));

    /// <summary>預覽後立即套用，回傳預覽與結果。</summary>
    internal static async Task<(UnitImportPreviewDto Preview, UnitImportResultDto Result)> ImportAsync(HttpClient client)
    {
        var preview = await PreviewImportAsync(client);
        var response = await ApplyImportAsync(client, preview.Fingerprint);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (preview, (await response.Content.ReadFromJsonAsync<UnitImportResultDto>(Json))!);
    }

    /// <summary>以該帳號登入一次（首次登入會建立使用者）並回傳使用者 id。</summary>
    internal static async Task<Guid> UserIdAsync(AppFactory factory, string account)
    {
        using var client = await factory.SignedInAsync(account);
        return (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
    }
}
