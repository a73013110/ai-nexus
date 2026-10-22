using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>AD 搜尋結果；<c>More</c> 表示符合的人超過上限，畫面請管理員縮小關鍵字，不提供翻頁。</summary>
public sealed record DirectoryPeopleDto(IReadOnlyList<DirectoryPersonDto> People, bool More);

/// <summary>
/// 匯入對話框的 AD 人員搜尋（<c>identity.users.import</c>）：最多 50 筆，附平台上的對應狀態。
/// 空白關鍵字直接回空清單、不查目錄；目錄資料在公司內本來就可讀，所以只限流、不寫稽核。
/// </summary>
internal sealed class SearchDirectoryPeople(IAdDirectory directory, DirectoryProvisioning provisioning)
{
    public const int Limit = 50;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/directory-people", (string? search, SearchDirectoryPeople handler, CancellationToken ct) => handler.HandleAsync(search, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectorySearchRateLimit)
        .WithName("SearchDirectoryPeople");

    public async Task<Result<DirectoryPeopleDto>> HandleAsync(string? search, CancellationToken ct)
    {
        var keyword = search?.Trim() ?? "";
        if (keyword.Length > 64 || keyword.Any(char.IsControl)) return IdentityErrors.InvalidSearch;
        if (keyword.Length == 0) return new DirectoryPeopleDto([], false);
        var found = await directory.SearchAsync(keyword, Limit, ct);
        return new DirectoryPeopleDto(await provisioning.DescribeAsync(found.People, ct), found.More);
    }
}
