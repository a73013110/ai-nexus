using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Library;

[Comment("使用者私人提示詞範本。")]
public sealed class PromptTemplate
{
    public const int TitleMaxLength = 80;
    public const int ContentMaxLength = LibraryModule.MaxTemplateCharacters;
    public const int MaxPerOwner = 100;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; private set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; private set; }
    [Comment("介面顯示標題。")]
    public string Title { get; private set; } = "";
    [Comment("提示詞內容。")]
    public string Content { get; private set; } = "";
    [Comment("資料最後修改時間，採 UTC offset。")]
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PromptTemplate Create(Guid owner) => new() { OwnerId = owner };

    public void Edit(string title, string content, DateTimeOffset now)
    {
        Title = title.Trim();
        Content = content.Trim();
        UpdatedAt = now;
    }

    public PromptTemplateDto ToDto() => new(Id, Title, Content, UpdatedAt);
}

internal static class PromptTemplateQueries
{
    public static IQueryable<PromptTemplate> OwnedBy(this IQueryable<PromptTemplate> templates, Guid owner) => templates.Where(x => x.OwnerId == owner);
}

internal sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> prompt)
    {
        prompt.ToTable("PromptTemplates", "library");
        prompt.HasKey(x => x.Id);
        prompt.Property(x => x.Title).HasMaxLength(PromptTemplate.TitleMaxLength);
        prompt.Property(x => x.Content).HasMaxLength(PromptTemplate.ContentMaxLength);
        prompt.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
    }
}
