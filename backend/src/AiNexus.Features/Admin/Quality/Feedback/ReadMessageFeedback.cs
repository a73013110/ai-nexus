using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Feedback;

/// <summary>使用者對自己對話中單則回答的評分；尚未評分時回 200 空內容。</summary>
internal sealed class ReadMessageFeedback(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/feedback", (Guid id, ICurrentUser user, ReadMessageFeedback handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync(found => TypedResults.Ok(found.Feedback)))
        .NotAudited("使用者自己對回覆的回饋");

    public async Task<Result<FeedbackOutcome>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == id && m.Role == "assistant" && c.OwnerId == actor select m.Id).AnyAsync(ct))
            return QualityErrors.ItemMissing;
        return new FeedbackOutcome(await (from f in db.MessageFeedback join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
            where f.MessageId == id && f.OwnerId == actor select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).SingleOrDefaultAsync(ct));
    }
}
