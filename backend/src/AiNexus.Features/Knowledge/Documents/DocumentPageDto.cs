namespace AiNexus.Features.Knowledge.Documents;

public sealed record DocumentPageDto(int PageNumber, string Text, string Extraction, bool NeedsReview);
