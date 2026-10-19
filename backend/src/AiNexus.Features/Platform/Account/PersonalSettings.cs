using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Platform.Inference.Models;

namespace AiNexus.Features.Platform.Account;

public sealed record UserSettingsDto(PreferencesDto Appearance, int ReadingFontSize = UserPreferences.DefaultReadingFontSize, double ReadingLineHeight = UserPreferences.DefaultReadingLineHeight,
    string Density = "comfortable", int SidebarWidth = UserPreferences.DefaultSidebarWidth, string ReadingWidth = "standard",
    bool EnterToSend = true, bool AutoFollow = true, bool SaveLocalDrafts = true,
    bool NotifyOnCompletion = false, string DefaultReasoningEffort = "auto");

/// <summary>個人設定與 <see cref="UserPreferences"/> 之間的雙向對應；新增設定欄位時兩個方向都在這裡補。</summary>
internal static class PersonalSettings
{
    public static UserSettingsDto ToSettingsDto(this UserPreferences p, ModelPresentation models) => new(models.Preferences(p), p.ReadingFontSize, p.ReadingLineHeight, p.Density,
        p.SidebarWidth, p.ReadingWidth, p.EnterToSend, p.AutoFollow, p.SaveLocalDrafts, p.NotifyOnCompletion, p.DefaultReasoningEffort);

    /// <summary>套用外觀以外的設定；外觀由 <c>UpdatePreferences.Apply</c> 檢查後套用，呼叫端須先驗證完整個請求。</summary>
    public static void ApplyLayout(this UserPreferences p, UserSettingsDto value)
    {
        p.ReadingFontSize = value.ReadingFontSize; p.ReadingLineHeight = value.ReadingLineHeight;
        p.Density = value.Density; p.SidebarWidth = value.SidebarWidth; p.ReadingWidth = value.ReadingWidth;
        p.EnterToSend = value.EnterToSend; p.AutoFollow = value.AutoFollow; p.SaveLocalDrafts = value.SaveLocalDrafts;
        p.NotifyOnCompletion = value.NotifyOnCompletion; p.DefaultReasoningEffort = value.DefaultReasoningEffort;
    }
}
