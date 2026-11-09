using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Features.Admin.Scheduling.Management;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Jobs.Queue;
using AiNexus.Platform.Data.Tables;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Scheduling;

/// <summary>測試任務的行為；每個測試換掉 <see cref="Behavior"/>，並記下每一輪收到的 context。</summary>
public sealed class Probe
{
    public Func<ScheduledTaskContext, CancellationToken, Task<TaskOutcome>> Behavior { get; set; } = (_, _) => Task.FromResult(TaskOutcome.Done());
    public string? Unavailable { get; set; }
    public List<ScheduledTaskContext> Contexts { get; } = [];
}

public sealed record ProbeParameters(int Value);

public sealed class ProbeTask(Probe probe) : IScheduledTask
{
    public const string Key = "test.probe";
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    public static ScheduledTaskDefinition Definition { get; } = new(Key, "測試任務", "測試", "整合測試用的任務。")
    {
        Unavailable = services => services.GetRequiredService<Probe>().Unavailable,
    };

    public Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        probe.Contexts.Add(context);
        return probe.Behavior(context, ct);
    }
}

public sealed class StartupProbeTask : IScheduledTask
{
    public const string Key = "test.startup";

    public static ScheduledTaskDefinition Definition { get; } = new(Key, "測試啟動任務", "測試", "整合測試用的啟動任務。")
    {
        FixedSchedule = Schedule.AtStartup(TimeSpan.FromMinutes(1)),
    };

    public Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct) => Task.FromResult(TaskOutcome.Done());
}

public sealed class SchedulingTests
{
    private static readonly Error ProbeFailed = Error.Unavailable("test_probe_failed");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Harness(AppFactory Factory, FakeTimeProvider Clock, Probe Probe) : IAsyncDisposable
    {
        public ScheduledTaskRunner Runner => Factory.Services.GetRequiredService<ScheduledTaskRunner>();

        public async Task<ScheduledTaskClaim?> ClaimAsync(string key = ProbeTask.Key, ScheduledTaskRunner? runner = null)
            => (await (runner ?? Runner).ClaimDueAsync(100, Ct)).SingleOrDefault(x => x.Plan.Key == key);

        public async Task<ScheduledTask> TaskAsync(string key = ProbeTask.Key)
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ScheduledTasks.AsNoTracking().SingleAsync(x => x.Key == key, Ct);
        }

        public async Task<List<ScheduledTaskRun>> RunsAsync(string key = ProbeTask.Key)
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ScheduledTaskRuns.AsNoTracking()
                .Where(x => x.TaskKey == key).OrderBy(x => x.StartedAt).ThenBy(x => x.Id).ToListAsync(Ct);
        }

        public async Task<Result> TasksAsync(Func<ScheduledTasks, Task<Result>> call)
        {
            using var scope = Factory.Services.CreateScope();
            return await call(scope.ServiceProvider.GetRequiredService<ScheduledTasks>());
        }

        /// <summary>認領並執行一輪，回傳這一輪的紀錄。</summary>
        public async Task<ScheduledTaskRun> RunOnceAsync()
        {
            var claim = await ClaimAsync();
            Assert.NotNull(claim);
            await Runner.ExecuteAsync(claim, CancellationToken.None);
            return (await RunsAsync())[^1];
        }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private static async Task<Harness> StartAsync(string[]? administrators = null, Action<SchedulingOptions>? scheduling = null)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var probe = new Probe();
        var factory = new AppFactory(clock: clock, administrators: administrators, services: services =>
        {
            services.AddSingleton(probe);
            services.AddScheduledTask<ProbeTask>();
            services.AddScheduledTask<StartupProbeTask>();
            // 預設不執行（開發機共用資料庫）；這裡的測試扮演執行排程的程序。
            services.PostConfigure<SchedulingOptions>(options =>
            {
                options.Execute = true;
                options.Tasks[ProbeTask.Key] = new() { Schedule = ProbeTask.Interval.ToString("c", CultureInfo.InvariantCulture), Timeout = ProbeTask.Timeout };
                options.Tasks[StartupProbeTask.Key] = new() { Timeout = ProbeTask.Timeout };
                scheduling?.Invoke(options);
            });
        });
        var harness = new Harness(factory, clock, probe);
        await harness.Runner.InitializeAsync(Ct);
        return harness;
    }

    [Fact]
    public async Task AScheduledRunIsClaimedOnceRecordedAndScheduledAfterItFinishes()
    {
        await using var app = await StartAsync();
        app.Probe.Behavior = (context, _) =>
        {
            context.Report("掃描", 3, 3);
            return Task.FromResult(TaskOutcome.Done("處理 3 筆"));
        };

        var claim = await app.ClaimAsync();
        Assert.NotNull(claim);
        Assert.Equal(ScheduledTaskTrigger.Scheduled, claim.Trigger);
        // 租約有效時不會被第二次認領（另一個程序也一樣）。
        Assert.Null(await app.ClaimAsync());

        await app.Runner.ExecuteAsync(claim, CancellationToken.None);
        var run = Assert.Single(await app.RunsAsync());
        Assert.Equal((ScheduledTaskRunStatuses.Succeeded, ScheduledTaskTriggers.Scheduled, "處理 3 筆"), (run.Status, run.TriggerKind, run.Summary));
        Assert.Equal(("掃描", 3, 3), (run.Stage, run.CompletedUnits, run.TotalUnits));
        var task = await app.TaskAsync();
        Assert.Null(task.LeaseToken);
        Assert.Equal(app.Clock.GetUtcNow() + ProbeTask.Interval, task.NextRunAt);

        Assert.Null(await app.ClaimAsync());
        app.Clock.Advance(ProbeTask.Interval);
        Assert.NotNull(await app.ClaimAsync());
    }

    [Fact]
    public async Task IdleRunsAndUnavailableTasksAreHandledWithoutRunning()
    {
        await using var app = await StartAsync();
        app.Probe.Behavior = (_, _) => Task.FromResult(TaskOutcome.Count(0, total => $"{total} 筆"));
        Assert.Equal(ScheduledTaskRunStatuses.Idle, (await app.RunOnceAsync()).Status);

        app.Clock.Advance(ProbeTask.Interval);
        app.Probe.Unavailable = "功能沒有啟用";
        Assert.Null(await app.ClaimAsync());
        Assert.Equal(SchedulingErrors.TaskUnavailable, (await app.TasksAsync(x => x.RequestRunAsync(ProbeTask.Key, null, null, Ct))).Error);
    }

    [Fact]
    public async Task AManualRunCarriesItsParametersAndStillRunsWhilePaused()
    {
        await using var app = await StartAsync(administrators: ["alice"]);
        using var admin = await app.Factory.SignedInAsync();
        var me = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me", Ct))!;
        await app.RunOnceAsync();

        (await admin.PostAsync($"/api/v1/scheduled-tasks/{ProbeTask.Key}/pause", null, Ct)).EnsureSuccessStatusCode();
        app.Clock.Advance(ProbeTask.Interval);
        // 暫停只停排程。
        Assert.Null(await app.ClaimAsync());

        Assert.True((await app.TasksAsync(x => x.RequestRunAsync(ProbeTask.Key, me.Id, new ProbeParameters(7), Ct))).IsSuccess);
        var claim = await app.ClaimAsync();
        Assert.NotNull(claim);
        Assert.Equal(ScheduledTaskTrigger.Manual, claim.Trigger);
        await app.Runner.ExecuteAsync(claim, CancellationToken.None);
        var context = app.Probe.Contexts[^1];
        Assert.Equal((me.Id, 7), (context.RequestedBy, context.Parameters<ProbeParameters>()!.Value));
        var run = (await app.RunsAsync())[^1];
        Assert.Equal((ScheduledTaskTriggers.Manual, me.Id), (run.TriggerKind, run.RequestedById));
        // 認領時清掉要求：同一個要求只跑一次。
        Assert.Null(await app.ClaimAsync());

        (await admin.PostAsync($"/api/v1/scheduled-tasks/{ProbeTask.Key}/resume", null, Ct)).EnsureSuccessStatusCode();
        Assert.False((await app.TaskAsync()).IsPaused);
    }

    [Fact]
    public async Task RepeatedFailuresNotifyAdministratorsAndARecoveryWithdrawsTheNotice()
    {
        await using var app = await StartAsync(administrators: ["alice"]);
        using var admin = await app.Factory.SignedInAsync();
        app.Probe.Behavior = (_, _) => Task.FromResult(TaskOutcome.Failed(ProbeFailed));

        for (var i = 0; i < ProbeTask.Definition.NotifyAfterFailures; i++)
        {
            var run = await app.RunOnceAsync();
            Assert.Equal((ScheduledTaskRunStatuses.Failed, "test_probe_failed"), (run.Status, run.ErrorCode));
            Assert.NotNull(run.IssueCode);
            Assert.Contains(run.IssueCode, run.ErrorMessage);
            // 失敗後依排程（或退避）重試；推進時間讓下一輪到期。
            app.Clock.Advance(ProbeTask.Interval);
        }
        Assert.Equal(ProbeTask.Definition.NotifyAfterFailures, (await app.TaskAsync()).ConsecutiveFailures);
        Assert.Equal(1, await UnreadAlertsAsync());

        app.Probe.Behavior = (_, _) => throw new InvalidOperationException("boom");
        var crashed = await app.RunOnceAsync();
        Assert.Equal((ScheduledTaskRunStatuses.Failed, SchedulingErrors.TaskFailed.Code), (crashed.Status, crashed.ErrorCode));
        Assert.DoesNotContain("boom", crashed.ErrorMessage);
        // 門檻只在剛達到時通知一次。
        Assert.Equal(1, await UnreadAlertsAsync());

        app.Clock.Advance(ProbeTask.Interval);
        app.Probe.Behavior = (_, _) => Task.FromResult(TaskOutcome.Done());
        await app.RunOnceAsync();
        Assert.Equal(0, (await app.TaskAsync()).ConsecutiveFailures);
        Assert.Equal(0, await UnreadAlertsAsync());

        async Task<int> UnreadAlertsAsync()
        {
            using var scope = app.Factory.Services.CreateScope();
            var target = FailureAlerts.TargetOf(ProbeTask.Key);
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
                .CountAsync(x => x.TargetKind == FailureAlerts.TargetKind && x.TargetId == target && x.ReadAt == null, Ct);
        }
    }

    [Fact]
    public async Task StoppingEndsTheRunAsCancelledAndKeepsTheSchedule()
    {
        await using var app = await StartAsync();
        var started = new TaskCompletionSource();
        app.Probe.Behavior = async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return TaskOutcome.Done();
        };
        var claim = await app.ClaimAsync();
        Assert.NotNull(claim);
        var execution = app.Runner.ExecuteAsync(claim, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Assert.True((await app.TasksAsync(x => x.CancelAsync(ProbeTask.Key, Ct))).IsSuccess);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal(ScheduledTaskRunStatuses.Cancelled, (await app.RunsAsync())[^1].Status);
        var task = await app.TaskAsync();
        Assert.Equal((0, false, (Guid?)null), (task.ConsecutiveFailures, task.CancelRequested, task.LeaseToken));
        Assert.Equal(app.Clock.GetUtcNow() + ProbeTask.Interval, task.NextRunAt);
    }

    [Fact]
    public async Task ARunPastItsTimeoutFailsAndAShutdownLeavesItDueForTheNextProcess()
    {
        await using var app = await StartAsync();
        var started = new TaskCompletionSource();
        app.Probe.Behavior = async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return TaskOutcome.Done();
        };

        var claim = await app.ClaimAsync();
        Assert.NotNull(claim);
        var execution = app.Runner.ExecuteAsync(claim, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        app.Clock.Advance(ProbeTask.Timeout);
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var timedOut = (await app.RunsAsync())[^1];
        Assert.Equal((ScheduledTaskRunStatuses.Failed, SchedulingErrors.TaskTimedOut.Code), (timedOut.Status, timedOut.ErrorCode));

        app.Clock.Advance(ProbeTask.Interval);
        started = new TaskCompletionSource();
        claim = await app.ClaimAsync();
        Assert.NotNull(claim);
        using var stop = new CancellationTokenSource();
        execution = app.Runner.ExecuteAsync(claim, stop.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await stop.CancelAsync();
        await execution.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal(ScheduledTaskRunStatuses.Interrupted, (await app.RunsAsync())[^1].Status);
        var task = await app.TaskAsync();
        Assert.True(task.NextRunAt <= app.Clock.GetUtcNow());
        Assert.NotNull(await app.ClaimAsync());
    }

    [Fact]
    public async Task AnExpiredLeaseIsTakenOverAndItsRunMarkedInterrupted()
    {
        await using var app = await StartAsync();
        var abandoned = await app.ClaimAsync();
        Assert.NotNull(abandoned);
        // 模擬程序在執行中死掉：紀錄留在 running、租約沒有釋放。
        using (var scope = app.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ScheduledTaskRuns.Add(new ScheduledTaskRun
            {
                TaskKey = ProbeTask.Key, TriggerKind = ScheduledTaskTriggers.Scheduled, Host = "gone", StartedAt = app.Clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(Ct);
        }

        Assert.Null(await app.ClaimAsync());
        // 租約 60 秒。
        app.Clock.Advance(TimeSpan.FromSeconds(61));
        await app.RunOnceAsync();
        var runs = await app.RunsAsync();
        Assert.Equal([ScheduledTaskRunStatuses.Interrupted, ScheduledTaskRunStatuses.Succeeded], runs.Select(x => x.Status));
    }

    [Fact]
    public async Task AStartupTaskRunsOnlyInTheProcessThatStartedIt()
    {
        await using var app = await StartAsync();
        // 另一個（較舊的）程序：沒有在這次啟動時初始化，不能認領新程序排上的啟動任務。
        var other = ActivatorUtilities.CreateInstance<ScheduledTaskRunner>(app.Factory.Services);
        Assert.Null(await app.ClaimAsync(StartupProbeTask.Key, other));

        var claim = await app.ClaimAsync(StartupProbeTask.Key);
        Assert.NotNull(claim);
        Assert.Equal(ScheduledTaskTrigger.Startup, claim.Trigger);
        await app.Runner.ExecuteAsync(claim, CancellationToken.None);
        Assert.Null((await app.TaskAsync(StartupProbeTask.Key)).NextRunAt);
    }

    [Fact]
    public async Task TheBoardRunsAndQueueAreAvailableToAdministrators()
    {
        await using var app = await StartAsync(administrators: ["alice"]);
        using var admin = await app.Factory.SignedInAsync();
        app.Probe.Behavior = (_, _) => Task.FromResult(TaskOutcome.Done("處理 1 筆"));
        await app.RunOnceAsync();

        var board = (await admin.GetFromJsonAsync<ScheduledTaskBoardDto>("/api/v1/scheduled-tasks", Ct))!;
        var probe = Assert.Single(board.Tasks, x => x.Key == ProbeTask.Key);
        Assert.Equal(("interval", 600, "處理 1 筆"), (probe.ScheduleKind, probe.IntervalSeconds, probe.LastRun?.Summary));
        Assert.Equal("處理 1 筆", Assert.Single(probe.TimelineRuns).Summary);
        Assert.Empty(probe.PlannedRuns);
        Assert.Null(probe.CurrentRun);
        Assert.Equal(TimeSpan.FromHours(24), board.TimelineTo - board.TimelineFrom);
        Assert.InRange(app.Clock.GetUtcNow() - board.TimelineFrom, TimeSpan.FromHours(4), TimeSpan.FromHours(5));

        (await admin.PostAsync($"/api/v1/scheduled-tasks/{ProbeTask.Key}/run", null, Ct)).EnsureSuccessStatusCode();
        Assert.NotNull((await app.TaskAsync()).RunRequestedAt);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/v1/scheduled-tasks/test.missing/run", null, Ct)).StatusCode);
        var runs = (await admin.GetFromJsonAsync<TablePage<ScheduledTaskRunDto>>($"/api/v1/scheduled-tasks/{ProbeTask.Key}/runs", Ct))!;
        Assert.Equal(1, runs.Total);

        Guid jobId;
        var me = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me", Ct))!;
        using (var scope = app.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = new BackgroundJob { OwnerId = me.Id, Kind = "evaluation", Label = "題庫評測", Status = RunStates.Queued, CreatedAt = app.Clock.GetUtcNow(), UpdatedAt = app.Clock.GetUtcNow() };
            db.Add(job);
            await db.SaveChangesAsync(Ct);
            jobId = job.Id;
        }
        var queue = (await admin.GetFromJsonAsync<TablePage<QueuedJobDto>>("/api/v1/background-jobs", Ct))!;
        var queued = Assert.Single(queue.Items);
        Assert.Equal(("題庫評測", me.DisplayName), (queued.Label, queued.OwnerName));
        (await admin.PostAsync($"/api/v1/background-jobs/{jobId}/cancel", null, Ct)).EnsureSuccessStatusCode();
        queue = (await admin.GetFromJsonAsync<TablePage<QueuedJobDto>>("/api/v1/background-jobs", Ct))!;
        Assert.True(queue.Items[0].CancelRequested || queue.Items[0].Status == RunStates.Cancelled);
    }

    [Fact]
    public async Task ExecuteOnlyLimitsThisProcessToTheListedTasks()
    {
        await using var app = await StartAsync(administrators: ["alice"], scheduling: options => options.ExecuteOnly = [StartupProbeTask.Key]);
        // 一次認領所有到期任務：應用程式本身註冊的任務也到期了，只有列出的那個會被認領。
        Assert.Equal([StartupProbeTask.Key], (await app.Runner.ClaimDueAsync(100, Ct)).Select(x => x.Plan.Key));
        // 手動要求照樣記下，留給執行它的程序。
        Assert.True((await app.TasksAsync(x => x.RequestRunAsync(ProbeTask.Key, null, null, Ct))).IsSuccess);
        Assert.Empty(await app.Runner.ClaimDueAsync(100, Ct));

        using var admin = await app.Factory.SignedInAsync();
        var board = (await admin.GetFromJsonAsync<ScheduledTaskBoardDto>("/api/v1/scheduled-tasks", Ct))!;
        Assert.False(board.ExecutesHere);
    }

    [Fact]
    public async Task OnlyAdministratorsSeeTheBoard()
    {
        await using var app = await StartAsync();
        using var user = await app.Factory.SignedInAsync("bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/scheduled-tasks", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/background-jobs", Ct)).StatusCode);
    }
}
