using AiNexus.Features.Collaboration;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Artifacts;

/// <summary>
/// A versioned document. Its name, owner, project and sharing live on the <see cref="WorkspaceResource"/> with the same
/// id; every save adds an immutable <see cref="ArtifactRevision"/>.
/// </summary>
public sealed class Artifact
{
    public const string Kind = "artifact";
    public const int TitleMaxLength = 120;
    public const int ContentMaxLength = ArtifactsModule.MaxContentCharacters;
    public const int MaxPerOwner = 200;
    public const int MaxVersions = 200;

    public Guid Id { get; set; }
    public int Version { get; set; } = 1;
    public Guid? SourceMessageId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>The rule of <see cref="ResourceAccess.Name"/>.</summary>
    internal static bool TitleIsValid(string? title) => title?.Trim() is { Length: >= 1 and <= TitleMaxLength } trimmed && !trimmed.Any(char.IsControl);

    /// <summary>1 to 64,000 characters, not only whitespace, and no control characters other than line breaks and tabs.</summary>
    internal static bool ContentIsValid(string? content) => content is { Length: >= 1 and <= ContentMaxLength } && !string.IsNullOrWhiteSpace(content)
        && !content.Any(x => char.IsControl(x) && x is not ('\n' or '\r' or '\t'));
}

public sealed class ArtifactRevision
{
    public Guid ArtifactId { get; set; }
    public int Version { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record ArtifactSummaryDto(ResourceDto Resource, int Version, Guid? ProjectId);
public sealed record ArtifactDto(ResourceDto Resource, int Version, int CurrentVersion, string Content, Guid? SourceMessageId, Guid? ProjectId);
public sealed record ArtifactRevisionDto(int Version, string Title, string Author, DateTimeOffset CreatedAt);

internal static class ArtifactErrors
{
    public const string ContentInvalidCode = "artifact_content_invalid";
    public const string TransformInputInvalidCode = "transform_input_invalid";

    public static readonly Error InvalidName = Error.Invalid("invalid_resource_name");
    public static readonly Error ContentInvalid = Error.Invalid(ContentInvalidCode);
    public static readonly Error VersionMissing = Error.NotFound("artifact_version_missing");
    public static readonly Error MessageNotFound = Error.NotFound("message_not_found");
    public static readonly Error LimitReached = Error.Conflict("artifact_limit");
    public static readonly Error ProjectAccessRequired = Error.Forbidden("project_access_required");
    public static readonly Error VersionLimit = Error.Conflict("artifact_version_limit");
    public static readonly Error VersionConflict = Error.Conflict("artifact_version_conflict");
    public static readonly Error ExportFormatInvalid = Error.Invalid("export_format_invalid");
    public static readonly Error TransformActionInvalid = Error.Invalid("transform_action_invalid");
}

internal sealed class ArtifactEntityConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> item)
    {
        item.ToTable("Artifacts", "content"); item.HasKey(x => x.Id);
    }
}

internal sealed class ArtifactRevisionConfiguration : IEntityTypeConfiguration<ArtifactRevision>
{
    public void Configure(EntityTypeBuilder<ArtifactRevision> revision)
    {
        revision.ToTable("ArtifactRevisions", "content"); revision.HasKey(x => new { x.ArtifactId, x.Version });
        revision.Property(x => x.Title).HasMaxLength(Artifact.TitleMaxLength); revision.Property(x => x.Content).HasMaxLength(Artifact.ContentMaxLength);
        revision.HasOne<Artifact>().WithMany().HasForeignKey(x => x.ArtifactId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class ArtifactConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new ArtifactEntityConfiguration());
        model.ApplyConfiguration(new ArtifactRevisionConfiguration());
    }
}
