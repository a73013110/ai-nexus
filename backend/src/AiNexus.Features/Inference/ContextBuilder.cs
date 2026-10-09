using System.Text;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

/// <summary>
/// Builds the model context of one branch: the newest complete rounds that fit the budget, oldest rounds dropped first.
/// Only the active path is followed, and text is read only for its newest messages that can still fit: ids, roles and
/// sizes of the tree come first, then the text of those messages and their files.
/// </summary>
public sealed class ContextBuilder(NexusDbContext db, IOptions<AttachmentOptions> attachments, AttachmentService storage)
{
    public static string SystemPrompt(string baseline, string instruction) => string.IsNullOrWhiteSpace(instruction) ? baseline : baseline + "\n\n此對話的使用者偏好：\n" + instruction;

    /// <summary>The context ending at a saved message, with image bytes.</summary>
    public async Task<IReadOnlyList<InferenceMessage>> BuildAsync(Guid conversation, Guid leaf, GenerationParameters parameters, CancellationToken ct)
        => await WithImagesAsync(Finish(await ComposeAsync(conversation, leaf, null, parameters, ct), parameters), ct);

    /// <summary>
    /// The context of a prompt that is not saved yet (or of the saved prompt being regenerated), without image bytes.
    /// History is immutable once answered, so the result equals what <see cref="BuildAsync"/> returns after the prompt is
    /// saved, for any parameters with the same history budget (<see cref="Administration.ModelPolicyService.BudgetAsync"/> keeps it).
    /// </summary>
    public async Task<IReadOnlyList<InferenceMessage>> PrepareAsync(Guid conversation, Guid? parent, Guid? regenerate, string? prompt, IReadOnlyList<Attachment> files, GenerationParameters parameters, CancellationToken ct)
        => Finish(regenerate is Guid user
            ? await ComposeAsync(conversation, user, null, parameters, ct)
            : await ComposeAsync(parent is null ? null : conversation, parent, WithFiles("user", prompt?.Trim() ?? "", files), parameters, ct), parameters);

    /// <summary>Fails like <see cref="WithImagesAsync"/> when an image is no longer stored, without reading the files.</summary>
    public async Task RequireImagesAsync(IReadOnlyList<InferenceMessage> messages, CancellationToken ct)
    {
        var imageIds = ImageIds(messages);
        if (imageIds.Length == 0) return;
        if (await db.Set<Attachment>().AsNoTracking().CountAsync(x => imageIds.Contains(x.Id) && x.StorageState == AttachmentStates.Ready, ct) != imageIds.Length) throw AttachmentMissing();
    }

    /// <summary>Loads the bytes of the images a prepared context refers to.</summary>
    public async Task<IReadOnlyList<InferenceMessage>> WithImagesAsync(IReadOnlyList<InferenceMessage> messages, CancellationToken ct)
    {
        var imageIds = ImageIds(messages);
        if (imageIds.Length == 0) return messages;
        var originals = await db.Set<Attachment>().AsNoTracking().Where(x => imageIds.Contains(x.Id) && x.StorageState == AttachmentStates.Ready).Select(x => new Attachment { Id = x.Id, StorageKey = x.StorageKey, Size = x.Size }).ToListAsync(ct);
        var data = new Dictionary<Guid, byte[]>();
        foreach (var original in originals) data.Add(original.Id, await storage.ReadAsync(original, ct));
        if (data.Count != imageIds.Length) throw AttachmentMissing();
        return messages.Select(x => x with { Images = x.Images?.Select(i => i with { Data = data[i.AttachmentId] }).ToArray() }).ToList();
    }

    public async Task<ContextUsageDto> PreviewAsync(Guid? conversation, Guid? parent, string? prompt, GenerationParameters parameters, CancellationToken ct, IReadOnlyList<Attachment>? files = null)
    {
        if (conversation is null && parent is not null) throw new ApiException(400, "invalid_parent", "上文需要指定對話。");
        var added = !string.IsNullOrWhiteSpace(prompt) || files?.Count > 0 ? WithFiles("user", prompt?.Trim() ?? "", files ?? []) : null;
        var composed = await ComposeAsync(parent is null ? null : conversation, parent, added, parameters, ct);
        var usage = Trim(composed, parameters);
        RequireVision(composed.Messages, parameters);
        return usage;
    }

    private static IReadOnlyList<InferenceMessage> Finish(Composed composed, GenerationParameters parameters)
    {
        var usage = Trim(composed, parameters);
        if (usage.BudgetExceeded) throw new ApiException(400, "context_budget_exceeded", "提問超過此模型的上下文預算，請縮短內容或選擇較大上下文的模型。");
        RequireVision(composed.Messages, parameters);
        return new[] { new InferenceMessage("system", parameters.SystemPrompt) }.Concat(composed.Messages).ToList();
    }

    private static Guid[] ImageIds(IReadOnlyList<InferenceMessage> messages) => messages.SelectMany(x => x.Images ?? []).Select(x => x.AttachmentId).Distinct().ToArray();
    private static ApiException AttachmentMissing() => new(409, "attachment_not_found", "對話附件已無法使用。");

    /// <summary>A branch's messages that may survive trimming, plus how many older messages were left out unread.</summary>
    private sealed record Composed(List<InferenceMessage> Messages, int Omitted);
    private sealed record Node(Guid Id, Guid? ParentId, string Role, string Status, int Length);

    /// <summary>The history ending at <paramref name="leaf"/>, followed by <paramref name="added"/> when given.</summary>
    private async Task<Composed> ComposeAsync(Guid? conversation, Guid? leaf, InferenceMessage? added, GenerationParameters parameters, CancellationToken ct)
    {
        var history = conversation is Guid id && leaf is Guid last
            ? await HistoryAsync(id, last, Budget(parameters) - (added is null ? 0 : Cost(added)), keepNewest: added is null, ct)
            : new Composed([], 0);
        if (added is not null) history.Messages.Add(added);
        return history;
    }

    private async Task<Composed> HistoryAsync(Guid conversation, Guid leaf, long budget, bool keepNewest, CancellationToken ct)
    {
        // Content.Length is a lower bound of the UTF-8 cost (SQL Server's LEN even ignores trailing spaces), so every
        // message older than the first one that cannot fit by this bound cannot survive Trim either.
        var tree = await db.Messages.AsNoTracking().Where(x => x.ConversationId == conversation)
            .Select(x => new Node(x.Id, x.ParentId, x.Role, x.Status, x.Content.Length)).ToDictionaryAsync(x => x.Id, ct);
        var path = new List<Node>(); // newest first
        var visited = new HashSet<Guid>();
        Guid? next = leaf;
        while (next is Guid id)
        {
            if (!visited.Add(id) || !tree.TryGetValue(id, out var node)) throw new ApiException(409, "invalid_history", "對話分支資料不完整。");
            // Answers still being generated never enter the context; empty finished answers are dropped once their text is read.
            if (node.Role == "user" || !RunStates.IsActive(node.Status)) path.Add(node);
            next = node.ParentId;
        }
        // Trim always keeps the newest message, so with nothing newer it is read whatever its size.
        var take = 0;
        var remaining = budget;
        var forced = keepNewest;
        foreach (var node in path)
        {
            var lower = node.Role == "user" || node.Length > 0 ? 32L + node.Length : 0;
            if (!forced && lower > remaining) break;
            remaining -= lower;
            take++;
            if (lower > 0) forced = false;
        }
        var kept = path.Take(take).Reverse().ToList();
        var omitted = path.Skip(take).Count(x => x.Role == "user" || x.Length > 0);
        if (kept.Count == 0) return new([], omitted);
        var ids = kept.Select(x => x.Id).ToArray();
        var texts = await db.Messages.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Content }).ToDictionaryAsync(x => x.Id, x => x.Content, ct);
        // Previews read metadata/text only. Binary payloads are loaded after trimming, by the worker.
        var links = await db.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Contains(x.MessageId))
            .Select(x => new { x.MessageId, x.Attachment.Id, x.Attachment.FileName, x.Attachment.ContentType, x.Attachment.ExtractedText }).ToListAsync(ct);
        var messages = new List<InferenceMessage>(kept.Count);
        foreach (var node in kept)
        {
            if (!texts.TryGetValue(node.Id, out var content)) throw new ApiException(409, "invalid_history", "對話分支資料不完整。");
            if (node.Role != "user" && content.Length == 0) continue;
            messages.Add(WithFiles(node.Role, content, links.Where(x => x.MessageId == node.Id)
                .Select(x => new Attachment { Id = x.Id, FileName = x.FileName, ContentType = x.ContentType, ExtractedText = x.ExtractedText }).DistinctBy(x => x.Id).ToList()));
        }
        // Trim drops whole rounds: once older messages are left out, the history it keeps starts at a user message.
        if (take < path.Count)
            while (messages.Count + (keepNewest ? 0 : 1) > 1 && messages[0].Role != "user") { messages.RemoveAt(0); omitted++; }
        return new(messages, omitted);
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

    // A conservative UTF-8 byte budget avoids assuming English token ratios for Chinese.
    // No server tokenizer is required; actual usage is recorded when supported.
    private static long Budget(GenerationParameters parameters) => parameters.ContextTokens - parameters.MaxOutputTokens - (Cost(parameters.SystemPrompt) + 128);

    private static ContextUsageDto Trim(Composed composed, GenerationParameters parameters)
    {
        var chain = composed.Messages;
        var system = Cost(parameters.SystemPrompt) + 128;
        var budget = Budget(parameters);
        var exceeded = budget < 0 || (chain.Count > 0 && Cost(chain[^1]) > budget);
        var count = chain.Count;
        var cost = chain.Sum(x => Cost(x));
        while (cost > budget && chain.Count > 1)
        {
            cost -= Cost(chain[0]);
            chain.RemoveAt(0);
            while (chain.Count > 1 && chain[0].Role != "user") { cost -= Cost(chain[0]); chain.RemoveAt(0); }
        }
        return new((int)Math.Min(int.MaxValue, cost + system), parameters.ContextTokens, parameters.MaxOutputTokens, composed.Omitted + count - chain.Count, exceeded);
    }
    public static long Estimate(IReadOnlyList<InferenceMessage> messages) => messages.Sum(Cost) + 128;
    private static long Cost(InferenceMessage message) => Cost(message.Content) + (message.Images ?? []).Sum(x => (long)x.EstimatedTokens);
    private static int Cost(string value) => Encoding.UTF8.GetByteCount(value) + 32;
}
