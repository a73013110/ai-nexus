using System.Text;
using AiNexus.BuildingBlocks;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Inference;

public sealed class ContextBuilder(NexusDbContext db)
{
    public async Task<IReadOnlyList<InferenceMessage>> BuildAsync(Guid conversation, Guid leaf, GenerationParameters parameters, CancellationToken ct)
    {
        var chain = await ChainAsync(conversation, leaf, ct);
        var preview = Trim(chain, parameters);
        if (preview.BudgetExceeded) throw new ApiException(400, "context_budget_exceeded", "提問超過此模型的上下文預算，請縮短內容或選擇較大上下文的模型。");
        return new[] { new InferenceMessage("system", parameters.SystemPrompt) }.Concat(chain).ToList();
    }

    public async Task<ContextUsageDto> PreviewAsync(Guid? conversation, Guid? parent, string? prompt, GenerationParameters parameters, CancellationToken ct)
    {
        if (conversation is null && parent is not null) throw new ApiException(400, "invalid_parent", "上文需要指定對話。");
        var chain = conversation is Guid id && parent is Guid leaf ? await ChainAsync(id, leaf, ct) : [];
        if (!string.IsNullOrWhiteSpace(prompt)) chain.Add(new("user", prompt.Trim()));
        return Trim(chain, parameters);
    }

    private async Task<List<InferenceMessage>> ChainAsync(Guid conversation, Guid leaf, CancellationToken ct)
    {
        var persisted = await db.Messages.AsNoTracking().Where(x => x.ConversationId == conversation).ToListAsync(ct);
        var lookup = persisted.Concat(db.Messages.Local.Where(x => x.ConversationId == conversation)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var chain = new List<InferenceMessage>();
        var visited = new HashSet<Guid>();
        Guid? next = leaf;
        while (next is Guid id)
        {
            if (!visited.Add(id) || !lookup.TryGetValue(id, out var message)) throw new ApiException(409, "invalid_history", "對話分支資料不完整。");
            if (message.Role == "user" || (!RunStates.IsActive(message.Status) && message.Content.Length > 0)) chain.Add(new(message.Role, message.Content));
            next = message.ParentId;
        }
        chain.Reverse();
        return chain;
    }

    private static ContextUsageDto Trim(List<InferenceMessage> chain, GenerationParameters parameters)
    {
        // A conservative UTF-8 byte budget avoids assuming English token ratios for Chinese.
        // No server tokenizer is required; actual usage is recorded when supported.
        var system = Cost(parameters.SystemPrompt) + 128;
        var budget = parameters.ContextTokens - parameters.MaxOutputTokens - system;
        var exceeded = chain.Count > 0 && Cost(chain[^1].Content) > budget;
        var count = chain.Count;
        var cost = chain.Sum(x => (long)Cost(x.Content));
        while (cost > budget && chain.Count > 1)
        {
            cost -= Cost(chain[0].Content);
            chain.RemoveAt(0);
            while (chain.Count > 1 && chain[0].Role != "user") { cost -= Cost(chain[0].Content); chain.RemoveAt(0); }
        }
        return new((int)Math.Min(int.MaxValue, cost + system), parameters.ContextTokens, parameters.MaxOutputTokens, count - chain.Count, exceeded);
    }
    private static int Cost(string value) => Encoding.UTF8.GetByteCount(value) + 32;
}
