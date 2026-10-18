using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Data;

public sealed class DomainEventDispatchTests
{
    private sealed record Probe(Guid OwnerId, string Name) : IDomainEvent;

    private sealed record Echo(Guid OwnerId, string Name) : IDomainEvent;

    private sealed record Unhandled(Guid OwnerId) : IDomainEvent;

    private sealed record Loop(Guid OwnerId) : IDomainEvent;

    private sealed record Saving(Guid OwnerId) : IDomainEvent;

    /// <summary>handler 所觀察到的結果，與測試共用。</summary>
    private sealed class Recorder
    {
        public ConcurrentQueue<string> Handled { get; } = new();
        public ConcurrentQueue<Guid?> Transactions { get; } = new();
    }

    // 新增一筆稽核紀錄、以批次更新改名擁有者，並發出後續事件。
    private sealed class ProbeHandler(AppDbContext db, DomainEvents events, Recorder recorder) : IDomainEventHandler<Probe>
    {
        public async Task HandleAsync(Probe e, CancellationToken ct)
        {
            recorder.Handled.Enqueue("probe:" + e.Name);
            recorder.Transactions.Enqueue(db.Database.CurrentTransaction?.TransactionId);
            await db.Users.Where(x => x.Id == e.OwnerId).ExecuteUpdateAsync(p => p.SetProperty(x => x.DisplayName, e.Name), ct);
            db.AuditEvents.Add(new() { OwnerId = e.OwnerId, Action = "probe.handled https://secret.example/token", Result = e.Name });
            events.Raise(new Echo(e.OwnerId, e.Name));
        }
    }

    private sealed class EchoHandler(AppDbContext db, Recorder recorder) : IDomainEventHandler<Echo>
    {
        public Task HandleAsync(Echo e, CancellationToken ct)
        {
            recorder.Handled.Enqueue("echo:" + e.Name);
            db.AuditEvents.Add(new() { OwnerId = e.OwnerId, Action = "echo.handled", Result = e.Name });
            return Task.CompletedTask;
        }
    }

    private sealed class LoopHandler(DomainEvents events, Recorder recorder) : IDomainEventHandler<Loop>
    {
        public Task HandleAsync(Loop e, CancellationToken ct)
        {
            recorder.Handled.Enqueue("loop");
            events.Raise(new Loop(e.OwnerId));
            return Task.CompletedTask;
        }
    }

    private sealed class SavingHandler(AppDbContext db) : IDomainEventHandler<Saving>
    {
        public async Task HandleAsync(Saving e, CancellationToken ct) => await db.SaveChangesAsync(ct);
    }

    private static AppFactory Factory(Recorder recorder) => new(services: services =>
    {
        services.AddSingleton(recorder);
        services.AddDomainEventHandler<Probe, ProbeHandler>();
        services.AddDomainEventHandler<Echo, EchoHandler>();
        services.AddDomainEventHandler<Loop, LoopHandler>();
        services.AddDomainEventHandler<Saving, SavingHandler>();
    });

    private static async Task<Guid> OwnerAsync(AppFactory f)
    {
        using var client = await f.SignedInAsync();
        return (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
    }

    private static async Task<(string Name, string[] Actions)> StateAsync(AppFactory f, Guid owner)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = await db.Users.Where(x => x.Id == owner).Select(x => x.DisplayName).SingleAsync();
        var actions = await db.AuditEvents.Where(x => x.OwnerId == owner && (x.Result == "committed" || x.Result == "rolled-back" || x.Result == "failed" || x.Result == "plain"))
            .OrderBy(x => x.Id).Select(x => x.Action).ToArrayAsync();
        return (name, actions);
    }

    [Fact]
    public async Task HandlersRunInTheSaveTransactionBeforeTheWritesAndRollBackWithIt()
    {
        var recorder = new Recorder();
        await using var f = Factory(recorder); var owner = await OwnerAsync(f);
        var original = (await StateAsync(f, owner)).Name;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            scope.ServiceProvider.GetRequiredService<DomainEvents>().Raise(new Probe(owner, "rolled-back"));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "publisher.saved", Result = "rolled-back" });
            await db.SaveChangesAsync();
            Assert.Equal(transaction.TransactionId, Assert.Single(recorder.Transactions));
            await transaction.RollbackAsync();
        }
        var after = await StateAsync(f, owner); Assert.Equal(original, after.Name); Assert.Empty(after.Actions);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            scope.ServiceProvider.GetRequiredService<DomainEvents>().Raise(new Probe(owner, "committed"));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "publisher.saved", Result = "committed" });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var (name, actions) = await StateAsync(f, owner);
        Assert.Equal("committed", name);
        // handler 的資料列由發布者的 save 一併寫入，並套用與發布者本身相同的稽核遮罩。
        Assert.Equal(3, actions.Length);
        Assert.Contains("publisher.saved", actions); Assert.Contains("echo.handled", actions);
        Assert.DoesNotContain(actions, x => x.Contains("secret.example"));
        Assert.Equal(["probe:rolled-back", "echo:rolled-back", "probe:committed", "echo:committed"], recorder.Handled);
    }

    [Fact]
    public async Task SaveWithoutATransactionWrapsHandlersAndWritesInOne()
    {
        var recorder = new Recorder();
        await using var f = Factory(recorder); var owner = await OwnerAsync(f);
        var original = (await StateAsync(f, owner)).Name;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            scope.ServiceProvider.GetRequiredService<DomainEvents>().Raise(new Probe(owner, "failed"));
            // 發布者自己的寫入在 handler 的批次更新已執行後才失敗。
            db.Users.Add(new User { Id = owner, Sid = "S-1-5-21-duplicate", Account = "duplicate", DisplayName = "duplicate" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.NotNull(Assert.Single(recorder.Transactions));
            Assert.Null(db.Database.CurrentTransaction);
        }
        var after = await StateAsync(f, owner); Assert.Equal(original, after.Name); Assert.Empty(after.Actions);
    }

    [Fact]
    public async Task EventsWithoutHandlersAreDroppedAndRunawayChainsFailTheSave()
    {
        var recorder = new Recorder();
        await using var f = Factory(recorder); var owner = await OwnerAsync(f);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var events = scope.ServiceProvider.GetRequiredService<DomainEvents>();
            events.Raise(new Unhandled(owner));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "plain.saved", Result = "plain" });
            await db.SaveChangesAsync();
            Assert.False(events.HasPending);

            events.Raise(new Loop(owner));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "loop.saved", Result = "failed" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Equal(DomainEvents.MaxRounds, recorder.Handled.Count);
            // 失敗的分派不會留下任何東西，影響同一 scope 之後的 save。
            Assert.False(events.HasPending);
            db.ChangeTracker.Clear();
            await db.SaveChangesAsync();
            Assert.Equal(DomainEvents.MaxRounds, recorder.Handled.Count);
        }
        Assert.Equal(["plain.saved"], (await StateAsync(f, owner)).Actions);
    }

    [Fact]
    public async Task HandlersCannotSaveAndSynchronousSavesRejectPendingEvents()
    {
        var recorder = new Recorder();
        await using var f = Factory(recorder); var owner = await OwnerAsync(f);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var events = scope.ServiceProvider.GetRequiredService<DomainEvents>();
        events.Raise(new Saving(owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        events.Raise(new Probe(owner, "sync"));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.False(events.HasPending);
        Assert.Empty(recorder.Handled);
    }
}
