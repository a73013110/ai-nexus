using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>外部系統的行政寫入共用的宣告。</summary>
internal static class ApiClientRules
{
    /// <summary>新增、改名、重新產生金鑰與刪除共用的寫入對象。快照只有名稱、末四碼與產生時間，金鑰雜湊不進稽核。</summary>
    public static AuditTarget Target(AppDbContext db, Guid id) => AuditTarget.Of(id, async ct =>
        await db.ApiClients.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Name, x.KeyHint, x.KeyIssuedAt }).SingleOrDefaultAsync(ct));

    /// <summary>名稱不分大小寫唯一（資料庫定序）；唯一索引是最後一道防線，先查才能回固定的錯誤代碼。</summary>
    public static Task<bool> NameTakenAsync(AppDbContext db, string name, Guid id, CancellationToken ct)
        => db.ApiClients.AnyAsync(x => x.Name == name && x.Id != id, ct);
}
