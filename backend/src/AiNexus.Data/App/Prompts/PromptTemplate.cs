using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Prompts;

/// <summary>使用者私人的提示詞範本；每位使用者最多 <see cref="MaxPerOwner"/> 筆。</summary>
public sealed class PromptTemplate
{
    public const int TitleMaxLength = 80;
    public const int ContentMaxLength = 12000;
    public const int MaxPerOwner = 100;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = "";
    public string Content { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PromptTemplate Create(Guid owner) => new() { OwnerId = owner };

    public void Edit(string title, string content, DateTimeOffset now)
    {
        Title = title.Trim();
        Content = content.Trim();
        UpdatedAt = now;
    }
}

internal sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> prompt)
    {
        prompt.ToTable("PromptTemplates", "prompts");
        prompt.HasKey(x => x.Id);
        prompt.Property(x => x.Title).HasMaxLength(PromptTemplate.TitleMaxLength);
        prompt.Property(x => x.Content).HasMaxLength(PromptTemplate.ContentMaxLength);
        prompt.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
