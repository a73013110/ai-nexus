using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Jobs;
using AiNexus.Features.Notifications;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;

namespace AiNexus.IntegrationTests.Jobs;

public sealed class BackgroundJobWorkerTests
{
    private sealed class UnfinishedHandler : IBackgroundJobHandler
    {
        public string Kind => "unfinished-test";
        public Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => Task.FromResult(Result.Success);
        public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
        {
            var user = await execution.Database.Users.SingleAsync(x => x.Id == execution.Job.OwnerId, ct);
            user.DisplayName = "Uncheckpointed mutation";
            throw new InvalidOperationException("Synthetic failure before checkpoint.");
        }
    }

    [Fact]
    public async Task TerminalNotificationDoesNotCommitUncheckpointedHandlerChanges()
    {
        await using var factory = new NexusFactory(services: services => services.AddScoped<IBackgroundJobHandler, UnfinishedHandler>());
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(me.Id, null, Guid.NewGuid(), "unfinished-test", "未完成的修改");
            id = job.Id; await scope.ServiceProvider.GetRequiredService<NexusDbContext>().SaveChangesAsync();
        }
        await Process(factory);
        Assert.Equal(me.DisplayName, (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.DisplayName);
        Assert.Equal("failed", (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}"))!.Status);
        Assert.Equal("task.failed", Assert.Single((await client.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Items).Type);
    }
}
