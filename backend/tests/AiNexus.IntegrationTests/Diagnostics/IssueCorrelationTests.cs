using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Chat;
using AiNexus.Features.Inference;
using AiNexus.Features.Jobs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.DiagnosticIssues;

namespace AiNexus.IntegrationTests.Diagnostics;

public sealed class IssueCorrelationTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public async Task GenerationFailureProducesSafeSseNotificationAndQueryableCorrelatedIssue()
    {
        await using var factory = new NexusFactory(workers: [typeof(DiagnosticWorker)], administrators: ["alice"]); factory.Provider.Fail = true;
        using var client = await factory.SignedInAsync(); var conversation = await CreateConversation(client);
        using var response = await PostRun(client, new CreateRunRequest(conversation.Id, "test-model", Secret, null, null), Guid.NewGuid().ToString()); response.EnsureSuccessStatusCode();
        var run = (await response.Content.ReadFromJsonAsync<RunDto>())!; var terminal = await WaitForTerminal(client, run.Id); Assert.Equal("failed", terminal.Status); Assert.True(Issues.ValidCode(terminal.IssueCode));
        var stream = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events"); Assert.Contains(terminal.IssueCode!, stream); Assert.DoesNotContain("fixture failure", stream); Assert.DoesNotContain(Secret, stream);
        var notifications = await client.GetStringAsync("/api/v1/notifications"); Assert.Contains(terminal.IssueCode!, notifications); Assert.DoesNotContain("fixture failure", notifications);
        var item = await WaitForIssue(factory, terminal.IssueCode!); Assert.Equal(run.Id, item.RunId); Assert.Equal(run.Id, item.OperationId); Assert.NotNull(item.TraceId); Assert.DoesNotContain("fixture failure", item.ExceptionDetail ?? "");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var persisted = await db.Runs.FindAsync(run.Id); Assert.Equal(persisted!.TraceId, item.TraceId);
        Assert.True(await db.Set<DiagnosticEvent>().AnyAsync(x => x.TraceId == item.TraceId && x.RequestId != null));
        Assert.Equal(1, await CountIssue(factory, terminal.IssueCode!));
    }

    [Fact]
    public async Task BackgroundRetryKeepsTraceAndJobButGetsDistinctIssueAndAttempt()
    {
        await using var factory = new NexusFactory(workers: [typeof(DiagnosticWorker)], administrators: ["alice"], services: services => services.AddScoped<IBackgroundJobHandler, FailingJob>());
        using var client = await factory.SignedInAsync(); var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; Guid jobId;
        using (var scope = factory.Services.CreateScope()) { using var trace = DiagnosticTrace.Start("test.enqueue"); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(owner, null, Guid.NewGuid(), "diagnostic-fixture", "safe task"); jobId = job.Id; await db.SaveChangesAsync(); }
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services); Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        var first = await client.GetFromJsonAsync<JobDto>("/api/v1/jobs/" + jobId); Assert.True(Issues.ValidCode(first!.IssueCode)); Assert.DoesNotContain(Secret, first.ErrorMessage!);
        (await client.PostAsync("/api/v1/jobs/" + jobId + "/retry", null)).EnsureSuccessStatusCode(); Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        var second = await client.GetFromJsonAsync<JobDto>("/api/v1/jobs/" + jobId); Assert.NotEqual(first.IssueCode, second!.IssueCode);
        var a = await WaitForIssue(factory, first.IssueCode!); var b = await WaitForIssue(factory, second.IssueCode!); Assert.Equal(a.TraceId, b.TraceId); Assert.NotEqual(a.SpanId, b.SpanId); Assert.Equal(jobId, a.JobId); Assert.Equal(1, a.Attempt); Assert.Equal(2, b.Attempt);
    }

    public sealed class FailingJob : IBackgroundJobHandler
    {
        public string Kind => "diagnostic-fixture";
        public Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct) => throw new ExternalServiceException(Error.Unavailable("fixture_failed"), Secret);
        public Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => Task.FromResult(Result.Success);
    }

    [Fact]
    public async Task ApiFrameworkFailureDoesNotReflectClientDataAndUsesServerGeneratedCorrelation()
    {
        await using var factory = new NexusFactory(workers: [typeof(DiagnosticWorker)]); using var client = await factory.SignedInAsync();
        client.DefaultRequestHeaders.Add("traceparent", "00-" + new string('a', 32) + "-" + new string('b', 16) + "-01");
        client.DefaultRequestHeaders.Add("X-Issue-Code", "NX-" + new string('A', 32));
        var response = await client.PostAsync("/api/v1/conversations", new StringContent("{malformed " + Secret, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); var text = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(Secret, text);
        using var parsed = JsonDocument.Parse(text); var code = parsed.RootElement.GetProperty("issueCode").GetString()!; Assert.True(Issues.ValidCode(code)); Assert.NotEqual("NX-" + new string('A', 32), code);
        var item = await WaitForIssue(factory, code); Assert.NotEqual(new string('a', 32), item.TraceId); Assert.Equal(LogLevel.Information, item.Level);
    }
}
