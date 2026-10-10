using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration.Users;

public sealed record AdminMessageDto(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, string? ModelId, IReadOnlyList<AttachmentDto> Attachments, RunTimingDto? Timing = null, string? ModelDisplayName = null);

public sealed record AdminConversationDetailDto(AdminConversationDto Conversation, string OwnerAccount, string OwnerName, string SystemInstruction, IReadOnlyList<AdminMessageDto> Messages, int Offset, int Total);

/// <summary>Any conversation's messages, 100 per page, with attachments and timings. The read is audited.</summary>
internal sealed class ReadAdminConversation(NexusDbContext db, AdministrativeReadAudit reads, ModelPresentation models)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/conversations/{id:guid}", async (Guid id, int? offset, ICurrentUser user, ReadAdminConversation handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, offset ?? 0, ct)).ToHttpResult())
        .WithName("ReadAdminConversation");

    public async Task<Result<AdminConversationDetailDto>> HandleAsync(Guid actor, Guid id, int offset, CancellationToken ct)
    {
        if (!await reads.AllowedAsync(actor, ct)) return AdministrationErrors.AdminRequired;
        if (!AdministrativeReadAudit.ValidPage(null, offset)) return AdministrationErrors.InvalidSearch;
        var value = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (value is null) return AdministrationErrors.NotFound;
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == value.OwnerId, ct);
        var query = db.Messages.AsNoTracking().Where(x => x.ConversationId == id);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(100).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var timings = await RunTiming.ReadAsync(db, rows.Where(x => x.RunId != null).Select(x => x.RunId!.Value).Distinct().ToArray(), ct);
        var attachments = await db.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.Size }).ToListAsync(ct);
        var messages = rows.Select(x => new AdminMessageDto(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.ModelId is { } model ? models.PublicId(model) : null,
            attachments.Where(a => a.MessageId == x.Id).Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, a.ContentType.StartsWith("image/", StringComparison.Ordinal), "reference")).ToArray(), x.RunId is Guid run ? timings.GetValueOrDefault(run) : null, models.DisplayName(x.ModelId))).ToArray();
        var audited = await reads.RecordAsync(actor, "admin.conversation_read", id, new { userId = owner.Id, conversationId = id, offset, count = messages.Length }, ct);
        return audited.IsSuccess
            ? new AdminConversationDetailDto(new(id, value.Title, value.CreatedAt, value.UpdatedAt, value.IsArchived, value.IsDeleted, total), owner.Account, owner.DisplayName, value.SystemInstruction, messages, offset, total)
            : audited.Error;
    }
}
