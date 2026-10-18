using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Diagnostics;

/// <summary>系統日誌的功能與 policy 名稱；功能 id 定義在 <see cref="FeatureIds"/>。</summary>
public static class DiagnosticConfiguration
{
    public const string Query = FeatureIds.LogsQuery, Detail = FeatureIds.LogsDetail, Export = FeatureIds.LogsExport;
    public const string QueryPolicy = Policies.Prefix + Query, DetailPolicy = Policies.Prefix + Detail, ExportPolicy = Policies.Prefix + Export;
}
