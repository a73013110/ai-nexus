using AiNexus.Data.App;
using AiNexus.Data.App.Billing;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Quality;
using AiNexus.Data.App.WebSearch;
using AiNexus.Features.Admin.Billing.Charges;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Features.Platform.WebSearch;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Knowledge.Retrieval;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Transcripts;

/// <summary>
/// 一段對話的所有訊息（含每個分支與重新生成的版本），組成前端共用的 <see cref="MessageDto"/>。
/// 擁有者開啟對話與管理員的稽核檢視都經過這裡，兩邊看到同樣的附件、引用、費用與網路來源。
/// </summary>
/// <remarks>
/// 每種關聯各用一次批次查詢取回，再在記憶體中組裝，避免逐則訊息查詢（N+1）。
/// 評分、費用與網路搜尋屬於對話擁有者，所以以 <see cref="Conversation.OwnerId"/> 過濾，不用檢視者。
/// 呼叫端負責授權；這裡不檢查誰在讀。
/// </remarks>
internal sealed class TranscriptReader(AppDbContext db, ModelPresentation presentation)
{
    public async Task<IReadOnlyList<MessageDto>> ReadAsync(Conversation conversation, CancellationToken ct)
    {
        var id = conversation.Id;
        var owner = conversation.OwnerId;
        var messages = await db.Set<Message>()
            .AsNoTracking()
            .Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        var messageIds = messages.Select(m => m.Id).ToList();

        var attachments = (await db.Set<MessageAttachment>()
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
            .ToListAsync(ct))
            .ToLookup(
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

        return messages.Select(x =>
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
    }
}
