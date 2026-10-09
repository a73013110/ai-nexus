using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>A versioned question bank. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same id.</summary>
public sealed class EvaluationSet
{
    public const string Kind = "evaluation";

    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public string CasesJson { get; set; } = "[]";
    public int Version { get; set; } = 1;

    public EvaluationSetDto ToDto(ResourceDto resource) => new(resource, Description, EvaluationJson.Parse<EvaluationCase>(CasesJson), Version);
}

internal static class EvaluationSetQueries
{
    /// <summary>The set as the actor may see it. Access failures stay exceptions of <see cref="ResourceAccess"/>.</summary>
    public static async Task<EvaluationSetDto> LoadSetAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, EvaluationSet.Kind, ct);
        var set = await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return set.ToDto(await access.DescribeAsync(actor, resource, ct));
    }
}

internal sealed class EvaluationSetConfiguration : IEntityTypeConfiguration<EvaluationSet>
{
    public void Configure(EntityTypeBuilder<EvaluationSet> s)
    {
        s.ToTable("EvaluationSets", "quality"); s.HasKey(x => x.Id); s.Property(x => x.Description).HasMaxLength(2000);
    }
}
