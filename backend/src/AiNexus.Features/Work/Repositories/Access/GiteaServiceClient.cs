using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>Gitea 上的一個組織儲存庫（目錄同步用）。</summary>
public sealed record GiteaRepository(long Id, string Owner, string Name, string? Description, bool IsPrivate, bool IsArchived);

/// <summary>
/// 以服務帳號呼叫 Gitea 的操作，只給儲存庫權限使用（ADR 0037）：讀目錄、讀與設定協作者。
/// 失敗一律丟 <see cref="ExternalServiceException"/>；設定與移除協作者是冪等的，可以重送。
/// </summary>
public interface IGiteaServiceClient
{
    /// <summary>權杖所屬的帳號（服務帳號）的登入名。</summary>
    Task<string> AccountAsync(CancellationToken ct);

    /// <summary>服務帳號所屬的組織（納管範圍）。</summary>
    Task<IReadOnlyList<string>> OrganizationsAsync(CancellationToken ct);

    Task<IReadOnlyList<GiteaRepository>> RepositoriesAsync(string organization, CancellationToken ct);

    /// <summary>可存取這個儲存庫的團隊名稱。</summary>
    Task<IReadOnlyList<string>> TeamsAsync(string owner, string name, CancellationToken ct);

    /// <summary>協作者的登入名（不含透過團隊或組織擁有者取得的存取）。</summary>
    Task<IReadOnlyList<string>> CollaboratorsAsync(string owner, string name, CancellationToken ct);

    /// <summary>使用者在這個儲存庫的有效權限（含團隊取得的）；只用來顯示未登記的協作者。</summary>
    Task<string> PermissionAsync(string owner, string name, string login, CancellationToken ct);

    /// <summary>加入或改權限；帳號不存在或已停用時丟 <c>gitea_account_missing</c>。</summary>
    Task SetCollaboratorAsync(string owner, string name, string login, string permission, CancellationToken ct);

    /// <summary>移除協作者；已經不是協作者時也算成功。</summary>
    Task RemoveCollaboratorAsync(string owner, string name, string login, CancellationToken ct);

    /// <summary>Gitea 是否有這個帳號；申請與授予前確認，避免核准後才發現無法加入。</summary>
    Task<bool> UserExistsAsync(string login, CancellationToken ct);
}

/// <summary>
/// 以服務帳號權杖（<see cref="GiteaOptions.ServiceToken"/>）呼叫 Gitea。路徑都在這裡組、每段都編碼，不接受外部傳入的路徑；
/// 回應內容與權杖不寫進錯誤或日誌。
/// </summary>
[Scoped<IGiteaServiceClient>]
public sealed class GiteaServiceClient(IHttpClientFactory clients, IOptions<GiteaOptions> options) : IGiteaServiceClient
{
    /// <summary>Gitea 預設的分頁上限（<c>MAX_RESPONSE_ITEMS</c>）；回傳少於這個數就是最後一頁。</summary>
    private const int PageSize = 50;
    private const int MaxPages = 200;

    public async Task<string> AccountAsync(CancellationToken ct)
    {
        using var user = await GetAsync("api/v1/user", ct);
        return GiteaJson.Text(user.RootElement, "login", 100);
    }

    public Task<IReadOnlyList<string>> OrganizationsAsync(CancellationToken ct)
        => PagesAsync("api/v1/user/orgs", x => GiteaJson.Text(x, "name", 100) is { Length: > 0 } name ? name : GiteaJson.Text(x, "username", 100), ct);

    public Task<IReadOnlyList<GiteaRepository>> RepositoriesAsync(string organization, CancellationToken ct)
        => PagesAsync($"api/v1/orgs/{Segment(organization)}/repos", x => new GiteaRepository(
            GiteaJson.Number(x, "id"), GiteaJson.Login(x, "owner"), GiteaJson.Text(x, "name", 100),
            GiteaJson.Text(x, "description", 500) is { Length: > 0 } description ? description : null,
            GiteaJson.Flag(x, "private"), GiteaJson.Flag(x, "archived")), ct);

    public async Task<IReadOnlyList<string>> TeamsAsync(string owner, string name, CancellationToken ct)
    {
        using var teams = await GetAsync($"{Repository(owner, name)}/teams", ct);
        return [.. GiteaJson.Items(teams.RootElement).Select(x => GiteaJson.Text(x, "name", 100)).Where(x => x.Length > 0)];
    }

    public Task<IReadOnlyList<string>> CollaboratorsAsync(string owner, string name, CancellationToken ct)
        => PagesAsync($"{Repository(owner, name)}/collaborators", x => GiteaJson.Text(x, "login", 100), ct);

    public async Task<string> PermissionAsync(string owner, string name, string login, CancellationToken ct)
    {
        using var permission = await GetAsync($"{Repository(owner, name)}/collaborators/{Segment(login)}/permission", ct);
        return GiteaJson.Text(permission.RootElement, "permission", 16);
    }

    public Task SetCollaboratorAsync(string owner, string name, string login, string permission, CancellationToken ct)
        => SendAsync(HttpMethod.Put, $"{Repository(owner, name)}/collaborators/{Segment(login)}", JsonContent.Create(new { permission }), ct);

    public Task RemoveCollaboratorAsync(string owner, string name, string login, CancellationToken ct)
        => SendAsync(HttpMethod.Delete, $"{Repository(owner, name)}/collaborators/{Segment(login)}", null, ct);

    public async Task<bool> UserExistsAsync(string login, CancellationToken ct)
    {
        try
        {
            using var user = await GetAsync($"api/v1/users/{Segment(login)}", ct);
            return true;
        }
        catch (ExternalServiceException error) when (error.Error == RepositoriesErrors.NotFound) { return false; }
    }

    private static string Repository(string owner, string name) => $"api/v1/repos/{Segment(owner)}/{Segment(name)}";

    private static string Segment(string value) => Uri.EscapeDataString(value);

    private async Task<IReadOnlyList<T>> PagesAsync<T>(string path, Func<JsonElement, T> read, CancellationToken ct)
    {
        var items = new List<T>();
        for (var page = 1; page <= MaxPages; page++)
        {
            using var document = await GetAsync($"{path}?page={page}&limit={PageSize}", ct);
            var rows = GiteaJson.Items(document.RootElement).ToList();
            items.AddRange(rows.Select(read));
            if (rows.Count < PageSize) break;
        }
        return items;
    }

    private Task<JsonDocument> GetAsync(string path, CancellationToken ct) => CallAsync(async timeout =>
    {
        using var response = await RequestAsync(HttpMethod.Get, path, null, timeout);
        return await BoundedHttpJson.ReadAsync(response, 2 * 1024 * 1024, timeout);
    }, ct);

    private Task SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct) => CallAsync(async timeout =>
    {
        using var response = await RequestAsync(method, path, content, timeout);
        return true;
    }, ct);

    private Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct) => ExternalHttp.CallAsync(
        TimeSpan.FromSeconds(options.Value.TimeoutSeconds),
        call,
        (failure, _) => throw new ExternalServiceException(failure == ExternalFailure.TimedOut ? RepositoriesErrors.Timeout : RepositoriesErrors.Unreachable),
        ct);

    private async Task<HttpResponseMessage> RequestAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.AccessEnabled) throw new ExternalServiceException(RepositoriesErrors.AccessDisabled);
        // 這裡的寫入（設定、移除協作者）都是冪等的，重送是安全的。
        var response = await ExternalHttp.SendAsync(clients.CreateClient(ControlledHttpClients.Tools), () =>
        {
            var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), path)) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("token", settings.ServiceToken);
            request.Headers.Accept.ParseAdd("application/json");
            return request;
        }, retries: 2, ct);
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        response.Dispose();
        throw new ExternalServiceException(status switch
        {
            HttpStatusCode.Unauthorized => RepositoriesErrors.ServiceUnauthorized,
            HttpStatusCode.Forbidden => RepositoriesErrors.ServiceForbidden,
            HttpStatusCode.NotFound => RepositoriesErrors.NotFound,
            // 設定協作者時 Gitea 對不存在或已停用的帳號回 422。
            HttpStatusCode.UnprocessableEntity => RepositoriesErrors.AccountMissing,
            _ => RepositoriesErrors.Unreachable,
        }, $"Gitea returned {(int)status}.");
    }
}
