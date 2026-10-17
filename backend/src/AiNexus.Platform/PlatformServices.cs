using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform;

/// <summary>
/// 跨橫切關注點平台共用服務註冊。
/// 由每個功能模組共享之主機核心基礎建設（診斷、資料保護金鑰、CSRF、嚴格授權、限流與安全 JSON 選項）。
/// </summary>
public static class PlatformServices
{
    /// <summary>
    /// 向 WebApplicationBuilder 註冊平台共用基礎服務。
    /// </summary>
    public static WebApplicationBuilder AddPlatform(this WebApplicationBuilder builder)
    {
        builder.AddNexusDiagnostics();
        builder.AddKeyRing();
        var services = builder.Services;

        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSettings<DatabasesOptions, DatabasesOptionsValidator>(DatabasesOptions.Section);
        services.AddCsrfProtection(builder.Environment, builder.Configuration);

        // 預設拒絕存取原則：所有端點除非明確宣告 AllowAnonymous，否則必須具備已驗證身分。
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddNexusProblemDetails();
        services.AddHealthChecks();
        services.AddRateLimiter(options => options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }
            return new ValueTask(Problems.WriteAsync(context.HttpContext, 429, "rate_limited"));
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.MaxDepth = 32;

            // 列舉以名稱（字串）傳遞，使 API 契約與前端產生之型別能列出核准的名稱。
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddControlledHttpClient(ControlledHttpClients.Tools);
        return builder;
    }

    private static void AddKeyRing(this WebApplicationBuilder builder)
    {
        var keyRing = builder.Configuration["DataProtection:KeyRingPath"];
        if (keyRing is null && builder.Environment.IsDevelopment())
        {
            keyRing = Path.Combine(builder.Configuration["LocalWorkspaceRoot"]!, ".local", "keys");
        }

        var protection = builder.Services.AddDataProtection().SetApplicationName("AiNexus");
        if (keyRing is null)
        {
            return;
        }

        Directory.CreateDirectory(keyRing);
        protection.PersistKeysToFileSystem(new DirectoryInfo(keyRing));
        if (OperatingSystem.IsWindows())
        {
            protection.ProtectKeysWithDpapi();
        }
    }
}
