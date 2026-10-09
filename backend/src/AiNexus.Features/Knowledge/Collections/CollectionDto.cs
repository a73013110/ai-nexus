using AiNexus.Features.Collaboration;

namespace AiNexus.Features.Knowledge.Collections;

public sealed record CollectionDto(ResourceDto Resource, string Description, int Documents, int ReadyDocuments);
