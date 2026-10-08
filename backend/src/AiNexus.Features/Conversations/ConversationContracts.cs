using AiNexus.Features.Inference;
namespace AiNexus.Features.Conversations;

public sealed record ConversationDto(Guid Id, string Title, Guid? ActiveLeafId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsFavorite = false, bool IsArchived = false, string SystemInstruction = "", IReadOnlyList<string>? Labels = null, Guid? ProjectId = null);
public sealed record MessageDto(Guid Id, Guid? ParentId, string Role, string Content, string Status, DateTimeOffset CreatedAt, Guid? RunId, string? ModelId, IReadOnlyList<AiNexus.Features.Attachments.AttachmentDto>? Attachments = null, string? ErrorCode = null, IReadOnlyList<AiNexus.Features.Knowledge.CitationDto>? Sources = null, int FeedbackRating = 0, AiNexus.Features.Billing.ChargeDto? Charge = null, IReadOnlyList<AiNexus.Features.WebSearch.WebSourceDto>? WebSources = null, AiNexus.Features.Billing.ChargeDto? WebSearchCharge = null, AiNexus.Features.Inference.RunTimingDto? Timing = null, string? ModelDisplayName = null, string? IssueCode = null);
public sealed record ConversationDetailDto(ConversationDto Conversation, IReadOnlyList<MessageDto> Messages, RunDto? ActiveRun);
public sealed record CreateConversationRequest(string? Title = null);
public sealed record RenameConversationRequest(string Title);
public sealed record SelectBranchRequest(Guid LeafId);
