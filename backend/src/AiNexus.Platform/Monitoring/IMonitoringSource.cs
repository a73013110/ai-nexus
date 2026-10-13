namespace AiNexus.Platform.Monitoring;

/// <summary>
/// One subsystem on the runtime monitoring page, owned by the module whose state it reports. Monitoring reads every
/// registered source lazily: only while someone watches, at most once per <see cref="Refresh"/>, in its own scope, off
/// the snapshot path and with a short timeout, so a source never slows the page. Keep reads cheap: in-memory state,
/// configuration or an indexed aggregate.
/// </summary>
public interface IMonitoringSource
{
    /// <summary>Stable id the frontend may key on, such as <c>jobs</c>.</summary>
    string Id { get; }

    /// <summary>Short Traditional Chinese name, such as 「背景工作」.</summary>
    string Name { get; }

    /// <summary>Position on the page; lower first.</summary>
    int Order { get; }

    /// <summary>How old a reading may get before the next snapshot starts another read.</summary>
    TimeSpan Refresh { get; }

    /// <summary>The current state. Throwing or exceeding the timeout shows the subsystem as unobserved.</summary>
    Task<SubsystemReading> ReadAsync(CancellationToken ct);
}

/// <summary>A registered source type; monitoring resolves it in a fresh scope for every read.</summary>
public sealed record MonitoringSourceRegistration(Type Type);

public static class MonitoringSourceRegistrations
{
    /// <summary>Adds a subsystem to runtime monitoring. Sources are scoped; each read gets its own scope.</summary>
    public static IServiceCollection AddMonitoringSource<TSource>(this IServiceCollection services) where TSource : class, IMonitoringSource
    {
        services.AddScoped<TSource>();
        return services.AddSingleton(new MonitoringSourceRegistration(typeof(TSource)));
    }
}
