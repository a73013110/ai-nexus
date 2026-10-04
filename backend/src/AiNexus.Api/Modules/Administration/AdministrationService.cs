using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Administration;

public sealed record AdminRoleDto(string Id, string Name, bool Enabled, IReadOnlyList<string> GroupIds, int UserCount);
public sealed record AdminGroupDto(string Id, string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy);
public sealed record AdminFeatureDto(string Id, string Name, string Route, int SortOrder, bool Enabled);
public sealed record AdminCatalogDto(IReadOnlyList<AdminRoleDto> Roles, IReadOnlyList<AdminGroupDto> Groups, IReadOnlyList<AdminFeatureDto> Features, IReadOnlyList<ModelDto> Models);
public sealed record AdminUserActivityDto(UsageTotalsDto Usage, int Conversations, long AttachmentBytes);
public sealed record AdminUserDto(Guid Id, string Account, string DisplayName, DateTimeOffset LastSeenAt, IReadOnlyList<string> RoleIds, AdminUserActivityDto? Activity = null);
public sealed record AdminUsersDto(IReadOnlyList<AdminUserDto> Users, int Total, int Offset);
public sealed record UserRolesRequest(IReadOnlyList<string> RoleIds);
public sealed record RoleUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> GroupIds);
public sealed record GroupPolicyRequest(IReadOnlyList<string>? AllowedModelIds = null, int? DailyRequestLimit = null, long? StoredAttachmentLimitBytes = null);
public sealed record GroupUpdateRequest(string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy = null);
public sealed record FeatureUpdateRequest(string Name, int SortOrder, bool Enabled);
public sealed record AuditDto(long Id, string Actor, string Action, Guid? ResourceId, string? Result, DateTimeOffset At, string? DetailsJson);
public sealed record AdminUsageDto(int Users, int Requests, int Completed, long InputTokens, long OutputTokens, int RequestsWithUsage);

public sealed class AdministrationService(NexusDbContext db, AccessService access, IOptions<InferenceOptions> inference, AdministrativeAudit mutations, UsageReports reports)
{
    public async Task<AdminCatalogDto> CatalogAsync(CancellationToken ct)
    {
        var roles = await db.Set<Role>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var groups = await db.Set<RoleGroup>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var features = await db.Set<Feature>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var links = await db.Set<RoleGroupRole>().AsNoTracking().ToListAsync(ct);
        var grants = await db.Set<RoleGroupFeature>().AsNoTracking().ToListAsync(ct);
        var policies = await db.Set<GroupModelPolicy>().AsNoTracking().ToDictionaryAsync(x => x.GroupId, ct);
        var counts = await db.Set<UserRole>().GroupBy(x => x.RoleId).Select(x => new { RoleId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);
        return new(roles.Select(x => new AdminRoleDto(x.Id, x.Name, x.Enabled, links.Where(y => y.RoleId == x.Id).Select(y => y.GroupId).ToArray(), counts.GetValueOrDefault(x.Id))).ToArray(),
            groups.Select(x => new AdminGroupDto(x.Id, x.Name, x.Enabled, grants.Where(y => y.GroupId == x.Id).Select(y => y.FeatureId).ToArray(), policies.TryGetValue(x.Id, out var p) ? new(p.AllowedModelsJson is null ? null : JsonSerializer.Deserialize<string[]>(p.AllowedModelsJson), p.DailyRequestLimit, p.StoredAttachmentLimitBytes) : null)).ToArray(),
            features.Select(x => new AdminFeatureDto(x.Id, x.Name, x.Route, x.SortOrder, x.Enabled)).ToArray(), inference.Value.Models.Select(x => x.ToDto()).ToArray());
    }
    public async Task<AdminUsersDto> UsersAsync(string? search, int offset, CancellationToken ct)
    {
        if (search?.Length > 120 || offset < 0) throw new ApiException(400, "invalid_search", "搜尋條件不正確。");
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Account.Contains(search) || x.DisplayName.Contains(search));
        var total = await query.CountAsync(ct); var rows = await query.OrderBy(x => x.Account).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray(); var roles = await db.Set<UserRole>().AsNoTracking().Where(x => ids.Contains(x.UserId)).ToListAsync(ct);
        var usage = (await reports.ByOwnersAsync(ids, ct)).ToDictionary(x => x.OwnerId, x => x.Usage);
        var conversations = await db.Conversations.Where(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId).Select(g => new { OwnerId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.OwnerId, x => x.Count, ct);
        var bytes = await db.Set<AiNexus.Modules.Attachments.Attachment>().Where(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId).Select(g => new { OwnerId = g.Key, Bytes = g.Sum(x => x.Size) }).ToDictionaryAsync(x => x.OwnerId, x => x.Bytes, ct);
        return new(rows.Select(x => new AdminUserDto(x.Id, x.Account, x.DisplayName, x.LastSeenAt, roles.Where(y => y.UserId == x.Id).Select(y => y.RoleId).ToArray(),
            new(usage.GetValueOrDefault(x.Id) ?? new(0, 0, 0, 0, 0, 0, 0), conversations.GetValueOrDefault(x.Id), bytes.GetValueOrDefault(x.Id)))).ToArray(), total, offset);
    }
    public async Task SetUserRolesAsync(Guid id, UserRolesRequest request, CancellationToken ct)
    {
        await mutations.MutateAsync("admin.user_roles", id, id.ToString(), async () =>
        {
            Keys(request.RoleIds);
            if (!await db.Users.AnyAsync(x => x.Id == id, ct)) throw Missing();
            await ExistingAsync(db.Set<Role>().Select(x => x.Id), request.RoleIds, ct);
            var old = await db.Set<UserRole>().Where(x => x.UserId == id).ToListAsync(ct);
            db.RemoveRange(old.Where(x => !request.RoleIds.Contains(x.RoleId)));
            db.AddRange(request.RoleIds.Where(x => !old.Any(y => y.RoleId == x)).Select(x => new UserRole { UserId = id, RoleId = x }));
        }, ct);
    }
    public async Task SaveRoleAsync(string id, RoleUpdateRequest request, CancellationToken ct)
    {
        await mutations.MutateAsync("admin.role", null, id, async () =>
        {
            Key(id); Name(request.Name); Keys(request.GroupIds);
            await ExistingAsync(db.Set<RoleGroup>().Select(x => x.Id), request.GroupIds, ct);
            var role = await db.Set<Role>().FindAsync([id], ct);
            if (role is null) { role = new() { Id = id }; db.Add(role); }
            role.Name = request.Name.Trim(); role.Enabled = request.Enabled;
            var old = await db.Set<RoleGroupRole>().Where(x => x.RoleId == id).ToListAsync(ct);
            db.RemoveRange(old.Where(x => !request.GroupIds.Contains(x.GroupId)));
            db.AddRange(request.GroupIds.Where(x => !old.Any(y => y.GroupId == x)).Select(x => new RoleGroupRole { RoleId = id, GroupId = x }));
        }, ct);
    }
    public async Task SaveGroupAsync(string id, GroupUpdateRequest request, CancellationToken ct)
    {
        await mutations.MutateAsync("admin.group", null, id, async () =>
        {
            Key(id); Name(request.Name); Keys(request.FeatureIds);
            if (request.Policy is { } policy)
            {
                if (policy.DailyRequestLimit is < 0 or > 100000 || policy.StoredAttachmentLimitBytes is < 1048576 or > 1073741824 ||
                    policy.AllowedModelIds?.Count > 24 || policy.AllowedModelIds?.Distinct().Count() != policy.AllowedModelIds?.Count ||
                    policy.AllowedModelIds?.Any(x => !inference.Value.Models.Any(m => m.Id == x)) == true)
                    throw new ApiException(400, "invalid_model_policy", "模型清單或群組配額不正確。");
            }
            await ExistingAsync(db.Set<Feature>().Select(x => x.Id), request.FeatureIds, ct);
            var group = await db.Set<RoleGroup>().FindAsync([id], ct);
            if (group is null) { group = new() { Id = id }; db.Add(group); }
            group.Name = request.Name.Trim(); group.Enabled = request.Enabled;
            var old = await db.Set<RoleGroupFeature>().Where(x => x.GroupId == id).ToListAsync(ct);
            db.RemoveRange(old.Where(x => !request.FeatureIds.Contains(x.FeatureId)));
            db.AddRange(request.FeatureIds.Where(x => !old.Any(y => y.FeatureId == x)).Select(x => new RoleGroupFeature { GroupId = id, FeatureId = x }));
            var value = await db.Set<GroupModelPolicy>().FindAsync([id], ct);
            if (request.Policy is null) { if (value is not null) db.Remove(value); }
            else
            {
                if (value is null) { value = new() { GroupId = id }; db.Add(value); }
                value.AllowedModelsJson = request.Policy.AllowedModelIds is null ? null : JsonSerializer.Serialize(request.Policy.AllowedModelIds.Order());
                value.DailyRequestLimit = request.Policy.DailyRequestLimit; value.StoredAttachmentLimitBytes = request.Policy.StoredAttachmentLimitBytes;
            }
        }, ct);
    }
    public async Task SaveFeatureAsync(string id, FeatureUpdateRequest request, CancellationToken ct)
    {
        await mutations.MutateAsync("admin.feature", null, id, async () =>
        {
            Key(id); Name(request.Name); if (request.SortOrder is < 0 or > 10000) throw new ApiException(400, "invalid_order", "排序值必須介於 0 與 10000。");
            var feature = await db.Set<Feature>().FindAsync([id], ct) ?? throw Missing();
            feature.Name = request.Name.Trim(); feature.SortOrder = request.SortOrder; feature.Enabled = request.Enabled;
        }, ct);
    }
    public async Task<AccessDto> EffectiveAsync(Guid id, CancellationToken ct) { if (!await db.Users.AnyAsync(x => x.Id == id, ct)) throw Missing(); return await access.ForUserAsync(id, ct); }
    public async Task<IReadOnlyList<AuditDto>> AuditAsync(long? before, CancellationToken ct, string? search = null, string? action = null, string? result = null, DateTimeOffset? from = null, DateTimeOffset? until = null)
    {
        if (search?.Length > 120 || action?.Length > 120 || result?.Length > 80 || before is <= 0 || from > until)
            throw new ApiException(400, "invalid_audit_filter", "稽核篩選條件不正確。");
        var query = from entry in db.AuditEvents.AsNoTracking() join actor in db.Users on entry.OwnerId equals actor.Id select new { entry, actor };
        if (before is { } cursor) query = query.Where(x => x.entry.Id < cursor);
        if (from is { } start) query = query.Where(x => x.entry.At >= start);
        if (until is { } end) query = query.Where(x => x.entry.At < end);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.entry.Action.StartsWith(action));
        if (!string.IsNullOrWhiteSpace(result)) query = result == "failed"
            ? query.Where(x => x.entry.Result != "saved" && x.entry.Result != "read" && x.entry.Result != "completed" && x.entry.Result != "success" && x.entry.Result != "granted")
            : query.Where(x => x.entry.Result == result);
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (Guid.TryParse(search, out var resource)) query = query.Where(x => x.entry.ResourceId == resource || x.actor.Id == resource);
            else query = query.Where(x => x.actor.Account.Contains(search) || x.actor.DisplayName.Contains(search) || x.entry.Action.Contains(search) || (x.entry.DetailsJson != null && x.entry.DetailsJson.Contains(search)));
        }
        return await query.OrderByDescending(x => x.entry.Id).Take(100)
            .Select(x => new AuditDto(x.entry.Id, x.actor.Account, x.entry.Action, x.entry.ResourceId, x.entry.Result, x.entry.At, x.entry.DetailsJson)).ToListAsync(ct);
    }
    public async Task<AdminUsageDto> UsageAsync(CancellationToken ct)
    {
        var totals = await reports.AllAsync(ct);
        return new(await db.Users.CountAsync(ct), totals.Requests, totals.Completed, totals.InputTokens, totals.OutputTokens, totals.RequestsWithUsage);
    }
    private static void Key(string id) { if (!Regex.IsMatch(id, "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)) throw new ApiException(400, "invalid_access_id", "識別碼使用小寫英文、數字、底線與連字號，最多 64 字元。"); }
    private static void Keys(IReadOnlyList<string> ids) { if (ids.Count > 50 || ids.Distinct().Count() != ids.Count) throw new ApiException(400, "invalid_access_ids", "授權清單過長或有重複。"); foreach (var id in ids) Key(id); }
    private static void Name(string name) { if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl)) throw new ApiException(400, "invalid_access_name", "請輸入 120 字元內的名稱。"); }
    private static async Task ExistingAsync(IQueryable<string> query, IReadOnlyList<string> ids, CancellationToken ct) { if (await query.CountAsync(x => ids.Contains(x), ct) != ids.Count) throw new ApiException(400, "unknown_access_id", "清單包含不存在的角色、群組或功能。"); }
    private static ApiException Missing() => new(404, "admin_resource_not_found", "找不到此管理項目。");
}
