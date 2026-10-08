using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories;

public sealed class GiteaOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://gitea.hanglong.com.tw/";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxFileBytes { get; set; } = 200000;
}
public sealed class RepositoryWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
public sealed class RepositoryConnection
{
    public Guid OwnerId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Login { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
}
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
public sealed record ConnectRepositoryRequest(string Token);
public sealed record RepositoryDto(string FullName, string Description, bool Private, string DefaultBranch, string Url);
public sealed record RepositoryPageDto(IReadOnlyList<RepositoryDto> Items, int Page, bool HasMore);
public sealed record RepositoryEntryDto(string Name, string Path, string Kind, long Size);
public sealed record RepositoryTreeDto(string Repository, string Commit, string Path, IReadOnlyList<RepositoryEntryDto> Entries);
public sealed record RepositoryFileDto(string Repository, string Commit, string Path, string Text, string Url);
public sealed record RepositoryIssueDto(int Number, string Title, string Body, string State, string Url);
public sealed record RepositoryCommitDto(string Sha, string Message);
public sealed record RepositoryImportRequest(string Repository, string Commit, string Path, Guid CollectionId);
public static class RepositoryConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var c = model.Entity<RepositoryConnection>(); c.ToTable("RepositoryConnections", "workspace"); c.HasKey(x => x.OwnerId);
        c.Property(x => x.BaseUrl).HasMaxLength(500); c.Property(x => x.Login).HasMaxLength(100); c.Property(x => x.ProtectedToken).HasMaxLength(4096);
        c.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        var i = model.Entity<RepositoryImport>(); i.ToTable("RepositoryImports", "knowledge"); i.HasKey(x => x.Id);
        i.Property(x => x.Repository).HasMaxLength(201); i.Property(x => x.Path).HasMaxLength(500); i.Property(x => x.Commit).HasMaxLength(64); i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.HasIndex(x => new { x.OwnerId, x.CollectionId, x.Repository, x.Commit, x.Path });
        i.HasOne<AiNexus.Features.Knowledge.KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
