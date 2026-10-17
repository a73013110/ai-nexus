namespace AiNexus.Features.Quality.Feedback;

public sealed record FeedbackDto(Guid MessageId, Guid ConversationId, string ConversationTitle, int Rating, string Reason, string Note, DateTimeOffset UpdatedAt);

/// <summary>使用者對回答的評分；尚未評分時 <c>Feedback</c> 為 null，端點回 200 且無內容。</summary>
public sealed record FeedbackOutcome(FeedbackDto? Feedback);
