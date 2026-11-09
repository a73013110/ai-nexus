using AiNexus.Data.App;
using AiNexus.Data.App.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Conversations;

internal static class ConversationQueries
{
    /// <summary>使用者自己、未被軟刪除的對話（含標籤）；找不到時回傳 null。</summary>
    public static Task<Conversation?> OwnedConversationAsync(this AppDbContext db, Guid owner, Guid id, CancellationToken ct) =>
        db.Conversations.Include(x => x.Labels).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);

    /// <summary>
    /// 標題或任一訊息內文含有 <paramref name="term"/> 的對話。刻意用子字串比對而不是全文索引：
    /// 中文斷詞後查不到詞的片段，索引也有延遲、剛送出的訊息會搜不到；掃描範圍已被擁有者與對話的索引限定在這個人自己的訊息。
    /// </summary>
    public static IQueryable<Conversation> Matching(this IQueryable<Conversation> query, AppDbContext db, string term)
    {
        var text = term.Trim();
        return query.Where(x => x.Title.Contains(text) || db.Messages.Any(m => m.ConversationId == x.Id && m.Content.Contains(text)));
    }

    /// <summary>對話是否還有正在生成的執行；有的話不能封存，也不能切換分支。</summary>
    public static Task<bool> HasActiveRunAsync(this AppDbContext db, Guid id, CancellationToken ct) =>
        db.GenerationRuns.AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct);
}
