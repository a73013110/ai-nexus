using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>系統日誌的功能與 policy 名稱；功能 id 定義在 <see cref="FeatureCatalog"/>。</summary>
public static class DiagnosticConfiguration
{
    public const string Query = FeatureCatalog.DiagnosticsLogs, Detail = FeatureCatalog.DiagnosticsLogsDetail, Export = FeatureCatalog.DiagnosticsLogsExport;
    public const string QueryPolicy = Policies.Prefix + Query, DetailPolicy = Policies.Prefix + Detail, ExportPolicy = Policies.Prefix + Export;
}
