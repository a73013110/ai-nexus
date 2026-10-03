namespace AiNexus.Modules.Identity;

public sealed class NexusUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sid { get; set; } = "";
    public string Account { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset LastSeenAt { get; set; }
    public UserPreferences Preferences { get; set; } = new();
}

public sealed class UserPreferences
{
    public string Theme { get; set; } = "system";
    public bool ReducedMotion { get; set; }
    public string? DefaultModelId { get; set; }
}
