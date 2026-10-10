using System.Text.Json;
using AiNexus.Features.Diagnostics;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.UnitTests.Diagnostics;

public sealed class DiagnosticQueryTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public void SafeMessagesRenderHistoricalTemplatesWithoutExposingDetailOnlyProperties()
    {
        var item = new DiagnosticEvent {
            MessageTemplate = "HTTP {StatusCode} in {DurationMs:0.###} ms; client {ClientAddress}; count {Count}; {{literal}}.",
            StatusCode = 200, DurationMs = 12.626123,
            PropertiesJson = JsonSerializer.Serialize(new { ClientAddress = "203.0.113.8", Count = 3, Password = Secret })
        };
        var summary = DiagnosticQuery.Describe(item);
        Assert.Equal("HTTP 200 in 12.626 ms; client [omitted]; count [omitted]; {literal}.", summary.Message);
        Assert.DoesNotContain("203.0.113.8", summary.Message);
        var detail = DiagnosticQuery.Describe(item, detail: true);
        Assert.Contains("client 203.0.113.8; count 3", detail.Message); Assert.DoesNotContain(Secret, detail.Message);
        item.MessageTemplate = "Count {Count:invalid}"; item.PropertiesJson = "{";
        Assert.Equal("Count [omitted]", DiagnosticMessage.Render(item, includeProperties: true));
        item.MessageTemplate = "Duration {DurationMs:N999999999}";
        Assert.Equal("Duration [invalid format]", DiagnosticMessage.Render(item));
    }
}
