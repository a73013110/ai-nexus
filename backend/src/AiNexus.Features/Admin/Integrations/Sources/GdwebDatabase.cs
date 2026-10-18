using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>公文系統（Gdweb）的唯讀來源資料庫，連線設定位於 <c>Databases:Gdweb</c>。</summary>
public sealed class GdwebDatabase : ISqlDatabaseDefinition
{
    private GdwebDatabase() { }

    public static string Name => "Gdweb";
    public static bool ReadOnly => true;
}
