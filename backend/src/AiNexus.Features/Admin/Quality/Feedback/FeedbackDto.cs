namespace AiNexus.Features.Admin.Quality.Feedback;

public sealed record FeedbackDto(Guid MessageId, Guid ConversationId, string ConversationTitle, int Rating, string Reason, string Note, DateTimeOffset UpdatedAt);

/// <summary>讀取評分的結果；尚未評分時 <c>Feedback</c> 為 null，端點回 204。</summary>
public sealed record FeedbackOutcome(FeedbackDto? Feedback);
