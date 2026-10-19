using AiNexus.Data.App;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Feedback;

/// <summary>使用者自己的評分：單則回答的評分（尚未評分時回 200 空內容），以及跨對話最近 100 筆。</summary>
internal sealed class ReadFeedback(AppDbContext db)
{
    public static RouteHandlerBuilder MapForMessage(RouteGroupBuilder api) => api
        .MapGet("/messages/{id:guid}/feedback", (Guid id, ICurrentUser user, ReadFeedback handler, CancellationToken ct) =>
            handler.ForMessageAsync(user.Id, id, ct).ToHttpResultAsync(found => TypedResults.Ok(found.Feedback)))
        .RequireAuthorization(Policies.Chat);

    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("/feedback", async (ICurrentUser user, ReadFeedback handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(user.Id, ct)));

    public async Task<Result<FeedbackOutcome>> ForMessageAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == id && m.Role == "assistant" && c.OwnerId == actor select m.Id).AnyAsync(ct))
            return QualityErrors.ItemMissing;
        return new FeedbackOutcome(await (from f in db.Set<MessageFeedback>() join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
            where f.MessageId == id && f.OwnerId == actor select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).SingleOrDefaultAsync(ct));
    }

    public async Task<IReadOnlyList<FeedbackDto>> ListAsync(Guid actor, CancellationToken ct)
        => await (from f in db.Set<MessageFeedback>() join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
            where f.OwnerId == actor orderby f.UpdatedAt descending
            select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).Take(100).ToListAsync(ct);
}
