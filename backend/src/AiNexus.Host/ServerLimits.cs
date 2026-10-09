namespace AiNexus.Host;

internal static class ServerLimits
{
    /// <summary>
    /// Server-wide ceilings. Each endpoint declares its own, smaller limit (see RequestBodyLimits); these only bound the
    /// largest declared upload so Kestrel and IIS never buffer more than the application could accept.
    /// </summary>
    public static void ConfigureServerLimits(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => { options.AddServerHeader = false; options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; });
        builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = 10 * 1024 * 1024);
        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 9 * 1024 * 1024);
    }
}
