using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Platform.Account;

/// <summary>閱讀與版面設定的形狀檢查。外觀（Appearance）不在這裡驗證，由 <c>UpdatePreferences.Apply</c> 使用它自己的錯誤代碼檢查。</summary>
internal sealed class UserSettingsValidator : RequestValidator<UserSettingsDto>
{
    public override Error Problem => AccountErrors.InvalidSettings;

    public UserSettingsValidator()
    {
        RuleFor(x => x.ReadingFontSize).InclusiveBetween(12, 24);
        RuleFor(x => x.ReadingLineHeight).Must(x => double.IsFinite(x) && x is >= 1 and <= 2.2).WithErrorCode(RuleCode.Range);
        RuleFor(x => x.SidebarWidth).InclusiveBetween(240, 360);
        RuleFor(x => x.Density).Must(x => x is "comfortable" or "compact").WithErrorCode(RuleCode.Unknown);
        RuleFor(x => x.ReadingWidth).Must(x => x is "narrow" or "standard" or "wide").WithErrorCode(RuleCode.Unknown);
        RuleFor(x => x.DefaultReasoningEffort).Must(x => x is "auto" or "minimal" or "low" or "medium" or "high").WithErrorCode(RuleCode.Unknown);
    }
}

/// <summary>一次儲存外觀偏好，以及閱讀、版面與輸入框設定。</summary>
internal sealed class SaveUserSettings(AppDbContext db, ModelPresentation models) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/settings", (UserSettingsDto body, ICurrentUser user, SaveUserSettings handler, CancellationToken ct) =>
            handler.HandleAsync(user.User, body, ct))
        .Audited(AccountAudit.SettingsUpdated)
        .WithName("SaveUserSettings");

    public async Task<Result<UserSettingsDto>> HandleAsync(User user, UserSettingsDto value, CancellationToken ct)
    {
        // 先驗證整個請求，再改動任何已追蹤的值，避免失敗時留下只改了一半的設定。
        var appearance = UpdatePreferences.Apply(user, value.Appearance, models);
        if (!appearance.IsSuccess) return appearance.Error;
        user.Preferences.ApplyLayout(value);
        await db.SaveChangesAsync(ct);
        return user.Preferences.ToSettingsDto(models);
    }
}
