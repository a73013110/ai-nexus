using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Billing;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Quality;
using AiNexus.Data.App.WebSearch;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Admin.Billing.Charges;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Features.Platform.WebSearch;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Transcripts;

/// <summary>
/// 對話完整明細傳輸物件，包含對話本體、所有訊息樹（含附件與引文等豐富資訊）以及可能正在進行中的背景生成 Run。
/// </summary>
public sealed record ConversationDetailDto(
    ConversationDto Conversation,
    IReadOnlyList<MessageDto> Messages,
    RunDto? ActiveRun);

/// <summary>
/// 取得使用者指定的單一對話完整明細。
/// </summary>
/// <remarks>
/// 每種關聯各用一次批次查詢取回，再在記憶體中組裝，避免逐則訊息查詢（N+1）。
/// 模型 ID 經 <see cref="ModelPresentation"/> 脫敏後才輸出給前端。
/// </remarks>
internal sealed class GetConversation(AppDbContext db, ModelPresentation presentation)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, GetConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .Audited(ChatAudit.ConversationRead)
        .WithName("GetConversation");

    public async Task<Result<ConversationDetailDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var conversation = await db.OwnedConversationAsync(owner, id, ct);
        if (conversation is null)
        {
            return ConversationsErrors.NotFound;
        }

        var messages = await db.Set<Message>()
            .AsNoTracking()
            .Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var run = await db.Set<GenerationRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ConversationId == id && x.ActiveOwnerId == owner, ct);

        var messageIds = messages.Select(m => m.Id).ToList();

        var links = await db.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(x => messageIds.Contains(x.MessageId))
            .Select(x => new
            {
                x.MessageId,
                x.Attachment.Id,
                x.Attachment.FileName,
                x.Attachment.ContentType,
                x.Attachment.Size,
                HasText = x.Attachment.ExtractedText != null
            })
            .ToListAsync(ct);

        var attachments = links.ToLookup(
            x => x.MessageId,
            x => new AttachmentDto(
                x.Id,
                x.FileName,
                x.ContentType,
                x.Size,
                x.ContentType.StartsWith("image/", StringComparison.Ordinal),
                x.HasText ? "extracted-text" : "vision"));

        var citations = (await db.Set<MessageCitation>()
            .AsNoTracking()
            .Where(x => messageIds.Contains(x.MessageId))
            .OrderBy(x => x.Number)
            .ToListAsync(ct))
            .ToLookup(x => x.MessageId, x => new CitationDto(x.Number, x.DocumentId, x.Title, x.PageNumber, x.Excerpt, x.EndPage));

        var ratings = await db.Set<MessageFeedback>()
            .AsNoTracking()
            .Where(x => x.OwnerId == owner && messageIds.Contains(x.MessageId))
            .ToDictionaryAsync(x => x.MessageId, x => x.Rating, ct);

        var charges = await db.Set<ModelCharge>()
            .AsNoTracking()
            .Where(x => x.OwnerId == owner && x.ConversationId == id)
            .ToDictionaryAsync(x => x.Id, ct);

        var searches = await db.Set<WebSearchRecord>()
            .AsNoTracking()
            .Where(x => x.OwnerId == owner && x.ConversationId == id && x.RunId != null)
            .ToDictionaryAsync(x => x.RunId!.Value, ct);

        var runIds = messages.Where(x => x.RunId != null).Select(x => x.RunId!.Value).Distinct().ToArray();
        var timings = await RunTiming.ReadAsync(db, runIds, ct);

        var messageDtos = messages.Select(x =>
        {
            var search = x.RunId is Guid runId ? searches.GetValueOrDefault(runId) : null;
            return presentation.Message(x) with
            {
                Timing = x.RunId is Guid timingId ? timings.GetValueOrDefault(timingId) : null,
                Attachments = attachments[x.Id].ToList(),
                Sources = citations[x.Id].ToList(),
                FeedbackRating = ratings.GetValueOrDefault(x.Id),
                Charge = x.RunId is Guid call && charges.TryGetValue(call, out var charge)
                    ? ChargeCalculator.Describe(charge)
                    : null,
                WebSources = search is null ? null : WebSearchService.Sources(search),
                WebSearchCharge = search is not null && charges.TryGetValue(search.Id, out var webCharge)
                    ? ChargeCalculator.Describe(webCharge)
                    : null
            };
        }).ToList();

        return new ConversationDetailDto(
            Conversation: conversation.ToDto(),
            Messages: messageDtos,
            ActiveRun: run is null ? null : presentation.Run(run));
    }
}
