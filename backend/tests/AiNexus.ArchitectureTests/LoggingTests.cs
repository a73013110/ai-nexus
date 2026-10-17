using System.Reflection;
using Microsoft.Extensions.Logging;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 診斷紀錄以 EventId 與 EventName 查詢，所以每個 [LoggerMessage] 都必須是固定且唯一的一組（見 docs/architecture/log-events.md）。
/// 在 [LoggerMessage] 以外寫日誌已由 CA1848 禁止，這裡不重複檢查。
/// </summary>
public sealed class LoggingTests
{
    private static readonly List<(string Method, LoggerMessageAttribute Event)> Events = [.. new[] { Assemblies.Platform, Assemblies.Features, Assemblies.Host }
        .SelectMany(assembly => assembly.GetTypes())
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Select(method => (Method: method.DeclaringType!.Name + "." + method.Name, Event: method.GetCustomAttribute<LoggerMessageAttribute>()!))
        .Where(x => x.Event is not null)];

    [Fact]
    public void EveryLogEventHasAFixedIdAndDottedName()
    {
        Assert.NotEmpty(Events);
        Assert.All(Events, x => Assert.True(x.Event.EventId > 0 && x.Event.EventName?.Contains('.', StringComparison.Ordinal) == true, x.Method + " needs EventId and a dotted EventName."));
    }

    [Fact]
    public void LogEventIdsAndNamesAreUnique()
    {
        var ids = Events.GroupBy(x => x.Event.EventId).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(x => x.Method)));
        var names = Events.GroupBy(x => x.Event.EventName).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(x => x.Method)));
        Assert.Empty(ids);
        Assert.Empty(names);
    }
}
