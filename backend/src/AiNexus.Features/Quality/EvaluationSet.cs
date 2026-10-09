using System.Text.Json;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Quality;

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

public sealed record EvaluationCase(string Question, string Reference = "", IReadOnlyList<string>? RequiredTerms = null, IReadOnlyList<string>? ForbiddenTerms = null);
public sealed record EvaluationSetDto(ResourceDto Resource, string Description, IReadOnlyList<EvaluationCase> Cases, int Version);

internal static class QualityErrors
{
    public static readonly Error ItemMissing = Error.NotFound("quality_item_missing");
    public static readonly Error AnswerPending = Error.Conflict("feedback_answer_pending");
    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error SetConflict = Error.Conflict("evaluation_set_conflict");
    public static readonly Error SetLimit = Error.Conflict("evaluation_set_limit");
    public static readonly Error EvaluationActive = Error.Conflict("evaluation_active");
    public static readonly Error RunLimit = Error.Conflict("evaluation_run_limit");
    public static readonly Error RetrievalMissing = Error.NotFound("retrieval_evaluation_missing");
    public static readonly Error CorpusInvalid = Error.Invalid("retrieval_corpus_invalid");
    public static readonly Error PagesInvalid = Error.Invalid("retrieval_pages_invalid");
    public static readonly Error RetrievalActive = Error.Conflict("retrieval_evaluation_active");
    public static readonly Error RetrievalLimit = Error.Conflict("retrieval_evaluation_limit");
}

/// <summary>Frozen JSON columns of sets and runs (default serializer options, as stored).</summary>
internal static class EvaluationJson
{
    public static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json)!;
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
