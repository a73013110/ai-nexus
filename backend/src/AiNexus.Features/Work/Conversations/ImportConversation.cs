using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Work.Conversations;

public sealed record ConversationBackup(int Version, string Title, string SystemInstruction, IReadOnlyList<string> Labels, Guid? ActiveLeafId, IReadOnlyList<BackupMessage> Messages);
public sealed record BackupMessage(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, IReadOnlyList<string> AttachmentNames);

/// <summary>
/// 只驗證版本、標題、指令與整體大小。無效標籤與訊息樹由 handler 檢查，錯誤代碼分別是 <c>invalid_labels</c> 與 <c>invalid_backup</c>。
/// </summary>
internal sealed class ConversationBackupValidator : RequestValidator<ConversationBackup>
{
    public const int MaxMessages = 400;
    public const long MaxContentCharacters = 1_000_000;

    public override string ProblemCode => ConversationsErrors.InvalidBackupCode;

    public ConversationBackupValidator()
    {
        RuleFor(x => x.Version).Equal(1);
        RuleFor(x => x.Title).Must(Conversation.TitleIsValid).WithErrorCode("length");
        RuleFor(x => x.SystemInstruction).NotNull().MaximumLength(Conversation.InstructionMaxLength);
        RuleFor(x => x.Labels).NotNull();
        RuleFor(x => x.Messages)
            .Must(x => x is { Count: <= MaxMessages }
                && x.All(m => m is { Content: not null })
                && x.Sum(m => (long)m.Content.Length) <= MaxContentCharacters)
            .WithErrorCode("size");
    }
}

/// <summary>
/// 把備份還原為使用者的新對話，訊息會重新指派 Id。每則訊息都必須屬於完整的 user/assistant 鏈（從 user 開始且無循環），
/// 作用中葉節點必須是回答。
/// </summary>
internal sealed class ImportConversation(AppDbContext db, TimeProvider clock, AuditTrail audit)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/import", async (ConversationBackup body, ICurrentUser user, ImportConversation handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(ConversationsModule.ImportBodyLimit)
        .Audited(ConversationsAudit.ConversationImported)
        .WithName("ImportConversation");

    public async Task<Result<ConversationDto>> HandleAsync(Guid owner, ConversationBackup backup, CancellationToken ct)
    {
        var labels = ConversationLabel.Clean(backup.Labels);
        if (labels is null) return ConversationsErrors.InvalidLabels;

        // 先建立索引並檢查每則訊息本身；TryAdd 同時擋掉重複的 Id，順序不能調換。
        var lookup = new Dictionary<Guid, BackupMessage>();
        foreach (var message in backup.Messages)
        {
            if (message.Id == Guid.Empty
                || !lookup.TryAdd(message.Id, message)
                || message.Content.Length > 65536
                || message.Role is not ("user" or "assistant")
                || message.Status is not (RunStates.Completed or RunStates.Failed or RunStates.Cancelled))
            {
                return ConversationsErrors.InvalidBackup;
            }
        }

        // 從每則訊息往上走到根：根必須是 user，且相鄰兩層角色交替，走到循環即拒絕。
        foreach (var message in backup.Messages)
        {
            var seen = new HashSet<Guid>();
            BackupMessage? next = message;
            while (next is not null)
            {
                if (!seen.Add(next.Id)) return ConversationsErrors.InvalidBackup;
                if (next.ParentId is not Guid parent)
                {
                    if (next.Role != "user") return ConversationsErrors.InvalidBackup;
                    break;
                }
                if (!lookup.TryGetValue(parent, out var ancestor) || ancestor.Role == next.Role) return ConversationsErrors.InvalidBackup;
                next = ancestor;
            }
        }

        if (backup.ActiveLeafId is Guid leaf
            && (!lookup.TryGetValue(leaf, out var activeMessage) || activeMessage.Role != "assistant"))
        {
            return ConversationsErrors.InvalidBackup;
        }
        if (lookup.Count > 0 && backup.ActiveLeafId is null) return ConversationsErrors.InvalidBackup;

        var ids = lookup.Keys.ToDictionary(x => x, _ => Guid.NewGuid());
        var now = clock.GetUtcNow();
        var conversation = new Conversation
        {
            OwnerId = owner,
            Title = backup.Title.Trim(),
            SystemInstruction = backup.SystemInstruction.Trim(),
            ActiveLeafId = backup.ActiveLeafId is Guid active ? ids[active] : null,
            CreatedAt = now,
            UpdatedAt = now,
        };
        conversation.Labels = labels.Select(x => new ConversationLabel { ConversationId = conversation.Id, Name = x }).ToList();
        db.Set<Conversation>().Add(conversation);
        foreach (var message in backup.Messages)
        {
            db.Set<Message>().Add(new()
            {
                Id = ids[message.Id],
                ConversationId = conversation.Id,
                ParentId = message.ParentId is Guid parent ? ids[parent] : null,
                Role = message.Role,
                Content = message.Content,
                Status = message.Status,
                CreatedAt = message.CreatedAt,
            });
        }
        audit.Describe(resource: conversation.Id);
        await db.SaveChangesAsync(ct);
        return conversation.ToDto();
    }
}
