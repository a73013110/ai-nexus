using AiNexus.Data.App.Knowledge;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge.Indexing;

public interface ITextChunker
{
    Result<StructuredChunk[]> Chunk(IReadOnlyList<DocumentPage> pages);
}
