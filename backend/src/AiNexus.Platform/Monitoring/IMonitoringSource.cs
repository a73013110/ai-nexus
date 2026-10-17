namespace AiNexus.Platform.Monitoring;

/// <summary>
/// 執行階段監控頁面上的單一子系統來源，由匯報該狀態的業務模組所擁有。
/// 監控系統採延遲讀取策略：僅在有人檢視時、至多每 <see cref="Refresh"/> 週期一次、於獨立的 DI Scope 中執行，並設置短逾時時間，確保任何單一來源的故障絕不拖慢整個監控頁面。
/// 讀取實作應維持極低成本：僅查詢記憶體狀態、設定值或具索引之彙總。
/// </summary>
public interface IMonitoringSource
{
    /// <summary>前端作為索引鍵的穩定識別碼，例如 <c>jobs</c>。</summary>
    string Id { get; }

    /// <summary>簡短繁體中文名稱，例如「背景工作」。</summary>
    string Name { get; }

    /// <summary>頁面顯示順序；數值較小者排在前方。</summary>
    int Order { get; }

    /// <summary>讀數快取有效期限；在此期間內不會重複讀取。</summary>
    TimeSpan Refresh { get; }

    /// <summary>讀取當前狀態。若拋出例外或逾時，頁面將該子系統標記為未觀察到（unobserved）。</summary>
    Task<SubsystemReading> ReadAsync(CancellationToken ct);
}

/// <summary>已註冊的監控來源型別；每次讀取時於全新的 DI Scope 中解析。</summary>
public sealed record MonitoringSourceRegistration(Type Type);

public static class MonitoringSourceRegistrations
{
    /// <summary>將子系統加入至執行階段監控。來源為 Scoped 生命週期，每次讀取皆擁有獨立 Scope。</summary>
    public static IServiceCollection AddMonitoringSource<TSource>(this IServiceCollection services) where TSource : class, IMonitoringSource
    {
        services.AddScoped<TSource>();
        return services.AddSingleton(new MonitoringSourceRegistration(typeof(TSource)));
    }
}
