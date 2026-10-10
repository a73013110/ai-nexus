using AiNexus.Features.Knowledge.Documents;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Indexing;

public interface ITextChunker
{
    Result<IReadOnlyList<StructuredChunk>> Chunk(IReadOnlyList<DocumentPage> pages, ChunkerSnapshot? configuration = null);
}
