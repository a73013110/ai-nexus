using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Projects;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Conversations;

public sealed class Conversation
{
    public const int TitleMaxLength = 120;
    public const int InstructionMaxLength = 4000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "新對話";
    public Guid? ActiveLeafId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public string SystemInstruction { get; set; } = "";
    public List<ConversationLabel> Labels { get; set; } = [];

    public static bool TitleIsValid(string? title) => title?.Trim().Length is >= 1 and <= TitleMaxLength;
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> conversation)
    {
        conversation.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        conversation.Property(x => x.UpdatedAt).HasValueGenerator<CreationTime>();
        conversation.ToTable("Conversations", "conversations");
        conversation.HasKey(x => x.Id);
        conversation.Property(x => x.Title).HasMaxLength(Conversation.TitleMaxLength);
        conversation.Property(x => x.SystemInstruction).HasMaxLength(Conversation.InstructionMaxLength);
        conversation.HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted);
        conversation.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        conversation.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
