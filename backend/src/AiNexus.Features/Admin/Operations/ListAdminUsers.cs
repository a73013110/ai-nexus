using AiNexus.Data.App;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Operations;

public sealed record AdminUserActivityDto(UsageTotalsDto Usage, int Conversations);

public sealed record AdminUserDto(Guid Id, string Account, string DisplayName, DateTimeOffset LastSeenAt, IReadOnlyList<string> RoleIds, AdminUserActivityDto? Activity = null, bool Enabled = true, UserAuthenticationDto? Authentication = null, AttachmentStorageDto? Storage = null);

public sealed record AdminUsersDto(IReadOnlyList<AdminUserDto> Users, int Total, int Offset);

/// <summary>未刪除使用者的列表，每頁 100 筆、依帳號排序，附角色、用量、對話數與儲存空間。查詢在 <c>operations.usp_ListAdminUsers</c>。</summary>
internal sealed class ListAdminUsers(ISqlDatabase<AppDbContext> sql, UsageReports reports, AttachmentQuota quota)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/users", async (string? search, int? offset, ListAdminUsers handler, CancellationToken ct) => (await handler.HandleAsync(search, offset ?? 0, ct)).ToHttpResult())
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
            var total = await results.ReadSingleAsync<int>();
            var rows = await results.ReadAsync<Row>();
            var roles = (await results.ReadAsync<RoleRow>()).ToLookup(x => x.UserId, x => x.RoleId);
            return new AdminUsersDto([.. rows.Select(x => x.ToDto([.. roles[x.Id]]))], total, offset);
        }, cancellationToken: ct);
    }

    /// <summary>procedure 第二個結果集的一列；欄位名稱與 procedure 的輸出一致，只在執行時對應（整合測試涵蓋）。</summary>
    private sealed record Row(Guid Id, string Account, string DisplayName, DateTimeOffset LastSeenAt, bool Enabled,
        bool AdEnabled, bool LocalEnabled, string? AdAccount, string? LocalAccount, bool HasLocalPassword,
        int Requests, int Completed, int Failed, int Cancelled, long InputTokens, long OutputTokens, int RequestsWithUsage,
        long TotalDurationMilliseconds, int TimedRequests, int Conversations,
        long UsedBytes, long LimitBytes, long RemainingBytes, long? PersonalLimitBytes, long? GroupLimitBytes, long DefaultLimitBytes, string LimitSource)
    {
        public AdminUserDto ToDto(IReadOnlyList<string> roleIds) => new(Id, Account, DisplayName, LastSeenAt, roleIds,
            new(new(Requests, Completed, Failed, Cancelled, InputTokens, OutputTokens, RequestsWithUsage, TotalDurationMilliseconds, TimedRequests), Conversations),
            Enabled,
            new(AdEnabled, LocalEnabled, AdAccount, LocalAccount, HasLocalPassword),
            new(UsedBytes, LimitBytes, RemainingBytes, PersonalLimitBytes, GroupLimitBytes, DefaultLimitBytes, LimitSource));
    }

    private sealed record RoleRow(Guid UserId, string RoleId);
}
