namespace AiNexus.Features.Admin.Audit;

/// <summary>結果篩選的 <c>success</c>／<c>failed</c> 群組依據：列在這裡的結果算受理，其餘都算失敗。</summary>
public static class AuditOutcomes
{
    // 舊紀錄保留當時的結果值，篩選條件必須涵蓋所有成功操作，否則舊紀錄會漏列。
    public static readonly string[] Accepted = ["saved", "read", "completed", "success", "granted", "granted_once", "created", "deleted", "soft_deleted", "queued", "running", "cancelled", "revoked", "removed", "assigned", "read-only", "private", "started", "restored", "activated", "cleared", "connected", "csv", "metrics", "reindexing", "snapshot"];
}
