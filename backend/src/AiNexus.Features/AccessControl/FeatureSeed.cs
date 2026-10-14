using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.AccessControl.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// The features a module adds, the built-in group that grants each one and their default menu placement, seeded with
/// <c>HasData</c>. A module declares one sealed subclass with a parameterless constructor next to its module class;
/// <c>ApplyConfigurationsFromAssembly</c> applies it like any other entity configuration. A module whose features need a
/// menu group of their own (say <c>workforce</c> 「員工服務」) overrides <see cref="NavigationGroups"/>; the built-in groups
/// are seeded by <see cref="NavigationGroupConfiguration"/>.
/// </summary>
public abstract class FeatureSeed(params PlatformFeature[] features) : IEntityTypeConfiguration<Feature>, IEntityTypeConfiguration<RoleGroupFeature>, IEntityTypeConfiguration<NavigationGroup>
{
    /// <summary>Menu groups this module adds; none by default.</summary>
    protected virtual IEnumerable<NavigationGroup> NavigationGroups => [];

    public void Configure(EntityTypeBuilder<Feature> builder)
        => builder.HasData(features.Select(x => new Feature { Id = x.Id, Name = x.Name, Route = x.Route, SortOrder = x.SortOrder, GroupId = x.GroupId, Icon = x.Icon }));

    public void Configure(EntityTypeBuilder<RoleGroupFeature> builder)
        => builder.HasData(features.Select(x => new RoleGroupFeature { GroupId = x.AdministratorsOnly ? BuiltInAccess.AdministratorsGroup : BuiltInAccess.WorkspaceGroup, FeatureId = x.Id }));

    public void Configure(EntityTypeBuilder<NavigationGroup> builder) => builder.HasData(NavigationGroups);
}
