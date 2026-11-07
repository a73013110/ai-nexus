using AiNexus.Data.App;
using AiNexus.Data.App.Identity;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>
/// 外部系統頁的一列。<see cref="KeyHint"/> 是金鑰末四碼；<see cref="LastUsedAt"/> 是最近一次以金鑰呼叫的時間（不論驗證結果），
/// 取自稽核，超過稽核保存期或從沒呼叫過為 null。
/// </summary>
public sealed record ApiClientDto(Guid Id, long RowVersion, string Name, string KeyHint, DateTimeOffset KeyIssuedAt, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt)
{
    // 外部系統的呼叫都以系統為稽核對象（ResourceId）；之後新增給外部系統的 API，把它的稽核動作加進來。
    private static readonly string[] CallActions = [IdentityAudit.CredentialsVerified.Name];

    public static IQueryable<ApiClientDto> Project(AppDbContext db, IQueryable<ApiClient> clients) => clients.Select(x => new ApiClientDto(
        x.Id, x.RowVersion, x.Name, x.KeyHint, x.KeyIssuedAt, x.CreatedAt,
        // ResourceId 有索引；同一系統的稽核幾乎都是呼叫，依 Id 倒序很快就找到。
        db.AuditEvents.Where(a => a.ResourceId == x.Id && CallActions.Contains(a.Action)).Max(a => (DateTimeOffset?)a.At)));
}

/// <summary>剛產生的金鑰。<see cref="Key"/> 只在這次回應出現，之後無法再查詢；遺失就重新產生。</summary>
public sealed record IssuedApiClientKeyDto(ApiClientDto Client, string Key);
