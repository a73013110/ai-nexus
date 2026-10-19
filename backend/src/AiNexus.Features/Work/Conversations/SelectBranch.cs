using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Conversations;

[ValidatedInHandler("Every check needs the database: the leaf must be a message of this conversation.")]
public sealed record SelectBranchRequest(Guid LeafId);

/// <summary>切換到使用者自己對話的另一個分支（指定葉節點訊息）。只允許閒置對話，並在對話的生成鎖內執行。</summary>
internal sealed class SelectBranch(AppDbContext db, GenerationScheduler scheduler, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPatch("/{id:guid}/branch", async (
                Guid id, SelectBranchRequest body, ICurrentUser user, SelectBranch handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.LeafId, ct)).ToHttpResult())
        .WithName("SelectBranch");

    public async Task<Result> HandleAsync(Guid owner, Guid id, Guid leaf, CancellationToken ct)
    {
        using var conversationLock = await scheduler.LockConversationAsync(id, ct);
        var conversation = await db.OwnedConversationAsync(owner, id, ct);
        if (conversation is null) return ConversationsErrors.NotFound;
        if (await db.HasActiveRunAsync(id, ct)) return ConversationsErrors.GenerationActive;
        if (!await db.Set<Message>().AnyAsync(x => x.Id == leaf && x.ConversationId == id, ct)) return ConversationsErrors.MessageNotFound;
        conversation.ActiveLeafId = leaf;
        conversation.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
