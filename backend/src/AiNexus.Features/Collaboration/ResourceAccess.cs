using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Collaboration;

public sealed class ResourceWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }

public sealed class ResourceAccess(NexusDbContext db, AccessService access, ResourceWriteLock writes)
{
    public async Task<IQueryable<WorkspaceResource>> QueryAsync(Guid actor, string kind, CancellationToken ct)
    {
        var effective = await access.ForUserAsync(actor, ct);
        var groups = effective.Groups.Select(x => x.Id).ToArray();
        var inheritProjects = effective.Features.Any(x => x.Id == "projects");
        var direct = db.Set<WorkspaceResource>().Where(x =>
            x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor) || db.Set<ResourceGroup>().Any(g => g.ResourceId == x.Id && groups.Contains(g.GroupId)));
        return db.Set<WorkspaceResource>().Where(x => x.Kind == kind &&
            (direct.Any(r => r.Id == x.Id) || inheritProjects && direct.Any(r => r.Kind == "project" && r.Id == x.ParentId)));
    }
    public async Task<WorkspaceResource> RequireAsync(Guid actor, Guid id, string kind, CancellationToken ct, bool write = false)
    {
        var resource = await (await QueryAsync(actor, kind, ct)).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        if (write && !await CanEditAsync(actor, resource, ct))
            throw new ApiException(403, "resource_read_only", "此項目目前只有檢視權限。");
        return resource;
    }
    public async Task<bool> CanEditAsync(Guid actor, WorkspaceResource value, CancellationToken ct) => value.OwnerId == actor ||
        await db.Set<ResourceMember>().AnyAsync(x => x.ResourceId == value.Id && x.UserId == actor && x.Role == "editor", ct) ||
        await db.Set<WorkspaceResource>().AnyAsync(x => x.Id == value.ParentId && x.Kind == "project" &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor && m.Role == "editor")), ct);
    public async Task<ResourceDto> DescribeAsync(Guid actor, WorkspaceResource value, CancellationToken ct) => new(value.Id, value.Name, value.Kind,
        await CanEditAsync(actor, value, ct), value.OwnerId == actor, value.UpdatedAt);

    /// <summary>Every resource the actor reads by <see cref="QueryAsync"/>, or the error of <see cref="RequireAsync"/>; one query for the whole list.</summary>
    public async Task RequireAllAsync(Guid actor, IReadOnlyCollection<Guid> ids, string kind, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var readable = await (await QueryAsync(actor, kind, ct)).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        if (ids.Any(id => !readable.Contains(id))) throw Missing();
    }

    /// <summary>The ids among <paramref name="values"/> that <see cref="CanEditAsync"/> allows, in at most two queries.</summary>
    public async Task<IReadOnlySet<Guid>> EditableAsync(Guid actor, IReadOnlyCollection<WorkspaceResource> values, CancellationToken ct)
    {
        var editable = values.Where(x => x.OwnerId == actor).Select(x => x.Id).ToHashSet();
        var rest = values.Where(x => !editable.Contains(x.Id)).ToArray();
        if (rest.Length == 0) return editable;
        var ids = rest.Select(x => x.Id).Distinct().ToArray();
        var named = await db.Set<ResourceMember>().Where(x => ids.Contains(x.ResourceId) && x.UserId == actor && x.Role == "editor").Select(x => x.ResourceId).ToListAsync(ct);
        var parents = rest.Where(x => x.ParentId != null).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var projects = parents.Length == 0 ? [] : await db.Set<WorkspaceResource>().Where(x => parents.Contains(x.Id) && x.Kind == "project" &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor && m.Role == "editor"))).Select(x => x.Id).ToListAsync(ct);
        foreach (var value in rest)
            if (named.Contains(value.Id) || value.ParentId is Guid parent && projects.Contains(parent)) editable.Add(value.Id);
        return editable;
    }

    /// <summary><see cref="DescribeAsync"/> for a list, in the list's order.</summary>
    public async Task<IReadOnlyList<ResourceDto>> DescribeAllAsync(Guid actor, IReadOnlyCollection<WorkspaceResource> values, CancellationToken ct)
    {
        var editable = await EditableAsync(actor, values, ct);
        return values.Select(value => new ResourceDto(value.Id, value.Name, value.Kind, editable.Contains(value.Id), value.OwnerId == actor, value.UpdatedAt)).ToArray();
    }
    public async Task<ResourceAclDto> AclAsync(Guid actor, Guid id, string kind, CancellationToken ct)
    {
        await OwnerAsync(actor, id, kind, ct);
        var members = await (from link in db.Set<ResourceMember>() join user in db.Users on link.UserId equals user.Id where link.ResourceId == id
            select new ResourceMemberDto(user.Id, user.Account, user.DisplayName, link.Role)).ToListAsync(ct);
        return new(members, await db.Set<ResourceGroup>().Where(x => x.ResourceId == id).Select(x => x.GroupId).ToListAsync(ct));
    }
    public async Task SetAclAsync(Guid actor, Guid id, string kind, ResourceAclRequest request, CancellationToken ct)
    {
        if (request.Members.Count > 50 || request.GroupIds.Count > 20 || request.Members.Select(x => x.UserId).Distinct().Count() != request.Members.Count || request.GroupIds.Distinct().Count() != request.GroupIds.Count || request.Members.Any(x => x.Role is not ("viewer" or "editor")))
            throw new ApiException(400, "invalid_resource_acl", "成員清單重複、過長或角色不正確。");
        await writes.Gate.WaitAsync(ct);
        try
        {
            var resource = await OwnerAsync(actor, id, kind, ct);
            if (request.Members.Any(x => x.UserId == resource.OwnerId)) throw new ApiException(400, "owner_is_implicit", "擁有者已具有完整權限，不需加入成員清單。");
            var users = request.Members.Select(x => x.UserId).ToArray();
            if (await db.Users.CountAsync(x => users.Contains(x.Id), ct) != users.Length || await db.Set<RoleGroup>().CountAsync(x => x.Enabled && request.GroupIds.Contains(x.Id), ct) != request.GroupIds.Count)
                throw new ApiException(400, "unknown_resource_member", "清單包含未登入過的帳號或停用的群組。");
            // Group grants are intentionally read-only; editors must be named members.
            var oldMembers = await db.Set<ResourceMember>().Where(x => x.ResourceId == id).ToListAsync(ct);
            db.RemoveRange(oldMembers.Where(x => !users.Contains(x.UserId)));
            foreach (var item in request.Members)
            {
                var member = oldMembers.SingleOrDefault(x => x.UserId == item.UserId);
                if (member is null) { member = new() { ResourceId = id, UserId = item.UserId }; db.Add(member); }
                member.Role = item.Role;
            }
            var oldGroups = await db.Set<ResourceGroup>().Where(x => x.ResourceId == id).ToListAsync(ct);
            db.RemoveRange(oldGroups.Where(x => !request.GroupIds.Contains(x.GroupId)));
            db.AddRange(request.GroupIds.Except(oldGroups.Select(x => x.GroupId)).Select(x => new ResourceGroup { ResourceId = id, GroupId = x }));
            resource.UpdatedAt = DateTimeOffset.UtcNow;
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "resource.acl", Result = "saved", DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { kind, users, request.GroupIds }) });
            await db.SaveChangesAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<WorkspaceResource> OwnerAsync(Guid actor, Guid id, string kind, CancellationToken ct)
        => await db.Set<WorkspaceResource>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor && x.Kind == kind, ct) ?? throw Missing();
    public static string Name(string value)
    {
        value = value.Trim();
        if (value.Length is < 1 or > 120 || value.Any(char.IsControl)) throw new ApiException(400, "invalid_resource_name", "名稱需為 1 至 120 個字元。");
        return value;
    }
    internal static ApiException Missing() => new(404, "resource_not_found", "找不到此項目，或你已沒有存取權限。");
}
