using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Accounts;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 帳號停用的主管，且他任主管的有效單位已沒有其他有效主管（ADR 0019）：主管勾選保留不動（復職即恢復），但該單位的主管範圍出現空缺。
/// 從 GD 匯入的單位，主管由 GD 決定：GD 沒更新時，重新匯入會再勾回停用者（不生效，只是空缺仍在），所以提示到 GD 更正。
/// </summary>
internal sealed class ManagerAccountReview(AppDbContext db, OrganizationDirectory directory, IActiveUsers activeUsers) : IAccountReviewSource
{
    public const string Vacant = "organization.manager_vacant";

    public async Task<AccountReviewReading> ReadAsync(CancellationToken ct)
    {
        var active = activeUsers.Ids;
        var managers = await db.Set<UnitMember>().AsNoTracking().Where(x => x.IsManager && !active.Contains(x.UserId)).Select(x => new { x.UserId, x.UnitId }).ToListAsync(ct);
        if (managers.Count == 0) return new([]);
        var vacant = await directory.ActiveUnitsWithoutManagerAsync(ct);
        return new([.. managers.Where(x => vacant.ContainsKey(x.UnitId)).GroupBy(x => x.UserId).Select(x =>
        {
            var units = x.Select(y => vacant[y.UnitId]).OrderBy(y => y.SortOrder).ThenBy(y => y.Name, StringComparer.CurrentCulture).ToList();
            var detail = string.Join("、", units.Select(y => y.Name)) + (units.Any(y => y.GdDepNo is not null) ? "。公文系統匯入的單位請在 GD 更正主管。" : "。");
            return new AccountReviewItem(x.Key, Vacant, "帳號已停用，任主管的單位沒有其他有效主管", detail, FeatureCatalog.Organization);
        })]);
    }
}
