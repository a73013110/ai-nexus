using NetArchTest.Rules;

namespace AiNexus.ArchitectureTests;

/// <summary>專案之間的依賴方向：Host → Features → Data → Platform，不可反向（ADR 0013）。</summary>
public sealed class LayerTests
{
    [Fact]
    public void PlatformDoesNotDependOnOtherProjects()
        => AssertNoDependency(Types.InAssembly(Assemblies.Platform), "AiNexus.Data", "AiNexus.Features", "AiNexus.Host");

    /// <summary>實體只依賴 Platform：引用模組型別會讓資料表跟著模組形成循環。</summary>
    [Fact]
    public void DataDoesNotDependOnFeaturesOrHost()
        => AssertNoDependency(Types.InAssembly(Assemblies.Data), "AiNexus.Features", "AiNexus.Host");

    [Fact]
    public void FeaturesDoNotDependOnHost()
        => AssertNoDependency(Types.InAssembly(Assemblies.Features), "AiNexus.Host");

    [Fact]
    public void FeatureModulesAreRegisteredThroughTheModuleContract()
    {
        var modules = Assemblies.Features.GetTypes().Concat(Assemblies.Data.GetTypes())
            .Where(t => t.Name.EndsWith("Module", StringComparison.Ordinal) && t.IsClass && !t.IsAbstract).ToList();
        Assert.NotEmpty(modules);
        Assert.All(modules, module => Assert.Contains(typeof(AiNexus.Platform.Modules.IFeatureModule), module.GetInterfaces()));
    }

    private static void AssertNoDependency(Types types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, "Forbidden dependency from: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}
