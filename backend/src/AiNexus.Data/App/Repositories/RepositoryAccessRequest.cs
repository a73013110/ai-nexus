using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>儲存庫權限申請的內容；簽核狀態只在流程實例（<c>flow.FlowInstances.SubjectId</c>＝<see cref="Id"/>），這裡不重複存。</summary>
public sealed class RepositoryAccessRequest
{
    public const int ReasonMaxLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RepositoryId { get; set; }
    public Guid UserId { get; set; }

    /// <summary><see cref="RepositoryPermissions.Read"/> 或 <see cref="RepositoryPermissions.Write"/>。</summary>
    public string Permission { get; set; } = "";

    public DateTimeOffset? ExpiresAt { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RepositoryAccessRequestConfiguration : IEntityTypeConfiguration<RepositoryAccessRequest>
{
    public void Configure(EntityTypeBuilder<RepositoryAccessRequest> request)
    {
        request.ToTable("RepositoryAccessRequests", "repositories");
        request.HasKey(x => x.Id);
        request.Property(x => x.Permission).HasMaxLength(8);
        request.Property(x => x.Reason).HasMaxLength(RepositoryAccessRequest.ReasonMaxLength);
        request.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        request.HasOne<ManagedRepository>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Restrict);
        request.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
