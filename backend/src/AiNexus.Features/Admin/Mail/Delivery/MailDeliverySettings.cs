using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 管理頁可切換的寄送設定（<c>mail.DeliverySettings</c>）。dispatcher 每一輪讀一次主鍵，不另做快取：
/// 成本只是一次索引查詢，儲存後下一輪就生效，也不必處理快取失效。
/// </summary>
public static class MailDeliverySettings
{
    /// <summary>暫時性與傳輸層失敗是否自動退避重試；沒有設定列時為 true。</summary>
    public static async Task<bool> AutoRetryAsync(AppDbContext db, CancellationToken ct)
        => await db.Set<DeliverySetting>().AsNoTracking().Where(x => x.Id == DeliverySetting.SingletonId).Select(x => (bool?)x.AutoRetry).SingleOrDefaultAsync(ct) ?? true;
}
