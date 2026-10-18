using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments.Extraction;
using AiNexus.Features.Attachments.Files;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Identity;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Attachments;

/// <summary>
/// 已上傳的檔案、儲存配額與檔案庫。其他模組透過 <see cref="AttachmentService"/>、<see cref="AttachmentQuota"/>、
/// <see cref="AttachmentLifecycle"/> 上傳、查詢與讀取檔案；每個端點用例各自一個檔案。
/// </summary>
public sealed class AttachmentsModule : IFeatureModule
{
    /// <summary>multipart 上傳的請求大小上限：最大可設定檔案大小加上表單額外負荷。</summary>
    public const long UploadBodyLimit = 9 * 1024 * 1024;

    /// <summary>每次上傳都會讀取、偵測類型、抽取文字並儲存檔案；請求大小受 <see cref="UploadBodyLimit"/> 限制。</summary>
    public const string UploadRateLimit = "attachment-upload";
    public const int UploadsPerMinute = 30;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<FilesDashboard>();
        services.AddMonitoringSource<StorageMonitoring>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<DocumentExtractor>();
        services.AddSingleton<AttachmentWriteLock>();
        services.AddSingleton<IAttachmentStorage, FileAttachmentStorage>();
        services.AddScoped<AttachmentQuota>();
        services.AddScoped<AttachmentLifecycle>();
        services.AddHostedService<AttachmentCleanupWorker>();
        services.AddSettings<AttachmentOptions, AttachmentOptionsValidator>(AttachmentOptions.Section)
            .Configure<IHostEnvironment, IConfiguration>((x, environment, config) =>
            {
                if (string.IsNullOrWhiteSpace(x.StoragePath) && environment.IsDevelopment()) x.StoragePath = Path.Combine(config["LocalWorkspaceRoot"]!, ".local", "data", "attachments");
            });
        services.AddRateLimiter(options => options.AddPerUserLimit(UploadRateLimit, UploadsPerMinute));
        services.AddFeaturePolicy(FeatureIds.Files);
        services.AddFeaturePolicy(Policies.Attachments, FeatureIds.Files, FeatureIds.Chat, FeatureIds.Knowledge, FeatureIds.Projects);
    }

    // 端點順序即已發布的 OpenAPI 順序：檔案庫在前、附件在後。知識庫模組也會列出檔案庫，
    // 因為一個檔案的使用處可能包含文件。
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

    /// <summary>儲存根目錄不可用時，啟動即失敗（連同每個主機指令），在接受任何請求之前就發現問題。</summary>
    public static void VerifyStorage(IServiceProvider services) => _ = services.GetRequiredService<IAttachmentStorage>();
}
