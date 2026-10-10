using System.Text;
using System.Text.Json;
using AiNexus.Platform.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Platform;

public sealed class OtlpExportTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public async Task OtlpWirePreservesOriginalTraceSpanTimestampAndMaskedMetadata()
    {
        var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var collector = builder.Build(); var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        collector.MapPost("/v1/logs", async (HttpContext http) => { using var bytes = new MemoryStream(); await http.Request.Body.CopyToAsync(bytes); received.TrySetResult(bytes.ToArray()); http.Response.StatusCode = 200; http.Response.ContentType = "application/x-protobuf"; });
        await collector.StartAsync();
        var endpoint = collector.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var health = new DiagnosticHealth(); using var exporter = new DiagnosticExporter(Options.Create(new DiagnosticOptions { OtlpEnabled = true, OtlpEndpoint = endpoint }), health);
        var at = DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture);
        var row = new DiagnosticEvent { IssueCode = Issues.NewCode(), TraceId = "1234567890abcdef1234567890abcdef", SpanId = "1234567890abcdef", At = at, Level = LogLevel.Error, MessageTemplate = "Controlled OTLP fixture.",
            PropertiesJson = JsonSerializer.Serialize(new { Password = Secret, SqlNumber = 30053 }), ExceptionDetail = "password=" + Secret };
        exporter.Export([row]); var body = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(row.IssueCode!, Encoding.UTF8.GetString(body)); Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(body));
        Assert.True(body.AsSpan().IndexOf(Convert.FromHexString(row.TraceId)) >= 0); Assert.True(body.AsSpan().IndexOf(Convert.FromHexString(row.SpanId)) >= 0);
        Assert.True(body.AsSpan().IndexOf(BitConverter.GetBytes((ulong)at.ToUnixTimeMilliseconds() * 1_000_000)) >= 0, "OTLP time_unix_nano must represent the original event, not export time.");
        await collector.StopAsync();
    }
}
