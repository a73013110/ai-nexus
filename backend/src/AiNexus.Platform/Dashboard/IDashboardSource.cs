namespace AiNexus.Platform.Dashboard;

/// <summary>
/// One section of the overview, owned by the module whose data it counts. The dashboard reads every registered source
/// the viewer may see, so a new feature appears on the overview by registering a source in its own module; the
/// frontend renders an unknown section with the generic widget until it gets a dedicated one.
/// </summary>
public interface IDashboardSource
{
    /// <summary>Feature grant that shows the section. An empty title or route falls back to this feature's name and route.</summary>
    string Feature { get; }

    /// <summary>Only an administrator reading the platform scope sees the section.</summary>
    bool PlatformOnly => false;

    /// <summary>Counts for the scope, or <c>null</c> when the section has nothing to say (for example, the service is off).</summary>
    Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct);
}

public static class DashboardSourceRegistration
{
    /// <summary>Adds a section to the overview. Sources are scoped and read one after another on the request's context.</summary>
    public static IServiceCollection AddDashboardSource<TSource>(this IServiceCollection services) where TSource : class, IDashboardSource
        => services.AddScoped<IDashboardSource, TSource>();
}
