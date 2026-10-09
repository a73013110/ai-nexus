using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>A versioned question bank. Its name, owner and sharing live on the <see cref="WorkspaceResource"/> with the same id.</summary>
[Comment("評測題庫、固定測試案例與版本。")]
public sealed class EvaluationSet
{
    public const string Kind = "evaluation";

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("業務物件的用途說明。")]
    public string Description { get; set; } = "";
    [Comment("固定評測案例的 JSON 快照。")]
    public string CasesJson { get; set; } = "[]";
    [Comment("業務版本號，用於歷史或樂觀並行控制。")]
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
