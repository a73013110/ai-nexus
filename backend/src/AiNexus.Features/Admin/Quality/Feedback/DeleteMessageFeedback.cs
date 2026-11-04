using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Feedback;

/// <summary>移除使用者對自己回答的評分；本來就沒有評分也算成功，重按或重送結果一樣。</summary>
internal sealed class DeleteMessageFeedback(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}/feedback", (Guid id, ICurrentUser user, DeleteMessageFeedback handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(QualityAudit.FeedbackRemoved);

    public async Task<Result> HandleAsync(Guid actor, Guid message, CancellationToken ct)
    {
        if (!await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == message && m.Role == "assistant" && c.OwnerId == actor select m.Id).AnyAsync(ct))
            return QualityErrors.ItemMissing;
        // 與儲存排同一個鎖：儲存讀到列後若被這裡刪掉，它的 UPDATE 會影響 0 列而失敗。
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockUserRowAsync(actor, ct);
        await db.MessageFeedback.Where(x => x.MessageId == message && x.OwnerId == actor).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
