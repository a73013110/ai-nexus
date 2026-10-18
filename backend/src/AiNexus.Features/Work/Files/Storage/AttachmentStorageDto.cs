namespace AiNexus.Features.Work.Files.Storage;

public sealed record AttachmentStorageDto(long UsedBytes, long LimitBytes, long RemainingBytes, long? PersonalLimitBytes, long? GroupLimitBytes, long DefaultLimitBytes, string LimitSource);
