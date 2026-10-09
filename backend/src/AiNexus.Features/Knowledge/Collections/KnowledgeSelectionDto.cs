namespace AiNexus.Features.Knowledge.Collections;

// Also the request body of SaveConversationKnowledge. Its limit (at most three distinct collections) is checked only
// after the conversation is found, inside the conversation's generation lock, so this request has no validator.
public sealed record KnowledgeSelectionDto(IReadOnlyList<Guid> CollectionIds);
