using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Conversations;

public sealed record ConversationBackup(int Version, string Title, string SystemInstruction, IReadOnlyList<string> Labels, Guid? ActiveLeafId, IReadOnlyList<BackupMessage> Messages);
public sealed record BackupMessage(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, IReadOnlyList<string> AttachmentNames);

/// <summary>
/// Version, title, instruction and overall size. The handler then reports invalid labels with their own code, and only
/// after that checks each message and the message tree, again as <c>invalid_backup</c>.
/// </summary>
internal sealed class ConversationBackupValidator : RequestValidator<ConversationBackup>
{
    public const int MaxMessages = 400;
    public const long MaxContentCharacters = 1_000_000;

    public override string ProblemCode => ConversationErrors.InvalidBackupCode;

    public ConversationBackupValidator()
    {
        RuleFor(x => x.Version).Equal(1);
        RuleFor(x => x.Title).Must(ConversationQueries.TitleIsValid).WithErrorCode("length");
        RuleFor(x => x.SystemInstruction).NotNull().MaximumLength(ConversationQueries.InstructionMaxLength);
        RuleFor(x => x.Labels).NotNull();
        RuleFor(x => x.Messages).Must(x => x is { Count: <= MaxMessages } && x.All(m => m is { Content: not null }) && x.Sum(m => (long)m.Content.Length) <= MaxContentCharacters).WithErrorCode("size");
    }
}

/// <summary>
/// Restores a backup as a new conversation of the user's, with new message ids. Every message must belong to a complete
/// user/assistant chain that starts with a user message and has no cycles, and the active leaf must be an answer.
/// </summary>
internal sealed class ImportConversation(NexusDbContext db, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/import", async (ConversationBackup body, ICurrentUser user, ImportConversation handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(ConversationsModule.ImportBodyLimit).WithName("ImportConversation").Produces<ConversationDto>();

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, ConversationBackup backup, CancellationToken ct)
    {
        var labels = ConversationQueries.CleanLabels(backup.Labels);
        if (labels is null) return ConversationErrors.InvalidLabels;
        var lookup = new Dictionary<Guid, BackupMessage>();
        foreach (var message in backup.Messages)
        {
            if (message.Id == Guid.Empty || !lookup.TryAdd(message.Id, message) || message.Content.Length > 65536 || message.Role is not ("user" or "assistant") || message.Status is not ("completed" or "failed" or "cancelled")) return ConversationErrors.InvalidBackup;
        }
        foreach (var message in backup.Messages)
        {
            var seen = new HashSet<Guid>();
            BackupMessage? next = message;
            while (next is not null)
            {
                if (!seen.Add(next.Id)) return ConversationErrors.InvalidBackup;
                if (next.ParentId is not Guid parent) { if (next.Role != "user") return ConversationErrors.InvalidBackup; break; }
                if (!lookup.TryGetValue(parent, out var ancestor) || ancestor.Role == next.Role) return ConversationErrors.InvalidBackup;
                next = ancestor;
            }
        }
        if (backup.ActiveLeafId is Guid leaf && (!lookup.TryGetValue(leaf, out var activeMessage) || activeMessage.Role != "assistant")) return ConversationErrors.InvalidBackup;
        if (lookup.Count > 0 && backup.ActiveLeafId is null) return ConversationErrors.InvalidBackup;
        var ids = lookup.Keys.ToDictionary(x => x, _ => Guid.NewGuid());
        var now = clock.GetUtcNow();
        var conversation = new Conversation { OwnerId = owner, Title = backup.Title.Trim(), SystemInstruction = backup.SystemInstruction.Trim(), ActiveLeafId = backup.ActiveLeafId is Guid active ? ids[active] : null, CreatedAt = now, UpdatedAt = now };
        conversation.Labels = labels.Select(x => new ConversationLabel { ConversationId = conversation.Id, Name = x }).ToList();
        db.Set<Conversation>().Add(conversation);
        foreach (var message in backup.Messages) db.Set<Message>().Add(new() { Id = ids[message.Id], ConversationId = conversation.Id, ParentId = message.ParentId is Guid parent ? ids[parent] : null, Role = message.Role, Content = message.Content, Status = message.Status, CreatedAt = message.CreatedAt });
        db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.imported", ResourceId = conversation.Id });
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
