using System.Reflection;
using System.Text.RegularExpressions;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Hosting;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 「背景作業」頁要列得出系統會自己做的每一件事：定期工作是排程任務，其餘的 hosted service 都必須宣告成 <see cref="IResidentService"/>。
/// 新增背景服務卻沒有宣告時在這裡失敗，不會多出管理頁看不到的背景動作。
/// </summary>
public sealed partial class ResidentServiceTests
{
    private static readonly List<Type> Hosted = [.. new[] { Assemblies.Platform, Assemblies.Data, Assemblies.Features, Assemblies.Host }
        .SelectMany(x => x.GetTypes())
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IHostedService).IsAssignableFrom(t))];

    [Fact]
    public void EveryHostedServiceIsADeclaredResidentService()
    {
        Assert.NotEmpty(Hosted);
        var undeclared = Hosted.Where(t => !typeof(IResidentService).IsAssignableFrom(t) || !typeof(BackgroundService).IsAssignableFrom(t))
            .Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(undeclared.Count == 0,
            "Hosted services must derive from BackgroundService and implement IResidentService so the scheduling page lists them (periodic work belongs in an IScheduledTask):\n" + string.Join('\n', undeclared));
    }

    [Fact]
    public void ResidentServiceKeysAreUniqueAndWellFormed()
    {
        var definitions = Hosted.Where(t => typeof(IResidentService).IsAssignableFrom(t))
            .Select(t => (ResidentServiceDefinition)t.GetProperty(nameof(IResidentService.Definition), BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!).ToList();
        Assert.All(definitions, x => Assert.Matches(KeyFormat(), x.Key));
        Assert.All(definitions, x => Assert.False(string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Category) || string.IsNullOrWhiteSpace(x.Description) || string.IsNullOrWhiteSpace(x.Rhythm), x.Key));
        Assert.Equal(definitions.Count, definitions.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count());
    }

    // 與排程任務的 Key 同一個格式：<module>.<角色>，小寫 kebab。
    [GeneratedRegex("^[a-z0-9-]+(\\.[a-z0-9-]+)+$")]
    private static partial Regex KeyFormat();
}
