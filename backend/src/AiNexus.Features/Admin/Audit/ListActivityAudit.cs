using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Audit;

/// <summary>
/// 一筆稽核紀錄。<c>Actor</c> 是實際操作者帳號，<c>ActingAs</c> 是代為操作時（測試身分）的擁有者帳號，
/// <c>Subject</c> 是資料主體（被查閱或被修改的使用者），<c>Feature</c> 是通過授權的功能。
/// </summary>
public sealed record AuditDto(long Id, string Actor, string Action, string Category, Guid? ResourceId, string Result, DateTimeOffset At, string? DetailsJson,
    string? ActingAs, string? Subject, string? Feature, string? ClientAddress, string? TraceId, Guid? OperationId, string? IssueCode);

/// <summary>
/// 符合條件的最新 25、50 或 100（預設）筆稽核紀錄，可用游標 <c>before</c> 往舊的讀。
/// 不回傳總筆數：游標分頁省掉大表 COUNT，期間新寫入的紀錄也不會讓翻頁重複。只列資料範圍內的紀錄（<see cref="AuditDataScope"/>）。
/// </summary>
internal sealed class ListActivityAudit(AppDbContext db, AuditDataScope scope)
{
    public static readonly int[] PageSizes = [25, 50, 100];

    public static void Map(RouteGroupBuilder audit) => audit
        .MapGet("", async ([AsParameters] ActivityAuditFilter filter, long? before, int? take, ICurrentUser user, ListActivityAudit handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, filter, before, take, ct)).ToHttpResult())
        .Audited(AuditAudit.EventRead)
        .WithName("ListAdminAudit");

    public async Task<Result<IReadOnlyList<AuditDto>>> HandleAsync(Guid user, ActivityAuditFilter filter, long? before, int? take, CancellationToken ct)
    {
        if (!filter.IsValid() || before is <= 0 || take is { } size && !PageSizes.Contains(size)) return AuditErrors.InvalidFilter;
        var rows = await filter.Query(db, await scope.PeopleAsync(user, FeatureCatalog.Audit, ct), before).Take(take ?? 100).ToListAsync(ct);
        return Result<IReadOnlyList<AuditDto>>.Ok(rows.Select(ActivityAuditFilter.Present).ToArray());
    }
}
