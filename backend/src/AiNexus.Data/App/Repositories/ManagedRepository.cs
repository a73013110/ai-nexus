using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>
/// 由服務帳號同步的 Gitea 組織儲存庫（ADR 0037），是授權與申請的對象。以 <see cref="GiteaId"/> 對應，改名或轉移組織時跟著更新；
/// Gitea 已沒有的不刪除（授權與申請仍引用），記 <see cref="MissingSince"/>。
/// </summary>
public sealed class ManagedRepository : IRowVersioned
{
    public const int OwnerMaxLength = 100;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int TeamsMaxLength = 1000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public long GiteaId { get; set; }
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>Owners 以外仍授予此庫存取的團隊（頓號分隔）；有值時逐人授權擋不住這些團隊的成員。</summary>
    public string? GrantingTeams { get; set; }

    public bool Requestable { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
    public DateTimeOffset? MissingSince { get; set; }
    public long RowVersion { get; set; }

    public string FullName => Owner + "/" + Name;
}

internal sealed class ManagedRepositoryConfiguration : IEntityTypeConfiguration<ManagedRepository>
{
    public void Configure(EntityTypeBuilder<ManagedRepository> repository)
    {
        repository.ToTable("ManagedRepositories", "repositories");
        repository.HasKey(x => x.Id);
        repository.Property(x => x.Owner).HasMaxLength(ManagedRepository.OwnerMaxLength);
        repository.Property(x => x.Name).HasMaxLength(ManagedRepository.NameMaxLength);
        repository.Property(x => x.Description).HasMaxLength(ManagedRepository.DescriptionMaxLength);
        repository.Property(x => x.GrantingTeams).HasMaxLength(ManagedRepository.TeamsMaxLength);
        repository.Property(x => x.RowVersion).IsRowVersion().HasConversion<byte[]>();
        repository.Ignore(x => x.FullName);
    }
}
