using AiNexus.Features.Collaboration;
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

internal sealed class ArtifactEntityConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> item)
    {
        item.ToTable("Artifacts", "content"); item.HasKey(x => x.Id);
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
