using System.Text;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public sealed class ContextBuilder(NexusDbContext db, IOptions<AttachmentOptions> attachments, AttachmentService storage)
{
    public static string SystemPrompt(string baseline, string instruction) => string.IsNullOrWhiteSpace(instruction) ? baseline : baseline + "\n\n此對話的使用者偏好：\n" + instruction;

    public async Task<IReadOnlyList<InferenceMessage>> BuildAsync(Guid conversation, Guid leaf, GenerationParameters parameters, CancellationToken ct)
    {
        var chain = await ChainAsync(conversation, leaf, ct);
        var preview = Trim(chain, parameters);
        if (preview.BudgetExceeded) throw new ApiException(400, "context_budget_exceeded", "提問超過此模型的上下文預算，請縮短內容或選擇較大上下文的模型。");
        RequireVision(chain, parameters);
        var imageIds = chain.SelectMany(x => x.Images ?? []).Select(x => x.AttachmentId).Distinct().ToArray();
        var originals = await db.Set<Attachment>().AsNoTracking().Where(x => imageIds.Contains(x.Id) && x.StorageState == AttachmentStates.Ready).Select(x => new Attachment { Id = x.Id, StorageKey = x.StorageKey, Size = x.Size }).ToListAsync(ct);
        var data = new Dictionary<Guid, byte[]>();
        foreach (var original in originals) data.Add(original.Id, await storage.ReadAsync(original, ct));
        if (data.Count != imageIds.Length) throw new ApiException(409, "attachment_not_found", "對話附件已無法使用。");
        chain = chain.Select(x => x with { Images = x.Images?.Select(i => i with { Data = data[i.AttachmentId] }).ToArray() }).ToList();
        return new[] { new InferenceMessage("system", parameters.SystemPrompt) }.Concat(chain).ToList();
    }

    public async Task<ContextUsageDto> PreviewAsync(Guid? conversation, Guid? parent, string? prompt, GenerationParameters parameters, CancellationToken ct, IReadOnlyList<Attachment>? files = null)
    {
        if (conversation is null && parent is not null) throw new ApiException(400, "invalid_parent", "上文需要指定對話。");
        var chain = conversation is Guid id && parent is Guid leaf ? await ChainAsync(id, leaf, ct) : [];
        if (!string.IsNullOrWhiteSpace(prompt) || files?.Count > 0) chain.Add(WithFiles("user", prompt?.Trim() ?? "", files ?? []));
        var usage = Trim(chain, parameters);
        RequireVision(chain, parameters);
        return usage;
    }

    private async Task<List<InferenceMessage>> ChainAsync(Guid conversation, Guid leaf, CancellationToken ct)
    {
        var persisted = await db.Messages.AsNoTracking().Where(x => x.ConversationId == conversation).ToListAsync(ct);
        var lookup = persisted.Concat(db.Messages.Local.Where(x => x.ConversationId == conversation)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var branch = new List<AiNexus.Modules.Conversations.Message>();
        var visited = new HashSet<Guid>();
        Guid? next = leaf;
        while (next is Guid id)
        {
            if (!visited.Add(id) || !lookup.TryGetValue(id, out var message)) throw new ApiException(409, "invalid_history", "對話分支資料不完整。");
            if (message.Role == "user" || (!RunStates.IsActive(message.Status) && message.Content.Length > 0)) branch.Add(message);
            next = message.ParentId;
        }
        branch.Reverse();
        var messageIds = branch.Select(x => x.Id).ToArray();
        // Context previews read metadata/text only. Binary payloads are loaded after trimming, by the worker.
        var links = await db.Set<MessageAttachment>().AsNoTracking().Where(x => messageIds.Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.ExtractedText }).ToListAsync(ct);
        var pending = db.Set<MessageAttachment>().Local.Where(x => messageIds.Contains(x.MessageId));
        var missingIds = pending.Select(x => x.AttachmentId).Except(links.Select(x => x.Id)).Distinct().ToArray();
        var pendingFiles = await db.Set<Attachment>().AsNoTracking().Where(x => missingIds.Contains(x.Id))
            .Select(x => new Attachment { Id = x.Id, FileName = x.FileName, ContentType = x.ContentType, ExtractedText = x.ExtractedText }).ToListAsync(ct);
        return branch.Select(message => WithFiles(message.Role, message.Content,
            links.Where(x => x.MessageId == message.Id).Select(x => new Attachment { Id = x.Id, FileName = x.FileName, ContentType = x.ContentType, ExtractedText = x.ExtractedText })
                .Concat(pending.Where(x => x.MessageId == message.Id).Join(pendingFiles, x => x.AttachmentId, x => x.Id, (_, file) => file)).DistinctBy(x => x.Id).ToList())).ToList();
    }

    private InferenceMessage WithFiles(string role, string content, IReadOnlyList<Attachment> files)
    {
        var text = new StringBuilder(content);
        foreach (var file in files.Where(x => x.ExtractedText is not null)) text.Append("\n\n--- 文件：").Append(file.FileName).Append(" ---\n").Append(file.ExtractedText).Append("\n--- 文件結束 ---");
        return new(role, text.ToString(), files.Where(x => x.ContentType.StartsWith("image/")).Select(x => new InferenceImage(x.Id, x.ContentType, null, attachments.Value.ImageTokenEstimate)).ToArray());
    }
    private static void RequireVision(List<InferenceMessage> chain, GenerationParameters parameters)
    {
        if (!parameters.SupportsImages && chain.Any(x => x.Images?.Count > 0)) throw new ApiException(400, "vision_not_supported", "目前模型不支援圖片，請切換支援圖片的模型，或移除圖片附件。");
    }

    private static ContextUsageDto Trim(List<InferenceMessage> chain, GenerationParameters parameters)
    {
        // A conservative UTF-8 byte budget avoids assuming English token ratios for Chinese.
        // No server tokenizer is required; actual usage is recorded when supported.
        var system = Cost(parameters.SystemPrompt) + 128;
        var budget = parameters.ContextTokens - parameters.MaxOutputTokens - system;
        var exceeded = budget < 0 || (chain.Count > 0 && Cost(chain[^1]) > budget);
        var count = chain.Count;
        var cost = chain.Sum(x => Cost(x));
        while (cost > budget && chain.Count > 1)
        {
            cost -= Cost(chain[0]);
            chain.RemoveAt(0);
            while (chain.Count > 1 && chain[0].Role != "user") { cost -= Cost(chain[0]); chain.RemoveAt(0); }
        }
        return new((int)Math.Min(int.MaxValue, cost + system), parameters.ContextTokens, parameters.MaxOutputTokens, count - chain.Count, exceeded);
    }
    public static long Estimate(IReadOnlyList<InferenceMessage> messages) => messages.Sum(Cost) + 128;
    private static long Cost(InferenceMessage message) => Cost(message.Content) + (message.Images ?? []).Sum(x => (long)x.EstimatedTokens);
    private static int Cost(string value) => Encoding.UTF8.GetByteCount(value) + 32;
}
