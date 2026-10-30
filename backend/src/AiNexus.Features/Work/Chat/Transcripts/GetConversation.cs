using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Runs;
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
/// 取得使用者指定的單一對話完整明細；訊息由 <see cref="TranscriptReader"/> 組裝。
/// </summary>
internal sealed class GetConversation(AppDbContext db, TranscriptReader transcripts, ModelPresentation presentation)
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

        var run = await db.Set<GenerationRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ConversationId == id && x.ActiveOwnerId == owner, ct);

        return new ConversationDetailDto(
            Conversation: conversation.ToDto(),
            Messages: await transcripts.ReadAsync(conversation, ct),
            ActiveRun: run is null ? null : presentation.Run(run));
    }
}
