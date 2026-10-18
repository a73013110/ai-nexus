using AiNexus.Data.App.Knowledge;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge.Indexing;

public interface ITextChunker
{
    Result<IReadOnlyList<StructuredChunk>> Chunk(IReadOnlyList<DocumentPage> pages);
}
