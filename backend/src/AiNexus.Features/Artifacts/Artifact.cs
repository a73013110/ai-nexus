using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Artifacts;

/// <summary>
/// 有版本的成果文件。名稱、擁有者、專案與分享設定存放在同 Id 的 <see cref="WorkspaceResource"/> 上；
/// 每次保存都會新增一筆不可變的 <see cref="ArtifactRevision"/>。
/// </summary>
[Comment("使用者保存的成果文件與目前版本。")]
public sealed class Artifact
{
    public const string Kind = "artifact";
    public const int TitleMaxLength = 120;
    public const int ContentMaxLength = ArtifactsModule.MaxContentCharacters;
    public const int MaxPerOwner = 200;
    public const int MaxVersions = 200;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("業務版本號，用於歷史或樂觀並行控制。")]
    public int Version { get; set; } = 1;
    [Comment("此成果版本所引用的來源訊息識別碼。")]
    public Guid? SourceMessageId { get; set; }
    [Comment("關聯專案的識別碼。")]
    public Guid? ProjectId { get; set; }

    /// <summary>與 <see cref="ResourceAccess.Name"/> 相同的規則：去除頭尾空白後 1 到 120 字元，且不含控制字元。</summary>
    internal static bool TitleIsValid(string? title) =>
        title?.Trim() is { Length: >= 1 and <= TitleMaxLength } trimmed && !trimmed.Any(char.IsControl);

    /// <summary>1 到 64,000 字元、不可全為空白，且除換行與 Tab 外不得含控制字元。</summary>
    internal static bool ContentIsValid(string? content) =>
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
    }
}
