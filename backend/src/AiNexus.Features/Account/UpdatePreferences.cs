using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Account;

/// <summary>The theme is checked first; the model id length and server approval are checked by the handler, in that order.</summary>
internal sealed class PreferencesValidator : RequestValidator<PreferencesDto>
{
    public override string ProblemCode => AccountErrors.InvalidTheme.Code;

    public PreferencesValidator() => RuleFor(x => x.Theme).Must(UpdatePreferences.ValidTheme).WithErrorCode("unknown");
}

/// <summary>Saves the user's theme, motion and default model preferences.</summary>
internal sealed class UpdatePreferences(NexusDbContext db, ModelPresentation models)
{
    public const int MaxModelIdLength = 160;

    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/preferences", async (PreferencesDto body, ICurrentUser user, UpdatePreferences handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.User, body, ct)).ToHttpResult())
        .WithName("UpdatePreferences").Produces<PreferencesDto>();

    public async Task<Result<PreferencesDto>> HandleAsync(NexusUser user, PreferencesDto value, CancellationToken ct)
    {
        var applied = Apply(user, value, models);
        if (!applied.IsSuccess) return applied.Error;
        await db.SaveChangesAsync(ct);
        return applied.Value;
    }

    public static bool ValidTheme(string? theme) => theme is "light" or "dark" or "system";

    /// <summary>Checks and applies <paramref name="value"/> to the tracked user without saving; nothing changes on failure.</summary>
    public static Result<PreferencesDto> Apply(NexusUser user, PreferencesDto value, ModelPresentation models)
    {
        if (!ValidTheme(value.Theme)) return AccountErrors.InvalidTheme;
        if (value.DefaultModelId?.Length > MaxModelIdLength) return AccountErrors.InvalidModel;
        string? model = null;
        if (value.DefaultModelId is not null && (model = models.InternalId(value.DefaultModelId)) is null) return AccountErrors.ModelNotAllowed;
        user.Preferences.Theme = value.Theme;
        user.Preferences.ReducedMotion = value.ReducedMotion;
        user.Preferences.DefaultModelId = model;
        return models.Preferences(user.Preferences);
    }
}
