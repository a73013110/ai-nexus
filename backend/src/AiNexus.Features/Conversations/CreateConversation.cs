using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Conversations;

public sealed record CreateConversationRequest(string? Title = null);

/// <summary>A missing title becomes 「新對話」; a given one must be 1 to 120 characters once trimmed.</summary>
internal sealed class CreateConversationRequestValidator : RequestValidator<CreateConversationRequest>
{
    public override string ProblemCode => ConversationsErrors.InvalidTitleCode;

    public CreateConversationRequestValidator()
        => RuleFor(x => x.Title).Must(x => x is null || ConversationQueries.TitleIsValid(x)).WithErrorCode("length");
}

/// <summary>Starts an empty conversation owned by the user. Other modules reach it through <see cref="ConversationService.CreateAsync"/>.</summary>
internal sealed class CreateConversation(NexusDbContext db, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CreateConversationRequest body, ICurrentUser user, CreateConversation handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, body.Title, ct).ToHttpResultAsync(created => TypedResults.Created($"/api/v1/conversations/{created.Id}", created)))
        .WithName("CreateConversation");

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, string? title, CancellationToken ct)
    {
        title = (title ?? "新對話").Trim();
        if (!ConversationQueries.TitleIsValid(title)) return ConversationsErrors.InvalidTitle;
        var now = clock.GetUtcNow();
        var conversation = new Conversation { OwnerId = owner, Title = title, CreatedAt = now, UpdatedAt = now };
        db.Set<Conversation>().Add(conversation);
        db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.created", ResourceId = conversation.Id });
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
