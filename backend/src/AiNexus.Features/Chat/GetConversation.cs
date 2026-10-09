using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Features.Chat;

public sealed record ConversationDetailDto(ConversationDto Conversation, IReadOnlyList<MessageDto> Messages, RunDto? ActiveRun);

/// <summary>
/// One of the user's conversations with every message (attachments, citations, the user's own ratings, charges, web
/// sources and timings) and the user's active run, if any.
/// </summary>
internal sealed class GetConversation(NexusDbContext db, ModelPresentation presentation)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, GetConversation handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("GetConversation").Produces<ConversationDetailDto>();

    public async Task<Result<ConversationDetailDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var conversation = await db.OwnedConversationAsync(owner, id, ct);
        if (conversation is null) return ConversationsErrors.NotFound;
        var messages = await db.Set<Message>().AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var run = await db.Set<GenerationRun>().AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == id && x.ActiveOwnerId == owner, ct);
        var links = await db.Set<MessageAttachment>().AsNoTracking().Where(x => messages.Select(m => m.Id).Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.Size, HasText = x.Attachment.ExtractedText != null }).ToListAsync(ct);
        var attachments = links.ToLookup(x => x.MessageId, x => new AttachmentDto(x.Id, x.FileName, x.ContentType, x.Size, x.ContentType.StartsWith("image/", StringComparison.Ordinal), x.HasText ? "extracted-text" : "vision"));
        var citations = (await db.Set<AiNexus.Features.Knowledge.Retrieval.MessageCitation>().AsNoTracking().Where(x => messages.Select(m => m.Id).Contains(x.MessageId)).OrderBy(x => x.Number).ToListAsync(ct))
            .ToLookup(x => x.MessageId, x => new AiNexus.Features.Knowledge.Retrieval.CitationDto(x.Number, x.DocumentId, x.Title, x.PageNumber, x.Excerpt, x.EndPage));
        var ratings = await db.Set<AiNexus.Features.Quality.Feedback.MessageFeedback>().AsNoTracking().Where(x => x.OwnerId == owner && messages.Select(m => m.Id).Contains(x.MessageId)).ToDictionaryAsync(x => x.MessageId, x => x.Rating, ct);
        var charges = await db.Set<AiNexus.Features.Billing.ModelCharge>().AsNoTracking().Where(x => x.OwnerId == owner && x.ConversationId == id).ToDictionaryAsync(x => x.Id, ct);
        var searches = await db.Set<AiNexus.Features.WebSearch.WebSearchRecord>().AsNoTracking().Where(x => x.OwnerId == owner && x.ConversationId == id && x.RunId != null).ToDictionaryAsync(x => x.RunId!.Value, ct);
        var timings = await RunTiming.ReadAsync(db, messages.Where(x => x.RunId != null).Select(x => x.RunId!.Value).Distinct().ToArray(), ct);
        return new ConversationDetailDto(conversation.ToDto(), messages.Select(x =>
        {
            var search = x.RunId is Guid runId ? searches.GetValueOrDefault(runId) : null;
            return presentation.Message(x) with { Timing = x.RunId is Guid timingId ? timings.GetValueOrDefault(timingId) : null, Attachments = attachments[x.Id].ToList(), Sources = citations[x.Id].ToList(), FeedbackRating = ratings.GetValueOrDefault(x.Id),
                Charge = x.RunId is Guid call && charges.TryGetValue(call, out var charge) ? AiNexus.Features.Billing.ChargeCalculator.Describe(charge) : null,
                WebSources = search is null ? null : AiNexus.Features.WebSearch.WebSearchService.Sources(search),
                WebSearchCharge = search is not null && charges.TryGetValue(search.Id, out var webCharge) ? AiNexus.Features.Billing.ChargeCalculator.Describe(webCharge) : null };
        }).ToList(), run is null ? null : presentation.Run(run));
    }
}
