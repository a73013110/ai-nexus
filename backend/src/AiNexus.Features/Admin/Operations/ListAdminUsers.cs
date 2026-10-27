using AiNexus.Data.App;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Operations;

public sealed record AdminUserActivityDto(UsageTotalsDto Usage, int Conversations);

/// <summary>
/// 使用者列與詳情共用；<paramref name="Activity"/> 只有列表回傳，詳情另有用量明細。
/// <paramref name="Position"/> 是在整份名單（使用者順序）中的位置，從 1 起算，只有列表回傳；<paramref name="SortOrder"/> 為 null 是未排序。
/// </summary>
public sealed record AdminUserDto(Guid Id, string Account, string DisplayName, DateTimeOffset LastSeenAt, IReadOnlyList<string> RoleIds, AdminUserActivityDto? Activity = null, bool Enabled = true, UserAuthenticationDto? Authentication = null, AttachmentStorageDto? Storage = null,
    string? Email = null, string? EmailSource = null, DateTimeOffset? EmailVerifiedAt = null, int? SortOrder = null, int? Position = null);

/// <summary><paramref name="Sorted"/> 是整份名單中已排序的人數（不受搜尋影響）。</summary>
public sealed record AdminUsersDto(IReadOnlyList<AdminUserDto> Users, int Total, int Offset, int Sorted);

/// <summary>
/// 未刪除使用者的列表，每頁 100 筆、依使用者順序（<see cref="AiNexus.Data.App.Identity.UserOrder"/>）排列，附通知信箱、角色、用量、對話數與儲存空間；
/// 搜尋涵蓋姓名、帳號與信箱。查詢在 <c>operations.usp_ListAdminUsers</c>。
/// </summary>
internal sealed class ListAdminUsers(ISqlDatabase<AppDbContext> sql, UsageReports reports, AttachmentQuota quota)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users", async (string? search, int? offset, ListAdminUsers handler, CancellationToken ct) => (await handler.HandleAsync(search, offset ?? 0, ct)).ToHttpResult())
        .Audited(IdentityAudit.UserRead)
        .WithName("ListAdminUsers");

    public async Task<Result<AdminUsersDto>> HandleAsync(string? search, int offset, CancellationToken ct)
    {
        if (search?.Length > 120 || offset < 0) return OperationsErrors.InvalidSearch;
        // 用量期間與預設儲存上限由程式決定（TimeProvider、設定），procedure 只負責查詢。
        var arguments = new
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Offset = offset,
            reports.Since,
            DefaultAttachmentLimitBytes = quota.DefaultLimitBytes,
        };
        return await sql.QueryProcedureAsync("operations.usp_ListAdminUsers", arguments, async results =>
        {
            var counts = await results.ReadSingleAsync<CountRow>();
            var rows = await results.ReadAsync<Row>();
            var roles = (await results.ReadAsync<RoleRow>()).ToLookup(x => x.UserId, x => x.RoleId);
            return new AdminUsersDto([.. rows.Select(x => x.ToDto([.. roles[x.Id]]))], counts.Total, offset, counts.Sorted);
        }, cancellationToken: ct);
    }

    /// <summary>procedure 第二個結果集的一列；欄位名稱與 procedure 的輸出一致，只在執行時對應（整合測試涵蓋）。</summary>
    private sealed record Row(Guid Id, string Account, string DisplayName, DateTimeOffset LastSeenAt, bool Enabled,
        bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, bool HasLocalPassword, bool DirectoryBound,
        string? Email, string? EmailSource, DateTimeOffset? EmailVerifiedAt, int? SortOrder, int Position,
        int Requests, int Completed, int Failed, int Cancelled, long InputTokens, long OutputTokens, int RequestsWithUsage,
        long TotalDurationMilliseconds, int TimedRequests, int Conversations,
        long UsedBytes, long LimitBytes, long RemainingBytes, long? PersonalLimitBytes, long? GroupLimitBytes, long DefaultLimitBytes, string LimitSource)
    {
        public AdminUserDto ToDto(IReadOnlyList<string> roleIds) => new(Id, Account, DisplayName, LastSeenAt, roleIds,
            new(new(Requests, Completed, Failed, Cancelled, InputTokens, OutputTokens, RequestsWithUsage, TotalDurationMilliseconds, TimedRequests), Conversations),
            Enabled,
            new(AdEnabled, LocalEnabled, AdAccount, LocalAccount, HasLocalPassword, DirectoryBound),
            new(UsedBytes, LimitBytes, RemainingBytes, PersonalLimitBytes, GroupLimitBytes, DefaultLimitBytes, LimitSource),
            Email, EmailSource, EmailVerifiedAt, SortOrder, Position);
    }

    private sealed record CountRow(int Total, int Sorted);

    private sealed record RoleRow(Guid UserId, string RoleId);
}
