namespace AiNexus.Features.Inference;

public static class RunStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
    public static bool IsActive(string status) => status is Queued or Running;
}
