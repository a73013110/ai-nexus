using AiNexus.Features.Attachments;
using AiNexus.Features.Billing;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;
using AiNexus.Features.WebSearch;
using AiNexus.Features.Knowledge.Retrieval;

namespace AiNexus.Features.Chat;

public sealed record MessageDto(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, Guid? RunId, string? ModelId, IReadOnlyList<AttachmentDto>? Attachments = null, string? ErrorCode = null, IReadOnlyList<CitationDto>? Sources = null, int FeedbackRating = 0, ChargeDto? Charge = null, IReadOnlyList<WebSourceDto>? WebSources = null, ChargeDto? WebSearchCharge = null, RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);

public static class MessagePresentation
{
    /// <summary>The message as the browser sees it: model references and labels go through <see cref="ModelPresentation"/>.</summary>
    public static MessageDto Message(this ModelPresentation models, Message message) => new(message.Id, message.ParentId, message.Role, message.Content, message.Status,
        message.CreatedAt, message.RunId, message.ModelId is { } id ? models.PublicId(id) : null, [], message.ErrorCode, ModelDisplayName: models.DisplayName(message.ModelId), IssueCode: message.IssueCode);
}
