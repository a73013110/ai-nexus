using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality;

/// <summary>The user's own ratings: the one on an answer (none yet is an empty 200), and the latest 100 across conversations.</summary>
internal static class ReadFeedback
{
    public static RouteHandlerBuilder MapForMessage(RouteGroupBuilder api) => api
        .MapGet("/messages/{id:guid}/feedback", async (Guid id, ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
        {
            var actor = user.Id;
            if (!await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == id && m.Role == "assistant" && c.OwnerId == actor select m.Id).AnyAsync(ct))
                return QualityErrors.ItemMissing.ToProblem();
            return Results.Ok(await (from f in db.Set<MessageFeedback>() join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
                where f.MessageId == id && f.OwnerId == actor select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).SingleOrDefaultAsync(ct));
        })
        .RequireAuthorization(Policies.Chat).WithTags("Quality").Produces<FeedbackDto>();

    public static RouteHandlerBuilder MapList(RouteGroupBuilder routes) => routes
        .MapGet("/feedback", async (ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
        {
            var actor = user.Id;
            return Results.Ok(await (from f in db.Set<MessageFeedback>() join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
                where f.OwnerId == actor orderby f.UpdatedAt descending
                select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).Take(100).ToListAsync(ct));
        })
        .Produces<IReadOnlyList<FeedbackDto>>();
}
