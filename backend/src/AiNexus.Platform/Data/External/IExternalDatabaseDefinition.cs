using AiNexus.Platform.Data.Sql;

namespace AiNexus.Platform.Data.External;

/// <summary>
/// 別的系統的資料庫（如公文系統 GD）：結構不歸本系統管、隨時可能連不上。以 <see cref="ExternalDatabaseServices.AddExternalDatabase{TDatabase}"/> 註冊，
/// 程式一律經 <see cref="ExternalDatabase{TDatabase}"/> 存取，監控自動出現一張卡片。
/// </summary>
public interface IExternalDatabaseDefinition : ISqlDatabaseDefinition
{
    /// <summary>監控卡片與流量相依的名稱，例如「公文系統 GD」。</summary>
    static abstract string DisplayName { get; }

    /// <summary>依賴它的功能，接在監控摘要的「……暫時無法使用」前面，例如「假單、出差單、假日與組織匯入」。</summary>
    static abstract string Dependents { get; }
}
