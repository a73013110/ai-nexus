using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Library;

public sealed class PromptTemplate
{
    public const int TitleMaxLength = 80;
    public const int ContentMaxLength = LibraryModule.MaxTemplateCharacters;
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

    public PromptTemplateDto ToDto() => new(Id, Title, Content, UpdatedAt);
}

public sealed record PromptTemplateDto(Guid Id, string Title, string Content, DateTimeOffset UpdatedAt);

internal static class LibraryErrors
{
    public static readonly Error NotFound = Error.NotFound("template_not_found");
    public static readonly Error LimitReached = Error.Invalid("template_limit");
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
