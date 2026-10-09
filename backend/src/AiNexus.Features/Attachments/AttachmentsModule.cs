using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Attachments;

/// <summary>
/// Uploaded files, their storage quota and the file library. Other modules upload, look up and read files through
/// <see cref="AttachmentService"/>, <see cref="AttachmentQuota"/> and <see cref="AttachmentLifecycle"/>; each endpoint
/// use case has its own file.
/// </summary>
public sealed class AttachmentsModule : IFeatureModule
{
    /// <summary>Multipart upload ceiling: one file at the largest configurable size plus form overhead.</summary>
    public const long UploadBodyLimit = 9 * 1024 * 1024;

    /// <summary>Each upload reads, sniffs, extracts and stores a file of up to <see cref="UploadBodyLimit"/>.</summary>
    public const string UploadRateLimit = "attachment-upload";
    public const int UploadsPerMinute = 30;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<AttachmentService>();
        services.AddScoped<RenameLibraryFile>();
        services.AddScoped<RetainLibraryFile>();
        services.AddScoped<RemoveAttachment>();
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
        services.AddRateLimiter(options => options.AddPerUserLimit(UploadRateLimit, UploadsPerMinute));
        services.AddFeaturePolicy(FeatureIds.Files);
        services.AddFeaturePolicy(Policies.Attachments, FeatureIds.Files, FeatureIds.Chat, FeatureIds.Knowledge, FeatureIds.Projects);
    }

    // Endpoint order is the published OpenAPI order: the file library first, then attachments. Knowledge lists the
    // library, since a file's usages include documents.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var library = api.MapGroup("/files").RequireAuthorization(Policies.Files).WithTags("File library");
        RenameLibraryFile.Map(library);
        RetainLibraryFile.Map(library);
        RemoveAttachment.MapLibrary(library);

        var routes = api.MapGroup("/attachments").RequireAuthorization(Policies.Attachments).WithTags("Attachments");
        GetAttachmentPolicy.Map(routes);
        GetAttachmentStorage.Map(routes);
        UploadAttachment.Map(routes);
        GetAttachment.Map(routes);
        DownloadAttachment.Map(routes);
        RemoveAttachment.MapDraft(routes);
    }

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
