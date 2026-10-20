using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>寄送紀錄的篩選條件；分頁（<c>before</c>、<c>take</c>）另外帶。</summary>
public sealed class MailMessageFilter
{
    /// <summary><see cref="MailStates"/> 其中之一；空值＝全部。</summary>
    public string? Status { get; set; }
    /// <summary>分類前綴（例：<c>account.</c>）。</summary>
    public string? Category { get; set; }
    /// <summary>
    /// 依格式判斷：GUID＝訊息或批次 ID；本體是 32 碼十六進位的 Message-ID（<c>&lt;…@…&gt;</c>）＝訊息 ID；
    /// email＝收件者（比對雜湊，所以要完整地址）；其他＝主旨開頭。
    /// </summary>
    public string? Search { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? Until { get; set; }

    internal bool IsValid()
        => (string.IsNullOrEmpty(Status) || MailStates.All.Contains(Status)) && !(Category?.Length > OutboundMessage.CategoryMaxLength)
           && !(Search?.Length > 254) && !(From > Until);

    /// <summary>符合條件的列，新到舊（<c>CreatedAt</c>、<c>Id</c> 遞減，走 <c>ix_OutboundMessages_CreatedAt</c>）。</summary>
    internal IQueryable<OutboundMessage> Query(AppDbContext db)
    {
        var query = db.Set<OutboundMessage>().AsNoTracking();
        if (!string.IsNullOrEmpty(Status)) { var status = Status; query = query.Where(x => x.Status == status); }
        if (!string.IsNullOrWhiteSpace(Category)) { var category = Category.Trim(); query = query.Where(x => x.Category.StartsWith(category)); }
        if (From is { } start) query = query.Where(x => x.CreatedAt >= start);
        if (Until is { } end) query = query.Where(x => x.CreatedAt < end);
        var search = Search?.Trim();
        if (string.IsNullOrEmpty(search)) return query;
        if (Guid.TryParse(search, out var id)) return query.Where(x => x.Id == id || x.BatchId == id);
        if (HeaderId(search) is { } header) return query.Where(x => x.Id == header);
        if (MailAddresses.TryNormalize(search, out var address)) { var hash = MailAddresses.Hash(address); return query.Where(x => x.RecipientHash == hash); }
        return query.Where(x => x.Subject.StartsWith(search));
    }

    /// <summary>從 <c>&lt;0123…cdef@domain&gt;</c> 取出訊息 ID；本體不是 32 碼十六進位就不是我方產生的 Message-ID。</summary>
    private static Guid? HeaderId(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0) return null;
        var local = value[..at].TrimStart('<');
        return local.Length == 32 && Guid.TryParseExact(local, "N", out var id) ? id : null;
    }
}
