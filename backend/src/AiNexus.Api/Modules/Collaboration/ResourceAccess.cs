using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Collaboration;

// Shared ACL for knowledge collections, projects and editable artifacts.
public sealed class WorkspaceResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ResourceMember
{
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "viewer";
}
public sealed class ResourceGroup
{
    public Guid ResourceId { get; set; }
    public string GroupId { get; set; } = "";
}
public sealed record ResourceDto(Guid Id, string Name, string Kind, bool CanEdit, bool IsOwner, DateTimeOffset UpdatedAt);
public sealed record ResourceMemberDto(Guid UserId, string Account, string DisplayName, string Role);
public sealed record ResourceAclDto(IReadOnlyList<ResourceMemberDto> Members, IReadOnlyList<string> GroupIds);
public sealed record ResourceAclRequest(IReadOnlyList<ResourceMemberUpdate> Members, IReadOnlyList<string> GroupIds);
public sealed record ResourceMemberUpdate(Guid UserId, string Role);
public sealed record DirectoryUserDto(Guid Id, string Account, string DisplayName);
public sealed record DirectoryGroupDto(string Id, string Name);
public sealed record NamedResourceRequest(string Name);
public sealed class ResourceWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }

public sealed class ResourceAccess(NexusDbContext db, AccessService access, ResourceWriteLock writes)
{
    public async Task<IQueryable<WorkspaceResource>> QueryAsync(Guid actor, string kind, CancellationToken ct)
    {
        var effective = await access.ForUserAsync(actor, ct);
        var groups = effective.Groups.Select(x => x.Id).ToArray();
        var inheritProjects = effective.Features.Any(x => x.Id == "projects");
        var direct = db.Set<WorkspaceResource>().Where(x => !x.IsDeleted &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor) || db.Set<ResourceGroup>().Any(g => g.ResourceId == x.Id && groups.Contains(g.GroupId))));
        return db.Set<WorkspaceResource>().Where(x => !x.IsDeleted && x.Kind == kind &&
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
        await db.Set<WorkspaceResource>().AnyAsync(x => x.Id == value.ParentId && x.Kind == "project" && !x.IsDeleted &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor && m.Role == "editor")), ct);
    public async Task<ResourceDto> DescribeAsync(Guid actor, WorkspaceResource value, CancellationToken ct) => new(value.Id, value.Name, value.Kind,
        await CanEditAsync(actor, value, ct), value.OwnerId == actor, value.UpdatedAt);
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
        => await db.Set<WorkspaceResource>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor && x.Kind == kind && !x.IsDeleted, ct) ?? throw Missing();
    public static string Name(string value)
    {
        value = value.Trim();
        if (value.Length is < 1 or > 120 || value.Any(char.IsControl)) throw new ApiException(400, "invalid_resource_name", "名稱需為 1 至 120 個字元。");
        return value;
    }
    private static ApiException Missing() => new(404, "resource_not_found", "找不到此項目，或你已沒有存取權限。");
}
public static class CollaborationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var resource = model.Entity<WorkspaceResource>(); resource.ToTable("Resources", "collaboration"); resource.HasKey(x => x.Id);
        resource.Property(x => x.Kind).HasMaxLength(24); resource.Property(x => x.Name).HasMaxLength(120);
        resource.HasIndex(x => new { x.OwnerId, x.Kind, x.UpdatedAt });
        resource.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        resource.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        var member = model.Entity<ResourceMember>(); member.ToTable("ResourceMembers", "collaboration"); member.HasKey(x => new { x.ResourceId, x.UserId }); member.Property(x => x.Role).HasMaxLength(12);
        member.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        member.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        var group = model.Entity<ResourceGroup>(); group.ToTable("ResourceGroups", "collaboration"); group.HasKey(x => new { x.ResourceId, x.GroupId }); group.Property(x => x.GroupId).HasMaxLength(64);
        group.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        group.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
