using System.Text.Json.Serialization;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform;

public static class PlatformServices
{
    /// <summary>Cross-cutting host services shared by every module.</summary>
    public static WebApplicationBuilder AddPlatform(this WebApplicationBuilder builder)
    {
        builder.AddNexusDiagnostics();
        builder.AddKeyRing();
        var services = builder.Services;
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddCsrfProtection(builder.Environment, builder.Configuration);
        // Deny by default: endpoints opt into anonymous access explicitly.
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddNexusProblemDetails();
        services.AddRateLimiter(options => options.OnRejected = (context, _) => new ValueTask(Problems.WriteAsync(context.HttpContext, 429, "rate_limited")));
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.MaxDepth = 32;
        });
        services.AddControlledHttpClient(ControlledHttpClients.Tools);
        return builder;
    }

    private static void AddKeyRing(this WebApplicationBuilder builder)
    {
        var keyRing = builder.Configuration["DataProtection:KeyRingPath"];
        if (keyRing is null && builder.Environment.IsDevelopment()) keyRing = Path.Combine(builder.Configuration["LocalWorkspaceRoot"]!, ".local", "keys");
        var protection = builder.Services.AddDataProtection().SetApplicationName("AiNexus");
        if (keyRing is null) return;
        Directory.CreateDirectory(keyRing);
        protection.PersistKeysToFileSystem(new DirectoryInfo(keyRing));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
    }
}
