using Microsoft.EntityFrameworkCore;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// <c>AppDbContext</c> 以 <c>ApplyConfigurationsFromAssembly</c> 套用實體設定，但它會靜默略過沒有公開無參數建構式的設定類別，
/// 該實體的對應就會從模型中消失，所以這裡提前擋下。
/// </summary>
public sealed class EntityConfigurationTests
{
    [Fact]
    public void EntityConfigurationsHaveAParameterlessConstructor()
    {
        var configurations = Assemblies.Data.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))).ToList();
        Assert.NotEmpty(configurations);
        var skipped = configurations.Where(t => t.GetConstructor(Type.EmptyTypes) is null).Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(skipped.Count == 0, "Give these configurations a public parameterless constructor:\n" + string.Join("\n", skipped));
    }
}
