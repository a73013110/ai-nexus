using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Work.Conversations;

public sealed record RenameConversationRequest(string Title);

internal sealed class RenameConversationRequestValidator : RequestValidator<RenameConversationRequest>
{
    public override Error Problem => ConversationsErrors.InvalidTitle;

    public RenameConversationRequestValidator() => RuleFor(x => x.Title).RequiredText(Conversation.TitleMaxLength);
}

/// <summary>重新命名使用者自己的對話。</summary>
internal sealed class RenameConversation(AppDbContext db, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPatch("/{id:guid}", (
                Guid id, RenameConversationRequest body, ICurrentUser user, RenameConversation handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, body.Title, ct))
        .Audited(ConversationsAudit.ConversationRenamed)
        .WithName("RenameConversation");

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, Guid id, string title, CancellationToken ct)
    {
        var conversation = await db.OwnedConversationAsync(owner, id, ct);
        if (conversation is null) return ConversationsErrors.NotFound;
        conversation.Title = title.Trim();
        conversation.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
