using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data.External;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Organization.Imports;

/// <summary>
/// 組織匯入的來源：GD 的部門表 <c>Sys_User_DEPT_BAS</c>，主管經 <see cref="GdAccounts.UsersOfAsync"/> 對到使用者。
/// 只讀用得到的欄位；登記桌 Email、單位類別、機關等目前沒有用途，不讀。名稱、上層等規則交給 Organization 的匯入計畫判斷，這裡原樣帶回。
/// </summary>
internal sealed class GdUnitSource(ExternalDatabase<GdDatabase> gd, GdAccounts accounts)
{
    private const string Sql = """
        SELECT [DEP_NO] AS [Key], [DEP_NAME] AS Name, [DEP_CHIEF] AS Chief, [DEP_ACHIEF] AS Deputies,
               [PARENT_NO] AS ParentKey, [OrderBy] AS SortOrder, [Disable] AS Disabled
        FROM [dbo].[Sys_User_DEPT_BAS]
        """;

    // DEP_ACHIEF 以「、」分隔；手動維護的資料也見過全形與半形逗號，一併接受。
    private static readonly char[] Separators = ['、', ',', '，'];

    /// <summary>
    /// 讀取全部部門，主管已對到使用者。GD 不可用時回 <see cref="OrganizationErrors.ImportNotConfigured"/>、
    /// <see cref="OrganizationErrors.ImportUnavailable"/> 或 <see cref="OrganizationErrors.ImportRejected"/>，不拋例外。
    /// </summary>
    public async Task<Result<IReadOnlyList<SourceUnit>>> ReadAsync(CancellationToken ct)
    {
        var read = await gd.ReadAsync<DepartmentRow>(Sql, null, ct);
        if (read.Rows is not { } rows) return ErrorOf(read.Failure);

        var departments = rows.Select(x => (Row: x, Ids: ManagerIds(x))).ToList();
        var matched = await accounts.UsersOfAsync(departments.SelectMany(x => x.Ids.Select(y => y.Id)), ct);
        if (matched.Rows is not { } matches) return ErrorOf(matched.Failure);
        var byId = matches.ToDictionary(x => x.GdId, StringComparer.OrdinalIgnoreCase);

        return Result<IReadOnlyList<SourceUnit>>.Ok([.. departments.Select(x => new SourceUnit(x.Row.Key, x.Row.Name, x.Row.ParentKey, x.Row.SortOrder, x.Row.Disabled,
            [.. x.Ids.Select(y => Manager(byId[y.Id], y.Deputy))]))]);
    }

    /// <summary>主管在前、副主管在後；空項與重複（不分大小寫，主管優先）略過。</summary>
    private static IReadOnlyList<(string Id, bool Deputy)> ManagerIds(DepartmentRow row)
    {
        var ids = new List<(string Id, bool Deputy)>();
        if (row.Chief?.Trim() is { Length: > 0 } chief) ids.Add((chief, false));
        foreach (var deputy in (row.Deputies ?? "").Split(Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (!ids.Any(x => string.Equals(x.Id, deputy, StringComparison.OrdinalIgnoreCase))) ids.Add((deputy, true));
        return ids;
    }

    private static SourceManager Manager(GdUserMatch match, bool deputy) => new(match.GdId, match.UserId, match.Issue switch
    {
        null => null,
        GdAccountIssue.NotInGd => SourceAccountIssue.AccountNotFound,
        GdAccountIssue.Disabled => SourceAccountIssue.AccountDisabled,
        GdAccountIssue.Ambiguous => SourceAccountIssue.UserAmbiguous,
        _ => SourceAccountIssue.UserNotFound,
    }, deputy);

    private static Error ErrorOf(ExternalFailure? failure) => failure switch
    {
        ExternalFailure.NotConfigured => OrganizationErrors.ImportNotConfigured,
        ExternalFailure.Rejected => OrganizationErrors.ImportRejected,
        _ => OrganizationErrors.ImportUnavailable,
    };

    private sealed class DepartmentRow
    {
        public int Key { get; init; }
        public string? Name { get; init; }
        public string? Chief { get; init; }
        public string? Deputies { get; init; }
        public int? ParentKey { get; init; }
        public int? SortOrder { get; init; }
        public bool Disabled { get; init; }
    }
}
