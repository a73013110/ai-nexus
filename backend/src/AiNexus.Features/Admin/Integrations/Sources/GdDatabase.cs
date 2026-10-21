using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>公文系統（Gd）的唯讀來源資料庫，連線設定位於 <c>Databases:Gd</c>。</summary>
public sealed class GdDatabase : ISqlDatabaseDefinition
{
    private GdDatabase() { }

    public static string Name => "Gd";
    public static bool ReadOnly => true;
}
