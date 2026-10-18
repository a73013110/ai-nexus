using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference.Models;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Account;

/// <summary>驗證順序固定：先檢查主題，再由 handler 檢查模型 ID 長度與伺服器是否核准該模型。</summary>
internal sealed class PreferencesValidator : RequestValidator<PreferencesDto>
{
    public override string ProblemCode => AccountErrors.InvalidTheme.Code;

    public PreferencesValidator() => RuleFor(x => x.Theme).Must(UpdatePreferences.ValidTheme).WithErrorCode("unknown");
}

/// <summary>儲存使用者的主題、動畫與預設模型偏好。</summary>
internal sealed class UpdatePreferences(AppDbContext db, ModelPresentation models)
{
    public const int MaxModelIdLength = 160;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/preferences", async (PreferencesDto body, ICurrentUser user, UpdatePreferences handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.User, body, ct)).ToHttpResult())
        .WithName("UpdatePreferences");

    public async Task<Result<PreferencesDto>> HandleAsync(User user, PreferencesDto value, CancellationToken ct)
    {
        var applied = Apply(user, value, models);
        if (!applied.IsSuccess) return applied.Error;
        await db.SaveChangesAsync(ct);
        return applied.Value;
    }

    public static bool ValidTheme(string? theme) => theme is "light" or "dark" or "system";

    /// <summary>檢查並把 <paramref name="value"/> 套用到已追蹤的使用者，但不儲存；失敗時不改動任何值。</summary>
    public static Result<PreferencesDto> Apply(User user, PreferencesDto value, ModelPresentation models)
    {
        if (!ValidTheme(value.Theme)) return AccountErrors.InvalidTheme;
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
        user.Preferences.DefaultModelId = model;
        return models.Preferences(user.Preferences);
    }
}
