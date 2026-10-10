using NetArchTest.Rules;

namespace AiNexus.ArchitectureTests;

/// <summary>Dependency direction between projects: Host → Features → Platform, never the reverse.</summary>
public sealed class LayerTests
{
    [Fact]
    public void PlatformDoesNotDependOnFeaturesOrHost()
        => AssertNoDependency(Types.InAssembly(Assemblies.Platform), "AiNexus.Features", "AiNexus.Host");

    [Fact]
    public void FeaturesDoNotDependOnHost()
        => AssertNoDependency(Types.InAssembly(Assemblies.Features), "AiNexus.Host");

    [Fact]
    public void FeatureModulesAreRegisteredThroughTheModuleContract()
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
