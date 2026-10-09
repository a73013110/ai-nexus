using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

public static class NexusDbContextOptions
{
    /// <summary>
    /// Adds the save-time behavior every <see cref="NexusDbContext"/> needs: domain events dispatched in the saving
    /// transaction, then audit rows completed and redacted. Call it wherever the database provider is chosen, including
    /// test hosts that replace the provider. <paramref name="services"/> is the scope the context is resolved from.
    /// </summary>
    public static DbContextOptionsBuilder AddNexusInterceptors(this DbContextOptionsBuilder options, IServiceProvider services)
    {
        // Order matters: handlers may add audit rows, which the audit interceptor then completes.
        if (services.GetService<DomainEvents>() is { } events) options.AddInterceptors(new DomainEventInterceptor(events));
        return options.AddInterceptors(new AuditEventInterceptor(services.GetService<IHttpContextAccessor>()));
    }
}
