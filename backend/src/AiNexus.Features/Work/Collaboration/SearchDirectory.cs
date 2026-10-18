using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Collaboration;

/// <summary>
/// 登入者分享資源時可指定的人員與已啟用群組。端點參數中的 <see cref="ICurrentUser"/> 不可省略：
/// 請求篩選靠它先解析並放行呼叫者，之後才會讀取資料。
/// </summary>
internal sealed class SearchDirectory(AppDbContext db)
{
    public static RouteHandlerBuilder MapUsers(RouteGroupBuilder api) => api
        .MapGet("/directory", async (string search, ICurrentUser user, SearchDirectory handler, CancellationToken ct) => TypedResults.Ok(await handler.UsersAsync(search, ct)))
        .WithName("SearchUserDirectory");

    public static RouteHandlerBuilder MapGroups(RouteGroupBuilder api) => api
        .MapGet("/directory/groups", async (ICurrentUser user, SearchDirectory handler, CancellationToken ct) => TypedResults.Ok(await handler.GroupsAsync(ct)))
        .WithName("ListDirectoryGroups");

    /// <summary>最多 20 筆。搜尋字需為 2 到 120 字，帳號或顯示名稱包含即符合；長度不符直接回傳空清單。</summary>
    public async Task<IReadOnlyList<DirectoryUserDto>> UsersAsync(string search, CancellationToken ct)
    {
        var term = search.Trim();
        if (term.Length is < 2 or > 120) return [];
        return await db.Users.AsNoTracking().Where(x => x.Account.Contains(term) || x.DisplayName.Contains(term)).OrderBy(x => x.Account).Take(20).Select(x => new DirectoryUserDto(x.Id, x.Account, x.DisplayName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DirectoryGroupDto>> GroupsAsync(CancellationToken ct)
        => await db.Set<RoleGroup>().AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Name).Select(x => new DirectoryGroupDto(x.Id, x.Name)).ToListAsync(ct);
}
