using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization;

/// <summary>使用者在一個有效單位的所屬。</summary>
public sealed record UnitMembership(Guid UnitId, bool IsPrimary, bool IsManager);

/// <summary>
/// 其他模組讀取組織的對外合約（員工服務的部門與主管、ADR 0017 的資料範圍）。只回答「現在」：
/// 有效單位＝自己與所有上層都啟用；停用的單位連同整棵下層都不算，帳號停用或刪除的成員也不算。
/// 每次呼叫整表讀取單位：部門數以百計，比遞迴 CTE 簡單，也讓「上層停用」的判斷只寫一次。
/// 也是 AccessControl 的 <see cref="IUnitMembership"/>：資料範圍的 <c>unit</c>、<c>unit-tree</c> 與主管範圍由這裡計算。
/// </summary>
public sealed class OrganizationDirectory(AppDbContext db, IActiveUsers activeUsers) : IUnitMembership
{
    /// <summary>使用者目前所屬的有效單位；帳號停用或刪除時回空清單。</summary>
    public async Task<IReadOnlyList<UnitMembership>> MembershipsAsync(Guid userId, CancellationToken ct)
    {
        var active = await ActiveUnitsAsync(ct);
        var rows = await ActiveMembers().AsNoTracking().Where(x => x.UserId == userId).ToListAsync(ct);
        return [.. rows.Where(x => active.ContainsKey(x.UnitId)).OrderBy(x => x.UnitId).Select(x => new UnitMembership(x.UnitId, x.IsPrimary, x.IsManager))];
    }

    /// <summary>指定單位與其所有下層中的有效單位（<c>unit-tree</c> 範圍用）；無效或不存在的單位不出現在結果。</summary>
    public async Task<IReadOnlySet<Guid>> SubtreeAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken ct)
    {
        var active = await ActiveUnitsAsync(ct);
        var children = active.Values.Where(x => x.ParentId is not null).ToLookup(x => x.ParentId!.Value, x => x.Id);
        var result = new HashSet<Guid>();
        var pending = new Stack<Guid>(unitIds.Where(active.ContainsKey));
        while (pending.TryPop(out var id))
        {
            if (!result.Add(id)) continue;
            foreach (var child in children[id]) pending.Push(child);
        }
        return result;
    }

    /// <summary>
    /// 指定單位目前的成員 ID（帳號啟用且未刪除），是可再組合的查詢：業務模組把它放進自己的 Where，
    /// 讓「範圍內的人」與業務資料在同一個 SQL 裡比對。單位 ID 應來自 <see cref="IDataScope"/>（已排除無效單位），這裡不再檢查。
    /// </summary>
    public IQueryable<Guid> MemberIds(IReadOnlyCollection<Guid> unitIds)
    {
        var units = unitIds.ToArray();
        return ActiveMembers().Where(x => units.Contains(x.UnitId)).Select(x => x.UserId);
    }

    /// <summary>主管＝組織管理的主管勾選（<see cref="UnitMember.IsManager"/>），是主管範圍唯一的來源，不另建主管群組（ADR 0017）。</summary>
    async Task<UnitsOfUser> IUnitMembership.UnitsOfAsync(Guid userId, CancellationToken ct)
    {
        var memberships = await MembershipsAsync(userId, ct);
        return new([.. memberships.Select(x => x.UnitId)], [.. memberships.Where(x => x.IsManager).Select(x => x.UnitId)]);
    }

    /// <summary>單位目前的有效成員（帳號啟用且未刪除）；單位無效時回空清單。</summary>
    public async Task<IReadOnlyList<Guid>> MembersAsync(Guid unitId, bool managersOnly, CancellationToken ct)
    {
        if (!(await ActiveUnitsAsync(ct)).ContainsKey(unitId)) return [];
        return await ActiveMembers().AsNoTracking()
            .Where(x => x.UnitId == unitId && (!managersOnly || x.IsManager))
            .OrderBy(x => x.UserId).Select(x => x.UserId).ToListAsync(ct);
    }

    /// <summary>單位目前是否有有效主管（帳號啟用且未刪除）；無效的單位一律回 false。帳號待辦據此判斷停用的主管是否留下空缺。</summary>
    internal async Task<IReadOnlyDictionary<Guid, Unit>> ActiveUnitsWithoutManagerAsync(CancellationToken ct)
    {
        var active = await ActiveUnitsAsync(ct);
        var managed = await ActiveMembers().Where(x => x.IsManager).Select(x => x.UnitId).Distinct().ToListAsync(ct);
        foreach (var unit in managed) active.Remove(unit);
        return active;
    }

    private async Task<Dictionary<Guid, Unit>> ActiveUnitsAsync(CancellationToken ct)
    {
        var units = await db.Set<Unit>().AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        return units.Values.Where(x => Active(units, x)).ToDictionary(x => x.Id);
    }

    /// <summary>自己與所有上層都啟用；壞資料走超過深度上限時也視為無效。</summary>
    private static bool Active(Dictionary<Guid, Unit> units, Unit unit)
    {
        var chain = UnitRules.SelfAndAncestors(units, unit.Id).ToList();
        return chain.Count <= UnitRules.MaxDepth && chain.All(x => x.Enabled);
    }

    /// <summary>帳號啟用且未刪除的成員列；「帳號停用或刪除不算」只在這裡判斷，條件來自 Identity 的 <see cref="IActiveUsers"/>。</summary>
    private IQueryable<UnitMember> ActiveMembers() => db.Set<UnitMember>().Where(x => activeUsers.Ids.Contains(x.UserId));
}
