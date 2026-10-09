using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

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
