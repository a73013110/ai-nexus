using AiNexus.BuildingBlocks;
using AiNexus.Database;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Conversations;

public sealed record ConversationSettingsRequest(bool? IsFavorite = null, bool? IsArchived = null, string? SystemInstruction = null, IReadOnlyList<string>? Labels = null);
public sealed record ConversationBackup(int Version, string Title, string SystemInstruction, IReadOnlyList<string> Labels, Guid? ActiveLeafId, IReadOnlyList<BackupMessage> Messages);
public sealed record BackupMessage(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, IReadOnlyList<string> AttachmentNames);

public sealed class ConversationOrganization(IEfHelper<INexusDatabase> ef, ConversationService conversations, GenerationScheduler scheduler)
{
    public async Task<IReadOnlyList<string>> LabelsAsync(Guid owner, CancellationToken ct)
        => await (from label in ef.Set<ConversationLabel>() join conversation in ef.Set<Conversation>() on label.ConversationId equals conversation.Id where conversation.OwnerId == owner && !conversation.IsDeleted select label.Name).Distinct().OrderBy(x => x).ToListAsync(ct);

    public async Task<ConversationDto> UpdateAsync(Guid owner, Guid id, ConversationSettingsRequest request, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            var conversation = await conversations.OwnedAsync(owner, id, ct);
            if (request.IsArchived == true) await RequireIdle(id, ct);
            if (request.SystemInstruction is { } instruction)
            {
                if (instruction.Length > 4000) throw new ApiException(400, "instruction_too_long", "對話指令最多 4,000 字元。");
                conversation.SystemInstruction = instruction.Trim();
            }
            if (request.IsFavorite is bool favorite) conversation.IsFavorite = favorite;
            if (request.IsArchived is bool archived) conversation.IsArchived = archived;
            if (request.Labels is { } requested)
            {
                var names = CleanLabels(requested);
                // Keep unchanged tracked keys; removing and adding the same key breaks EF identity tracking.
                var removed = conversation.Labels.Where(x => !names.Contains(x.Name, StringComparer.Ordinal)).ToList();
                ef.Set<ConversationLabel>().RemoveRange(removed);
                foreach (var label in removed) conversation.Labels.Remove(label);
                foreach (var name in names.Where(name => !conversation.Labels.Any(x => x.Name == name))) conversation.Labels.Add(new() { ConversationId = id, Name = name });
            }
            ef.Set<AuditEvent>().Add(new() { OwnerId = owner, Action = "conversation.organized", ResourceId = id });
            await ef.SaveChangesAsync(ct);
            return conversation.ToDto();
        }
        finally { scheduler.StateGate.Release(); }
    }

    public async Task<ConversationDto> DuplicateAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await scheduler.StateGate.WaitAsync(ct);
        try
        {
            var original = await conversations.OwnedAsync(owner, id, ct);
            await RequireIdle(id, ct);
            var messages = await ef.Set<Message>().AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
            var ids = messages.ToDictionary(x => x.Id, _ => Guid.NewGuid());
            var clone = new Conversation { OwnerId = owner, Title = original.Title[..Math.Min(original.Title.Length, 115)] + " · 副本", SystemInstruction = original.SystemInstruction, ActiveLeafId = original.ActiveLeafId is Guid leaf ? ids[leaf] : null };
            clone.Labels = original.Labels.Select(x => new ConversationLabel { ConversationId = clone.Id, Name = x.Name }).ToList();
            ef.Set<Conversation>().Add(clone);
            foreach (var message in messages) ef.Set<Message>().Add(new() { Id = ids[message.Id], ConversationId = clone.Id, ParentId = message.ParentId is Guid parent ? ids[parent] : null, Role = message.Role, Content = message.Content, Status = message.Status, ModelId = message.ModelId, ErrorCode = message.ErrorCode, CreatedAt = message.CreatedAt });
            var links = await ef.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Keys.Contains(x.MessageId)).ToListAsync(ct);
            foreach (var link in links) ef.Set<MessageAttachment>().Add(new() { MessageId = ids[link.MessageId], AttachmentId = link.AttachmentId });
            ef.Set<AuditEvent>().Add(new() { OwnerId = owner, Action = "conversation.duplicated", ResourceId = clone.Id });
            await ef.SaveChangesAsync(ct);
            return clone.ToDto();
        }
        finally { scheduler.StateGate.Release(); }
    }

    public async Task<ConversationBackup> ExportAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var detail = await conversations.DetailAsync(owner, id, ct);
        if (detail.ActiveRun is not null) throw new ApiException(409, "generation_active", "請先停止生成再備份。");
        return new(1, detail.Conversation.Title, detail.Conversation.SystemInstruction, detail.Conversation.Labels ?? [], detail.Conversation.ActiveLeafId,
            detail.Messages.Select(x => new BackupMessage(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.Attachments?.Select(f => f.FileName).ToList() ?? [])).ToList());
    }

    public async Task<ConversationDto> ImportAsync(Guid owner, ConversationBackup backup, CancellationToken ct)
    {
        if (backup.Version != 1 || backup.Title.Trim().Length is < 1 or > 120 || backup.SystemInstruction.Length > 4000 || backup.Messages.Count > 400 || backup.Messages.Sum(x => (long)x.Content.Length) > 1_000_000)
            throw new ApiException(400, "invalid_backup", "備份格式不正確或內容過大（最多 400 則訊息）。");
        var labels = CleanLabels(backup.Labels);
        var lookup = new Dictionary<Guid, BackupMessage>();
        foreach (var message in backup.Messages)
        {
            if (message.Id == Guid.Empty || !lookup.TryAdd(message.Id, message) || message.Content.Length > 65536 || message.Role is not ("user" or "assistant") || message.Status is not ("completed" or "failed" or "cancelled")) InvalidBackup();
        }
        foreach (var message in backup.Messages)
        {
            var seen = new HashSet<Guid>();
            BackupMessage? next = message;
            while (next is not null)
            {
                if (!seen.Add(next.Id)) InvalidBackup();
                if (next.ParentId is not Guid parent) { if (next.Role != "user") InvalidBackup(); break; }
                if (!lookup.TryGetValue(parent, out var ancestor) || ancestor.Role == next.Role) InvalidBackup();
                next = ancestor;
            }
        }
        if (backup.ActiveLeafId is Guid leaf && (!lookup.TryGetValue(leaf, out var activeMessage) || activeMessage.Role != "assistant")) InvalidBackup();
        if (lookup.Count > 0 && backup.ActiveLeafId is null) InvalidBackup();
        var ids = lookup.Keys.ToDictionary(x => x, _ => Guid.NewGuid());
        var conversation = new Conversation { OwnerId = owner, Title = backup.Title.Trim(), SystemInstruction = backup.SystemInstruction.Trim(), ActiveLeafId = backup.ActiveLeafId is Guid active ? ids[active] : null };
        conversation.Labels = labels.Select(x => new ConversationLabel { ConversationId = conversation.Id, Name = x }).ToList();
        ef.Set<Conversation>().Add(conversation);
        foreach (var message in backup.Messages) ef.Set<Message>().Add(new() { Id = ids[message.Id], ConversationId = conversation.Id, ParentId = message.ParentId is Guid parent ? ids[parent] : null, Role = message.Role, Content = message.Content, Status = message.Status, CreatedAt = message.CreatedAt });
        ef.Set<AuditEvent>().Add(new() { OwnerId = owner, Action = "conversation.imported", ResourceId = conversation.Id });
        await ef.SaveChangesAsync(ct);
        return conversation.ToDto();
    }

    private async Task RequireIdle(Guid id, CancellationToken ct)
    {
        if (await ef.Set<GenerationRun>().AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct)) throw new ApiException(409, "generation_active", "請先停止生成再整理對話。");
    }
    private static string[] CleanLabels(IReadOnlyList<string> labels)
    {
        if (labels.Count > 5 || labels.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 24 || x.Any(char.IsControl))) throw new ApiException(400, "invalid_labels", "最多 5 個標籤，每個 1 至 24 字元。");
        return labels.Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void InvalidBackup() => throw new ApiException(400, "invalid_backup", "備份訊息分支不完整或包含循環。");
}
