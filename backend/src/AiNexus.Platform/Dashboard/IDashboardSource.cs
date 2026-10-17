namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 總覽儀表板的區塊資料來源，由統計該資料的業務模組自行擁有。
/// 儀表板會動態讀取檢視者有權限看見的所有註冊來源，因此新增功能時只需在自身模組中註冊來源，前端即可透過通用 Widget 自動渲染。
/// </summary>
public interface IDashboardSource
{
    /// <summary>顯示此區塊所需之功能授權代碼。若未提供 Title 或 Route，則預設回退至該功能的名稱與路徑。</summary>
    string Feature { get; }

    /// <summary>讀取指定統計範圍下的數據指標。當該區塊無內容可呈現時（例如功能未啟用）回傳 <c>null</c>。</summary>
    Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct);
}

public static class DashboardSourceRegistration
{
    /// <summary>將區塊資料來源加入至總覽儀表板。來源為 Scoped 生命週期，在當前請求內循序執行讀取。</summary>
    public static IServiceCollection AddDashboardSource<TSource>(this IServiceCollection services) where TSource : class, IDashboardSource
        => services.AddScoped<IDashboardSource, TSource>();
}
