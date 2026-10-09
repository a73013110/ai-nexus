using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using AiNexus.Features.Sharing;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class DomainEventTests
{
    private sealed record Probe(Guid OwnerId, string Name) : IDomainEvent;
    private sealed record Echo(Guid OwnerId, string Name) : IDomainEvent;
    private sealed record Unhandled(Guid OwnerId) : IDomainEvent;
    private sealed record Loop(Guid OwnerId) : IDomainEvent;
    private sealed record Saving(Guid OwnerId) : IDomainEvent;

    /// <summary>What the handlers observed, shared with the test.</summary>
    private sealed class Recorder
    {
        public ConcurrentQueue<string> Handled { get; } = new();
        public ConcurrentQueue<Guid?> Transactions { get; } = new();
    }

    // Adds an audit row, renames the owner with a bulk update and raises a follow-up event.
    private sealed class ProbeHandler(NexusDbContext db, DomainEvents events, Recorder recorder) : IDomainEventHandler<Probe>
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

    private sealed class EchoHandler(NexusDbContext db, Recorder recorder) : IDomainEventHandler<Echo>
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

    private sealed class SavingHandler(NexusDbContext db) : IDomainEventHandler<Saving>
    {
        public async Task HandleAsync(Saving e, CancellationToken ct) => await db.SaveChangesAsync(ct);
    }

    private static NexusFactory Factory(Recorder recorder) => new(backgroundJobs: false, services: services =>
    {
        services.AddSingleton(recorder);
        services.AddDomainEventHandler<Probe, ProbeHandler>();
        services.AddDomainEventHandler<Echo, EchoHandler>();
        services.AddDomainEventHandler<Loop, LoopHandler>();
        services.AddDomainEventHandler<Saving, SavingHandler>();
    });

    private static async Task<Guid> OwnerAsync(NexusFactory f)
    {
        using var client = await f.SignedInAsync();
        return (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
    }

    private static async Task<(string Name, string[] Actions)> StateAsync(NexusFactory f, Guid owner)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
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
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
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
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            scope.ServiceProvider.GetRequiredService<DomainEvents>().Raise(new Probe(owner, "committed"));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "publisher.saved", Result = "committed" });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var (name, actions) = await StateAsync(f, owner);
        Assert.Equal("committed", name);
        // Handler rows are written by the publisher's save and get the same audit redaction as the publisher's own.
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
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            scope.ServiceProvider.GetRequiredService<DomainEvents>().Raise(new Probe(owner, "failed"));
            // The publisher's own write fails after the handler's bulk update already ran.
            db.Users.Add(new NexusUser { Id = owner, Sid = "S-1-5-21-duplicate", Account = "duplicate", DisplayName = "duplicate" });
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
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var events = scope.ServiceProvider.GetRequiredService<DomainEvents>();
            events.Raise(new Unhandled(owner));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "plain.saved", Result = "plain" });
            await db.SaveChangesAsync();
            Assert.False(events.HasPending);

            events.Raise(new Loop(owner));
            db.AuditEvents.Add(new() { OwnerId = owner, Action = "loop.saved", Result = "failed" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Equal(DomainEvents.MaxRounds, recorder.Handled.Count);
            // A failed dispatch leaves nothing behind for a later save in the same scope.
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
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); var events = scope.ServiceProvider.GetRequiredService<DomainEvents>();
        events.Raise(new Saving(owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        events.Raise(new Probe(owner, "sync"));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.False(events.HasPending);
        Assert.Empty(recorder.Handled);
    }

    [Fact]
    public async Task DeletingAnArtifactRevokesItsSharesInTheSameTransaction()
    {
        await using var f = new NexusFactory(backgroundJobs: false); using var alice = await f.SignedInAsync(); using var bob = await f.SignedInAsync("bob");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var source = (await (await alice.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("成果", "內容"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        var share = (await (await alice.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("artifact", source.Resource.Id, [bobId]))).Content.ReadFromJsonAsync<ShareDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/artifacts/{source.Resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/v1/artifacts/{source.Resource.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/shares/{share.Id}")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var link = await db.Set<ShareLink>().SingleAsync(x => x.Id == share.Id);
        Assert.True(link.IsRevoked); Assert.Equal("", link.SnapshotJson);
        Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == source.Resource.Id && x.Action == "artifact.deleted").ToArrayAsync());
    }

    [Fact]
    public async Task DeletingAProjectDetachesItsArtifacts()
    {
        await using var f = new NexusFactory(backgroundJobs: false); using var owner = await f.SignedInAsync();
        var project = (await (await owner.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("container"))).Content.ReadFromJsonAsync<ProjectDto>())!;
        var artifact = (await (await owner.PostAsJsonAsync($"/api/v1/projects/{project.Resource.Id}/artifacts", new CreateArtifactRequest("kept", "body"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        using (var scope = f.Services.CreateScope())
            Assert.Equal(project.Resource.Id, (await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<Artifact>().SingleAsync(x => x.Id == artifact.Resource.Id)).ProjectId);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/projects/{project.Resource.Id}")).StatusCode);
        using (var scope = f.Services.CreateScope())
            Assert.Null((await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<Artifact>().SingleAsync(x => x.Id == artifact.Resource.Id)).ProjectId);
        Assert.Equal("body", (await owner.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{artifact.Resource.Id}"))!.Content);
    }
}
