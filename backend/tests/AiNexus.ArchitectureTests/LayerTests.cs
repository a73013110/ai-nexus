using NetArchTest.Rules;
using Xunit;

namespace AiNexus.ArchitectureTests;

/// <summary>Dependency direction between projects: Host → Features → Platform, never the reverse.</summary>
public sealed class LayerTests
{
    [Fact]
    public void Platform_does_not_depend_on_features_or_host()
        => AssertNoDependency(Types.InAssembly(Assemblies.Platform), "AiNexus.Features", "AiNexus.Host");

    [Fact]
    public void Features_do_not_depend_on_host()
        => AssertNoDependency(Types.InAssembly(Assemblies.Features), "AiNexus.Host");

    [Fact]
    public void Feature_modules_are_registered_through_the_module_contract()
    {
        var modules = Assemblies.Features.GetTypes().Where(t => t.Name.EndsWith("Module", StringComparison.Ordinal) && t.IsClass && !t.IsAbstract).ToList();
        Assert.NotEmpty(modules);
        Assert.All(modules, module => Assert.Contains(typeof(AiNexus.Platform.Modules.IFeatureModule), module.GetInterfaces()));
    }

    private static void AssertNoDependency(Types types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, "Forbidden dependency from: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}
