using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Identity;

public sealed record UserSettingsDto(PreferencesDto Appearance, int ReadingFontSize = 17, double ReadingLineHeight = 1.8,
    string Density = "comfortable", int SidebarWidth = 264, string ReadingWidth = "standard",
    bool EnterToSend = true, bool AutoFollow = true, bool SaveLocalDrafts = true,
    bool NotifyOnCompletion = false, string DefaultReasoningEffort = "auto");
public sealed record UsageDayDto(DateOnly Date, int Requests, long InputTokens, long OutputTokens);
public sealed record PersonalUsageDto(int Days, int Requests, int Completed, int Failed, int Cancelled,
    long InputTokens, long OutputTokens, int RequestsWithUsage, long AttachmentBytes,
    IReadOnlyList<UsageDayDto> Daily);

public sealed class PersonalSettingsService(NexusDbContext db, CurrentUser current, ModelPresentation models)
{
    public async Task<UserSettingsDto> GetAsync(CancellationToken ct) => Map((await current.GetAsync(ct)).Preferences);
    public async Task<UserSettingsDto> SaveAsync(UserSettingsDto value, CancellationToken ct)
    {
        if (value.ReadingFontSize is < 16 or > 24 || !double.IsFinite(value.ReadingLineHeight) || value.ReadingLineHeight is < 1.5 or > 2.2 ||
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
    public async Task<PersonalUsageDto> UsageAsync(CancellationToken ct)
    {
        var owner = (await current.GetAsync(ct)).Id;
        var since = DateTimeOffset.UtcNow.Date.AddDays(-29);
        var rows = await db.Runs.AsNoTracking().Where(x => x.OwnerId == owner && x.CreatedAt >= since)
            .Select(x => new { x.CreatedAt, x.Status, x.InputTokens, x.OutputTokens })
            .Concat(db.Set<ModelInvocation>().AsNoTracking().Where(x => x.OwnerId == owner && x.CreatedAt >= since).Select(x => new { x.CreatedAt, x.Status, x.InputTokens, x.OutputTokens })).ToListAsync(ct);
        var bytes = await db.Set<Attachment>().Where(x => x.OwnerId == owner).SumAsync(x => (long?)x.Size, ct) ?? 0;
        var daily = rows.GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.UtcDateTime)).OrderBy(x => x.Key)
            .Select(x => new UsageDayDto(x.Key, x.Count(), x.Sum(y => y.InputTokens ?? 0), x.Sum(y => y.OutputTokens ?? 0))).ToArray();
        return new(30, rows.Count, rows.Count(x => x.Status == "completed"), rows.Count(x => x.Status == "failed"), rows.Count(x => x.Status == "cancelled"),
            rows.Sum(x => x.InputTokens ?? 0), rows.Sum(x => x.OutputTokens ?? 0), rows.Count(x => x.InputTokens.HasValue && x.OutputTokens.HasValue), bytes, daily);
    }
    private UserSettingsDto Map(UserPreferences p) => new(models.Preferences(p), p.ReadingFontSize, p.ReadingLineHeight, p.Density,
        p.SidebarWidth, p.ReadingWidth, p.EnterToSend, p.AutoFollow, p.SaveLocalDrafts, p.NotifyOnCompletion, p.DefaultReasoningEffort);
}
