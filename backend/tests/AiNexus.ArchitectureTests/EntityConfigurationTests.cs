using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// <c>NexusDbContext</c> applies entity configurations with <c>ApplyConfigurationsFromAssembly</c>, which silently skips a
/// configuration class without a public parameterless constructor; its mapping would vanish from the model.
/// </summary>
public sealed class EntityConfigurationTests
{
    [Fact]
    public void Entity_configurations_have_a_parameterless_constructor()
    {
        var configurations = Assemblies.Features.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))).ToList();
        Assert.NotEmpty(configurations);
        var skipped = configurations.Where(t => t.GetConstructor(Type.EmptyTypes) is null).Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(skipped.Count == 0, "Give these configurations a public parameterless constructor:\n" + string.Join("\n", skipped));
    }
}
