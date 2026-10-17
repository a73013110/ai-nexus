using AiNexus.Features.Attachments;
using AiNexus.Features.Billing.Charges;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference.Models;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.WebSearch;

namespace AiNexus.Features.Chat.Transcripts;

/// <summary>
/// 前端介面呈現之訊息資料傳輸物件。
/// </summary>
/// <remarks>
/// <c>ModelId</c> 是對外脫敏後的識別碼，內部模型 ID 不會出現在這個 DTO 中；<c>ModelDisplayName</c> 由 <see cref="ModelPresentation"/> 產生。
/// </remarks>
public sealed record MessageDto(
    Guid Id,
    Guid? ParentId,
    string Role,
    string Content,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? RunId,
    string? ModelId,
    IReadOnlyList<AttachmentDto>? Attachments = null,
    string? ErrorCode = null,
    IReadOnlyList<CitationDto>? Sources = null,
    int FeedbackRating = 0,
    ChargeDto? Charge = null,
    IReadOnlyList<WebSourceDto>? WebSources = null,
    ChargeDto? WebSearchCharge = null,
    RunTimingDto? Timing = null,
    string? ModelDisplayName = null,
    string? IssueCode = null);

/// <summary>
/// 訊息實體轉換為前端展示資料之擴充方法。
/// </summary>
public static class MessagePresentation
{
    /// <summary>
    /// 將領域訊息實體轉換為瀏覽器呈現用之 <see cref="MessageDto"/>。
    /// 內部模型識別碼與顯示名稱皆經由 <see cref="ModelPresentation"/> 進行對外脫敏與轉換。
    /// </summary>
    public static MessageDto Message(this ModelPresentation models, Message message) => new(
        Id: message.Id,
        ParentId: message.ParentId,
        Role: message.Role,
        Content: message.Content,
        Status: message.Status,
        CreatedAt: message.CreatedAt,
        RunId: message.RunId,
        ModelId: message.ModelId is { } id ? models.PublicId(id) : null,
        Attachments: [],
        ErrorCode: message.ErrorCode,
        ModelDisplayName: models.DisplayName(message.ModelId),
        IssueCode: message.IssueCode);
}
