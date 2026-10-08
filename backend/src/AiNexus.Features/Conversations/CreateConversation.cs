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
    public override string ProblemCode => ConversationErrors.InvalidTitleCode;

    public CreateConversationRequestValidator()
        => RuleFor(x => x.Title).Must(x => x is null || ConversationQueries.TitleIsValid(x)).WithErrorCode("length");
}

/// <summary>Starts an empty conversation owned by the user. Other modules reach it through <see cref="ConversationService.CreateAsync"/>.</summary>
internal static class CreateConversation
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", async (CreateConversationRequest body, ICurrentUser user, NexusDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var created = await HandleAsync(db, clock, user.Id, body.Title, ct);
            return created.IsSuccess ? Results.Created($"/api/v1/conversations/{created.Value.Id}", created.Value) : created.Error.ToProblem();
        })
        .WithName("CreateConversation").Produces<ConversationDto>(201);

    public static async Task<Result<ConversationDto>> HandleAsync(NexusDbContext db, TimeProvider clock, Guid owner, string? title, CancellationToken ct)
    {
        title = (title ?? "新對話").Trim();
        if (!ConversationQueries.TitleIsValid(title)) return ConversationErrors.InvalidTitle;
        var now = clock.GetUtcNow();
        var conversation = new Conversation { OwnerId = owner, Title = title, CreatedAt = now, UpdatedAt = now };
        db.Set<Conversation>().Add(conversation);
        db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.created", ResourceId = conversation.Id });
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
