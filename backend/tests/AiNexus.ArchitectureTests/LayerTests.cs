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
        var modules = Assemblies.Features.GetTypes()
            .Where(t => t.Name.EndsWith("Module", StringComparison.Ordinal) && t.IsClass && !t.IsAbstract).ToList();
        Assert.NotEmpty(modules);
        Assert.All(modules, module => Assert.Contains(typeof(AiNexus.Platform.Modules.IFeatureModule), module.GetInterfaces()));
    }

    /// <summary>組合產生器只看得到 Features 組件（ADR 0029）；放在其他專案的模組不會被註冊，編譯也不會報錯。</summary>
    [Fact]
    public void FeatureModulesLiveOnlyInFeatures()
    {
        var elsewhere = Assemblies.Data.GetTypes().Concat(Assemblies.Platform.GetTypes())
            .Where(t => t.IsClass && typeof(AiNexus.Platform.Modules.IFeatureModule).IsAssignableFrom(t))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(elsewhere.Count == 0, "Move these modules to AiNexus.Features (the composition generator cannot see them):\n" + string.Join('\n', elsewhere));
    }

    private static void AssertNoDependency(Types types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, "Forbidden dependency from: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}
