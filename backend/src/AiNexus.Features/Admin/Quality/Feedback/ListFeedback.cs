using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Feedback;

/// <summary>使用者自己跨對話最近 100 筆評分，給品質評測頁。</summary>
internal sealed class ListFeedback(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/feedback", (ICurrentUser user, ListFeedback handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct)).Audited(QualityAudit.FeedbackRead);

    public async Task<IReadOnlyList<FeedbackDto>> HandleAsync(Guid actor, CancellationToken ct)
        => await (from f in db.MessageFeedback join m in db.Messages on f.MessageId equals m.Id join c in db.Conversations on m.ConversationId equals c.Id
            where f.OwnerId == actor orderby f.UpdatedAt descending
            select new FeedbackDto(f.MessageId, c.Id, c.Title, f.Rating, f.Reason, f.Note, f.UpdatedAt)).Take(100).ToListAsync(ct);
}
