using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories;

public sealed class GiteaOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://gitea.hanglong.com.tw/";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxFileBytes { get; set; } = 200000;
}

/// <summary>Serializes the module's writes (connection, imports, review creation) within one host.</summary>
public sealed class RepositoryWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }

/// <summary>A user's own Gitea token, encrypted for that user and host. Only <see cref="RepositoryService"/> decrypts it.</summary>
public sealed class RepositoryConnection
{
    public Guid OwnerId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Login { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Provenance of a knowledge document imported as a snapshot of one file at a pinned commit.</summary>
public sealed class RepositoryImport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid DocumentId { get; set; }
    public string Repository { get; set; } = "";
    public string Path { get; set; } = "";
    public string Commit { get; set; } = "";
    public string BaseUrl { get; set; } = "";
}

public sealed record RepositoryStatusDto(bool Available, bool Connected, string BaseUrl, string? Login, string Notice);
public sealed record RepositoryDto(string FullName, string Description, bool Private, string DefaultBranch, string Url);
public sealed record RepositoryPageDto(IReadOnlyList<RepositoryDto> Items, int Page, bool HasMore);
public sealed record RepositoryEntryDto(string Name, string Path, string Kind, long Size);
public sealed record RepositoryTreeDto(string Repository, string Commit, string Path, IReadOnlyList<RepositoryEntryDto> Entries);
public sealed record RepositoryFileDto(string Repository, string Commit, string Path, string Text, string Url);
public sealed record RepositoryIssueDto(int Number, string Title, string Body, string State, string Url);
public sealed record RepositoryCommitDto(string Sha, string Message);

internal static class RepositoryErrors
{
    public static readonly Error Disabled = Error.Unavailable("gitea_disabled");
    public static readonly Error NotConnected = Error.Conflict("gitea_not_connected");
    public static readonly Error ReconnectRequired = Error.Conflict("gitea_reconnect_required");
    public static readonly Error DiffUnsupported = Error.Conflict("gitea_diff_unsupported");
    public static readonly Error InvalidRepository = Error.Invalid("invalid_repository");
    public static readonly Error InvalidCommit = Error.Invalid("invalid_commit");
    public static readonly Error InvalidPath = Error.Invalid("invalid_repository_path");
    public static readonly Error InvalidPage = Error.Invalid("invalid_page");
    public static readonly Error Empty = Error.Conflict("repository_empty");
    public static readonly Error NotDirectory = Error.Invalid("repository_not_directory");
    public static readonly Error FileLimit = Error.TooLarge("repository_file_limit");
    public static readonly Error NotText = Error.Invalid("repository_not_text");
    public static readonly Error ReviewNotFound = Error.NotFound("review_not_found");
    public static readonly Error ReviewHostChanged = Error.Conflict("review_host_changed");
    public static readonly Error ReviewEmptyRange = Error.Invalid("review_empty_range");
    public static readonly Error ReviewPurposeInvalid = Error.Invalid("review_purpose_invalid");
    public static readonly Error IdempotencyConflict = Error.Conflict("idempotency_conflict");

    /// <summary>For callers that can only fail by exception, such as background jobs, which record the error code.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }

    public static T OrThrow<T>(this Result<T> result) => result.IsSuccess ? result.Value : throw result.Error.ToException();

    public static void OrThrow(this Result result)
    {
        if (!result.IsSuccess) throw result.Error.ToException();
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class RepositoryConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new RepositoryConnectionConfiguration());
        model.ApplyConfiguration(new RepositoryImportConfiguration());
    }
}

internal sealed class RepositoryConnectionConfiguration : IEntityTypeConfiguration<RepositoryConnection>
{
    public void Configure(EntityTypeBuilder<RepositoryConnection> c)
    {
        c.ToTable("RepositoryConnections", "workspace"); c.HasKey(x => x.OwnerId);
        c.Property(x => x.BaseUrl).HasMaxLength(500); c.Property(x => x.Login).HasMaxLength(100); c.Property(x => x.ProtectedToken).HasMaxLength(4096);
        c.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RepositoryImportConfiguration : IEntityTypeConfiguration<RepositoryImport>
{
    public void Configure(EntityTypeBuilder<RepositoryImport> i)
    {
        i.ToTable("RepositoryImports", "knowledge"); i.HasKey(x => x.Id);
        i.Property(x => x.Repository).HasMaxLength(201); i.Property(x => x.Path).HasMaxLength(500); i.Property(x => x.Commit).HasMaxLength(64); i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.HasIndex(x => new { x.OwnerId, x.CollectionId, x.Repository, x.Commit, x.Path });
        i.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
