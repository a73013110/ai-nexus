using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Collaboration;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Threading;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Collaboration;

/// <summary>
/// 只在本程序內序列化寫入（多實例部署不互斥）：依資源鎖住版本化儲存、ACL 變更與刪除，或依其他以 ID 為範圍的不變式。
/// 不同資源互不等待。同時取兩把鎖時，必須先取擁有者範圍的鎖，否則可能死結。
/// </summary>
public sealed class ResourceWriteLock
{
    private readonly KeyedAsyncLock<(string Scope, Guid Id)> keys = new();

    public Task<IDisposable> AcquireAsync(Guid resource, CancellationToken ct) => keys.AcquireAsync(("resource", resource), ct);

    /// <summary>為不屬於資源本身、但以 ID 為範圍的不變式加鎖，例如單一擁有者的額度或單一訊息的回饋。<paramref name="scope"/> 用來區分用途，避免不同用途的鎖互相等待。</summary>
    public Task<IDisposable> AcquireAsync(string scope, Guid id, CancellationToken ct) => keys.AcquireAsync((scope, id), ct);
}

/// <summary>資源的可見性、編輯權與 ACL 判斷；所有分享資源的模組都透過它決定能否讀寫。</summary>
public sealed class ResourceAccess(AppDbContext db, AccessService access, ResourceWriteLock writes, TimeProvider clock)
{
    public async Task<IQueryable<Resource>> QueryAsync(Guid actor, string kind, CancellationToken ct)
    {
        var effective = await access.ForUserAsync(actor, ct);
        var groups = effective.Groups.Select(x => x.Id).ToArray();
        var inheritProjects = effective.Features.Any(x => x.Id == "projects");
        var direct = db.Set<Resource>().Where(x =>
            x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor) || db.Set<ResourceGroup>().Any(g => g.ResourceId == x.Id && groups.Contains(g.GroupId)));
        return db.Set<Resource>().Where(x => x.Kind == kind &&
            (direct.Any(r => r.Id == x.Id) || inheritProjects && direct.Any(r => r.Kind == "project" && r.Id == x.ParentId)));
    }
    public async Task<Result<Resource>> RequireAsync(Guid actor, Guid id, string kind, CancellationToken ct, bool write = false)
    {
        var resource = await (await QueryAsync(actor, kind, ct)).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (resource is null) return CollaborationErrors.ResourceNotFound;
        if (write && !await CanEditAsync(actor, resource, ct)) return CollaborationErrors.ResourceReadOnly;
        return resource;
    }
    public async Task<bool> CanEditAsync(Guid actor, Resource value, CancellationToken ct) => value.OwnerId == actor ||
        await db.Set<ResourceMember>().AnyAsync(x => x.ResourceId == value.Id && x.UserId == actor && x.Role == "editor", ct) ||
        await db.Set<Resource>().AnyAsync(x => x.Id == value.ParentId && x.Kind == "project" &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor && m.Role == "editor")), ct);
    public async Task<ResourceDto> DescribeAsync(Guid actor, Resource value, CancellationToken ct) => new(value.Id, value.Name, value.Kind,
        await CanEditAsync(actor, value, ct), value.OwnerId == actor, value.UpdatedAt);

    /// <summary>與 <see cref="RequireAsync"/> 的可見性規則相同，但一次查完整批；任一筆不可見即回傳 not found。</summary>
    public async Task<Result> RequireAllAsync(Guid actor, IReadOnlyCollection<Guid> ids, string kind, CancellationToken ct)
    {
        if (ids.Count == 0) return Result.Success;
        var readable = await (await QueryAsync(actor, kind, ct)).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        return ids.All(readable.Contains) ? Result.Success : CollaborationErrors.ResourceNotFound;
    }

    /// <summary>與 <see cref="CanEditAsync"/> 判斷相同，但以至多兩次查詢批次完成，回傳可編輯的 id 集合。</summary>
    public async Task<IReadOnlySet<Guid>> EditableAsync(Guid actor, IReadOnlyCollection<Resource> values, CancellationToken ct)
    {
        var editable = values.Where(x => x.OwnerId == actor).Select(x => x.Id).ToHashSet();
        var rest = values.Where(x => !editable.Contains(x.Id)).ToArray();
        if (rest.Length == 0) return editable;
        var ids = rest.Select(x => x.Id).Distinct().ToArray();
        var named = await db.Set<ResourceMember>().Where(x => ids.Contains(x.ResourceId) && x.UserId == actor && x.Role == "editor").Select(x => x.ResourceId).ToListAsync(ct);
        var parents = rest.Where(x => x.ParentId != null).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var projects = parents.Length == 0 ? [] : await db.Set<Resource>().Where(x => parents.Contains(x.Id) && x.Kind == "project" &&
            (x.OwnerId == actor || db.Set<ResourceMember>().Any(m => m.ResourceId == x.Id && m.UserId == actor && m.Role == "editor"))).Select(x => x.Id).ToListAsync(ct);
        foreach (var value in rest)
            if (named.Contains(value.Id) || value.ParentId is Guid parent && projects.Contains(parent)) editable.Add(value.Id);
        return editable;
    }

    /// <summary>批次版本的 <see cref="DescribeAsync"/>，結果保持輸入順序。</summary>
    public async Task<IReadOnlyList<ResourceDto>> DescribeAllAsync(Guid actor, IReadOnlyCollection<Resource> values, CancellationToken ct)
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
            // 群組授權刻意只給唯讀；可編輯者必須是具名成員，不能靠群組取得編輯權。
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
            resource.UpdatedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "resource.acl", Result = "saved", DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { kind, users, request.GroupIds }) });
            await db.SaveChangesAsync(ct);
        }
        return Result.Success;
    }
    public async Task<Result<Resource>> OwnerAsync(Guid actor, Guid id, string kind, CancellationToken ct)
        => await db.Set<Resource>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == actor && x.Kind == kind, ct) is { } resource
            ? resource : CollaborationErrors.ResourceNotFound;

    /// <summary>資源名稱：去除前後空白後 1 到 120 字，且不可含控制字元。</summary>
    public static bool IsValidName(string? value) => value?.Trim() is { Length: >= 1 and <= 120 } trimmed && !trimmed.Any(char.IsControl);

    public static Result<string> Name(string value) => IsValidName(value) ? value.Trim() : CollaborationErrors.InvalidName;
}
