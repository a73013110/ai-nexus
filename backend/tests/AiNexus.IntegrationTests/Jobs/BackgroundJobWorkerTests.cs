using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;

namespace AiNexus.IntegrationTests.Jobs;

public sealed class BackgroundJobWorkerTests
{
    private sealed class UnfinishedHandler : IBackgroundJobHandler
    {
        public Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => Task.FromResult(Result.Success);
        public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
        {
            var user = await execution.Database.Users.SingleAsync(x => x.Id == execution.Job.OwnerId, ct);
            user.DisplayName = "Uncheckpointed mutation";
            throw new InvalidOperationException("Synthetic failure before checkpoint.");
        }
    }

    private sealed class CountingHandler : IBackgroundJobHandler
    {
        public static int Calls;
        public Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => Task.FromResult(Result.Success);
        public Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(Result.Success);
        }
    }

    /// <summary>外部服務暫時異常時使用者會反覆重試；累計嘗試再多，明確的重試仍要受理並執行。</summary>
    [Fact]
    public async Task UserRetryIsNotLimitedByPastAttempts()
    {
        await using var factory = new AppFactory(services: services => services.AddBackgroundJobHandler<CountingHandler>("counting-retry"));
        using var client = await factory.SignedInAsync();
        var id = await EnqueueAsync(factory, "counting-retry", (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);
        await UpdateAsync(factory, id, p => p
            .SetProperty(x => x.Status, "failed")
            .SetProperty(x => x.ActiveKey, (string?)null)
            .SetProperty(x => x.Attempt, 9));
        CountingHandler.Calls = 0;

        (await client.PostAsync($"/api/v1/jobs/{id}/retry", null)).EnsureSuccessStatusCode();
        await Process(factory);

        var job = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}"))!;
        Assert.Equal(("completed", 10), (job.Status, job.Attempt));
        Assert.Equal(1, CountingHandler.Calls);
    }

    /// <summary>租約到期的自動接手有上限：讓程序中止的工作不能無限循環，超過就不再執行而直接失敗。</summary>
    [Fact]
    public async Task ExpiredLeaseIsNotRecoveredPastTheAttemptLimit()
    {
        await using var factory = new AppFactory(services: services => services.AddBackgroundJobHandler<CountingHandler>("counting-recovery"));
        using var client = await factory.SignedInAsync();
        var id = await EnqueueAsync(factory, "counting-recovery", (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);
        await UpdateAsync(factory, id, p => p
            .SetProperty(x => x.Status, "running")
            .SetProperty(x => x.LeaseToken, Guid.NewGuid())
            .SetProperty(x => x.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1))
            .SetProperty(x => x.Attempt, 6));
        CountingHandler.Calls = 0;

        await Process(factory);

        var job = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}"))!;
        Assert.Equal(("failed", "job_recovery_limit"), (job.Status, job.ErrorCode));
        Assert.Equal(0, CountingHandler.Calls);
        (await client.PostAsync($"/api/v1/jobs/{id}/retry", null)).EnsureSuccessStatusCode();
        await Process(factory);
        Assert.Equal("completed", (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}"))!.Status);
    }

    private static async Task<Guid> EnqueueAsync(AppFactory factory, string kind, Guid owner)
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(owner, null, Guid.NewGuid(), kind, "重試測試");
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        return job.Id;
    }

    private static async Task UpdateAsync(AppFactory factory, Guid id, Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<BackgroundJob>> set)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().BackgroundJobs.Where(x => x.Id == id).ExecuteUpdateAsync(set);
    }

    [Fact]
    public async Task TerminalNotificationDoesNotCommitUncheckpointedHandlerChanges()
    {
        await using var factory = new AppFactory(services: services => services.AddBackgroundJobHandler<UnfinishedHandler>("unfinished-test"));
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(me.Id, null, Guid.NewGuid(), "unfinished-test", "未完成的修改");
            id = job.Id; await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        await Process(factory);
        Assert.Equal(me.DisplayName, (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.DisplayName);
        Assert.Equal("failed", (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{id}"))!.Status);
        Assert.Equal("task.failed", Assert.Single((await client.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Items).Type);
    }
}
