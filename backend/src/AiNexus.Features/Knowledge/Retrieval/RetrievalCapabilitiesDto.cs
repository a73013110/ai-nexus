using AiNexus.Data.App;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed record RetrievalCapabilitiesDto(SqlVectorCapabilitiesDto? Sql, bool TestStore, RetrievalConnectionDto Embedding, RetrievalConnectionDto Rerank);
