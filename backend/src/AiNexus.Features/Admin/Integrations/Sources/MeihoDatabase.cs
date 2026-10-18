using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>校務系統（Meiho）的唯讀來源資料庫，連線設定位於 <c>Databases:Meiho</c>。</summary>
public sealed class MeihoDatabase : ISqlDatabaseDefinition
{
    private MeihoDatabase() { }

    public static string Name => "Meiho";
    public static bool ReadOnly => true;
}
