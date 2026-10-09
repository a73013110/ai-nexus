using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Http.Features;

namespace AiNexus.Platform.Http;

/// <summary>Per-endpoint request body ceiling, declared next to the endpoint that needs it.</summary>
public sealed class RequestBodyLimitMetadata(Func<IServiceProvider, long> resolve)
{
    public long Resolve(IServiceProvider services) => resolve(services);
}

public static class RequestBodyLimits
{
    /// <summary>Ordinary JSON requests.</summary>
    public const long Default = 64 * 1024;

    /// <summary>JSON-escaped UTF-16 characters can occupy six bytes; keeps character limits usable for Chinese clients.</summary>
    public static long ForJsonCharacters(int characters) => characters * 6L + 8192;

    public static TBuilder WithRequestBodyLimit<TBuilder>(this TBuilder builder, long bytes) where TBuilder : IEndpointConventionBuilder
        => builder.WithRequestBodyLimit(_ => bytes);

    public static TBuilder WithRequestBodyLimit<TBuilder>(this TBuilder builder, Func<IServiceProvider, long> resolve) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new RequestBodyLimitMetadata(resolve));

    /// <summary>Rejects declared oversize bodies early and caps streamed bodies at the endpoint's limit.</summary>
    public static IApplicationBuilder UseRequestBodyLimits(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        var limit = http.GetEndpoint()?.Metadata.GetMetadata<RequestBodyLimitMetadata>()?.Resolve(http.RequestServices) ?? Default;
        if (http.Request.ContentLength > limit)
        {
            await Problems.WriteAsync(http, StatusCodes.Status413PayloadTooLarge, "request_too_large");
            return;
        }
        var bodySize = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false }) bodySize.MaxRequestBodySize = limit;
        await next(http);
    });
}
