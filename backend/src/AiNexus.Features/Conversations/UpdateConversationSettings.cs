using AiNexus.Features.Identity;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Conversations;

public sealed record ConversationSettingsRequest(bool? IsFavorite = null, bool? IsArchived = null, string? SystemInstruction = null, IReadOnlyList<string>? Labels = null);

/// <summary>只驗證指令長度；無效標籤有自己的代碼，由 handler 回報。</summary>
internal sealed class ConversationSettingsRequestValidator : RequestValidator<ConversationSettingsRequest>
{
    public override string ProblemCode => ConversationsErrors.InstructionTooLongCode;

    public ConversationSettingsRequestValidator() => RuleFor(x => x.SystemInstruction).MaximumLength(Conversation.InstructionMaxLength);
}

/// <summary>
/// 修改使用者自己對話的收藏、封存、指令與標籤；只變更有帶入的欄位。在對話的生成鎖內執行，生成中的對話不能封存。
/// </summary>
internal sealed class UpdateConversationSettings(NexusDbContext db, GenerationScheduler scheduler)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPatch("/{id:guid}/settings", async (Guid id, ConversationSettingsRequest body, ICurrentUser user, UpdateConversationSettings handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("UpdateConversationSettings");

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, Guid id, ConversationSettingsRequest request, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            var conversation = await db.OwnedConversationAsync(owner, id, ct);
            if (conversation is null) return ConversationsErrors.NotFound;
            if (request.IsArchived == true && await db.HasActiveRunAsync(id, ct)) return ConversationsErrors.GenerationActive;
            if (request.SystemInstruction is { } instruction) conversation.SystemInstruction = instruction.Trim();
            if (request.IsFavorite is bool favorite) conversation.IsFavorite = favorite;
            if (request.IsArchived is bool archived) conversation.IsArchived = archived;
            if (request.Labels is { } requested)
            {
                var names = ConversationLabel.Clean(requested);
                if (names is null) return ConversationsErrors.InvalidLabels;
                // 保留未變動的標籤實體：同一個主鍵若先移除再加回，會破壞 EF 的追蹤身分。
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
