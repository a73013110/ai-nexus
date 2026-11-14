using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>
/// 儲存庫授權的期望狀態（ADR 0037）：每人每庫只有一筆未結束的授權（filtered unique index），同步任務據此設定 Gitea 協作者。
/// 人員授權存 <see cref="UserId"/>，同步時以目前的 AD 帳號換算登入名；部署用等非人員帳號只存 <see cref="Login"/>。
/// 結束（撤銷、到期、被取代、放棄）只填結束欄位，不刪除；<see cref="SyncState"/> 改回 pending，由同步任務移除協作者。
/// </summary>
public sealed class RepositoryGrant : IRowVersioned
{
    public const int LoginMaxLength = 100;
    public const int ReasonMaxLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RepositoryId { get; set; }
    public Guid? UserId { get; set; }
    public string? Login { get; set; }

    /// <summary><see cref="RepositoryPermissions"/> 之一。</summary>
    public string Permission { get; set; } = "";

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary><see cref="RepositoryGrantSources"/> 之一。</summary>
    public string Source { get; set; } = "";

    public Guid? RequestId { get; set; }
    public Guid? GrantedById { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedById { get; set; }

    /// <summary><see cref="RepositoryGrantEndReasons"/> 之一；有效的授權為 null。</summary>
    public string? EndReason { get; set; }

    /// <summary><see cref="RepositorySyncStates"/> 之一。</summary>
    public string SyncState { get; set; } = RepositorySyncStates.Pending;

    public DateTimeOffset? SyncedAt { get; set; }
    public string? SyncError { get; set; }
    public long RowVersion { get; set; }
}

public static class RepositoryPermissions
{
    public const string Read = "read";
    public const string Write = "write";

    /// <summary>可改設定、加協作者；只有管理員能授予。</summary>
    public const string Admin = "admin";

    /// <summary>由低到高；比較用。</summary>
    public static int Rank(string permission) => permission switch { Read => 1, Write => 2, Admin => 3, _ => 0 };
}

public static class RepositoryGrantSources
{
    public const string Request = "request";
    public const string Steward = "steward";
    public const string Admin = "admin";
}

public static class RepositoryGrantEndReasons
{
    public const string Revoked = "revoked";
    public const string Expired = "expired";

    /// <summary>被同一人同一庫的新授權取代（升級、改由管理員授予）；不移除協作者，由新授權接手。</summary>
    public const string Replaced = "replaced";

    /// <summary>本人放棄。</summary>
    public const string Released = "released";
}

public static class RepositorySyncStates
{
    public const string Pending = "pending";
    public const string Applied = "applied";
    public const string Failed = "failed";
}

internal sealed class RepositoryGrantConfiguration : IEntityTypeConfiguration<RepositoryGrant>
{
    public void Configure(EntityTypeBuilder<RepositoryGrant> grant)
    {
        grant.ToTable("RepositoryGrants", "repositories");
        grant.HasKey(x => x.Id);
        grant.Property(x => x.Login).HasMaxLength(RepositoryGrant.LoginMaxLength);
        grant.Property(x => x.Permission).HasMaxLength(8);
        grant.Property(x => x.Source).HasMaxLength(16);
        grant.Property(x => x.Reason).HasMaxLength(RepositoryGrant.ReasonMaxLength);
        grant.Property(x => x.EndReason).HasMaxLength(16);
        grant.Property(x => x.SyncState).HasMaxLength(16);
        grant.Property(x => x.SyncError).HasMaxLength(64);
        grant.Property(x => x.GrantedAt).HasValueGenerator<CreationTime>();
        grant.Property(x => x.RowVersion).IsRowVersion().HasConversion<byte[]>();
        grant.HasOne<ManagedRepository>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Restrict);
        grant.HasOne<RepositoryAccessRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        grant.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        grant.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedById).OnDelete(DeleteBehavior.Restrict);
        grant.HasOne<User>().WithMany().HasForeignKey(x => x.EndedById).OnDelete(DeleteBehavior.Restrict);
    }
}
