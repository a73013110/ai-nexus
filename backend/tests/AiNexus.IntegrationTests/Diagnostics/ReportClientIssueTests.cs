using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Diagnostics;
using AiNexus.Platform.Diagnostics;
using Microsoft.AspNetCore.Http;
using static AiNexus.IntegrationTests.Support.DiagnosticIssues;

namespace AiNexus.IntegrationTests.Diagnostics;

public sealed class ReportClientIssueTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public async Task ClientReportIsUntrustedBoundedDeduplicatedAndPrivate()
    {
        await using var factory = new NexusFactory(workers: [typeof(DiagnosticWorker)], administrators: ["alice"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var body = new ClientIssueRequest("exception", new string('A', 64));
        var first = await alice.PostAsJsonAsync("/api/v1/client-issues", body); first.EnsureSuccessStatusCode(); var code = (await first.Content.ReadFromJsonAsync<ClientIssueResponse>())!.IssueCode;
        var again = await alice.PostAsJsonAsync("/api/v1/client-issues", body); Assert.Equal(code, (await again.Content.ReadFromJsonAsync<ClientIssueResponse>())!.IssueCode);
        var invalid = await alice.PostAsJsonAsync("/api/v1/client-issues", new { kind = Secret, fingerprint = Secret }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.DoesNotContain(Secret, await invalid.Content.ReadAsStringAsync());
        var entry = await WaitForIssue(factory, code); Assert.True(entry.UntrustedClient); Assert.NotNull(entry.RequestId); Assert.NotNull(entry.TraceId);
        Assert.Equal(1, await CountIssue(factory, code));
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/logs?issueCode=" + code)).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/logs?issueCode=" + code)).StatusCode);
    }
}
