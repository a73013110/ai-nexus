using System.Net;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Diagnostics;
using AiNexus.Platform.Diagnostics;
using AiNexus.UnitTests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Platform;

public sealed class DiagnosticRequestMiddlewareTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public async Task StartedSseFailureUsesSafeEventAndOneQueryableIssue()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions()); var buffer = new DiagnosticBuffer(options, health);
        using var loggerProvider = new DiagnosticLoggerProvider(buffer, options, new EnvironmentFixture());
        using var services = new ServiceCollection().AddLogging(x => x.AddProvider(loggerProvider)).AddSingleton<Issues>().BuildServiceProvider();
        using var body = new MemoryStream(); var http = new DefaultHttpContext { RequestServices = services };
        http.Features.Set<IHttpResponseFeature>(new StartedSseResponse()); http.Response.Body = body; http.Response.ContentType = "text/event-stream";
        var failure = new HttpRequestException("Bearer " + Secret + " https://private.test/provider");
        var boundary = new DiagnosticRequestMiddleware(_ => throw failure);
        await boundary.InvokeAsync(http, services.GetRequiredService<Issues>(), services.GetRequiredService<ILogger<DiagnosticRequestMiddleware>>());
        var response = Encoding.UTF8.GetString(body.ToArray()); Assert.Contains("event: error", response); Assert.DoesNotContain(Secret, response); Assert.DoesNotContain("private.test", response);
        var issue = services.GetRequiredService<Issues>().Report(failure, "service_unavailable");
        Assert.Contains(issue, response); Assert.Single(buffer.Drain(), x => x.IssueCode == issue);
    }

    private sealed class StartedSseResponse : HttpResponseFeature { public override bool HasStarted => true; }

    [Fact]
    public async Task RequestContextUsesConnectionAddressMasksHeadersAndRecordsCancellationOnce()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions());
        var buffer = new DiagnosticBuffer(options, health); using var provider = new DiagnosticLoggerProvider(buffer, options, new EnvironmentFixture());
        using var services = new ServiceCollection().AddLogging(x => x.AddProvider(provider)).AddSingleton<Issues>().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.8");
        http.Request.Headers["X-Forwarded-For"] = "198.51.100.99";
        http.Request.Headers.UserAgent = "FixtureBrowser/1 token=" + Secret;
        http.Request.Headers.Cookie = "Session=" + Secret;
        http.Request.Protocol = "HTTP/2"; http.Request.Scheme = "https";
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); http.RequestAborted = cancelled.Token;
        await new DiagnosticRequestMiddleware(_ => throw new OperationCanceledException(cancelled.Token))
            .InvokeAsync(http, services.GetRequiredService<Issues>(), services.GetRequiredService<ILogger<DiagnosticRequestMiddleware>>());
        var row = Assert.Single(buffer.Drain());
        using var properties = JsonDocument.Parse(row.PropertiesJson);
        Assert.Equal("203.0.113.8", properties.RootElement.GetProperty("ClientAddress").GetString());
        Assert.Equal("cancelled", properties.RootElement.GetProperty("RequestOutcome").GetString());
        Assert.True(properties.RootElement.GetProperty("RequestAborted").GetBoolean());
        Assert.Equal("HTTP/2", properties.RootElement.GetProperty("RequestProtocol").GetString());
        Assert.DoesNotContain(Secret, row.PropertiesJson); Assert.DoesNotContain("198.51.100.99", row.PropertiesJson);
        Assert.DoesNotContain("{StatusCode}", DiagnosticQuery.Describe(row).Message);
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(403, true)]
    public async Task DiagnosticReadMetadataSuppressesOnlySuccessfulRequestNoise(int status, bool logged)
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions());
        var buffer = new DiagnosticBuffer(options, health); using var provider = new DiagnosticLoggerProvider(buffer, options, new EnvironmentFixture());
        using var services = new ServiceCollection().AddLogging(x => x.AddProvider(provider)).AddSingleton<Issues>().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new SuppressSuccessfulRequestLog()), "diagnostic-read"));
        await new DiagnosticRequestMiddleware(context => { context.Response.StatusCode = status; context.Response.ContentLength = 1; return Task.CompletedTask; })
            .InvokeAsync(http, services.GetRequiredService<Issues>(), services.GetRequiredService<ILogger<DiagnosticRequestMiddleware>>());
        Assert.Equal(logged, buffer.Drain().Any(x => x.EventId == DiagnosticEvents.Request));
    }
}
