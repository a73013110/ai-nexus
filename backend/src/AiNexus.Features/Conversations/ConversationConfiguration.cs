using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

public static class ConversationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var conversation = model.Entity<Conversation>();
        conversation.ToTable("Conversations", "conversations");
        conversation.HasKey(x => x.Id);
        conversation.Property(x => x.Title).HasMaxLength(120);
        conversation.Property(x => x.SystemInstruction).HasMaxLength(4000);
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.UpdatedAt });
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.IsArchived, x.IsFavorite, x.UpdatedAt });
        conversation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        conversation.HasOne<AiNexus.Features.Projects.Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        var label = model.Entity<ConversationLabel>();
        label.ToTable("ConversationLabels", "conversations");
        label.HasKey(x => new { x.ConversationId, x.Name });
        label.Property(x => x.Name).HasMaxLength(24);
        label.HasOne<Conversation>().WithMany(x => x.Labels).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        var message = model.Entity<Message>();
        message.ToTable("Messages", "conversations");
        message.HasKey(x => x.Id);
        message.Property(x => x.Role).HasMaxLength(16);
        message.Property(x => x.Status).HasMaxLength(16);
        message.Property(x => x.ModelId).HasMaxLength(160);
        message.Property(x => x.ErrorCode).HasMaxLength(80); message.Property(x => x.IssueCode).HasMaxLength(40);
        message.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        message.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        message.HasOne<Message>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
