using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Organization.Imports;

/// <summary>
/// 從來源部門算出要對組織做的變更；預覽與套用共用，同樣的輸入一定得到同樣的預覽與 <see cref="Fingerprint"/>。
/// </summary>
/// <remarks>
/// 規則（docs/features/organization.md）：
/// <list type="bullet">
/// <item>以 <c>GdDepNo</c> 對應；還沒綁定的部門若在同一個上層有同名、未綁定的手建單位，就綁定它（唯一索引本來就不允許兩者並存，綁定才不會重複）。</item>
/// <item>已綁定單位的名稱、上層、啟用與順序以來源為準；GD 已沒有的部門停用、不刪除。手建單位不受影響。</item>
/// <item>名稱、成環、深度等問題不讓整批失敗：該列略過、照舊（已綁定的維持原樣，新的不建立，其下層若無處可掛也略過），並列入問題。
/// 略過會改變其他列的位置，所以反覆檢查到沒有新的略過為止；每輪至少多略過一列，一定會停。</item>
/// <item>主管以來源為準，只動 <c>IsManager</c>：新主管不是成員就加入（不設主要所屬），不再是主管的人保留成員資格。停用的單位不同步主管。</item>
/// </list>
/// </remarks>
internal sealed class UnitImportPlan
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<UnitTarget> units;
    private readonly IReadOnlyList<MemberTarget> members;
    private readonly IReadOnlyDictionary<Guid, Unit> loadedUnits;
    private readonly IReadOnlyList<UnitMember> loadedMembers;

    private UnitImportPlan(UnitImportPreviewDto preview, IReadOnlyList<UnitTarget> units, IReadOnlyList<MemberTarget> members,
        IReadOnlyDictionary<Guid, Unit> loadedUnits, IReadOnlyList<UnitMember> loadedMembers)
    {
        Preview = preview;
        this.units = units;
        this.members = members;
        this.loadedUnits = loadedUnits;
        this.loadedMembers = loadedMembers;
    }

    public UnitImportPreviewDto Preview { get; }

    public string Fingerprint => Preview.Fingerprint;

    /// <summary>讀本地單位、成員與相關使用者後算出計畫。<paramref name="tracking"/> 為 true 時載入的實體受追蹤，之後可 <see cref="ApplyAsync"/>。</summary>
    public static async Task<Result<UnitImportPlan>> CreateAsync(AppDbContext db, IReadOnlyList<SourceUnit> source, bool tracking, CancellationToken ct)
    {
        // GD 一個部門都沒有多半是連到錯的資料庫或權限被收走；照算會把所有已匯入的單位停用，所以拒絕。
        if (source.Count == 0) return OrganizationErrors.ImportEmpty;
        IQueryable<Unit> unitQuery = tracking ? db.Set<Unit>() : db.Set<Unit>().AsNoTracking();
        IQueryable<UnitMember> memberQuery = tracking ? db.Set<UnitMember>() : db.Set<UnitMember>().AsNoTracking();
        // 單位與所屬都是百筆等級（ADR 0017），整表讀進來比逐單位查簡單。
        var units = await unitQuery.ToListAsync(ct);
        var members = await memberQuery.ToListAsync(ct);
        var userIds = source.SelectMany(x => x.Managers).Where(x => x.UserId is not null).Select(x => x.UserId!.Value)
            .Concat(members.Where(x => x.IsManager).Select(x => x.UserId)).ToHashSet();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => (x.Account, x.DisplayName), ct);
        return Build(source, units, members, users);
    }

    /// <summary>
    /// 套用到 <see cref="CreateAsync"/>（tracking）載入的實體；呼叫端負責交易與最後的 <c>SaveChanges</c>。
    /// 改名或搬移的單位先換成暫用名稱寫入一次：同層兩個單位互換名稱這類變更，逐列更新時會在中途撞到同層名稱唯一索引。
    /// </summary>
    public async Task<UnitImportResultDto> ApplyAsync(AppDbContext db, CancellationToken ct)
    {
        var renamed = units.Where(x => !x.IsNew && (loadedUnits[x.Id].ParentId != x.ParentId || loadedUnits[x.Id].Name != x.Name)).ToList();
        if (renamed.Count > 0)
        {
            foreach (var target in renamed) loadedUnits[target.Id].Name = "~import~" + target.Id.ToString("N");
            await db.SaveChangesAsync(ct);
        }
        foreach (var target in units)
        {
            var unit = target.IsNew ? new Unit { Id = target.Id } : loadedUnits[target.Id];
            unit.ParentId = target.ParentId; unit.Name = target.Name; unit.Enabled = target.Enabled; unit.SortOrder = target.SortOrder; unit.GdDepNo = target.Key;
            if (target.IsNew) db.Add(unit);
        }
        foreach (var target in members)
        {
            var member = loadedMembers.SingleOrDefault(x => x.UnitId == target.UnitId && x.UserId == target.UserId);
            if (member is null) db.Add(new UnitMember { UnitId = target.UnitId, UserId = target.UserId, IsPrimary = false, IsManager = target.IsManager });
            else member.IsManager = target.IsManager;
        }
        var changes = Preview.Units.ToLookup(x => x.Action);
        return new(changes[UnitImportAction.Create].Count(), changes[UnitImportAction.Adopt].Count(), changes[UnitImportAction.Update].Count(),
            changes[UnitImportAction.Disable].Count(), Preview.Managers.Count(x => x.Added), Preview.Managers.Count(x => !x.Added), Preview.Issues.Count);
    }

    private static Result<UnitImportPlan> Build(IReadOnlyList<SourceUnit> source, IReadOnlyList<Unit> loaded, IReadOnlyList<UnitMember> members,
        IReadOnlyDictionary<Guid, (string Account, string DisplayName)> users)
    {
        var rows = source.DistinctBy(x => x.Key).ToDictionary(x => x.Key);
        var local = loaded.ToDictionary(x => x.Id);
        var bound = loaded.Where(x => x.GdDepNo is not null).ToDictionary(x => x.GdDepNo!.Value);
        var skipped = new Dictionary<int, UnitImportIssueCode>();
        var names = new Dictionary<int, string?>();
        foreach (var row in rows.Values)
        {
            var name = row.Name?.Trim();
            names[row.Key] = string.IsNullOrEmpty(name) ? null : name;
            if (string.IsNullOrEmpty(name)) skipped[row.Key] = UnitImportIssueCode.NameMissing;
            else if (name.Length > Unit.NameMaxLength || name.Any(char.IsControl)) skipped[row.Key] = UnitImportIssueCode.NameInvalid;
        }

        // 0、null 或不存在的上層都是最上層；指向自己算成環。
        int? ParentKey(SourceUnit row) => row.ParentKey is { } key && key != 0 && rows.ContainsKey(key) ? key : null;
        foreach (var row in rows.Values)
        {
            var steps = 0;
            for (var at = ParentKey(row); at is { } key && steps++ <= rows.Count; at = ParentKey(rows[key]))
            {
                if (key != row.Key) continue;
                skipped.TryAdd(row.Key, UnitImportIssueCode.Cycle);
                break;
            }
        }

        // 新單位的 ID 在各輪之間固定，避免每輪換 ID。
        var newIds = new Dictionary<int, Guid>();
        Attempt attempt;
        while (true)
        {
            attempt = Place();
            var more = Validate(attempt);
            if (more.Count == 0) break;
            foreach (var (key, code) in more) skipped.TryAdd(key, code);
        }
        if (UnitRules.Structure([.. attempt.Nodes.Values.Select(x => x.ToUnit())]) is { } invalid) return invalid;
        return Describe(attempt);

        Attempt Place()
        {
            var nodes = loaded.ToDictionary(x => x.Id, Node.From);
            var applied = new Dictionary<int, Guid>();
            var adopted = new HashSet<Guid>();
            foreach (var row in rows.Values.OrderBy(x => x.Key)) Visit(row);

            void Visit(SourceUnit row)
            {
                if (applied.ContainsKey(row.Key) || skipped.ContainsKey(row.Key)) return;
                Guid? parentId = null;
                if (ParentKey(row) is { } parentKey)
                {
                    // 成環的列已先略過，遞迴一定會停在最上層或略過的列。
                    Visit(rows[parentKey]);
                    if (applied.TryGetValue(parentKey, out var placed)) parentId = placed;
                    else if (bound.TryGetValue(parentKey, out var kept)) parentId = kept.Id;
                    else
                    {
                        skipped[row.Key] = UnitImportIssueCode.ParentSkipped;
                        return;
                    }
                }
                var name = names[row.Key]!;
                Guid id;
                if (bound.TryGetValue(row.Key, out var mine)) id = mine.Id;
                else if (loaded.FirstOrDefault(x => x.GdDepNo is null && !adopted.Contains(x.Id) && x.ParentId == parentId && SameName(x.Name, name)) is { } manual)
                {
                    id = manual.Id;
                    adopted.Add(id);
                }
                else
                {
                    if (!newIds.TryGetValue(row.Key, out id)) newIds[row.Key] = id = Guid.NewGuid();
                    nodes[id] = new Node { Id = id };
                }
                var node = nodes[id];
                node.ParentId = parentId; node.Name = name; node.Enabled = !row.Disabled; node.Key = row.Key;
                applied[row.Key] = id;
            }

            foreach (var (key, unit) in bound)
                if (!rows.ContainsKey(key)) nodes[unit.Id].Enabled = false;

            // 同層依來源順序重新編號：沒有順序的排在有順序之後，再以 DEP_NO 決定先後。
            // 不直接沿用來源的值：本地同值時依名稱排，DEP_NO 就不再是穩定次序；重新編號也自然落在 0～10000。
            foreach (var level in applied.GroupBy(x => nodes[x.Value].ParentId))
            {
                var position = 0;
                foreach (var (key, id) in level.OrderBy(x => rows[x.Key].SortOrder is null).ThenBy(x => rows[x.Key].SortOrder).ThenBy(x => x.Key))
                    nodes[id].SortOrder = Math.Min(++position * UnitRules.SortStep, UnitRules.MaxSortOrder);
            }
            return new(nodes, applied);
        }

        // 找出這一輪會違反結構規則的來源列。只怪罪「這次匯入改了位置或名稱」的列：其他單位維持原樣，問題不是它們造成的。
        Dictionary<int, UnitImportIssueCode> Validate(Attempt attempt)
        {
            var nodes = attempt.Nodes;
            var keyOf = attempt.Applied.ToDictionary(x => x.Value, x => x.Key);
            var more = new Dictionary<int, UnitImportIssueCode>();
            bool Moved(Node node) => keyOf.ContainsKey(node.Id) && (!local.TryGetValue(node.Id, out var before) || before.ParentId != node.ParentId);
            bool Relabeled(Node node) => Moved(node) || (keyOf.ContainsKey(node.Id) && !SameName(local[node.Id].Name, node.Name));

            foreach (var level in nodes.Values.GroupBy(x => (x.ParentId, Name: x.Name.ToUpperInvariant())).Where(x => x.Count() > 1))
            {
                var changed = level.Where(Relabeled).OrderBy(x => keyOf[x.Id]).ToList();
                // 同名的單位裡有沒動到的（手建或維持原名的），新來的全部讓開；都是這次才同名的，保留 DEP_NO 最小的一個。
                foreach (var node in changed.Skip(changed.Count == level.Count() ? 1 : 0)) more.TryAdd(keyOf[node.Id], UnitImportIssueCode.NameDuplicate);
            }

            foreach (var node in nodes.Values)
            {
                var chain = new List<Node>();
                for (var at = node; at is not null && chain.Count <= nodes.Count; at = at.ParentId is { } parent ? nodes.GetValueOrDefault(parent) : null)
                {
                    if (chain.Count > 0 && at.Id == node.Id)
                    {
                        foreach (var member in chain.Where(Moved)) more.TryAdd(keyOf[member.Id], UnitImportIssueCode.Cycle);
                        chain.Clear();
                        break;
                    }
                    chain.Add(at);
                }
                // 走超過單位數表示上層有環（由環上的單位處理）；超過深度時讓最近一個被搬動的上層（含自己）回到原位。
                if (chain.Count > UnitRules.MaxDepth && chain.Count <= nodes.Count && chain.FirstOrDefault(Moved) is { } culprit)
                    more.TryAdd(keyOf[culprit.Id], UnitImportIssueCode.DepthExceeded);
            }
            return more;
        }

        UnitImportPlan Describe(Attempt attempt)
        {
            var nodes = attempt.Nodes;
            var original = loaded.ToDictionary(x => x.Id, Node.From);
            var targets = new List<UnitTarget>();
            var changes = new List<UnitImportChangeDto>();
            foreach (var (key, id) in attempt.Applied)
            {
                var node = nodes[id];
                if (!local.TryGetValue(id, out var before))
                {
                    targets.Add(node.ToTarget(isNew: true));
                    changes.Add(new(key, null, UnitImportAction.Create, PathOf(nodes, id), null, node.Enabled, []));
                    continue;
                }
                var fields = Fields(before, node);
                var adopt = before.GdDepNo is null;
                if (fields.Count == 0 && !adopt) continue;
                targets.Add(node.ToTarget(isNew: false));
                var previous = fields.Contains(UnitImportField.Name) || fields.Contains(UnitImportField.Parent) ? PathOf(original, id) : null;
                changes.Add(new(key, id, adopt ? UnitImportAction.Adopt : UnitImportAction.Update, PathOf(nodes, id), previous, node.Enabled, fields));
            }
            foreach (var (key, unit) in bound.Where(x => !rows.ContainsKey(x.Key) && x.Value.Enabled))
            {
                targets.Add(nodes[unit.Id].ToTarget(isNew: false));
                changes.Add(new(key, unit.Id, UnitImportAction.Disable, PathOf(nodes, unit.Id), null, false, [UnitImportField.Enabled]));
            }

            var issues = skipped.Select(x => new UnitImportIssueDto(x.Key, names[x.Key], x.Value, null)).ToList();
            var memberTargets = new List<MemberTarget>();
            var managers = new List<UnitImportManagerDto>();
            var byUnit = members.ToLookup(x => x.UnitId);
            foreach (var (key, id) in attempt.Applied)
            {
                // 停用的單位不參與資料範圍，也不能加入新成員；主管等它啟用後的下一次匯入再同步。
                if (!nodes[id].Enabled) continue;
                var row = rows[key];
                issues.AddRange(row.Managers.Where(x => x.UserId is null).Select(x => new UnitImportIssueDto(key, names[key], IssueOf(x.Issue), x.SourceId)));
                var wanted = row.Managers.Where(x => x.UserId is not null).DistinctBy(x => x.UserId).ToList();
                var current = byUnit[id].ToList();
                var path = PathOf(nodes, id);
                var unitId = local.ContainsKey(id) ? id : (Guid?)null;
                foreach (var manager in wanted)
                {
                    var userId = manager.UserId!.Value;
                    var existing = current.FirstOrDefault(x => x.UserId == userId);
                    if (existing?.IsManager == true) continue;
                    memberTargets.Add(new(id, userId, true));
                    managers.Add(Manager(key, unitId, path, userId, added: true, manager.Deputy, joins: existing is null));
                }
                foreach (var member in current.Where(x => x.IsManager && wanted.All(y => y.UserId != x.UserId)))
                {
                    memberTargets.Add(new(id, member.UserId, false));
                    managers.Add(Manager(key, unitId, path, member.UserId, added: false, deputy: false, joins: false));
                }
            }

            var sortedUnits = changes.OrderBy(x => x.Action).ThenBy(x => string.Join('\u0001', x.Path), StringComparer.Ordinal).ThenBy(x => x.GdDepNo).ToList();
            var sortedManagers = managers.OrderBy(x => string.Join('\u0001', x.Path), StringComparer.Ordinal).ThenBy(x => !x.Added)
                .ThenBy(x => x.Account, StringComparer.Ordinal).ThenBy(x => x.UserId).ToList();
            var sortedIssues = issues.OrderBy(x => x.GdDepNo).ThenBy(x => x.Code).ThenBy(x => x.GdUserId, StringComparer.Ordinal).ToList();
            // 指紋涵蓋預覽的全部內容（新單位沒有 ID，以 DEP_NO 表示）：只要使用者看到的任何一項會不同，套用就拒絕。
            var content = JsonSerializer.SerializeToUtf8Bytes(new { source = rows.Count, sortedUnits, sortedManagers, sortedIssues }, Json);
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(content));
            return new(new(fingerprint, rows.Count, sortedUnits, sortedManagers, sortedIssues), targets, memberTargets, local, members);
        }

        UnitImportManagerDto Manager(int key, Guid? unitId, IReadOnlyList<string> path, Guid userId, bool added, bool deputy, bool joins)
        {
            var (account, displayName) = users.TryGetValue(userId, out var user) ? user : ("", "");
            return new(key, unitId, path, userId, account, displayName, added, deputy, joins);
        }
    }

    private static List<UnitImportField> Fields(Unit before, Node after)
    {
        var fields = new List<UnitImportField>();
        if (before.Name != after.Name) fields.Add(UnitImportField.Name);
        if (before.ParentId != after.ParentId) fields.Add(UnitImportField.Parent);
        if (before.Enabled != after.Enabled) fields.Add(UnitImportField.Enabled);
        if (before.SortOrder != after.SortOrder) fields.Add(UnitImportField.SortOrder);
        return fields;
    }

    private static string[] PathOf(IReadOnlyDictionary<Guid, Node> nodes, Guid id)
    {
        var path = new List<string>();
        for (var at = nodes.GetValueOrDefault(id); at is not null && path.Count <= UnitRules.MaxDepth; at = at.ParentId is { } parent ? nodes.GetValueOrDefault(parent) : null)
            path.Add(at.Name);
        path.Reverse();
        return [.. path];
    }

    // 比照 UnitRules.Structure：資料庫定序不分大小寫。
    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static UnitImportIssueCode IssueOf(SourceAccountIssue? issue) => issue switch
    {
        SourceAccountIssue.AccountNotFound => UnitImportIssueCode.AccountNotFound,
        SourceAccountIssue.AccountDisabled => UnitImportIssueCode.AccountDisabled,
        SourceAccountIssue.UserAmbiguous => UnitImportIssueCode.UserAmbiguous,
        _ => UnitImportIssueCode.UserNotFound,
    };

    /// <summary>一輪安排的結果：全部單位（本地＋新增）的匯入後狀態，與每個套用的來源列對到的單位 ID。</summary>
    private sealed record Attempt(Dictionary<Guid, Node> Nodes, Dictionary<int, Guid> Applied);

    private sealed record UnitTarget(Guid Id, bool IsNew, int? Key, Guid? ParentId, string Name, bool Enabled, int SortOrder);

    private sealed record MemberTarget(Guid UnitId, Guid UserId, bool IsManager);

    /// <summary>單位在計算中的可變副本；不直接改實體，預覽才不會留下追蹤中的變更。</summary>
    private sealed class Node
    {
        public Guid Id { get; init; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public int SortOrder { get; set; }
        public int? Key { get; set; }

        public static Node From(Unit unit) => new() { Id = unit.Id, ParentId = unit.ParentId, Name = unit.Name, Enabled = unit.Enabled, SortOrder = unit.SortOrder, Key = unit.GdDepNo };

        public Unit ToUnit() => new() { Id = Id, ParentId = ParentId, Name = Name, Enabled = Enabled, SortOrder = SortOrder, GdDepNo = Key };

        public UnitTarget ToTarget(bool isNew) => new(Id, isNew, Key, ParentId, Name, Enabled, SortOrder);
    }
}
