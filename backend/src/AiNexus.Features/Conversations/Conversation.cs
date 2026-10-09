using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Conversations;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "新對話";
    public Guid? ActiveLeafId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public string SystemInstruction { get; set; } = "";
    public List<ConversationLabel> Labels { get; set; } = [];
}

public sealed class ConversationLabel
{
    public Guid ConversationId { get; set; }
    public string Name { get; set; } = "";
}

public sealed record ConversationDto(Guid Id, string Title, Guid? ActiveLeafId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsFavorite = false, bool IsArchived = false, string SystemInstruction = "", IReadOnlyList<string>? Labels = null, Guid? ProjectId = null);

public static class ConversationMappings
{
    public static ConversationDto ToDto(this Conversation x) => new(x.Id, x.Title, x.ActiveLeafId, x.CreatedAt, x.UpdatedAt, x.IsFavorite, x.IsArchived, x.SystemInstruction, x.Labels.Select(l => l.Name).Order().ToArray(), x.ProjectId);
    public static MessageDto ToDto(this Message x) => new(x.Id, x.ParentId, x.Role, x.Content, x.Status, x.CreatedAt, x.RunId, x.ModelId, [], x.ErrorCode, IssueCode: x.IssueCode);
}

internal static class ConversationErrors
{
    public static readonly Error NotFound = Error.NotFound("conversation_not_found");
    public static readonly Error InvalidQuery = Error.Invalid("invalid_query");
    public static readonly Error InvalidTitle = Error.Invalid(InvalidTitleCode);
    public static readonly Error InvalidLabels = Error.Invalid("invalid_labels");
    public static readonly Error InvalidBackup = Error.Invalid(InvalidBackupCode);
    public static readonly Error GenerationActive = Error.Conflict("generation_active");
    public static readonly Error MessageNotFound = Error.NotFound("message_not_found");

    public const string InvalidTitleCode = "invalid_title";
    public const string InstructionTooLongCode = "instruction_too_long";
    public const string InvalidBackupCode = "invalid_backup";

    /// <summary>For <see cref="ConversationService"/>, whose callers in other modules can only fail by exception.</summary>
    public static ApiException ToException(this Error error)
    {
        var status = Problems.Status(error.Kind);
        return new(status, error.Code, PublicErrorCatalog.Message(error.Code, status));
    }
}

internal static class ConversationQueries
{
    public const int TitleMaxLength = 120;
    public const int InstructionMaxLength = 4000;

    public static bool TitleIsValid(string? title) => title?.Trim().Length is >= 1 and <= TitleMaxLength;

    /// <summary>The user's own conversation that is not deleted (soft-delete filter), with its labels; null when there is none.</summary>
    public static Task<Conversation?> OwnedConversationAsync(this NexusDbContext db, Guid owner, Guid id, CancellationToken ct)
        => db.Set<Conversation>().Include(x => x.Labels).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);

    public static Task<bool> HasActiveRunAsync(this NexusDbContext db, Guid id, CancellationToken ct)
        => db.Set<GenerationRun>().AnyAsync(x => x.ConversationId == id && x.ActiveOwnerId != null, ct);

    /// <summary>At most 5 labels of 1 to 24 characters, trimmed and distinct; null when the list is invalid.</summary>
    public static string[]? CleanLabels(IReadOnlyList<string> labels)
    {
        if (labels.Count > 5 || labels.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 24 || x.Any(char.IsControl))) return null;
        return labels.Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }
}

/// <summary>Entry point kept for <c>NexusDbContext</c>.</summary>
public static class ConversationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new ConversationEntityConfiguration());
        model.ApplyConfiguration(new ConversationLabelConfiguration());
        model.ApplyConfiguration(new MessageConfiguration());
    }
}

internal sealed class ConversationEntityConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> conversation)
    {
        conversation.ToTable("Conversations", "conversations");
        conversation.HasKey(x => x.Id);
        conversation.Property(x => x.Title).HasMaxLength(ConversationQueries.TitleMaxLength);
        conversation.Property(x => x.SystemInstruction).HasMaxLength(ConversationQueries.InstructionMaxLength);
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.UpdatedAt });
        conversation.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.IsArchived, x.IsFavorite, x.UpdatedAt });
        conversation.HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted);
        conversation.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        conversation.HasOne<AiNexus.Features.Projects.Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConversationLabelConfiguration : IEntityTypeConfiguration<ConversationLabel>
{
    public void Configure(EntityTypeBuilder<ConversationLabel> label)
    {
        label.ToTable("ConversationLabels", "conversations");
        label.HasKey(x => new { x.ConversationId, x.Name });
        label.Property(x => x.Name).HasMaxLength(24);
        label.HasOne<Conversation>().WithMany(x => x.Labels).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
