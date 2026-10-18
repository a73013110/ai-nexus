using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.Data.App;

/// <summary>主資料庫在請求與啟動階段的檢查。</summary>
public static class DatabaseStartup
{
    /// <summary>
    /// 連線尚未設定完成時，除認證端點外的 API 請求直接回傳 503，避免每個請求都去連一個不存在的資料庫而逾時。
    /// </summary>
    public static IApplicationBuilder UseStorageReadiness(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        // 認證端點放行，讓管理員仍能登入並完成連線設定。
        if (http.Request.Path.StartsWithSegments("/api/v1") && !http.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            http.RequestServices.GetRequiredService<StorageReadiness>().RequireConfigured();
        }

        await next(http);
    });

    /// <summary>
    /// 在背景服務與 API 請求開始前檢查資料庫。結構由 DBA 以 <c>database/App</c> 部署，這裡不建立也不修改結構。
    /// </summary>
    /// <returns>若資料庫無法使用則回傳 <see langword="false"/>，呼叫端應立即停止 Host 啟動。</returns>
    public static async Task<bool> EnsureDatabaseReadyAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StorageReadiness>();
        if (!storage.Configured)
        {
            return true;
        }

        try
        {
            // RCSI 只檢查並記錄警告，不在此切換：切換需要排他鎖，必須由 DBA 於維護期間手動執行。
            await scope.ServiceProvider.GetRequiredService<DatabaseSchema>().ReadCommittedSnapshotAsync(CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            await DiagnosticStartup.RecordAsync(ex, app.Configuration, app.Environment);
            Console.Error.WriteLine(ex is ExternalServiceException external
                ? external.Message
                : DatabaseDiagnosis.Describe(ex, AppDbContext.DatabaseName));
            return false;
        }
    }
}
