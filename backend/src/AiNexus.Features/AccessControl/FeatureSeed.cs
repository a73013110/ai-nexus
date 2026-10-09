using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// The features a module adds and the built-in group that grants each one, seeded with <c>HasData</c>. A module declares
/// one sealed subclass with a parameterless constructor next to its module class; <c>ApplyConfigurationsFromAssembly</c>
/// applies it like any other entity configuration.
/// </summary>
public abstract class FeatureSeed(params PlatformFeature[] features) : IEntityTypeConfiguration<Feature>, IEntityTypeConfiguration<RoleGroupFeature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
        => builder.HasData(features.Select(x => new Feature { Id = x.Id, Name = x.Name, Route = x.Route, SortOrder = x.SortOrder }));

    public void Configure(EntityTypeBuilder<RoleGroupFeature> builder)
        => builder.HasData(features.Select(x => new RoleGroupFeature { GroupId = x.AdministratorsOnly ? BuiltInAccess.AdministratorsGroup : BuiltInAccess.WorkspaceGroup, FeatureId = x.Id }));
}
