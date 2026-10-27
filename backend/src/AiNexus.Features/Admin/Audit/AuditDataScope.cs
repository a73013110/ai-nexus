using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Admin.Organization;

namespace AiNexus.Features.Admin.Audit;

/// <summary>
/// 活動稽核與功能使用量的資料範圍（ADR 0017）：紀錄的操作者、擁有者或資料主體是範圍內的人才看得到。<c>own</c>＝自己，
/// <c>unit</c>／<c>unit-tree</c>＝範圍單位目前的成員（稽核紀錄沒有存當時的單位，所以人調走後，新單位的主管看得到他以前的紀錄，原單位的看不到）；
/// 沒有對應使用者的紀錄（系統動作、未驗證的登入失敗）只有 <c>all</c> 看得到。
/// </summary>
internal sealed class AuditDataScope(AppDbContext db, IDataScope scopes, OrganizationDirectory organization)
{
    /// <summary>範圍內的使用者 ID，是可組合進稽核查詢的子查詢；<c>all</c> 時為 null（不篩選），沒有範圍時是空集合。</summary>
    public async Task<IQueryable<Guid>?> PeopleAsync(Guid user, string feature, CancellationToken ct)
    {
        var scope = await scopes.ForAsync(user, feature, ct);
        if (scope.All) return null;
        var own = scope.Own;
        var members = organization.MemberIds(scope.UnitIds);
        return db.Users.Where(x => own && x.Id == user || members.Contains(x.Id)).Select(x => x.Id);
    }
}
