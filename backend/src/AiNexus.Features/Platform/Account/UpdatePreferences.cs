using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Platform.Account;

/// <summary>驗證順序固定：先檢查主題與文字大小，再由 handler 檢查模型 ID 長度與伺服器是否核准該模型。</summary>
internal sealed class PreferencesValidator : RequestValidator<PreferencesDto>
{
    public override Error Problem => AccountErrors.InvalidAppearance;

    public PreferencesValidator()
    {
        RuleFor(x => x.Theme).Must(UpdatePreferences.ValidTheme).WithErrorCode(RuleCode.Unknown);
        RuleFor(x => x.TextScale).Must(UpdatePreferences.ValidTextScale).WithErrorCode(RuleCode.Range);
    }
}

/// <summary>儲存使用者的主題、動畫、文字大小與預設模型偏好。</summary>
internal sealed class UpdatePreferences(AppDbContext db, ModelPresentation models) : IEndpointSlice
{
    private const int MaxModelIdLength = 160;

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/preferences", (PreferencesDto body, ICurrentUser user, UpdatePreferences handler, CancellationToken ct) =>
            handler.HandleAsync(user.User, body, ct))
        .NotAudited("介面偏好（主題、側欄）")
        .WithName("UpdatePreferences");

    public async Task<Result<PreferencesDto>> HandleAsync(User user, PreferencesDto value, CancellationToken ct)
    {
        var applied = Apply(user, value, models);
        if (!applied.IsSuccess) return applied.Error;
        await db.SaveChangesAsync(ct);
        return applied.Value;
    }

    public static bool ValidTheme(string? theme) => theme is "light" or "dark" or "system";

    /// <summary>前端只提供這個範圍內的檔位；超過 130% 時窄螢幕的側欄與對話框放不下。</summary>
    public static bool ValidTextScale(int scale) => scale is >= 100 and <= 130;

    /// <summary>檢查並把 <paramref name="value"/> 套用到已追蹤的使用者，但不儲存；失敗時不改動任何值。</summary>
    public static Result<PreferencesDto> Apply(User user, PreferencesDto value, ModelPresentation models)
    {
        if (!ValidTheme(value.Theme) || !ValidTextScale(value.TextScale)) return AccountErrors.InvalidAppearance;
        if (value.DefaultModelId?.Length > MaxModelIdLength) return AccountErrors.InvalidModel;
        string? model = null;
        if (value.DefaultModelId is not null)
        {
            // 模型必須由伺服器核准才會轉成內部 ID，前端送來的字串不可直接存。
            model = models.InternalId(value.DefaultModelId);
            if (model is null) return AccountErrors.ModelNotAllowed;
        }
        user.Preferences.Theme = value.Theme;
        user.Preferences.ReducedMotion = value.ReducedMotion;
        user.Preferences.TextScale = value.TextScale;
        user.Preferences.DefaultModelId = model;
        return models.Preferences(user.Preferences);
    }
}
