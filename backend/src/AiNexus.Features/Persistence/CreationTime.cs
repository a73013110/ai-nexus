using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace AiNexus.Features.Persistence;

/// <summary>
/// Fills a creation timestamp from <see cref="NexusDbContext.Clock"/> when the entity is added and the property still
/// has its default value. Code that reads the timestamp before adding the entity sets it explicitly.
/// </summary>
public sealed class CreationTime : ValueGenerator<DateTimeOffset>
{
    public override bool GeneratesTemporaryValues => false;

    public override DateTimeOffset Next(EntityEntry entry) => ((NexusDbContext)entry.Context).Clock.GetUtcNow();
}
