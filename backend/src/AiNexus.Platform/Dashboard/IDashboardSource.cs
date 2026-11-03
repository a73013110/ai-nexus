using AiNexus.Platform.Modules;

namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 總覽儀表板的區塊資料來源，由統計該資料的業務模組自行擁有。
/// 儀表板會動態讀取檢視者有權限看見的所有來源，新增功能時只要實作這個介面（由組合產生器註冊），前端即可透過通用 Widget 自動渲染。
/// </summary>
[RegisterImplementations(typeof(DashboardSourceRegistration), nameof(DashboardSourceRegistration.AddDashboardSource))]
public interface IDashboardSource
{
    /// <summary>區塊所需的功能授權代碼；未提供 Title 或 Route 時，會沿用該功能的名稱與路徑。</summary>
    string Feature { get; }

    /// <summary>讀取指定範圍的指標。區塊沒有內容可顯示時（例如功能未啟用）回傳 <c>null</c>。</summary>
    Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct);
}

public static class DashboardSourceRegistration
{
    /// <summary>註冊區塊來源。來源為 Scoped，同一請求內會依序讀取。</summary>
    public static IServiceCollection AddDashboardSource<TSource>(this IServiceCollection services) where TSource : class, IDashboardSource
        => services.AddScoped<IDashboardSource, TSource>();
}
