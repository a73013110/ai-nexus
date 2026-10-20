using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Features.Admin.Mail.Messages;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail;

/// <summary>
/// 郵件寄送平台：其他模組透過 <see cref="MailOutbox"/> 在自己的交易內入列，<see cref="MailDispatcher"/> 在背景寄出，
/// <see cref="MailRetentionWorker"/> 依保存期限清理。傳輸由 <c>Mail:Transport</c> 決定（SMTP、pickup 目錄或停用）。
/// 管理端的寄送紀錄（查詢、詳情、重寄、測試信）用來查證「到底有沒有寄出」。
/// </summary>
public sealed class MailModule : IFeatureModule
{
    /// <summary>測試信會實際寄出，每人每分鐘 3 封。</summary>
    public const string TestRateLimit = "mail-test";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<MailOptions, MailOptionsValidator>(MailOptions.Section)
            .Configure<IHostEnvironment, IConfiguration>((x, environment, config) =>
            {
                // 與 Files.StoragePath 同一做法：開發機不必設定就能寫 .eml 到 .local/mail；Testing 預設不寄；其他環境必須明確設定（驗證會擋）。
                if (x.Transport.Length == 0 && environment.IsDevelopment()) x.Transport = MailTransports.Pickup;
                if (x.Transport.Length == 0 && environment.IsEnvironment("Testing")) x.Transport = MailTransports.Disabled;
                if (!environment.IsDevelopment()) return;
                if (x.Pickup.Directory.Length == 0) x.Pickup.Directory = Path.Combine(config["LocalWorkspaceRoot"] ?? environment.ContentRootPath, ".local", "mail");
                if (x.From.Address.Length == 0) x.From.Address = "no-reply@ainexus.localhost";
                if (x.PublicBaseUrl.Length == 0) x.PublicBaseUrl = "https://localhost:5080";
            });
        services.AddSingleton<MailRenderer>();
        services.AddSingleton<MailContents>();
        services.AddScoped<MailOutbox>();
        services.AddSingleton<MailCircuit>();
        services.AddSingleton<MailThrottle>();
        services.AddSingleton<IMailTransport>(provider => provider.GetRequiredService<IOptions<MailOptions>>().Value.Transport switch
        {
            MailTransports.Smtp => ActivatorUtilities.CreateInstance<SmtpMailTransport>(provider),
            MailTransports.Pickup => ActivatorUtilities.CreateInstance<PickupMailTransport>(provider),
            _ => new DisabledMailTransport(),
        });
        services.AddHostedService<MailDispatcher>();
        services.AddHostedService<MailRetentionWorker>();
        services.AddMonitoringSource<MailMonitoring>();
        services.AddFeaturePolicy(FeatureCatalog.MailMessages);
        services.AddFeaturePolicy(FeatureCatalog.MailMessagesResend);
        services.AddRateLimiter(options => options.AddPerUserLimit(TestRateLimit, 3));
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var messages = api.MapGroup("/admin/mail-messages").RequireAuthorization(Policies.MailMessages);
        ListMailMessages.Map(messages);
        GetMailMessage.Map(messages);
        ResendMailMessage.Map(messages);
        SendTestMail.Map(api.MapGroup("/admin/mail-tests").RequireAuthorization(Policies.MailMessages));
        GetMailStatus.Map(api.MapGroup("/admin/mail-status").RequireAuthorization(Policies.MailMessages));
    }
}
