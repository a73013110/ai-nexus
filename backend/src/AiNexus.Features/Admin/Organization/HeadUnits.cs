using AiNexus.Data.App.Organization;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Organization;

/// <summary>
/// 首長單位（ADR 0024）：公文系統常把董事會、總經理與各處排在公司底下同一層，單位樹表達不了誰管誰。
/// 標了 <see cref="Unit.HeadRank"/> 的子單位，其主管管轄上層與上層底下「層級不比它高」的單位（含自己）及下層；數字越小層級越高。
/// 上層有首長單位時，主管由首長單位決定，上層自己的主管勾選不生效。管理、匯入與資料範圍都用這裡的判斷。
/// </summary>
internal static class HeadUnits
{
    /// <summary>有首長單位（不論是否啟用）的上層：自己的主管勾選不生效，匯入也不同步主管。首長單位停用時不改回成員勾選，失敗即拒絕。</summary>
    public static HashSet<Guid> LedUnits(IEnumerable<Unit> units)
        => [.. units.Where(x => x.HeadRank is not null && x.ParentId is not null).Select(x => x.ParentId!.Value)];

    /// <summary>
    /// 首長單位的結構規則，套在變更後的完整單位清單：要有上層、層級在範圍內、同一個上層內不重複，
    /// 首長單位底下不能再有首長單位（不連鎖：管轄只看首長單位自己勾選的主管）。
    /// </summary>
    public static Error? Check(IReadOnlyCollection<Unit> units)
    {
        var heads = units.Where(x => x.HeadRank is not null).ToList();
        if (heads.Any(x => x.ParentId is null || x.HeadRank is < 1 or > Unit.MaxHeadRank)) return OrganizationErrors.HeadRankInvalid;
        if (heads.GroupBy(x => (x.ParentId, x.HeadRank)).Any(x => x.Count() > 1)) return OrganizationErrors.HeadRankDuplicate;
        var ids = heads.Select(x => x.Id).ToHashSet();
        return heads.Any(x => ids.Contains(x.ParentId!.Value)) ? OrganizationErrors.HeadUnitNested : null;
    }

    /// <summary>
    /// 主管勾選的單位換算成實際管轄：<c>Managed</c> 是任主管的單位，首長單位再加上它的上層；<c>Tree</c> 再加上下層，
    /// 首長單位的上層只展開層級不比它高的子單位，例如總經理看不到與它同層、層級較高的董事會。
    /// 只算 <paramref name="active"/> 裡的單位；<paramref name="managerOf"/> 應已排除被首長單位帶領的上層。
    /// </summary>
    public static (HashSet<Guid> Managed, HashSet<Guid> Tree) Govern(IReadOnlyDictionary<Guid, Unit> active, IEnumerable<Guid> managerOf)
    {
        var children = active.Values.Where(x => x.ParentId is not null).ToLookup(x => x.ParentId!.Value);
        HashSet<Guid> managed = [], roots = [];
        foreach (var id in managerOf.Where(active.ContainsKey))
        {
            managed.Add(id);
            roots.Add(id);
            if (active[id] is not { HeadRank: { } rank, ParentId: { } parent } || !active.ContainsKey(parent)) continue;
            managed.Add(parent);
            roots.UnionWith(children[parent].Where(x => x.HeadRank is null || x.HeadRank >= rank).Select(x => x.Id));
        }
        var tree = Subtree(children, roots);
        tree.UnionWith(managed);
        return (managed, tree);
    }

    /// <summary>指定單位與其所有下層；<paramref name="children"/> 只含有效單位，所以停用的單位連同下層都不會出現。</summary>
    public static HashSet<Guid> Subtree(ILookup<Guid, Unit> children, IEnumerable<Guid> roots)
    {
        var result = new HashSet<Guid>();
        var pending = new Stack<Guid>(roots);
        while (pending.TryPop(out var id))
        {
            if (!result.Add(id)) continue;
            foreach (var child in children[id]) pending.Push(child.Id);
        }
        return result;
    }
}
