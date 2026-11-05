namespace AiNexus.Data.App.Identity;

public sealed class UserPreferences
{
    /// <summary>全站文字大小的百分比；套用在根元素字級，所有以 rem 寫的字級與間距一起縮放。</summary>
    public const int DefaultTextScale = 100;
    public const double DefaultReadingLineHeight = 1.2;
    public const int DefaultSidebarWidth = 240;
    public string Theme { get; set; } = "system";
    public bool ReducedMotion { get; set; }
    public string? DefaultModelId { get; set; }
    public int TextScale { get; set; } = DefaultTextScale;
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
