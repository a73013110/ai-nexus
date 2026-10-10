using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Conversations;

namespace AiNexus.Features.Chat;

/// <summary>A version 1 backup of one of the user's own idle conversations; attachments are listed by name only.</summary>
internal sealed class ExportConversation(GetConversation conversations)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/export", (Guid id, ICurrentUser user, ExportConversation handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ExportConversation");

    public async Task<Result<ConversationBackup>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var found = await conversations.HandleAsync(owner, id, ct);
        if (!found.IsSuccess) return found.Error;
        var detail = found.Value;
        if (detail.ActiveRun is not null) return ConversationsErrors.GenerationActive;
        return new ConversationBackup(1, detail.Conversation.Title, detail.Conversation.SystemInstruction, detail.Conversation.Labels ?? [], detail.Conversation.ActiveLeafId,
            detail.Messages.Select(x => new BackupMessage(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.Attachments?.Select(f => f.FileName).ToList() ?? [])).ToList());
    }
}
