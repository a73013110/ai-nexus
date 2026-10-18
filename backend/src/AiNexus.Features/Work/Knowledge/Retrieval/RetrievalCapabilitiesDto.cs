using AiNexus.Data.App;

namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed record RetrievalCapabilitiesDto(SqlVectorCapabilitiesDto? Sql, RetrievalConnectionDto Embedding, RetrievalConnectionDto Rerank);
