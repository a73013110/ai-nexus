using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Integrations.Sources;

/// <summary>
/// 公文系統（Gd）的來源資料庫，連線設定位於 <c>Databases:Gd</c>，Integrations 與出勤共用。
/// 不宣告唯讀：出勤的假日回寫要寫 <c>Pub_Days</c>（ADR 0018），寫入範圍由程式限定；只想讀時由部署設 <c>ReadOnly=true</c>。
/// </summary>
public sealed class GdDatabase : ISqlDatabaseDefinition
{
    private GdDatabase() { }

    public static string Name => "Gd";
}
