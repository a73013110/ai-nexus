using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Platform.Data.Sql;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.MailFixtures;

namespace AiNexus.IntegrationTests.Mail;

public sealed class MailDispatcherTests
{
    [Fact]
    public async Task SentMessagesRecordTheRelayResponseAndSensitiveContentIsDeleted()
    {
        await using var factory = new AppFactory(services: services => services.PostConfigure<MailOptions>(x => x.MaxConcurrency = 1));
        var greeting = await EnqueueAsync(factory, new Greeting(), "小陳", "chen@company.com");
        var code = await EnqueueAsync(factory, new Code(), "654321", "chen@company.com");
        Assert.Equal(2, await Dispatcher(factory).DispatchAsync(CancellationToken.None));

        var sent = factory.Mail.Sent.ToList();
        // 安全信（Priority=Security）先寄。
        Assert.Equal([code, greeting], sent.Select(x => x.Id));
        var mime = sent[1].Message;
        Assert.Equal($"{greeting:N}@fixture.test", mime.MessageId);
        Assert.Equal("auto-generated", mime.Headers["Auto-Submitted"]);
        Assert.Contains("這是一封測試信。", mime.TextBody, StringComparison.Ordinal);
        Assert.Contains("654321", sent[0].Message.TextBody, StringComparison.Ordinal);

        var message = await MessageAsync(factory, greeting);
        Assert.Equal((MailStates.Sent, 1, MailTransports.Smtp, (Guid?)null), (message.Status, message.Attempts, message.Transport, message.LeaseToken));
        Assert.NotNull(message.SentAt);
        // 回應裡的 email 寫入前遮罩。
        Assert.StartsWith("250 2.0.0 OK", message.ProviderResponse, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture@example.test", message.ProviderResponse, StringComparison.Ordinal);
        Assert.Equal(MailAttemptOutcomes.Sent, Assert.Single(await AttemptsAsync(factory, greeting)).Outcome);
        Assert.NotNull(await ContentAsync(factory, greeting));
        Assert.Null(await ContentAsync(factory, code));
        Assert.NotNull((await MessageAsync(factory, code)).ContentPurgedAt);
        Assert.Equal(0, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentDispatchersSendEachMessageOnce()
    {
        await using var factory = new AppFactory(services: services => services.PostConfigure<MailOptions>(x => x.MaxPerMinute = 1000));
        for (var i = 0; i < 30; i++) await EnqueueAsync(factory, new Greeting(), $"#{i}", $"user{i}@company.com");
        var claimed = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Dispatcher(factory).DispatchAsync(CancellationToken.None)));
        while (await Dispatcher(factory).DispatchAsync(CancellationToken.None) > 0) { }
        Assert.Equal(30, factory.Mail.Sent.Count);
        Assert.Equal(30, factory.Mail.Sent.Select(x => x.Id).Distinct().Count());
        Assert.True(claimed.Sum() <= 30);
    }

    [Fact]
    public async Task TemporaryFailuresBackOffAndPermanentFailuresStop()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock);
        var busy = await EnqueueAsync(factory, new Greeting(), "busy", "busy@company.com");
        var unknown = await EnqueueAsync(factory, new Greeting(), "unknown", "unknown@company.com");
        factory.Mail.Failure = mail => mail.Id == busy
            ? new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, "4.2.1 Mailbox busy")
            : new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "5.1.1 unknown@company.com does not exist");
        Assert.Equal(2, await Dispatcher(factory).DispatchAsync(CancellationToken.None));

        var retry = await MessageAsync(factory, busy);
        Assert.Equal((MailStates.Queued, MailFailures.Deferred, 1), (retry.Status, retry.ErrorCode, retry.Attempts));
        Assert.InRange(retry.NextAttemptAt - clock.GetUtcNow(), TimeSpan.FromSeconds(47), TimeSpan.FromSeconds(73));
        var attempt = Assert.Single(await AttemptsAsync(factory, busy));
        Assert.Equal((MailAttemptOutcomes.Transient, 450, "4.2.1"), (attempt.Outcome, attempt.SmtpStatus, attempt.EnhancedStatus));

        var rejected = await MessageAsync(factory, unknown);
        Assert.Equal((MailStates.Failed, MailFailures.Rejected), (rejected.Status, rejected.ErrorCode));
        Assert.Equal(550, Assert.Single(await AttemptsAsync(factory, unknown)).SmtpStatus);

        // 退避時間未到不認領；到了再寄一次就成功。
        Assert.Equal(0, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        factory.Mail.Failure = null;
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal((MailStates.Sent, 2), ((await MessageAsync(factory, busy)).Status, (await MessageAsync(factory, busy)).Attempts));
        Assert.Equal([MailAttemptOutcomes.Transient, MailAttemptOutcomes.Sent], (await AttemptsAsync(factory, busy)).Select(x => x.Outcome));
    }

    [Fact]
    public async Task TransportFailuresPauseSendingWithoutBurningTheRestOfTheQueue()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock, services: services => services.PostConfigure<MailOptions>(x => x.MaxConcurrency = 1));
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++) ids.Add(await EnqueueAsync(factory, new Greeting(), $"#{i}", $"user{i}@company.com"));
        factory.Mail.Failure = _ => new AuthenticationException("535 5.7.8 Username and Password not accepted");
        Assert.Equal(3, await Dispatcher(factory).DispatchAsync(CancellationToken.None));

        var circuit = factory.Services.GetRequiredService<MailCircuit>();
        Assert.True(circuit.IsOpen);
        var messages = new List<OutboundMessage>();
        foreach (var id in ids) messages.Add(await MessageAsync(factory, id));
        var first = Assert.Single(messages, x => x.Attempts == 1);
        Assert.Equal((MailStates.Queued, MailFailures.TransportUnavailable, circuit.OpenUntil!.Value), (first.Status, first.ErrorCode, first.NextAttemptAt));
        Assert.Equal(MailAttemptOutcomes.Transport, Assert.Single(await AttemptsAsync(factory, first.Id)).Outcome);
        // 斷路器開啟後，同一輪剩下的信退回佇列：不記嘗試、不算次數。
        foreach (var rest in messages.Where(x => x.Id != first.Id))
        {
            Assert.Equal((MailStates.Queued, 0, (string?)null), (rest.Status, rest.Attempts, rest.ErrorCode));
            Assert.Empty(await AttemptsAsync(factory, rest.Id));
        }
        Assert.Equal(0, await Dispatcher(factory).DispatchAsync(CancellationToken.None));

        factory.Mail.Failure = null;
        clock.Advance(MailCircuit.OpenFor);
        Assert.Equal(3, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.False(circuit.IsOpen);
        Assert.Equal(3, factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task RecoveredTransportBringsMailsDeferredByTheFailureForward()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock, services: services => services.PostConfigure<MailOptions>(x => x.MaxConcurrency = 1));
        var ids = new[] { await EnqueueAsync(factory, new Greeting(), "a", "a@company.com"), await EnqueueAsync(factory, new Greeting(), "b", "b@company.com") };
        var dispatcher = Dispatcher(factory);
        factory.Mail.Failure = _ => new MailSendException(MailAttemptStages.Auth, null, new AuthenticationException("535: 5.7.3 Authentication unsuccessful"));
        Assert.Equal(2, await dispatcher.DispatchAsync(CancellationToken.None));
        var failed = (await Task.WhenAll(ids.Select(id => MessageAsync(factory, id)))).Single(x => x.Attempts == 1);
        var attempt = Assert.Single(await AttemptsAsync(factory, failed.Id));
        // 失敗的細節（遮罩後的伺服器回應）寫進嘗試，管理頁才看得出原因。
        Assert.Equal(("AuthenticationException: 535: 5.7.3 Authentication unsuccessful", MailAttemptStages.Auth), (attempt.Response, attempt.Stage));

        // 管理者修好設定後結束暫停：另一封寄出即視為恢復，被故障延後的那封提前到現在，不再等暫停結束。
        factory.Mail.Failure = null;
        factory.Services.GetRequiredService<MailCircuit>().Resume();
        Assert.Equal(1, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.Equal(clock.GetUtcNow(), (await MessageAsync(factory, failed.Id)).NextAttemptAt);
        Assert.Equal(1, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.Equal(2, factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task ARestartRetriesConnectionFailuresAtOnceButKeepsServerDeferrals()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock);
        var refused = await EnqueueAsync(factory, new Greeting(), "a", "a@company.com");
        var greylisted = await EnqueueAsync(factory, new Greeting(), "b", "b@company.com");
        factory.Mail.Failure = mail => mail.Id == greylisted
            ? new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, "4.2.1 Greylisted")
            : new MailSendException(MailAttemptStages.Connect, null, new IOException("Connection reset"));
        var dispatcher = Dispatcher(factory);
        Assert.Equal(2, await dispatcher.DispatchAsync(CancellationToken.None));
        Assert.Equal(0, await dispatcher.DispatchAsync(CancellationToken.None));

        // 新的 dispatcher（程序重啟）一啟動就提前連線故障的信；4xx 是伺服器要求稍後再試，照原退避。
        factory.Mail.Failure = null;
        Assert.Equal(1, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal([refused], factory.Mail.Sent.Select(x => x.Id));
        Assert.Equal(MailStates.Queued, (await MessageAsync(factory, greylisted)).Status);
    }

    [Fact]
    public async Task ExpiredLeasesAreReclaimedAsAbandonedAttemptsAndExpiredMessagesAreNotSent()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock);
        var stuck = await EnqueueAsync(factory, new Greeting(), "stuck", "stuck@company.com");
        var expired = await EnqueueAsync(factory, new Code(), "000000", "late@company.com", new(ExpiresAt: clock.GetUtcNow().AddMinutes(15)));
        using (var scope = factory.Services.CreateScope())
        {
            // 模擬寄到一半中止的 worker：已認領、租約已過期。
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().Where(x => x.Id == stuck)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, MailStates.Sending).SetProperty(x => x.Attempts, 1)
                    .SetProperty(x => x.LeaseToken, Guid.NewGuid()).SetProperty(x => x.LeaseUntil, clock.GetUtcNow().AddMinutes(1)));
        }
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(2, await Dispatcher(factory).DispatchAsync(CancellationToken.None));

        Assert.Equal([(1, MailAttemptOutcomes.Abandoned), (2, MailAttemptOutcomes.Sent)], (await AttemptsAsync(factory, stuck)).Select(x => (x.Attempt, x.Outcome)));
        var late = await MessageAsync(factory, expired);
        Assert.Equal((MailStates.Failed, MailFailures.Expired), (late.Status, late.ErrorCode));
        Assert.Null(await ContentAsync(factory, expired));
        Assert.Equal([stuck], factory.Mail.Sent.Select(x => x.Id));
    }

    [Fact]
    public async Task ClaimingTakesDueMessagesAndExpiredLeasesByPriorityWithoutWritingAudit()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var audits = new ProcedureAudits();
        await using var factory = new AppFactory(clock: clock, services: audits.Register);
        var stuck = await EnqueueAsync(factory, new Greeting(), "stuck", "stuck@company.com");
        clock.Advance(TimeSpan.FromSeconds(1));
        var queued = await EnqueueAsync(factory, new Greeting(), "queued", "queued@company.com");
        var later = await EnqueueAsync(factory, new Greeting(), "later", "later@company.com");
        var leased = await EnqueueAsync(factory, new Greeting(), "leased", "leased@company.com");
        clock.Advance(TimeSpan.FromSeconds(1));
        var code = await EnqueueAsync(factory, new Code(), "123456", "code@company.com");
        var now = clock.GetUtcNow();
        using (var scope = factory.Services.CreateScope())
        {
            // stuck：寄到一半中止、租約已過期；leased：另一個 worker 寄送中、租約未到期；later：退避中。
            var messages = scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>();
            await messages.Where(x => x.Id == stuck).ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, MailStates.Sending).SetProperty(x => x.Attempts, 1)
                .SetProperty(x => x.LeaseToken, Guid.NewGuid()).SetProperty(x => x.LeaseUntil, now.AddSeconds(-1)));
            await messages.Where(x => x.Id == leased).ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, MailStates.Sending).SetProperty(x => x.Attempts, 1)
                .SetProperty(x => x.LeaseToken, Guid.NewGuid()).SetProperty(x => x.LeaseUntil, now.AddMinutes(1)));
            await messages.Where(x => x.Id == later).ExecuteUpdateAsync(p => p.SetProperty(x => x.NextAttemptAt, now.AddMinutes(5)));
        }

        // 只取 @Take 封：安全信優先，同優先序 NextAttemptAt 早的先；OUTPUT 不保證順序，比對前自己排。
        var lease = Guid.NewGuid();
        var first = await ClaimAsync(factory, 2, now, lease);
        Assert.Equal([(code, 1, MailStates.Queued), (stuck, 2, MailStates.Sending)],
            first.OrderByDescending(x => x.Priority).ThenBy(x => x.NextAttemptAt).Select(x => (x.Id, x.Attempts, x.PreviousStatus)));
        var reclaimed = await MessageAsync(factory, stuck);
        Assert.Equal((MailStates.Sending, (Guid?)lease, (DateTimeOffset?)(now + MailDispatcher.LeaseDuration), now),
            (reclaimed.Status, reclaimed.LeaseToken, reclaimed.LeaseUntil, reclaimed.UpdatedAt));

        // 已認領的、租約未到期的與退避中的都不會再被認領。
        Assert.Equal([queued], (await ClaimAsync(factory, 10, now, Guid.NewGuid())).Select(x => x.Id));
        Assert.Equal(1, (await MessageAsync(factory, leased)).Attempts);
        audits.AssertReadOnly(ClaimProcedure);
    }

    [Fact]
    public async Task ClaimingInsideASerializableTransactionRestoresTheCallersIsolationLevel()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock);
        var id = await EnqueueAsync(factory, new Greeting(), "x", "chen@company.com");
        using var scope = factory.Services.CreateScope();
        var sql = scope.ServiceProvider.GetRequiredService<ISqlDatabase<AppDbContext>>();
        // 與連線池中留著行政稽核 SERIALIZABLE 的連線相同處境：READPAST 在這個層級會被拒（錯誤 650），procedure 內設回 READ COMMITTED 才能認領。
        await using var transaction = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var now = clock.GetUtcNow();
        Assert.Equal(id, Assert.Single(await sql.QueryProcedureAsync<Claimed>(ClaimProcedure, new { Take = 10, Now = now, Lease = Guid.NewGuid(), LeaseUntil = now + MailDispatcher.LeaseDuration })).Id);
        // procedure 結束時恢復呼叫前的層級（4＝SERIALIZABLE），不會留給同一條連線之後的命令。
        Assert.Equal((short)4, await sql.QuerySingleAsync<short>("SELECT transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id = @@SPID"));
    }

    [Fact]
    public async Task PerMinuteAndPerDayLimitsHoldBackTheQueue()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock, services: services => services.PostConfigure<MailOptions>(x => { x.MaxPerMinute = 2; x.MaxPerDay = 3; x.GiveUpAfterHours = 48; }));
        for (var i = 0; i < 5; i++) await EnqueueAsync(factory, new Greeting(), $"#{i}", $"user{i}@company.com");
        Assert.Equal(2, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal(0, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromMinutes(1));
        // 每日上限 3：只剩 1 封額度。
        Assert.Equal(1, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(2, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal(5, factory.Mail.Sent.Count);
    }

    [Fact]
    public async Task DisabledTransportKeepsMessagesQueued()
    {
        await using var factory = new AppFactory(services: services => services.PostConfigure<MailOptions>(x => x.Transport = MailTransports.Disabled));
        var id = await EnqueueAsync(factory, new Greeting(), "x", "chen@company.com");
        Assert.Equal(0, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal(MailStates.Queued, (await MessageAsync(factory, id)).Status);
        Assert.False(factory.Services.CreateScope().ServiceProvider.GetRequiredService<MailOutbox>().Available);
    }

    private const string ClaimProcedure = "mail.usp_ClaimOutboundMessages";

    private static async Task<IReadOnlyList<Claimed>> ClaimAsync(AppFactory factory, int take, DateTimeOffset now, Guid lease)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISqlDatabase<AppDbContext>>().QueryProcedureAsync<Claimed>(ClaimProcedure,
            new { Take = take, Now = now, Lease = lease, LeaseUntil = now + MailDispatcher.LeaseDuration });
    }

    // 與 MailDispatcher 的 Claim 相同：名稱、順序與型別對應 procedure 的 OUTPUT。
    private sealed record Claimed(Guid Id, int Attempts, MailPriority Priority, DateTimeOffset NextAttemptAt, string PreviousStatus);
}
