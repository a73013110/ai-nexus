using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

public sealed class AttachmentsModule : IFeatureModule
{
    /// <summary>Multipart upload ceiling: one file at the largest configurable size plus form overhead.</summary>
    public const long UploadBodyLimit = 9 * 1024 * 1024;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<AttachmentService>();
        services.AddScoped<FileLibraryService>();
        services.AddScoped<DocumentExtractor>();
        services.AddSingleton<AttachmentWriteLock>();
        services.AddSingleton<IAttachmentStorage, FileAttachmentStorage>();
        services.AddScoped<AttachmentQuota>();
        services.AddScoped<AttachmentLifecycle>();
        services.AddHostedService<AttachmentCleanupWorker>();
        services.AddOptions<AttachmentOptions>().BindConfiguration("Attachments")
            .Configure<IHostEnvironment, IConfiguration>((x, environment, config) =>
            {
                if (string.IsNullOrWhiteSpace(x.StoragePath) && environment.IsDevelopment()) x.StoragePath = Path.Combine(config["LocalWorkspaceRoot"]!, ".local", "data", "attachments");
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AttachmentOptions>, AttachmentOptionsValidator>();
        services.AddFeaturePolicy("files");
        services.AddFeaturePolicy("feature:attachments", "files", BuiltInAccess.ChatFeature, "knowledge", "projects");
    }

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapAttachments();

    /// <summary>Fails startup (and every host command) when the storage root is unusable, before any request is accepted.</summary>
    public static void VerifyStorage(IServiceProvider services) => _ = services.GetRequiredService<IAttachmentStorage>();
}

internal sealed class AttachmentOptionsValidator : IValidateOptions<AttachmentOptions>
{
    public ValidateOptionsResult Validate(string? name, AttachmentOptions x)
        => x.MaxFileBytes is >= 1024 and <= 8 * 1024 * 1024 && x.MaxFilesPerMessage is >= 1 and <= 8 && x.MaxMessageBytes >= x.MaxFileBytes && x.MaxMessageBytes <= 16 * 1024 * 1024
           && x.DefaultOwnerLimitBytes >= x.MaxMessageBytes && x.DefaultOwnerLimitBytes <= AttachmentOptions.MaximumLimitBytes && x.MaxExtractedCharacters is >= 1000 and <= 256000
           && x.MaxPdfPages is >= 1 and <= 100 && x.ImageTokenEstimate is >= 1024 and <= 16384 && x.DraftRetentionDays is >= 1 and <= 365 && x.CleanupIntervalMinutes is >= 1 and <= 1440
           && Path.IsPathFullyQualified(x.StoragePath)
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid attachment storage or limits.");
}
