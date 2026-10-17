using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Chat.Transcripts;

/// <summary>
/// 匯出使用者擁有的閒置對話備份（版本 1 格式）。
/// </summary>
/// <remarks>
/// 有生成中的 Run 時拒絕匯出（回傳 <see cref="ConversationsErrors.GenerationActive"/>），避免備份到不完整的回答。
/// 附件只記檔名，不含二進位內容。
/// </remarks>
internal sealed class ExportConversation(GetConversation conversations)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/export", (Guid id, ICurrentUser user, ExportConversation handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ExportConversation");

    public async Task<Result<ConversationBackup>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var found = await conversations.HandleAsync(owner, id, ct);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var detail = found.Value;

        if (detail.ActiveRun is not null)
        {
            return ConversationsErrors.GenerationActive;
        }

        return new ConversationBackup(
            Version: 1,
            Title: detail.Conversation.Title,
            SystemInstruction: detail.Conversation.SystemInstruction,
            Labels: detail.Conversation.Labels ?? [],
            ActiveLeafId: detail.Conversation.ActiveLeafId,
            Messages: detail.Messages.Select(x => new BackupMessage(
                Id: x.Id,
                ParentId: x.ParentId,
                Role: x.Role,
                Content: x.Content,
                Status: x.Status,
                CreatedAt: x.CreatedAt,
                AttachmentNames: x.Attachments?.Select(f => f.FileName).ToList() ?? []
            )).ToList()
        );
    }
}
