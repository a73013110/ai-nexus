namespace AiNexus.Features.Identity.Users;

public sealed class UserPreferences
{
    public const int DefaultReadingFontSize = 15;
    public const double DefaultReadingLineHeight = 1.2;
    public const int DefaultSidebarWidth = 240;
    public string Theme { get; set; } = "system";
    public bool ReducedMotion { get; set; }
    public string? DefaultModelId { get; set; }
    public int ReadingFontSize { get; set; } = DefaultReadingFontSize;
    public double ReadingLineHeight { get; set; } = DefaultReadingLineHeight;
    public string Density { get; set; } = "comfortable";
    public int SidebarWidth { get; set; } = DefaultSidebarWidth;
    public string ReadingWidth { get; set; } = "standard";
    public bool EnterToSend { get; set; } = true;
    public bool AutoFollow { get; set; } = true;
    public bool SaveLocalDrafts { get; set; } = true;
    public bool NotifyOnCompletion { get; set; }
    public string DefaultReasoningEffort { get; set; } = "auto";
}
