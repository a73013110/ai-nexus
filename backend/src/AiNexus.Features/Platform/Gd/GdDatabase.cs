using AiNexus.Platform.Data.External;

namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 公文系統 GD 的資料庫，連線設定位於 <c>Databases:Gd</c>；出勤（假單、假日與回寫）與組織匯入共用，一律經 <c>ExternalDatabase&lt;GdDatabase&gt;</c>。
/// 不宣告唯讀：出勤的假日回寫要寫 <c>Pub_Days</c>（ADR 0018），寫入範圍由程式限定；只想讀時由部署設 <c>ReadOnly=true</c>。
/// </summary>
public sealed class GdDatabase : IExternalDatabaseDefinition
{
    private GdDatabase() { }

    public static string Name => "Gd";
    public static string DisplayName => "公文系統 GD";
    public static string Dependents => "假單、出差單、假日與組織匯入";
}
