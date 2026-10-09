using System.Reflection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// Diagnostics are queried by EventId and EventName, so every [LoggerMessage] names a fixed, unique pair
/// (docs/LOG_EVENTS.md). CA1848 already forbids logging outside [LoggerMessage] methods.
/// </summary>
public sealed class LoggingTests
{
    private static readonly List<(string Method, LoggerMessageAttribute Event)> Events = [.. new[] { Assemblies.Platform, Assemblies.Features, Assemblies.Api }
        .SelectMany(assembly => assembly.GetTypes())
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Select(method => (Method: method.DeclaringType!.Name + "." + method.Name, Event: method.GetCustomAttribute<LoggerMessageAttribute>()!))
        .Where(x => x.Event is not null)];

    [Fact]
    public void Every_log_event_has_a_fixed_id_and_dotted_name()
    {
        Assert.NotEmpty(Events);
        Assert.All(Events, x => Assert.True(x.Event.EventId > 0 && x.Event.EventName?.Contains('.', StringComparison.Ordinal) == true, x.Method + " needs EventId and a dotted EventName."));
    }

    [Fact]
    public void Log_event_ids_and_names_are_unique()
    {
        var ids = Events.GroupBy(x => x.Event.EventId).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(x => x.Method)));
        var names = Events.GroupBy(x => x.Event.EventName).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(x => x.Method)));
        Assert.Empty(ids);
        Assert.Empty(names);
    }
}
