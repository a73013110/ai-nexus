namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed record CitationDto(int Number, Guid DocumentId, string Title, int PageNumber, string Excerpt, int EndPage = 0);
