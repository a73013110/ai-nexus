namespace AiNexus.Features.Knowledge.Documents;

public sealed record DocumentDto(Guid Id, Guid? CollectionId, string FileName, string ContentType, string Status, int PageCount, int ChunkCount, string? Warning, Guid? JobId, bool CanEdit, bool HasOriginal, int TextVersion = 0);
