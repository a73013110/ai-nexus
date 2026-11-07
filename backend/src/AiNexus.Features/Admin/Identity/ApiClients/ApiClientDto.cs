using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>
/// 外部系統頁的一列。<see cref="KeyHint"/> 是金鑰末四碼；<see cref="LastUsedAt"/> 與 <see cref="RecentCalls"/>
/// （近 <see cref="RecentDays"/> 天的呼叫次數）都取自稽核，不論驗證結果；超過稽核保存期的呼叫不算。
/// </summary>
public sealed record ApiClientDto(
    Guid Id, long RowVersion, string Name, string? Contact, bool Enabled, string KeyHint,
    DateTimeOffset KeyIssuedAt, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, int RecentCalls)
{
    public const int RecentDays = 30;

    /// <summary>
    /// 呼叫以系統為稽核對象（ResourceId）。動作以常數寫進 SQL，才對得上只收帳密驗證的 filtered 索引
    /// <c>ix_AuditEvents_ResourceId_At</c>；之後新增給外部系統的 API，要同時放寬這個索引的條件。
    /// </summary>
    public static IQueryable<ApiClientDto> Project(AppDbContext db, IQueryable<ApiClient> clients, DateTimeOffset now)
    {
        var since = now.AddDays(-RecentDays);
        var calls = db.AuditEvents.Where(a => a.Action == EF.Constant(IdentityAudit.CredentialsVerified.Name));
        return clients.Select(x => new ApiClientDto(
            x.Id, x.RowVersion, x.Name, x.Contact, x.Enabled, x.KeyHint, x.KeyIssuedAt, x.CreatedAt,
            calls.Where(a => a.ResourceId == x.Id).Max(a => (DateTimeOffset?)a.At),
            calls.Count(a => a.ResourceId == x.Id && a.At >= since)));
    }
}

/// <summary>剛產生的金鑰。<see cref="Key"/> 只在這次回應出現，之後無法再查詢；遺失就重新產生。</summary>
public sealed record IssuedApiClientKeyDto(ApiClientDto Client, string Key);
