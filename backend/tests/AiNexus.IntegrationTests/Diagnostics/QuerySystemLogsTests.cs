using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Diagnostics;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiNexus.IntegrationTests.Diagnostics;

public sealed class QuerySystemLogsTests
{
    [Fact]
    public async Task QueryDetailExportPermissionsAreIndependentAndReadsAreAudited()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var alice = await factory.SignedInAsync();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var code = Issues.NewCode();
        var row = new DiagnosticEvent { IssueCode = code, Level = LogLevel.Error, ExceptionType = "InvalidOperationException", ExceptionDetail = "[message omitted]", Category = "=HYPERLINK(\"evil\")", At = DateTimeOffset.UtcNow.AddMinutes(-1) }; db.Add(row); await db.SaveChangesAsync();
        var range = Range(); var page = await alice.GetFromJsonAsync<DiagnosticPage>("/api/v1/admin/logs?" + range + "&issueCode=" + code); Assert.Single(page!.Events);
        var json = await (await alice.GetAsync("/api/v1/admin/logs?" + range)).Content.ReadAsStringAsync(); Assert.DoesNotContain("exceptionDetail", json); Assert.DoesNotContain("ExceptionDetail", json);
        var detail = await alice.GetFromJsonAsync<DiagnosticDetail>("/api/v1/admin/logs/" + row.LogId); Assert.Equal(row.ExceptionDetail, detail!.ExceptionDetail);
        var exported = await alice.GetAsync("/api/v1/admin/logs/export?" + range); exported.EnsureSuccessStatusCode(); Assert.Contains("'=HYPERLINK", await exported.Content.ReadAsStringAsync());
        db.RemoveRange(await db.Set<RoleGroupFeature>().Where(x => x.GroupId == "administrators" && (x.FeatureId == DiagnosticConfiguration.Detail || x.FeatureId == DiagnosticConfiguration.Export)).ToListAsync()); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/v1/admin/logs/" + row.LogId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.GetAsync("/api/v1/admin/logs/export?" + range)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync("/api/v1/admin/logs?" + range)).StatusCode);
        Assert.Contains(await db.AuditEvents.AsNoTracking().ToArrayAsync(), x => x.Action == "logs.export");
        Assert.Contains(await db.AuditEvents.AsNoTracking().ToArrayAsync(), x => x.Action == "logs.detail");
    }

    [Fact]
    public async Task CursorIsBoundToActorFiltersAndTimeWindowAndHasNoDuplicates()
    {
        await using var factory = new NexusFactory(administrators: ["alice", "bob"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var code = Issues.NewCode(); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var at = DateTimeOffset.UtcNow.AddMinutes(-1); db.AddRange(Enumerable.Range(0, 7).Select(i => new DiagnosticEvent { IssueCode = code, At = at.AddSeconds(i), Level = LogLevel.Error })); await db.SaveChangesAsync();
        var query = "/api/v1/admin/logs?" + Range() + "&issueCode=" + code + "&take=3";
        var first = await alice.GetFromJsonAsync<DiagnosticPage>(query); var second = await alice.GetFromJsonAsync<DiagnosticPage>(query + "&cursor=" + Uri.EscapeDataString(first!.NextCursor!));
        Assert.Equal(3, first.Events.Count); Assert.Equal(3, second!.Events.Count); Assert.Empty(first.Events.Select(x => x.LogId).Intersect(second.Events.Select(x => x.LogId)));
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync(query + "&cursor=" + Uri.EscapeDataString(first.NextCursor!))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync(query + "&level=Warning&cursor=" + Uri.EscapeDataString(first.NextCursor!))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync("/api/v1/admin/logs?take=100000")).StatusCode);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task CursorSortAndPageSizeAreBoundAndTimestampTiesHaveNoGaps(string direction)
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var client = await factory.SignedInAsync();
        var code = Issues.NewCode(); using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var at = DateTimeOffset.UtcNow.AddMinutes(-2);
        db.AddRange(Enumerable.Range(0, 8).Select(i => new DiagnosticEvent { IssueCode = code, At = at.AddSeconds(i / 2), Level = LogLevel.Error }));
        await db.SaveChangesAsync();
        var query = "/api/v1/admin/logs?" + Range() + "&issueCode=" + code + "&take=3&sortDirection=" + direction;
        var first = (await client.GetFromJsonAsync<DiagnosticPage>(query))!;
        var token = Uri.EscapeDataString(first.NextCursor!);
        var second = (await client.GetFromJsonAsync<DiagnosticPage>(query + "&cursor=" + token))!;
        var third = (await client.GetFromJsonAsync<DiagnosticPage>(query + "&cursor=" + Uri.EscapeDataString(second.NextCursor!)))!;
        var all = first.Events.Concat(second.Events).Concat(third.Events).ToArray();
        Assert.Equal(8, all.Length); Assert.Equal(8, all.Select(x => x.LogId).Distinct().Count()); Assert.Null(third.NextCursor);
        Assert.Equal(direction == "asc" ? all.Select(x => x.At).Order() : all.Select(x => x.At).OrderDescending(), all.Select(x => x.At));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(query.Replace("take=3", "take=4") + "&cursor=" + token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(query.Replace("sortDirection=" + direction, "sortDirection=" + (direction == "asc" ? "desc" : "asc")) + "&cursor=" + token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(query.Replace("sortDirection=" + direction, "sortDirection=invalid"))).StatusCode);
    }

    private static string Range() => "from=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O")) + "&to=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));
}
