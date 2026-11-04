using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Features.Admin.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Feedback;

/// <summary>評分 1（有幫助）或 -1（待改善）；移除評分走 <c>DELETE</c>。</summary>
public sealed record FeedbackRequest(int Rating, string Reason = "", string Note = "");

internal sealed class FeedbackRequestValidator : RequestValidator<FeedbackRequest>
{
    public override Error Problem => QualityErrors.FeedbackInvalid;

    public FeedbackRequestValidator()
    {
        RuleFor(x => x.Rating).Must(x => x is 1 or -1).WithErrorCode(RuleCode.Unknown);
        RuleFor(x => x.Reason).Must(x => x is "" or "incorrect" or "citation" or "incomplete" or "format" or "other").WithErrorCode(RuleCode.Unknown);
        RuleFor(x => x.Note).OptionalContent(2000);
    }
}

/// <summary>為使用者擁有之對話中已完成的 AI 回答評分；不複製回答內容。</summary>
internal sealed class SaveMessageFeedback(AppDbContext db, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/feedback", (Guid id, FeedbackRequest body, ICurrentUser user, SaveMessageFeedback handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body, ct))
        .Audited(QualityAudit.FeedbackUpdated);

    public async Task<Result<FeedbackDto>> HandleAsync(Guid actor, Guid message, FeedbackRequest request, CancellationToken ct)
    {
        var source = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id where m.Id == message && m.Role == "assistant" && c.OwnerId == actor select new { Message = m, Conversation = c }).SingleOrDefaultAsync(ct);
        if (source is null) return QualityErrors.ItemMissing;
        if (RunStates.IsActive(source.Message.Status)) return QualityErrors.AnswerPending;
        // 每則訊息只能有一筆：同一回答並行儲存時，不可讓兩個請求都 INSERT。訊息一定屬於這位使用者，所以鎖他的列就能排隊。
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockUserRowAsync(actor, ct);
        var row = await db.MessageFeedback.FindAsync([message], ct);
        if (row is null) { row = new() { MessageId = message, OwnerId = actor }; db.Add(row); }
        row.Rating = request.Rating; row.Reason = request.Reason; row.Note = request.Note.Trim(); row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new FeedbackDto(message, source.Conversation.Id, source.Conversation.Title, row.Rating, row.Reason, row.Note, row.UpdatedAt);
    }
}
