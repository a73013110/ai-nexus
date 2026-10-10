using AiNexus.Platform.Validation;
namespace AiNexus.Features.Knowledge.Collections;

/// <summary>Also the request body of SaveConversationKnowledge.</summary>
[ValidatedInHandler("The limit (at most three distinct collections) is checked only after the conversation is found, inside its generation lock.")]
public sealed record KnowledgeSelectionDto(IReadOnlyList<Guid> CollectionIds);
