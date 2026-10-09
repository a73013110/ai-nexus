using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Indexing;

public interface ITextChunker
{
    IReadOnlyList<StructuredChunk> Chunk(IReadOnlyList<DocumentPage> pages, ChunkerSnapshot? configuration = null);
}
