namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 內部日誌接收回執。
/// 僅供內部管道追蹤日誌是否成功寫入記憶體佇列，絕不序列化也不由客戶端提供。
/// </summary>
public sealed class DiagnosticDelivery
{
    public bool Accepted { get; internal set; }
}
