using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Releases;

/// <summary>
/// 部署歷史的一列：資料庫由 update.sql 最後一步寫入，程式在新版本第一次啟動時寫入（ADR 0027、0028）。
/// 只新增、不修改；目前版本是該部分 <see cref="Id"/> 最大的一列。
/// </summary>
public sealed class Deployment
{
    public int Id { get; set; }
    /// <summary><see cref="DeploymentComponents"/>。</summary>
    public string Component { get; set; } = "";
    public string Version { get; set; } = "";
    /// <summary>建置的 commit（完整 SHA）。</summary>
    public string Revision { get; set; } = "";
    /// <summary>資料庫更新包的比對起點（tag 或 commit）；新建的庫與程式為 null。</summary>
    public string? StartPoint { get; set; }
    public DateTimeOffset DeployedAt { get; set; }
    public string Host { get; set; } = "";
}

/// <summary>部署的部分，與資料庫的 check constraint 一致。</summary>
public static class DeploymentComponents
{
    public const string App = "app", Database = "database";
}

internal sealed class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> deployment)
    {
        deployment.ToTable("Deployments", "releases");
        deployment.HasKey(x => x.Id);
        deployment.Property(x => x.Component).HasMaxLength(16);
        deployment.Property(x => x.Version).HasMaxLength(64);
        deployment.Property(x => x.Revision).HasMaxLength(40);
        deployment.Property(x => x.StartPoint).HasMaxLength(64);
        deployment.Property(x => x.Host).HasMaxLength(128);
    }
}
