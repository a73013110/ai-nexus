using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Administration;

/// <summary>存取管理的角色、功能群組、功能與 policy 識別字；值定義在 <see cref="BuiltInAccess"/> 與 <see cref="FeatureIds"/>。</summary>
public static class AdministrationConfiguration
{
    public const string Role = BuiltInAccess.AdministratorRole, Group = BuiltInAccess.AdministratorsGroup, Feature = FeatureIds.Admin, Policy = Policies.Admin;
}
