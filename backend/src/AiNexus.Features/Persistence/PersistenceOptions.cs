using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence;

/// <summary>主機如何處理 Nexus 資料庫的結構；連線設定位於 <c>Databases:Nexus</c>。</summary>
public sealed class PersistenceOptions : IValidatableObject
{
    public const string Section = "Persistence";

    /// <summary>
    /// 啟動時是否自動套用待執行的 migration。關閉時只檢查；有待執行的 migration 就拒絕啟動，由 DBA 或腳本先行套用。
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>
    /// 此選項只有一個旗標、沒有自己的驗證規則。仍實作此介面，才能和其他選項一樣經由產生的 validator 驗證
    /// （產生器不接受沒有可驗證內容的型別）；未知的設定鍵仍會在綁定時失敗。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];
}

[OptionsValidator]
public sealed partial class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>;
