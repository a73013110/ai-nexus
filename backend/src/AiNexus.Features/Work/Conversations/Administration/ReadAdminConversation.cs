using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Conversations.Administration;

public sealed record AdminMessageDto(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, string? ModelId, IReadOnlyList<AttachmentDto> Attachments, RunTimingDto? Timing = null, string? ModelDisplayName = null);

public sealed record AdminConversationDetailDto(AdminConversationDto Conversation, string OwnerAccount, string OwnerName, string SystemInstruction, IReadOnlyList<AdminMessageDto> Messages, int Offset, int Total);

/// <summary>任一對話的訊息，每頁 100 筆，附附件與執行耗時；讀取會稽核。</summary>
internal sealed class ReadAdminConversation(AppDbContext db, AccessService access, AuditTrail audit, ModelPresentation models)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/conversations/{id:guid}", async (Guid id, int? offset, ICurrentUser user, ReadAdminConversation handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, offset ?? 0, ct)).ToHttpResult())
        .Audited(ConversationsAudit.ConversationContentRead)
        .WithName("ReadAdminConversation");

    public async Task<Result<AdminConversationDetailDto>> HandleAsync(Guid actor, Guid id, int offset, CancellationToken ct)
    {
        if (!await access.HoldsAsync(actor, FeatureCatalog.IdentityUsers, ct)) return AccessControlErrors.FeatureForbidden;
        if (!ListAdminUserConversations.ValidPage(null, offset)) return ConversationsErrors.InvalidSearch;
        var value = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null) return ConversationsErrors.AdminResourceNotFound;
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == value.OwnerId, ct);
        var query = db.Messages.AsNoTracking().Where(x => x.ConversationId == id);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var timings = await RunTiming.ReadAsync(db, rows.Where(x => x.RunId != null).Select(x => x.RunId!.Value).Distinct().ToArray(), ct);
        var attachments = (await db.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.Size }).ToListAsync(ct))
            .ToLookup(x => x.MessageId, x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.Size, x.ContentType.StartsWith("image/", StringComparison.Ordinal), "reference"));
        var messages = rows.Select(x => new AdminMessageDto(
            x.Id,
            x.ParentId,
            x.Role,
            x.Content,
            x.Status,
            x.CreatedAt,
            x.ModelId is { } model ? models.PublicId(model) : null,
            attachments[x.Id].ToArray(),
            x.RunId is Guid run ? timings.GetValueOrDefault(run) : null,
            models.DisplayName(x.ModelId))).ToArray();
        audit.Describe(subject: owner.Id, details: new { count = messages.Length });
        var conversation = new AdminConversationDto(id, value.Title, value.CreatedAt, value.UpdatedAt, value.IsArchived, value.IsDeleted, total);
        return new AdminConversationDetailDto(conversation, owner.AdAccount ?? owner.LocalAccount ?? "", owner.DisplayName, value.SystemInstruction, messages, offset, total);
    }
}
