using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>要以 AD 資料更新的平台使用者（最多 100 位）。</summary>
public sealed record DirectorySyncRequest(IReadOnlyList<Guid> UserIds);

internal sealed class DirectorySyncRequestValidator : RequestValidator<DirectorySyncRequest>
{
    public DirectorySyncRequestValidator() => RuleFor(x => x.UserIds).NotNull().WithErrorCode("required")
        .Must(x => x is { Count: > 0 and <= DirectoryProvisioning.MaxBatch }).WithErrorCode("count")
        .Must(x => x is null || x.Distinct().Count() == x.Count).WithErrorCode("duplicate");
}

/// <summary>
/// 「從 AD 更新」（<c>identity.users.sync</c>）：以 SID 查目錄，套用與登入相同的同步規則（帳號、AD 帳號改名、姓名、目錄管理的信箱），每人一筆 <c>admin.user_sync</c> 稽核。
/// AD 找不到或已停用的人只回報、不停用平台帳號：目錄查詢可能只是暫時不完整（搜尋根、權限、DC 複寫），
/// 自動停用一旦誤判會一次鎖住大量使用者；AD 停用本來就擋下 AD 登入，是否停用平台帳號（含本地密碼）由管理員決定。
/// </summary>
internal sealed class SyncDirectoryUsers(AppDbContext db, IAdDirectory directory, DirectoryProvisioning provisioning)
{
    public const string Action = "admin.user_sync";

    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/directory-syncs", (DirectorySyncRequest body, ICurrentUser user, SyncDirectoryUsers handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectoryWriteRateLimit)
        .WithName("SyncDirectoryUsers");

    public async Task<Result<DirectoryOutcomesDto>> HandleAsync(Guid actor, DirectorySyncRequest request, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Where(x => request.UserIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var linked = users.Values.Where(x => x.DeletedAt is null && UserAccounts.DirectoryBound(x)).Select(x => x.Sid).ToArray();
        var people = (await directory.FindAsync(linked, ct)).ToDictionary(x => x.Sid, StringComparer.OrdinalIgnoreCase);
        var items = new List<DirectoryOutcomeDto>();
        // 不存在的 ID 直接略過：列表上的使用者只會軟刪除，查不到代表請求不是從列表來的。
        foreach (var id in request.UserIds)
        {
            if (!users.TryGetValue(id, out var user)) continue;
            if (user.DeletedAt is not null) items.Add(DirectoryProvisioning.Unmatched(user, DirectoryOutcomes.Deleted));
            else if (!UserAccounts.DirectoryBound(user)) items.Add(DirectoryProvisioning.Unmatched(user, DirectoryOutcomes.NotLinked));
            else if (!people.TryGetValue(user.Sid, out var person)) items.Add(DirectoryProvisioning.Unmatched(user, DirectoryOutcomes.NotFound));
            else items.Add(await provisioning.ApplyAsync(actor, FeatureCatalog.IdentityUsersSync, Action, person, includeDisabled: true, allowCreate: false, ct));
        }
        provisioning.Completed("sync", items);
        return new DirectoryOutcomesDto(items);
    }
}
