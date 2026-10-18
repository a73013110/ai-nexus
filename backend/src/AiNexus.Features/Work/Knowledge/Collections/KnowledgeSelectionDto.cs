using AiNexus.Platform.Validation;
namespace AiNexus.Features.Work.Knowledge.Collections;

/// <summary>對話知識庫選取的資料，同時也是 SaveConversationKnowledge 的請求本體。</summary>
[ValidatedInHandler("The limit (at most three distinct collections) is checked only after the conversation is found, inside its generation lock.")]
public sealed record KnowledgeSelectionDto(IReadOnlyList<Guid> CollectionIds);
