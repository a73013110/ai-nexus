using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Platform.Data.External;

/// <summary>外部資料庫的註冊。</summary>
public static class ExternalDatabaseServices
{
    /// <summary>
    /// 宣告 <c>Databases:&lt;Name&gt;</c>（含唯讀檢查與顯示名稱），註冊 <see cref="ExternalDatabase{TDatabase}"/>（單例），並加入監控。
    /// 新增一個外部資料庫只需要定義與這一行，見 docs/architecture/external-databases.md。
    /// </summary>
    public static IServiceCollection AddExternalDatabase<TDatabase>(this IServiceCollection services) where TDatabase : IExternalDatabaseDefinition
    {
        services.AddDatabase(TDatabase.Name, TDatabase.ReadOnly, TDatabase.DisplayName);
        services.AddSqlDatabase<TDatabase>();
        services.AddSingleton<ExternalDatabase<TDatabase>>();
        return services.AddMonitoringSource<ExternalDatabaseMonitoring<TDatabase>>();
    }
}
