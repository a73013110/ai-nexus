using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Conversations;

public sealed record RenameConversationRequest(string Title);

internal sealed class RenameConversationRequestValidator : RequestValidator<RenameConversationRequest>
{
    public override string ProblemCode => ConversationsErrors.InvalidTitleCode;

    public RenameConversationRequestValidator() => RuleFor(x => x.Title).Must(ConversationQueries.TitleIsValid).WithErrorCode("length");
}

/// <summary>Renames one of the user's own conversations.</summary>
internal sealed class RenameConversation(NexusDbContext db, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPatch("/{id:guid}", async (Guid id, RenameConversationRequest body, ICurrentUser user, RenameConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.Title, ct)).ToHttpResult())
        .WithName("RenameConversation").Produces<ConversationDto>();

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, Guid id, string title, CancellationToken ct)
    {
        var conversation = await db.OwnedConversationAsync(owner, id, ct);
        if (conversation is null) return ConversationsErrors.NotFound;
        conversation.Title = title.Trim();
        conversation.UpdatedAt = clock.GetUtcNow();
        db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.renamed", ResourceId = id });
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
