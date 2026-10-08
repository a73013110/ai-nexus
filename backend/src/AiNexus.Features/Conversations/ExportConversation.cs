using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Conversations;

/// <summary>A version 1 backup of one of the user's own idle conversations; attachments are listed by name only.</summary>
internal static class ExportConversation
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/export", async (Guid id, ICurrentUser user, GetConversation conversations, CancellationToken ct) => (await HandleAsync(conversations, user.Id, id, ct)).ToHttpResult())
        .WithName("ExportConversation").Produces<ConversationBackup>();

    private static async Task<Result<ConversationBackup>> HandleAsync(GetConversation conversations, Guid owner, Guid id, CancellationToken ct)
    {
        var found = await conversations.HandleAsync(owner, id, ct);
        if (!found.IsSuccess) return found.Error;
        var detail = found.Value;
        if (detail.ActiveRun is not null) return ConversationErrors.GenerationActive;
        return new ConversationBackup(1, detail.Conversation.Title, detail.Conversation.SystemInstruction, detail.Conversation.Labels ?? [], detail.Conversation.ActiveLeafId,
            detail.Messages.Select(x => new BackupMessage(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.Attachments?.Select(f => f.FileName).ToList() ?? [])).ToList());
    }
}
