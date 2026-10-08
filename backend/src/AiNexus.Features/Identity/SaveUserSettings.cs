using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Identity;

/// <summary>Reading and layout settings. The nested appearance is checked afterwards by the handler, with its own codes.</summary>
internal sealed class UserSettingsValidator : RequestValidator<UserSettingsDto>
{
    public override string ProblemCode => "invalid_settings";

    public UserSettingsValidator()
    {
        RuleFor(x => x.ReadingFontSize).InclusiveBetween(12, 24);
        RuleFor(x => x.ReadingLineHeight).Must(x => double.IsFinite(x) && x is >= 1 and <= 2.2).WithErrorCode("range");
        RuleFor(x => x.SidebarWidth).InclusiveBetween(240, 360);
        RuleFor(x => x.Density).Must(x => x is "comfortable" or "compact").WithErrorCode("unknown");
        RuleFor(x => x.ReadingWidth).Must(x => x is "narrow" or "standard" or "wide").WithErrorCode("unknown");
        RuleFor(x => x.DefaultReasoningEffort).Must(x => x is "auto" or "minimal" or "low" or "medium" or "high").WithErrorCode("unknown");
    }
}

/// <summary>Saves the appearance preferences and the reading, layout and composer settings together.</summary>
internal sealed class SaveUserSettings(NexusDbContext db, ModelPresentation models)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/settings", async (UserSettingsDto body, ICurrentUser user, SaveUserSettings handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.User, body, ct)).ToHttpResult())
        .WithName("SaveUserSettings").Produces<UserSettingsDto>();

    public async Task<Result<UserSettingsDto>> HandleAsync(NexusUser user, UserSettingsDto value, CancellationToken ct)
    {
        // Validate the complete request before changing any tracked values.
        var appearance = UpdatePreferences.Apply(user, value.Appearance, models);
        if (!appearance.IsSuccess) return appearance.Error;
        var p = user.Preferences;
        p.ReadingFontSize = value.ReadingFontSize; p.ReadingLineHeight = value.ReadingLineHeight;
        p.Density = value.Density; p.SidebarWidth = value.SidebarWidth; p.ReadingWidth = value.ReadingWidth;
        p.EnterToSend = value.EnterToSend; p.AutoFollow = value.AutoFollow; p.SaveLocalDrafts = value.SaveLocalDrafts;
        p.NotifyOnCompletion = value.NotifyOnCompletion; p.DefaultReasoningEffort = value.DefaultReasoningEffort;
        await db.SaveChangesAsync(ct);
        return p.ToSettingsDto(models);
    }
}
