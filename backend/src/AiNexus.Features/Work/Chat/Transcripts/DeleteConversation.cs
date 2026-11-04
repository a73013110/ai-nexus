using AiNexus.Data.App;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Transcripts;

/// <summary>
/// 刪除對話的交易中發布的領域事件。
/// 訂閱者（例如 Sharing 模組撤銷分享）會在交易提交前同步執行。
/// </summary>
public sealed record ConversationDeleted(Guid ConversationId, Guid OwnerId) : IDomainEvent;

/// <summary>軟刪除使用者擁有的閒置對話，解除其附件連結，並把孤立的檔案標記為待清理。</summary>
/// <remarks>
/// 鎖的順序：先取對話生成鎖，交易內才鎖配額行。
/// 實體檔案的刪除放在交易與所有鎖釋放之後，避免磁碟 I/O 拖長交易。
/// </remarks>
internal sealed class DeleteConversation(
    AppDbContext db,
    GenerationScheduler scheduler,
    DomainEvents events,
    AttachmentLifecycle lifecycle,
    AttachmentQuota quota) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteConversation handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(ChatAudit.ConversationDeleted)
        .WithName("DeleteConversation");

    public async Task<Result> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        using (await scheduler.LockConversationAsync(id, ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await quota.LockOwnerAsync(owner, ct);

            // 列鎖之後才檢查：別的程序建立生成時也先鎖同一列，這裡才讀得到它剛提交的 run。
            var conversation = await db.OwnedConversationAsync(owner, id, ct);
            if (conversation is null)
            {
                return ConversationsErrors.NotFound;
            }

            if (await db.HasActiveRunAsync(id, ct))
            {
                return ConversationsErrors.GenerationActive;
            }

            var links = await db.MessageAttachments
                .Where(x => db.Messages.Any(m => m.Id == x.MessageId && m.ConversationId == id))
                .ToListAsync(ct);

            var fileIds = links.Select(x => x.AttachmentId).Distinct().ToList();

            db.MessageAttachments.RemoveRange(links);
            conversation.IsDeleted = true;
            events.Raise(new ConversationDeleted(id, owner));

            await db.SaveChangesAsync(ct);

            // 標記這些檔案已無引用（若無其他引用則列入待清理）
            await lifecycle.MarkUnusedAsync(fileIds, ct);
            await transaction.CommitAsync(ct);
        }

        // 交易與鎖都釋放後才刪實體檔案，避免磁碟 I/O 拖長交易
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
