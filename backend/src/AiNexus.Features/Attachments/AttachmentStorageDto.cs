namespace AiNexus.Features.Attachments;

public sealed record AttachmentStorageDto(long UsedBytes, long LimitBytes, long RemainingBytes, long? PersonalLimitBytes, long? GroupLimitBytes, long DefaultLimitBytes, string LimitSource);
