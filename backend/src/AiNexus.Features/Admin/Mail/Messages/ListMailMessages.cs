using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 寄送紀錄，新到舊，每頁 25、50（預設）或 100 筆；<c>before</c> 是上一頁最後一筆的 ID（游標）。
/// 不回傳總數：大表的 COUNT 與深 offset 成本隨資料量成長，期間新入列的信也不會讓翻頁重複。
/// </summary>
internal sealed class ListMailMessages(AppDbContext db)
{
    public static readonly int[] PageSizes = [25, 50, 100];

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", ([AsParameters] MailMessageFilter filter, Guid? before, int? take, ListMailMessages handler, CancellationToken ct) =>
            handler.HandleAsync(filter, before, take, ct).ToHttpResultAsync())
        .Audited(MailAudit.MessageRead)
        .WithName("ListMailMessages");

    public async Task<Result<IReadOnlyList<MailMessageDto>>> HandleAsync(MailMessageFilter filter, Guid? before, int? take, CancellationToken ct)
    {
        if (!filter.IsValid() || take is { } size && !PageSizes.Contains(size)) return MailErrors.FilterInvalid;
        var query = filter.Query(db);
        if (before is { } cursor)
        {
            var previous = await db.Set<OutboundMessage>().AsNoTracking().Where(x => x.Id == cursor).Select(x => (DateTimeOffset?)x.CreatedAt).SingleOrDefaultAsync(ct);
            if (previous is not { } at) return MailErrors.CursorInvalid;
            query = query.Where(x => x.CreatedAt < at || (x.CreatedAt == at && x.Id.CompareTo(cursor) < 0));
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(take ?? 50).ToListAsync(ct);
        return Result<IReadOnlyList<MailMessageDto>>.Ok(rows.Select(MailMessageDto.From).ToArray());
    }
}
