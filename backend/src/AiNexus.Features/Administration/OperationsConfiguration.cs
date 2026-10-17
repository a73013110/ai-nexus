using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Administration;

/// <summary>
/// 平台營運：平台用量、檢索管理、模型與工具價格，以及全平台總覽。它有自己的授權，與存取管理（<see cref="AdministrationConfiguration"/>）分開。
/// </summary>
public sealed class OperationsConfiguration() : FeatureSeed(new PlatformFeature(Feature, "平台營運", "/admin/operations", 91, BuiltInAccess.SystemNavigation, "chart", AdministratorsOnly: true))
{
    public const string Feature = FeatureIds.Operations, Policy = Policies.Operations;
}
