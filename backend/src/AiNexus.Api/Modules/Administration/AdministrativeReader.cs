using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Administration;

public sealed record AdminUserDetailDto(AdminUserDto User, PersonalUsageDto Usage, IReadOnlyList<UsageKindDto> Kinds, int Conversations);
public sealed record AdminConversationDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsArchived, bool IsDeleted, int Messages);
public sealed record AdminConversationPageDto(IReadOnlyList<AdminConversationDto> Items, int Total, int Offset);
public sealed record AdminMessageDto(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, string? ModelId, IReadOnlyList<AttachmentDto> Attachments);
public sealed record AdminConversationDetailDto(AdminConversationDto Conversation, string OwnerAccount, string OwnerName, string SystemInstruction, IReadOnlyList<AdminMessageDto> Messages, int Offset, int Total);

/// <summary>Explicit, read-only administrative access. Never weakens conversation owner checks.</summary>
public sealed class AdministrativeReader(NexusDbContext db, CurrentUser current, AccessService access, UsageReports usage, ModelPresentation models)
{
    public async Task<AdminUserDetailDto> UserAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        var roles = await db.Set<UserRole>().Where(x => x.UserId == id).Select(x => x.RoleId).ToListAsync(ct);
        var detail = new AdminUserDetailDto(new(id, user.Account, user.DisplayName, user.LastSeenAt, roles), await usage.ForOwnerAsync(id, ct), await usage.KindsAsync(id, ct), await db.Conversations.CountAsync(x => x.OwnerId == id, ct));
        await AuditAsync("admin.user_usage_read", id, new { userId = id }, ct);
        return detail;
    }
    public async Task<AdminConversationPageDto> ConversationsAsync(Guid owner, string? search, int offset, bool includeDeleted, CancellationToken ct)
    {
        await RequireAsync(ct); Validate(search, offset);
        if (!await db.Users.AnyAsync(x => x.Id == owner, ct)) throw Missing();
        var query = db.Conversations.AsNoTracking().Where(x => x.OwnerId == owner && (includeDeleted || !x.IsDeleted));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search) || db.Messages.Any(m => m.ConversationId == x.Id && m.Content.Contains(search)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(offset).Take(50)
            .Select(x => new AdminConversationDto(x.Id, x.Title, x.CreatedAt, x.UpdatedAt, x.IsArchived, x.IsDeleted, db.Messages.Count(m => m.ConversationId == x.Id))).ToListAsync(ct);
        await AuditAsync("admin.conversations_list", owner, new { userId = owner, offset, includeDeleted, searchApplied = !string.IsNullOrWhiteSpace(search), count = rows.Count }, ct);
        return new(rows, total, offset);
    }
    public async Task<AdminConversationDetailDto> ConversationAsync(Guid id, int offset, CancellationToken ct)
    {
        await RequireAsync(ct); Validate(null, offset);
        var value = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == value.OwnerId, ct);
        var query = db.Messages.AsNoTracking().Where(x => x.ConversationId == id);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var attachments = await db.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.Size }).ToListAsync(ct);
        var messages = rows.Select(x => new AdminMessageDto(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.ModelId is { } model ? models.PublicId(model) : null,
            attachments.Where(a => a.MessageId == x.Id).Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, a.ContentType.StartsWith("image/"), "reference")).ToArray())).ToArray();
        await AuditAsync("admin.conversation_read", id, new { userId = owner.Id, conversationId = id, offset, count = messages.Length }, ct);
        return new(new(id, value.Title, value.CreatedAt, value.UpdatedAt, value.IsArchived, value.IsDeleted, total), owner.Account, owner.DisplayName, value.SystemInstruction, messages, offset, total);
    }
    private async Task RequireAsync(CancellationToken ct)
    {
        if (!(await access.ForUserAsync((await current.GetAsync(ct)).Id, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature))
            throw new ApiException(403, "admin_required", "需要平台管理權限。");
    }
    private async Task AuditAsync(string action, Guid resource, object details, CancellationToken ct)
    {
        await RequireAsync(ct);
        db.AuditEvents.Add(new() { OwnerId = (await current.GetAsync(ct)).Id, ResourceId = resource, Action = action, Result = "read", DetailsJson = JsonSerializer.Serialize(details) });
        // The response is released only after its sensitive read is audited successfully.
        await db.SaveChangesAsync(ct);
    }
    private static void Validate(string? search, int offset) { if (search?.Length > 120 || offset < 0) throw new ApiException(400, "invalid_search", "搜尋條件不正確。"); }
    private static ApiException Missing() => new(404, "admin_resource_not_found", "找不到此管理項目。");
}
