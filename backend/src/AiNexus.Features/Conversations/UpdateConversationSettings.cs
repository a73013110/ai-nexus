using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Conversations;

public sealed record ConversationSettingsRequest(bool? IsFavorite = null, bool? IsArchived = null, string? SystemInstruction = null, IReadOnlyList<string>? Labels = null);

/// <summary>The instruction length. Invalid labels have their own code and are reported by the handler.</summary>
internal sealed class ConversationSettingsRequestValidator : RequestValidator<ConversationSettingsRequest>
{
    public override string ProblemCode => ConversationErrors.InstructionTooLongCode;

    public ConversationSettingsRequestValidator() => RuleFor(x => x.SystemInstruction).MaximumLength(ConversationQueries.InstructionMaxLength);
}

/// <summary>
/// Favorite, archive, instruction and labels of one of the user's own conversations; only the given fields change.
/// Runs under the conversation's generation lock, and a conversation cannot be archived while an answer is being generated.
/// </summary>
internal sealed class UpdateConversationSettings(NexusDbContext db, GenerationScheduler scheduler)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPatch("/{id:guid}/settings", async (Guid id, ConversationSettingsRequest body, ICurrentUser user, UpdateConversationSettings handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("UpdateConversationSettings").Produces<ConversationDto>();

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, Guid id, ConversationSettingsRequest request, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            var conversation = await db.OwnedConversationAsync(owner, id, ct);
            if (conversation is null) return ConversationErrors.NotFound;
            if (request.IsArchived == true && await db.HasActiveRunAsync(id, ct)) return ConversationErrors.GenerationActive;
            if (request.SystemInstruction is { } instruction) conversation.SystemInstruction = instruction.Trim();
            if (request.IsFavorite is bool favorite) conversation.IsFavorite = favorite;
            if (request.IsArchived is bool archived) conversation.IsArchived = archived;
            if (request.Labels is { } requested)
            {
                var names = ConversationQueries.CleanLabels(requested);
                if (names is null) return ConversationErrors.InvalidLabels;
                // Keep unchanged tracked keys; removing and adding the same key breaks EF identity tracking.
                var removed = conversation.Labels.Where(x => !names.Contains(x.Name, StringComparer.Ordinal)).ToList();
                db.Set<ConversationLabel>().RemoveRange(removed);
                foreach (var label in removed) conversation.Labels.Remove(label);
                foreach (var name in names.Where(name => !conversation.Labels.Any(x => x.Name == name))) conversation.Labels.Add(new() { ConversationId = id, Name = name });
            }
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.organized", ResourceId = id });
            await db.SaveChangesAsync(ct);
            return conversation.ToDto();
        }
        finally { conversationLock.Dispose(); }
    }
}
