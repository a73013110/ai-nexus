using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;

namespace AiNexus.Modules.Identity;

public sealed record UserSettingsDto(PreferencesDto Appearance, int ReadingFontSize = 17, double ReadingLineHeight = 1.8,
    string Density = "comfortable", int SidebarWidth = 264, string ReadingWidth = "standard",
    bool EnterToSend = true, bool AutoFollow = true, bool SaveLocalDrafts = true,
    bool NotifyOnCompletion = false, string DefaultReasoningEffort = "auto");
public sealed record UsageDayDto(DateOnly Date, int Requests, long InputTokens, long OutputTokens);
public sealed record PersonalUsageDto(int Days, int Requests, int Completed, int Failed, int Cancelled,
    long InputTokens, long OutputTokens, int RequestsWithUsage, long AttachmentBytes,
    IReadOnlyList<UsageDayDto> Daily);

public sealed class PersonalSettingsService(NexusDbContext db, CurrentUser current, ModelPresentation models, UsageReports reports)
{
    public async Task<UserSettingsDto> GetAsync(CancellationToken ct) => Map((await current.GetAsync(ct)).Preferences);
    public async Task<UserSettingsDto> SaveAsync(UserSettingsDto value, CancellationToken ct)
    {
        if (value.ReadingFontSize is < 12 or > 24 || !double.IsFinite(value.ReadingLineHeight) || value.ReadingLineHeight is < 1 or > 2.2 ||
            value.SidebarWidth is < 240 or > 360 || value.Density is not ("comfortable" or "compact") ||
            value.ReadingWidth is not ("narrow" or "standard" or "wide") ||
            value.DefaultReasoningEffort is not ("auto" or "minimal" or "low" or "medium" or "high"))
            throw new ApiException(400, "invalid_settings", "個人設定超出允許範圍，請重新選擇。");
        // Validate the complete request before changing any tracked values.
        await current.UpdatePreferencesAsync(value.Appearance, ct, persist: false);
        var p = (await current.GetAsync(ct)).Preferences;
        p.ReadingFontSize = value.ReadingFontSize; p.ReadingLineHeight = value.ReadingLineHeight;
        p.Density = value.Density; p.SidebarWidth = value.SidebarWidth; p.ReadingWidth = value.ReadingWidth;
        p.EnterToSend = value.EnterToSend; p.AutoFollow = value.AutoFollow; p.SaveLocalDrafts = value.SaveLocalDrafts;
        p.NotifyOnCompletion = value.NotifyOnCompletion; p.DefaultReasoningEffort = value.DefaultReasoningEffort;
        await db.SaveChangesAsync(ct);
        return Map(p);
    }
    public async Task<PersonalUsageDto> UsageAsync(CancellationToken ct) => await reports.ForOwnerAsync((await current.GetAsync(ct)).Id, ct);
    private UserSettingsDto Map(UserPreferences p) => new(models.Preferences(p), p.ReadingFontSize, p.ReadingLineHeight, p.Density,
        p.SidebarWidth, p.ReadingWidth, p.EnterToSend, p.AutoFollow, p.SaveLocalDrafts, p.NotifyOnCompletion, p.DefaultReasoningEffort);
}
