using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.AccessControl.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// 模組的功能種子：以 <c>HasData</c> 寫入功能列、預設授權群組與預設選單位置。模組在自己的模組類別旁宣告一個 sealed 子類別，
/// 由 <c>ApplyConfigurationsFromAssembly</c> 自動套用。模組若需要自己的選單分群（例如 <c>workforce</c>），覆寫 <see cref="NavigationGroups"/>；
/// 內建分群則由 <see cref="NavigationGroupConfiguration"/> 寫入。
/// </summary>
public abstract class FeatureSeed(params PlatformFeature[] features) : IEntityTypeConfiguration<Feature>, IEntityTypeConfiguration<RoleGroupFeature>, IEntityTypeConfiguration<NavigationGroup>
{
    /// <summary>此模組額外新增的選單分群；預設沒有。</summary>
    protected virtual IEnumerable<NavigationGroup> NavigationGroups => [];

    public void Configure(EntityTypeBuilder<Feature> builder)
        => builder.HasData(features.Select(x => new Feature { Id = x.Id, Name = x.Name, Route = x.Route, SortOrder = x.SortOrder, GroupId = x.GroupId, Icon = x.Icon }));

    public void Configure(EntityTypeBuilder<RoleGroupFeature> builder)
        => builder.HasData(features.Select(x => new RoleGroupFeature
        {
            GroupId = x.AdministratorsOnly ? BuiltInAccess.AdministratorsGroup : BuiltInAccess.WorkspaceGroup,
            FeatureId = x.Id,
        }));

    public void Configure(EntityTypeBuilder<NavigationGroup> builder) => builder.HasData(NavigationGroups);
}
