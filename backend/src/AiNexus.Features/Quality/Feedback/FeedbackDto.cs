namespace AiNexus.Features.Quality.Feedback;

public sealed record FeedbackDto(Guid MessageId, Guid ConversationId, string ConversationTitle, int Rating, string Reason, string Note, DateTimeOffset UpdatedAt);

/// <summary>The user's rating of an answer; <c>Feedback</c> is null when there is none, which answers 200 without a body.</summary>
public sealed record FeedbackOutcome(FeedbackDto? Feedback);
