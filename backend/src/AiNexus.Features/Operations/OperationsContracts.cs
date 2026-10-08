namespace AiNexus.Features.Operations;

public sealed record StatusDto(string Storage, string Authentication, int QueueDepth, bool Generating);
