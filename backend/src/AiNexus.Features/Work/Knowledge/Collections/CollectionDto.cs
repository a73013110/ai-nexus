using AiNexus.Features.Work.Collaboration;

namespace AiNexus.Features.Work.Knowledge.Collections;

public sealed record CollectionDto(ResourceDto Resource, string Description, int Documents, int ReadyDocuments);
