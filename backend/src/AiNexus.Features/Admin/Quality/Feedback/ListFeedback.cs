using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality.Feedback;

/// <summary>使用者自己跨對話的評分，給品質評測頁；排序、搜尋與分頁走共用的 <see cref="TableRequest"/>（ADR 0026）。</summary>
internal sealed class ListFeedback(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/feedback", ([AsParameters] TableRequest table, ICurrentUser user, ListFeedback handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, table, ct)).Audited(QualityAudit.FeedbackRead);

    public Task<Result<TablePage<FeedbackDto>>> HandleAsync(Guid actor, TableRequest table, CancellationToken ct)
        => (from f in db.MessageFeedback.AsNoTracking()
            join m in db.Messages on f.MessageId equals m.Id
            join c in db.Conversations on m.ConversationId equals c.Id
            where f.OwnerId == actor
            select new Row { MessageId = f.MessageId, ConversationId = c.Id, ConversationTitle = c.Title, Rating = f.Rating, Reason = f.Reason, Note = f.Note, UpdatedAt = f.UpdatedAt })
            .ToTablePageAsync(table, Columns, q => q.Select(x => new FeedbackDto(x.MessageId, x.ConversationId, x.ConversationTitle, x.Rating, x.Reason, x.Note, x.UpdatedAt)), ct);

    // 用可設定屬性的類別而不是 record 建構子：EF 才能在投影之後繼續翻譯篩選與排序。
    private sealed class Row
    {
        public Guid MessageId { get; init; }
        public Guid ConversationId { get; init; }
        public string ConversationTitle { get; init; } = "";
        public int Rating { get; init; }
        public string Reason { get; init; } = "";
        public string Note { get; init; } = "";
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private static readonly TableColumns<Row> Columns = new TableColumns<Row>()
        .Text("conversationTitle", x => x.ConversationTitle)
        .Range("updatedAt", x => x.UpdatedAt)
        .Key(x => x.MessageId)
        .DefaultSort("updatedAt", descending: true);
}
