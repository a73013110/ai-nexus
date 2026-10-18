namespace AiNexus.Features.Work.Files;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, bool IsImage, string AnalysisMode);
