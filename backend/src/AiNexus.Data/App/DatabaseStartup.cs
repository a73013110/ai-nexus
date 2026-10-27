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
    /// 在背景服務與 API 請求開始前檢查資料庫：RCSI 與 EF 模型對實際結構的唯讀比對。結構由 DBA 以 <c>database/App</c> 部署，這裡不建立也不修改結構。
    /// </summary>
    /// <returns>若資料庫無法使用或結構與 EF 模型不一致則回傳 <see langword="false"/>，呼叫端應立即停止 Host 啟動。</returns>
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
            var schema = scope.ServiceProvider.GetRequiredService<DatabaseSchema>();
            // RCSI 只檢查並記錄警告，不在此切換：切換需要排他鎖，必須由 DBA 於維護期間手動執行。
            // 連不上時回傳 null：照常啟動，由健康檢查回報，讓網站能在 SQL 恢復後自行就緒。
            if (await schema.ReadCommittedSnapshotAsync(CancellationToken.None) is null)
            {
                return true;
            }

            var blocking = (await schema.CompareModelAsync(CancellationToken.None)).Where(x => x.Blocking).ToList();
            if (blocking.Count == 0)
            {
                return true;
            }

            Console.Error.WriteLine($"資料庫結構與這一版程式的 EF 對應不一致（{blocking.Count} 項），停止啟動：");
            foreach (var difference in blocking)
            {
                Console.Error.WriteLine("  " + difference);
            }
            Console.Error.WriteLine("請以結構描述比對把發布套件的 database\\App.dacpac 套用到這個資料庫，或確認網站與資料庫是同一版（docs/operations/database-setup.md）。");
            return false;
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
