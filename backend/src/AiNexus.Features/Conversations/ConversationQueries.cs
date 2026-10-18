using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

internal static class ConversationQueries
{
    /// <summary>使用者自己、未被軟刪除的對話（含標籤）；找不到時回傳 null。</summary>
    public static Task<Conversation?> OwnedConversationAsync(this AppDbContext db, Guid owner, Guid id, CancellationToken ct) =>
        db.Set<Conversation>().Include(x => x.Labels).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);

    /// <summary>對話是否還有正在生成的執行；有的話不能封存，也不能切換分支。</summary>
    public static Task<bool> HasActiveRunAsync(this AppDbContext db, Guid id, CancellationToken ct) =>
        db.Set<GenerationRun>().AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct);
}
