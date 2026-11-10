using AiNexus.Platform.Modules;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Scheduling;

/// <summary>
/// 常駐服務：隨程序啟動、一直跑到停站的 <see cref="BackgroundService"/>（佇列消費者、取樣器、日誌管線、排程引擎本身）。
/// 實作後「背景作業」頁的常駐服務清單自動列出；每個 hosted service 都必須實作（架構測試檢查），專案不留管理頁看不到的背景動作。
/// </summary>
/// <remarks>
/// 定期要做的事寫成 <see cref="IScheduledTask"/>，不寫常駐服務：只有每個程序都得自己做（本機檔案、記憶體狀態、自己手上的租約），
/// 或要立刻反應的工作（佇列）才常駐。狀態由 <see cref="ResidentServices"/> 直接讀 <see cref="BackgroundService.ExecuteTask"/>，實作不必回報。
/// </remarks>
[RegisterImplementations(typeof(ResidentServiceRegistrations), nameof(ResidentServiceRegistrations.AddResidentService))]
public interface IResidentService
{
    /// <summary>服務的識別、名稱與說明；只是管理頁的宣告，不影響執行。</summary>
    static abstract ResidentServiceDefinition Definition { get; }
}

/// <summary>
/// 常駐服務的宣告。<paramref name="Key"/> 是穩定識別（小寫、以點分隔模組與角色，例如 <c>mail.dispatcher</c>），
/// <paramref name="Category"/> 與排程任務用同一組模組名稱，<paramref name="Description"/> 一句話說它做什麼，
/// <paramref name="Rhythm"/> 是給管理員看的節奏（多久做一次、什麼會叫醒它）。
/// </summary>
public sealed record ResidentServiceDefinition(string Key, string Name, string Category, string Description, string Rhythm)
{
    /// <summary>
    /// 這個程序依設定不做事的原因（功能沒開、這個站台不負責），照常運作時回傳 null。管理頁每次讀取都會呼叫，所以只讀設定或記憶體。
    /// 沒有宣告原因卻已結束的服務，管理頁顯示為「已停止」，那是要查的狀況。
    /// </summary>
    public Func<IServiceProvider, string?>? Inactive { get; init; }
}

/// <summary>已註冊的常駐服務。介面有 static abstract 成員，不能當型別引數，所以另外登記定義與型別。</summary>
public sealed record ResidentServiceRegistration(ResidentServiceDefinition Definition, Type Type);

public static class ResidentServiceRegistrations
{
    /// <summary>由組合產生器呼叫（Platform 自己的服務手動呼叫）；hosted service 的註冊另外進行，這裡只登記宣告。</summary>
    public static IServiceCollection AddResidentService<TService>(this IServiceCollection services) where TService : BackgroundService, IResidentService
    {
        services.TryAddSingleton<ResidentServices>();
        return services.AddSingleton(new ResidentServiceRegistration(TService.Definition, typeof(TService)));
    }
}
