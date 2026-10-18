using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Diagnostics;

/// <summary>系統日誌的功能與 policy 名稱；功能列由 <c>database/App/Seed</c> 寫入。</summary>
public static class DiagnosticConfiguration
{
    public const string Query = "logs.query", Detail = "logs.detail", Export = "logs.export";
    public const string QueryPolicy = Policies.Prefix + Query, DetailPolicy = Policies.Prefix + Detail, ExportPolicy = Policies.Prefix + Export;
}
