namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 內部日誌接收回執，只用來追蹤日誌是否成功進入記憶體佇列。不會被序列化，也不由客戶端提供。
/// </summary>
public sealed class DiagnosticDelivery
{
    public bool Accepted { get; internal set; }
}
