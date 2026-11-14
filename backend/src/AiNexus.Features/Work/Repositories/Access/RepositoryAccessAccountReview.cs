using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Accounts;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>
/// 帳號已停用，但仍有有效的儲存庫授權（ADR 0019 不自動撤銷，ADR 0037）：復職即恢復，離職則由管理員撤銷。
/// Gitea 帳號由 HLAD 同步停用時已經登入不了，但個人權杖與 SSH 金鑰要等授權撤銷、協作者移除後才確定失效。
/// </summary>
internal sealed class RepositoryAccessAccountReview(AppDbContext db, IActiveUsers activeUsers) : IAccountReviewSource
{
    public const string Remaining = "repositories.access_remaining";

    public async Task<AccountReviewReading> ReadAsync(CancellationToken ct)
    {
        var active = activeUsers.Ids;
        var grants = await (from grant in db.RepositoryGrants.AsNoTracking()
                            join repository in db.ManagedRepositories on grant.RepositoryId equals repository.Id
                            where grant.EndedAt == null && grant.UserId != null && !active.Contains(grant.UserId.Value)
                            select new { UserId = grant.UserId!.Value, Name = repository.Owner + "/" + repository.Name }).ToListAsync(ct);
        return new([.. grants.GroupBy(x => x.UserId).Select(x => new AccountReviewItem(x.Key, Remaining, "帳號已停用，仍有儲存庫權限",
            string.Join("、", x.Select(y => y.Name).Order(StringComparer.OrdinalIgnoreCase)) + "。離職請在儲存庫權限頁撤銷。", FeatureCatalog.RepositoriesAccess))]);
    }
}
