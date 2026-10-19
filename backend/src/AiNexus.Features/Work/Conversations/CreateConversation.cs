using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Work.Conversations;

public sealed record CreateConversationRequest(string? Title = null);

/// <summary>沒有標題時使用「新對話」；給定的標題去除頭尾空白後必須為 1 到 120 字元。</summary>
internal sealed class CreateConversationRequestValidator : RequestValidator<CreateConversationRequest>
{
    public override string ProblemCode => ConversationsErrors.InvalidTitleCode;

    public CreateConversationRequestValidator()
        => RuleFor(x => x.Title).Must(x => x is null || Conversation.TitleIsValid(x)).WithErrorCode("length");
}

/// <summary>建立使用者自己的空對話。其他模組經由 <see cref="ConversationService.CreateAsync"/> 建立。</summary>
internal sealed class CreateConversation(AppDbContext db, TimeProvider clock)
{
    /// <summary>未給標題時的預設值；首次提問時只有仍是這個標題才會自動改成提問摘要。</summary>
    public const string DefaultTitle = "新對話";

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", (CreateConversationRequest body, ICurrentUser user, CreateConversation handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, body.Title, ct).ToHttpResultAsync(created =>
                TypedResults.Created($"/api/v1/conversations/{created.Id}", created)))
        .WithName("CreateConversation");

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, string? title, CancellationToken ct)
    {
        title = (title ?? DefaultTitle).Trim();
        if (!Conversation.TitleIsValid(title)) return ConversationsErrors.InvalidTitle;
        var now = clock.GetUtcNow();
        var conversation = new Conversation { OwnerId = owner, Title = title, CreatedAt = now, UpdatedAt = now };
        db.Set<Conversation>().Add(conversation);
        db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.created", ResourceId = conversation.Id });
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
