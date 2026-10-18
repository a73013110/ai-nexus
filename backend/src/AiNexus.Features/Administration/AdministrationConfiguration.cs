using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Administration;

/// <summary>存取管理的角色、功能群組、功能與 policy 識別字；對應的資料列由 <c>database/App/Seed</c> 寫入。</summary>
public static class AdministrationConfiguration
{
    public const string Role = "administrator", Group = BuiltInAccess.AdministratorsGroup, Feature = FeatureIds.Admin, Policy = Policies.Admin;
}
