using AiNexus.Features.Inference;

namespace AiNexus.Features.Identity;

public sealed record UserSettingsDto(PreferencesDto Appearance, int ReadingFontSize = UserPreferences.DefaultReadingFontSize, double ReadingLineHeight = UserPreferences.DefaultReadingLineHeight,
    string Density = "comfortable", int SidebarWidth = UserPreferences.DefaultSidebarWidth, string ReadingWidth = "standard",
    bool EnterToSend = true, bool AutoFollow = true, bool SaveLocalDrafts = true,
    bool NotifyOnCompletion = false, string DefaultReasoningEffort = "auto");
public sealed record UsageDayDto(DateOnly Date, int Requests, long InputTokens, long OutputTokens);
public sealed record PersonalUsageDto(int Days, int Requests, int Completed, int Failed, int Cancelled,
    long InputTokens, long OutputTokens, int RequestsWithUsage,
    IReadOnlyList<UsageDayDto> Daily, AiNexus.Features.Attachments.AttachmentStorageDto Storage, long TotalDurationMilliseconds = 0, int TimedRequests = 0, TokenUsageDto? Tokens = null);

internal static class PersonalSettings
{
    public static UserSettingsDto ToSettingsDto(this UserPreferences p, ModelPresentation models) => new(models.Preferences(p), p.ReadingFontSize, p.ReadingLineHeight, p.Density,
        p.SidebarWidth, p.ReadingWidth, p.EnterToSend, p.AutoFollow, p.SaveLocalDrafts, p.NotifyOnCompletion, p.DefaultReasoningEffort);
}
