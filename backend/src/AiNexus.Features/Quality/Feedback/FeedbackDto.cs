namespace AiNexus.Features.Quality.Feedback;

public sealed record FeedbackDto(Guid MessageId, Guid ConversationId, string ConversationTitle, int Rating, string Reason, string Note, DateTimeOffset UpdatedAt);
