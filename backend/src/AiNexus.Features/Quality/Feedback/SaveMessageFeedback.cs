using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Quality.Feedback;

/// <summary>評分 1 或 -1 即儲存；0 則清除。</summary>
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

/// <summary>為使用者擁有之對話中已完成的 AI 回答評分；不複製回答內容。</summary>
internal sealed class SaveMessageFeedback(NexusDbContext db, ResourceWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapPut("/messages/{id:guid}/feedback", (Guid id, FeedbackRequest body, ICurrentUser user, SaveMessageFeedback handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct).ToHttpResultAsync(saved => TypedResults.Ok(saved.Feedback)))
        .RequireAuthorization(Policies.Chat).WithTags("Quality");

    public async Task<Result<FeedbackOutcome>> HandleAsync(Guid actor, Guid message, FeedbackRequest request, CancellationToken ct)
    {
        var source = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == message && m.Role == "assistant" && c.OwnerId == actor select new { Message = m, Conversation = c }).SingleOrDefaultAsync(ct);
        if (source is null) return QualityErrors.ItemMissing;
        if (RunStates.IsActive(source.Message.Status)) return QualityErrors.AnswerPending;
        // 每則訊息只能有一筆：同一回答並行儲存時，不可讓兩個請求都 INSERT。
        using (await writes.AcquireAsync("feedback", message, ct))
        {
            var row = await db.Set<MessageFeedback>().FindAsync([message], ct);
            if (request.Rating == 0) { if (row is not null) db.Remove(row); await db.SaveChangesAsync(ct); return new FeedbackOutcome(null); }
            if (row is null) { row = new() { MessageId = message, OwnerId = actor }; db.Add(row); }
            row.Rating = request.Rating; row.Reason = request.Reason; row.Note = request.Note.Trim(); row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return new FeedbackOutcome(new(message, source.Conversation.Id, source.Conversation.Title, row.Rating, row.Reason, row.Note, row.UpdatedAt));
        }
    }
}
