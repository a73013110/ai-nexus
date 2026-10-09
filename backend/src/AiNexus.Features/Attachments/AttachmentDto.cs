namespace AiNexus.Features.Attachments;

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, bool IsImage, string AnalysisMode);
