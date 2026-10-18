using AiNexus.Data.App;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Inference;
using System.Text;
using AiNexus.Features.Attachments;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;
using AiNexus.Features.Inference.ModelTasks;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Chat.Context;

/// <summary>
/// 組裝送入模型的對話上下文：沿分支路徑由新到舊回溯，超出 token 預算時整回合從最舊的一端捨棄，
/// 保證送出的歷史一定從使用者提問開始。
/// 採兩階段載入：先以節點長度試算保留範圍，只讀保留節點的內容；圖片位元組延後到推論前才載入
/// （見 <see cref="WithImagesAsync"/>），排隊中的生成不持有圖片資料。
/// </summary>
public sealed class ContextBuilder(
    AppDbContext db,
    IOptions<AttachmentOptions> attachments,
    AttachmentService storage)
{
    /// <summary>組合基底系統提示詞與使用者特定指令偏好。</summary>
    public static string SystemPrompt(string baseline, string instruction) =>
        string.IsNullOrWhiteSpace(instruction)
            ? baseline
            : baseline + "\n\n此對話的使用者偏好：\n" + instruction;

    /// <summary>
    /// 建置指定葉節點訊息為止的完整模型上下文（包含圖片二進位內容）。
    /// </summary>
    public async Task<Result<IReadOnlyList<InferenceMessage>>> BuildAsync(
        Guid conversation,
        Guid leaf,
        GenerationParameters parameters,
        CancellationToken ct)
    {
        var composed = await ComposeAsync(conversation, leaf, null, parameters, ct);
        if (!composed.IsSuccess) return composed.Error;

        var messages = Finish(composed.Value, parameters);
        return messages.IsSuccess ? await WithImagesAsync(messages.Value, ct) : messages;
    }

    /// <summary>
    /// 預先準備即將發問（或重新生成）的上下文（不包含圖片位元組資料，僅保留元資料）。
    /// 由於歷史對話不可變，此處計算出的修剪範圍與儲存後呼叫 BuildAsync 的結果完全一致。
    /// </summary>
    public async Task<Result<IReadOnlyList<InferenceMessage>>> PrepareAsync(
        Guid conversation,
        Guid? parent,
        Guid? regenerate,
        string? prompt,
        IReadOnlyList<Attachment> files,
        GenerationParameters parameters,
        CancellationToken ct)
    {
        var composed = regenerate is Guid user
            ? await ComposeAsync(conversation, user, null, parameters, ct)
            : await ComposeAsync(parent is null ? null : conversation, parent, WithFiles("user", prompt?.Trim() ?? "", files), parameters, ct);

        return composed.IsSuccess ? Finish(composed.Value, parameters) : composed.Error;
    }

    /// <summary>
    /// 驗證訊息所引用的圖片附件是否皆完好存在於儲存體中（不讀取二進位資料）。
    /// </summary>
    public async Task<Result> RequireImagesAsync(IReadOnlyList<InferenceMessage> messages, CancellationToken ct)
    {
        var imageIds = ImageIds(messages);
        if (imageIds.Length == 0) return Result.Success;

        var readyCount = await db.Set<Attachment>()
            .AsNoTracking()
            .CountAsync(x => imageIds.Contains(x.Id) && x.StorageState == AttachmentStates.Ready, ct);

        if (readyCount != imageIds.Length)
        {
            return ChatErrors.AttachmentMissing;
        }

        return Result.Success;
    }

    /// <summary>
    /// 載入訊息鏈中所參照圖片的實體二進位資料。
    /// </summary>
    public async Task<Result<IReadOnlyList<InferenceMessage>>> WithImagesAsync(
        IReadOnlyList<InferenceMessage> messages,
        CancellationToken ct)
    {
        var imageIds = ImageIds(messages);
        if (imageIds.Length == 0) return Result<IReadOnlyList<InferenceMessage>>.Ok(messages);

        var originals = await db.Set<Attachment>()
            .AsNoTracking()
            .Where(x => imageIds.Contains(x.Id) && x.StorageState == AttachmentStates.Ready)
            .Select(x => new Attachment { Id = x.Id, StorageKey = x.StorageKey, Size = x.Size })
            .ToListAsync(ct);

        var data = new Dictionary<Guid, byte[]>();
        foreach (var original in originals)
        {
            data.Add(original.Id, await storage.ReadAsync(original, ct));
        }

        if (data.Count != imageIds.Length)
        {
            return ChatErrors.AttachmentMissing;
        }

        return messages.Select(x => x with
        {
            Images = x.Images?.Select(i => i with { Data = data[i.AttachmentId] }).ToArray()
        }).ToList();
    }

    /// <summary>
    /// 試算草稿提問的上下文用量預估（不儲存任何訊息）。
    /// </summary>
    public async Task<Result<ContextUsageDto>> PreviewAsync(
        Guid? conversation,
        Guid? parent,
        string? prompt,
        GenerationParameters parameters,
        CancellationToken ct,
        IReadOnlyList<Attachment>? files = null)
    {
        if (conversation is null && parent is not null)
        {
            return ConversationsErrors.InvalidParent;
        }

        var added = !string.IsNullOrWhiteSpace(prompt) || files?.Count > 0
            ? WithFiles("user", prompt?.Trim() ?? "", files ?? [])
            : null;

        var composed = await ComposeAsync(parent is null ? null : conversation, parent, added, parameters, ct);
        if (!composed.IsSuccess) return composed.Error;

        var usage = Trim(composed.Value, parameters);
        if (!SupportsVision(composed.Value.Messages, parameters))
        {
            return InferenceErrors.VisionNotSupported;
        }

        return usage;
    }

    private static Result<IReadOnlyList<InferenceMessage>> Finish(Composed composed, GenerationParameters parameters)
    {
        var usage = Trim(composed, parameters);
        if (usage.BudgetExceeded) return InferenceErrors.ContextBudgetExceeded;
        if (!SupportsVision(composed.Messages, parameters)) return InferenceErrors.VisionNotSupported;

        return new[] { new InferenceMessage("system", parameters.SystemPrompt) }
            .Concat(composed.Messages)
            .ToList();
    }

    private static Guid[] ImageIds(IReadOnlyList<InferenceMessage> messages) =>
        messages.SelectMany(x => x.Images ?? []).Select(x => x.AttachmentId).Distinct().ToArray();

    private sealed record Composed(List<InferenceMessage> Messages, int Omitted);
    private sealed record Node(Guid Id, Guid? ParentId, string Role, string Status, int Length);

    private async Task<Result<Composed>> ComposeAsync(
        Guid? conversation,
        Guid? leaf,
        InferenceMessage? added,
        GenerationParameters parameters,
        CancellationToken ct)
    {
        var history = conversation is Guid id && leaf is Guid last
            ? await HistoryAsync(id, last, Budget(parameters) - (added is null ? 0 : MessageCost.Of(added)), keepNewest: added is null, ct)
            : new Composed([], 0);

        if (history.IsSuccess && added is not null)
        {
            history.Value.Messages.Add(added);
        }

        return history;
    }

    private async Task<Result<Composed>> HistoryAsync(
        Guid conversation,
        Guid leaf,
        long budget,
        bool keepNewest,
        CancellationToken ct)
    {
        // 一次撈整段對話的節點元資料（不含內容），內容只在確認保留後才讀取
        var tree = await db.Messages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversation)
            .Select(x => new Node(x.Id, x.ParentId, x.Role, x.Status, x.Content.Length))
            .ToDictionaryAsync(x => x.Id, ct);

        var path = new List<Node>(); // 由新到舊收集，保留後再 Reverse 還原成時間順序
        var visited = new HashSet<Guid>();
        Guid? next = leaf;

        while (next is Guid id)
        {
            if (!visited.Add(id) || !tree.TryGetValue(id, out var node))
            {
                return ChatErrors.InvalidHistory;
            }

            // 仍在生成中的回答不納入歷史；已完成但無內容的空回答由後續過濾
            if (node.Role == "user" || !RunStates.IsActive(node.Status))
            {
                path.Add(node);
            }

            next = node.ParentId;
        }

        // 空的非使用者回答不占額度；forced 讓最新一則即使超出預算也保留，避免整段歷史被清空
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
        if (kept.Count == 0) return new Composed([], omitted);

        var ids = kept.Select(x => x.Id).ToArray();

        // 只讀保留節點的內容，被修剪掉的長文本不進記憶體
        var texts = await db.Messages
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Content })
            .ToDictionaryAsync(x => x.Id, x => x.Content, ct);

        var links = await db.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(x => ids.Contains(x.MessageId))
            .Select(x => new
            {
                x.MessageId,
                x.Attachment.Id,
                x.Attachment.FileName,
                x.Attachment.ContentType,
                x.Attachment.ExtractedText
            })
            .ToListAsync(ct);

        var messages = new List<InferenceMessage>(kept.Count);
        foreach (var node in kept)
        {
            if (!texts.TryGetValue(node.Id, out var content))
            {
                return ChatErrors.InvalidHistory;
            }

            if (node.Role != "user" && content.Length == 0) continue;

            var messageAttachments = links
                .Where(x => x.MessageId == node.Id)
                .Select(x => new Attachment
                {
                    Id = x.Id,
                    FileName = x.FileName,
                    ContentType = x.ContentType,
                    ExtractedText = x.ExtractedText
                })
                .DistinctBy(x => x.Id)
                .ToList();

            messages.Add(WithFiles(node.Role, content, messageAttachments));
        }

        // 修剪完整回合：若有較舊訊息被排除，確保剩下的歷史起點為 user 訊息
        if (take < path.Count)
        {
            while (messages.Count + (keepNewest ? 0 : 1) > 1 && messages[0].Role != "user")
            {
                messages.RemoveAt(0);
                omitted++;
            }
        }

        return new Composed(messages, omitted);
    }

    private InferenceMessage WithFiles(string role, string content, IReadOnlyList<Attachment> files)
    {
        var text = new StringBuilder(content);
        foreach (var file in files.Where(x => x.ExtractedText is not null))
        {
            text.Append("\n\n--- 文件：").Append(file.FileName).Append(" ---\n")
                .Append(file.ExtractedText)
                .Append("\n--- 文件結束 ---");
        }

        var images = files
            .Where(x => x.ContentType.StartsWith("image/", StringComparison.Ordinal))
            .Select(x => new InferenceImage(x.Id, x.ContentType, null, attachments.Value.ImageTokenEstimate))
            .ToArray();

        return new(role, text.ToString(), images);
    }

    private static bool SupportsVision(List<InferenceMessage> chain, GenerationParameters parameters) =>
        parameters.SupportsImages || !chain.Any(x => x.Images?.Count > 0);

    // 保守的 Token 預算評估，依據 ContextTokens 扣減最大輸出與系統提示詞開銷
    private static long Budget(GenerationParameters parameters) =>
        parameters.ContextTokens - parameters.MaxOutputTokens - (MessageCost.Of(parameters.SystemPrompt) + 128);

    private static ContextUsageDto Trim(Composed composed, GenerationParameters parameters)
    {
        var chain = composed.Messages;
        var system = MessageCost.Of(parameters.SystemPrompt) + 128;
        var budget = Budget(parameters);
        var exceeded = budget < 0 || (chain.Count > 0 && MessageCost.Of(chain[^1]) > budget);
        var count = chain.Count;
        var cost = chain.Sum(MessageCost.Of);

        // 超出容量預算時，從最前端成對移除訊息，並確保剩餘鏈仍以 user 開頭
        while (cost > budget && chain.Count > 1)
        {
            cost -= MessageCost.Of(chain[0]);
            chain.RemoveAt(0);

            while (chain.Count > 1 && chain[0].Role != "user")
            {
                cost -= MessageCost.Of(chain[0]);
                chain.RemoveAt(0);
            }
        }

        return new(
            (int)Math.Min(int.MaxValue, cost + system),
            parameters.ContextTokens,
            parameters.MaxOutputTokens,
            composed.Omitted + count - chain.Count,
            exceeded);
    }
}
