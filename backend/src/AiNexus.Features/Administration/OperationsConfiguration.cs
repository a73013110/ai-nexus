using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Administration;

/// <summary>
/// Platform operations: platform usage, retrieval administration, model and tool prices and the platform-wide overview.
/// It is a grant of its own, separate from access management (<see cref="AdministrationConfiguration"/>).
/// </summary>
public sealed class OperationsConfiguration() : FeatureSeed(new PlatformFeature(Feature, "平台營運", "/admin/operations", 91, BuiltInAccess.SystemNavigation, "chart", AdministratorsOnly: true))
{
    public const string Feature = FeatureIds.Operations, Policy = Policies.Operations;
}
