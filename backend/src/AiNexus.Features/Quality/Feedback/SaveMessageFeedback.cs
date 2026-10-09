using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality.Feedback;

/// <summary>Rating 1 or -1 saves the feedback; 0 clears it.</summary>
public sealed record FeedbackRequest(int Rating, string Reason = "", string Note = "");

internal sealed class FeedbackRequestValidator : RequestValidator<FeedbackRequest>
{
    public override string ProblemCode => "feedback_invalid";

    public FeedbackRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(-1, 1);
        RuleFor(x => x.Reason).Must(x => x is "" or "incorrect" or "citation" or "incomplete" or "format" or "other").WithErrorCode("unknown");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

/// <summary>Rates a finished assistant answer in a conversation the user owns. The answer text is never copied.</summary>
internal sealed class SaveMessageFeedback(NexusDbContext db, ResourceWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapPut("/messages/{id:guid}/feedback", async (Guid id, FeedbackRequest body, ICurrentUser user, SaveMessageFeedback handler, CancellationToken ct) =>
        {
            var saved = await handler.HandleAsync(user.Id, id, body, ct);
            // A cleared rating answers 200 without a body.
            return saved.IsSuccess ? Results.Ok(saved.Value.Feedback) : saved.Error.ToProblem();
        })
        .RequireAuthorization(Policies.Chat).WithTags("Quality").Produces<FeedbackDto>();

    /// <summary><c>Feedback</c> is null when the rating was cleared.</summary>
    public sealed record Outcome(FeedbackDto? Feedback);

    public async Task<Result<Outcome>> HandleAsync(Guid actor, Guid message, FeedbackRequest request, CancellationToken ct)
    {
        var source = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == message && m.Role == "assistant" && c.OwnerId == actor select new { Message = m, Conversation = c }).SingleOrDefaultAsync(ct);
        if (source is null) return QualityErrors.ItemMissing;
        if (RunStates.IsActive(source.Message.Status)) return QualityErrors.AnswerPending;
        // One row per message: concurrent saves of the same answer must not both insert.
        using (await writes.AcquireAsync("feedback", message, ct))
        {
            var row = await db.Set<MessageFeedback>().FindAsync([message], ct);
            if (request.Rating == 0) { if (row is not null) db.Remove(row); await db.SaveChangesAsync(ct); return new Outcome(null); }
            if (row is null) { row = new() { MessageId = message, OwnerId = actor }; db.Add(row); }
            row.Rating = request.Rating; row.Reason = request.Reason; row.Note = request.Note.Trim(); row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return new Outcome(new(message, source.Conversation.Id, source.Conversation.Title, row.Rating, row.Reason, row.Note, row.UpdatedAt));
        }
    }
}
