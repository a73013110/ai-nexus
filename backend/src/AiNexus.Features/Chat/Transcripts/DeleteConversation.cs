using AiNexus.Features.Attachments;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat.Transcripts;

/// <summary>
/// 刪除對話的交易中發布的領域事件。
/// 訂閱者（例如 Sharing 模組撤銷分享）會在交易提交前同步執行。
/// </summary>
public sealed record ConversationDeleted(Guid ConversationId, Guid OwnerId) : IDomainEvent;

/// <summary>軟刪除使用者擁有的閒置對話，解除其附件連結，並把孤立的檔案標記為待清理。</summary>
/// <remarks>
/// 鎖的順序：對話生成鎖 → 附件寫入閘門，都在交易之前取得；交易內才鎖配額行。
/// 實體檔案的刪除放在交易與所有鎖釋放之後，避免磁碟 I/O 拖長交易。
/// </remarks>
internal sealed class DeleteConversation(
    NexusDbContext db,
    GenerationScheduler scheduler,
    AttachmentWriteLock attachmentWrites,
    DomainEvents events,
    AttachmentLifecycle lifecycle,
    AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, DeleteConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("DeleteConversation");

    public async Task<Result> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            await attachmentWrites.Gate.WaitAsync(ct);
            try
            {
                var conversation = await db.OwnedConversationAsync(owner, id, ct);
                if (conversation is null)
                {
                    return ConversationsErrors.NotFound;
                }

                if (await db.HasActiveRunAsync(id, ct))
                {
                    return ConversationsErrors.GenerationActive;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await quota.LockOwnerAsync(owner, ct);

                var links = await db.Set<MessageAttachment>()
                    .Where(x => db.Set<Message>().Any(m => m.Id == x.MessageId && m.ConversationId == id))
                    .ToListAsync(ct);

                var fileIds = links.Select(x => x.AttachmentId).Distinct().ToList();

                db.Set<MessageAttachment>().RemoveRange(links);
                conversation.IsDeleted = true;
                events.Raise(new ConversationDeleted(id, owner));

                db.AuditEvents.Add(new()
                {
                    OwnerId = owner,
                    Action = "conversation.deleted",
                    ResourceId = id
                });

                await db.SaveChangesAsync(ct);

                // 標記這些檔案已無引用（若無其他引用則列入待清理）
                await lifecycle.MarkUnusedAsync(fileIds, ct);
                await transaction.CommitAsync(ct);
            }
            finally
            {
                attachmentWrites.Gate.Release();
            }
        }
        finally
        {
            conversationLock.Dispose();
        }

        // 交易與鎖都釋放後才刪實體檔案，避免磁碟 I/O 拖長交易
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
