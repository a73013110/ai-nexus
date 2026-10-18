using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Artifacts;

/// <summary>
/// 有版本的成果文件。名稱、擁有者、專案與分享設定存放在同 Id 的 <see cref="Resource"/> 上；
/// 每次保存都會新增一筆不可變的 <see cref="ArtifactRevision"/>。
/// </summary>
public sealed class Artifact
{
    public const string Kind = "artifact";
    public const int TitleMaxLength = 120;
    public const int ContentMaxLength = 64000;
    public const int MaxPerOwner = 200;
    public const int MaxVersions = 200;

    public Guid Id { get; set; }
    public int Version { get; set; } = 1;
    public Guid? SourceMessageId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>與 <c>ResourceAccess.Name</c> 相同的規則：去除頭尾空白後 1 到 120 字元，且不含控制字元。</summary>
    public static bool TitleIsValid(string? title) =>
        title?.Trim() is { Length: >= 1 and <= TitleMaxLength } trimmed && !trimmed.Any(char.IsControl);

    /// <summary>1 到 64,000 字元、不可全為空白，且除換行與 Tab 外不得含控制字元。</summary>
    public static bool ContentIsValid(string? content) =>
        content is { Length: >= 1 and <= ContentMaxLength }
        && !string.IsNullOrWhiteSpace(content)
        && !content.Any(x => char.IsControl(x) && x is not ('\n' or '\r' or '\t'));
}

internal sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> item)
    {
        item.ToTable("Artifacts", "artifacts");
        item.HasKey(x => x.Id);
        item.HasOne<Resource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<Message>().WithMany().HasForeignKey(x => x.SourceMessageId).OnDelete(DeleteBehavior.Restrict);
        // Artifact 可以隸屬於專案；刪除專案前，ResourceLifecycle 會先解除這些 Artifact 的關聯。
        item.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
