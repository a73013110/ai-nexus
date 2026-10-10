using System.Text.Json;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.UnitTests.Platform;

public sealed class DiagnosticRedactorTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public void BoundaryOmitsArbitraryObjectsAndContentAndScrubsInjection()
    {
        var values = DiagnosticRedactor.Properties(new Dictionary<string, object?> {
            ["Password"] = Secret, ["Authorization"] = "Bearer " + Secret, ["Prompt"] = Secret, ["Query"] = Secret,
            ["Count"] = new DangerousObject(), ["Reason"] = "password=" + Secret + " https://internal.test?q=" + Secret + " C:\\private\\secret.txt\r\nforged record",
            ["SqlNumber"] = 30053
        });
        var json = DiagnosticRedactor.Json(values); Assert.DoesNotContain(Secret, json); Assert.DoesNotContain("internal.test", json);
        Assert.DoesNotContain("private\\", json); Assert.DoesNotContain("\r", json); Assert.Contains("30053", json); Assert.Contains("OBJECT OMITTED", json);
        var detail = DiagnosticRedactor.Exception(new InvalidOperationException(Secret, new HttpRequestException("Bearer " + Secret)));
        Assert.DoesNotContain(Secret, detail); Assert.Contains("InvalidOperationException", detail); Assert.Contains("HttpRequestException", detail);
        Assert.True(DiagnosticRedactor.Text(new string('a', 100000)).Length <= 512);
    }

    private sealed class DangerousObject { public override string ToString() => throw new Exception("Arbitrary object must never be rendered"); }

    [Fact]
    public void RecoveryBoundsAllMetadataAndFiltersNestedProperties()
    {
        var item = new DiagnosticEvent { Category = "password=" + Secret, Instance = "https://internal.test/secret", TraceId = "spoof", IssueCode = "user-code",
            PropertiesJson = JsonSerializer.Serialize(new { Token = Secret, Prompt = Secret, Reason = "Bearer " + Secret, Count = new { secret = Secret }, SqlNumber = 30053 }) };
        DiagnosticRedactor.Normalize(item); Assert.DoesNotContain(Secret, JsonSerializer.Serialize(item)); Assert.DoesNotContain("internal.test", item.Instance);
        Assert.Null(item.TraceId); Assert.Null(item.IssueCode); Assert.Contains("30053", item.PropertiesJson);
    }
}
