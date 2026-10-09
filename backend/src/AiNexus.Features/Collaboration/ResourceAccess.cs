using AiNexus.Platform.Threading;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Collaboration;

/// <summary>
/// Serializes writes in this process per resource (versioned saves, ACL changes, deletion) or per other id-scoped invariant;
/// writes to different resources do not wait for each other. Callers taking two keys take the owner-scoped key first.
/// </summary>
public sealed class ResourceWriteLock
{
    private readonly KeyedAsyncLock<(string Scope, Guid Id)> keys = new();

    /// <summary>The write lock of one resource.</summary>
    public Task<IDisposable> AcquireAsync(Guid resource, CancellationToken ct) => keys.AcquireAsync(("resource", resource), ct);

    /// <summary>The lock of another per-id invariant, such as one owner's limit or one message's feedback.</summary>
    public Task<IDisposable> AcquireAsync(string scope, Guid id, CancellationToken ct) => keys.AcquireAsync((scope, id), ct);
}

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
    public async Task<Result<WorkspaceResource>> RequireAsync(Guid actor, Guid id, string kind, CancellationToken ct, bool write = false)
    {
        var resource = await (await QueryAsync(actor, kind, ct)).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (resource is null) return CollaborationErrors.ResourceNotFound;
        if (write && !await CanEditAsync(actor, resource, ct)) return CollaborationErrors.ResourceReadOnly;
        return resource;
    }
    public async Task<bool> CanEditAsync(Guid actor, WorkspaceResource value, CancellationToken ct) => value.OwnerId == actor ||
        await db.Set<ResourceMember>().AnyAsync(x => x.ResourceId == value.Id && x.UserId == actor && x.Role == "editor", ct) ||
        await db.Set<WorkspaceResource>().AnyAsync(x => x.Id == value.ParentId && x.Kind == "project" &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor && m.Role == "editor")), ct);
    public async Task<ResourceDto> DescribeAsync(Guid actor, WorkspaceResource value, CancellationToken ct) => new(value.Id, value.Name, value.Kind,
        await CanEditAsync(actor, value, ct), value.OwnerId == actor, value.UpdatedAt);

    /// <summary>Every resource the actor reads by <see cref="QueryAsync"/>, or the error of <see cref="RequireAsync"/>; one query for the whole list.</summary>
    public async Task<Result> RequireAllAsync(Guid actor, IReadOnlyCollection<Guid> ids, string kind, CancellationToken ct)
    {
        if (ids.Count == 0) return Result.Success;
        var readable = await (await QueryAsync(actor, kind, ct)).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        return ids.All(readable.Contains) ? Result.Success : CollaborationErrors.ResourceNotFound;
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
    public async Task<Result<ResourceAclDto>> AclAsync(Guid actor, Guid id, string kind, CancellationToken ct)
    {
        var owned = await OwnerAsync(actor, id, kind, ct);
        if (!owned.IsSuccess) return owned.Error;
        var members = await (from link in db.Set<ResourceMember>() join user in db.Users on link.UserId equals user.Id where link.ResourceId == id
            select new ResourceMemberDto(user.Id, user.Account, user.DisplayName, link.Role)).ToListAsync(ct);
        return new ResourceAclDto(members, await db.Set<ResourceGroup>().Where(x => x.ResourceId == id).Select(x => x.GroupId).ToListAsync(ct));
    }
    public async Task<Result> SetAclAsync(Guid actor, Guid id, string kind, ResourceAclRequest request, CancellationToken ct)
    {
        using (await writes.AcquireAsync(id, ct))
        {
            var owned = await OwnerAsync(actor, id, kind, ct);
            if (!owned.IsSuccess) return owned.Error;
            var resource = owned.Value;
            if (request.Members.Any(x => x.UserId == resource.OwnerId)) return CollaborationErrors.OwnerIsImplicit;
            var users = request.Members.Select(x => x.UserId).ToArray();
            if (await db.Users.CountAsync(x => users.Contains(x.Id), ct) != users.Length || await db.Set<RoleGroup>().CountAsync(x => x.Enabled && request.GroupIds.Contains(x.Id), ct) != request.GroupIds.Count)
                return CollaborationErrors.UnknownMember;
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
        return Result.Success;
    }
    public async Task<Result<WorkspaceResource>> OwnerAsync(Guid actor, Guid id, string kind, CancellationToken ct)
        => await db.Set<WorkspaceResource>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor && x.Kind == kind, ct) is { } resource
            ? resource : CollaborationErrors.ResourceNotFound;

    /// <summary>A resource name: trimmed, 1 to 120 characters, no control characters.</summary>
    public static bool IsValidName(string? value) => value?.Trim() is { Length: >= 1 and <= 120 } trimmed && !trimmed.Any(char.IsControl);

    public static Result<string> Name(string value) => IsValidName(value) ? value.Trim() : CollaborationErrors.InvalidName;
}
