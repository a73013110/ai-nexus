using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Chat;

/// <summary>
/// Copies one of the user's own idle conversations with its whole message tree, labels and attachment links. Runs under
/// the conversation's generation lock, in a transaction that holds the owner's attachment quota lock.
/// </summary>
internal sealed class DuplicateConversation(NexusDbContext db, GenerationScheduler scheduler, AttachmentQuota quota, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/duplicate", async (Guid id, ICurrentUser user, DuplicateConversation handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("DuplicateConversation");

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(owner, ct);
            var original = await db.OwnedConversationAsync(owner, id, ct);
            if (original is null) return ConversationsErrors.NotFound;
            if (await db.HasActiveRunAsync(id, ct)) return ConversationsErrors.GenerationActive;
            var messages = await db.Set<Message>().AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
            var ids = messages.ToDictionary(x => x.Id, _ => Guid.NewGuid());
            var now = clock.GetUtcNow();
            var clone = new Conversation { OwnerId = owner, Title = original.Title[..Math.Min(original.Title.Length, 115)] + " · 副本", SystemInstruction = original.SystemInstruction, ActiveLeafId = original.ActiveLeafId is Guid leaf ? ids[leaf] : null, CreatedAt = now, UpdatedAt = now };
            clone.Labels = original.Labels.Select(x => new ConversationLabel { ConversationId = clone.Id, Name = x.Name }).ToList();
            db.Set<Conversation>().Add(clone);
            foreach (var message in messages) db.Set<Message>().Add(new() { Id = ids[message.Id], ConversationId = clone.Id, ParentId = message.ParentId is Guid parent ? ids[parent] : null, Role = message.Role, Content = message.Content, Status = message.Status, ModelId = message.ModelId, ErrorCode = message.ErrorCode, IssueCode = message.IssueCode, CreatedAt = message.CreatedAt });
            var links = await db.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Keys.Contains(x.MessageId)).ToListAsync(ct);
            foreach (var link in links) db.Set<MessageAttachment>().Add(new() { MessageId = ids[link.MessageId], AttachmentId = link.AttachmentId });
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.duplicated", ResourceId = clone.Id });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return clone.ToDto();
        }
        finally { conversationLock.Dispose(); }
    }
}
