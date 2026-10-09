using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Account;
using AiNexus.Features.Persistence;
using AiNexus.Features.Attachments;
using AiNexus.Features.AccessControl;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class AttachmentStorageTests
{
    private static async Task<HttpResponseMessage> Upload(HttpClient client, int size = 600)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(new string('x', size))), "file", "../../original.txt");
        return await client.PostAsync("/api/v1/attachments", body);
    }

    [Fact]
    public async Task RawBytesAreOutsideDatabaseAndDownloadsRequireOwnership()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var response = await Upload(alice); response.EnsureSuccessStatusCode();
        var file = (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var metadata = await db.Set<Attachment>().SingleAsync();
        Assert.Null(db.Model.FindEntityType(typeof(Attachment))!.FindProperty("Data"));
        Assert.Equal("original.txt", metadata.FileName); Assert.Equal(32, metadata.StorageKey.Length);
        var root = scope.ServiceProvider.GetRequiredService<IOptions<AttachmentOptions>>().Value.StoragePath;
        Assert.Single(Directory.GetFiles(root, "*.blob", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(root, "*.upload", SearchOption.AllDirectories));
        Assert.Equal(new string('x', 600), await alice.GetStringAsync($"/api/v1/attachments/{file.Id}/content"));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
        var storage = (await alice.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!;
        Assert.Equal(5_000_000_000, storage.LimitBytes); Assert.Equal(600, storage.UsedBytes); Assert.Equal(4_999_999_400, storage.RemainingBytes);
        (await alice.DeleteAsync($"/api/v1/attachments/{file.Id}")).EnsureSuccessStatusCode();
        Assert.Empty(Directory.GetFiles(root, "*.blob", SearchOption.AllDirectories));
        Assert.Empty(await db.Set<Attachment>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PersonalOverrideExceedsBothGroupAndDefaultCapsAndCanRestoreInheritance()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var member = await factory.SignedInAsync("bob");
        var owner = (await member.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Add(new GroupModelPolicy { GroupId = BuiltInAccess.WorkspaceGroup, StoredAttachmentLimitBytes = 1000 }); await db.SaveChangesAsync();
        }
        (await Upload(member)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(member)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync($"/api/v1/admin/users/{owner}/storage", new AttachmentStorageLimitRequest(10_000_000_000))).StatusCode);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/storage", new AttachmentStorageLimitRequest(10_000_000_000))).EnsureSuccessStatusCode();
        (await Upload(member)).EnsureSuccessStatusCode();
        var capacity = (await member.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!;
        Assert.Equal(10_000_000_000, capacity.LimitBytes); Assert.Equal(1200, capacity.UsedBytes); Assert.Equal("personal", capacity.LimitSource);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/storage", new AttachmentStorageLimitRequest(null))).EnsureSuccessStatusCode();
        capacity = (await member.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!;
        Assert.Equal(1000, capacity.LimitBytes); Assert.Equal(0, capacity.RemainingBytes); Assert.Equal("group", capacity.LimitSource);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(member, 1)).StatusCode);
        using var audit = factory.Services.CreateScope(); Assert.Equal(2, await audit.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.CountAsync(x => x.Action == "admin.user_storage" && x.Result == "saved"));
    }

    [Fact]
    public async Task DatabaseReservationPreventsConcurrentUploadsAcrossIndependentProcessGates()
    {
        await using var factory = new NexusFactory(attachments: x => { x.MaxFileBytes = 1024; x.MaxMessageBytes = 1024; x.DefaultOwnerLimitBytes = 1024; }, services: services =>
        {
            // A separate semaphore per service scope simulates independent application processes.
            services.RemoveAll<AttachmentWriteLock>(); services.AddScoped<AttachmentWriteLock>();
        });
        using var client = await factory.SignedInAsync();
        var responses = await Task.WhenAll(Upload(client), Upload(client));
        Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.RequestEntityTooLarge);
        var capacity = (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!;
        Assert.Equal(600, capacity.UsedBytes);
    }

    [Fact]
    public async Task WriteAndDeleteFailuresRetainDurableCleanupAndReleaseQuotaAfterRetry()
    {
        FaultStorage? fault = null;
        await using var factory = new NexusFactory(services: services =>
        {
            services.RemoveAll<IAttachmentStorage>();
            services.AddSingleton<IAttachmentStorage>(sp => fault = new FaultStorage(new FileAttachmentStorage(sp.GetRequiredService<IOptions<AttachmentOptions>>(), sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>())));
        });
        using var client = await factory.SignedInAsync();
        fault!.FailWrite = true; fault.FailDelete = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Upload(client)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            Assert.Equal(AttachmentStates.Deleting, (await db.Set<Attachment>().SingleAsync()).StorageState);
            Assert.Equal(600, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
            fault.FailDelete = false;
            await scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>().DeletePendingAsync(CancellationToken.None);
            Assert.Empty(await db.Set<Attachment>().ToListAsync());
        }
        fault.FailWrite = false;
        var response = await Upload(client); response.EnsureSuccessStatusCode(); var file = (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
        fault.FailDelete = true;
        (await client.DeleteAsync($"/api/v1/attachments/{file.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
        Assert.Equal(600, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
        fault.FailDelete = false;
        using var retry = factory.Services.CreateScope();
        await retry.ServiceProvider.GetRequiredService<AttachmentLifecycle>().ReclaimAsync(CancellationToken.None);
        Assert.Equal(0, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
    }

    [Fact]
    public async Task ScheduledCleanupReclaimsReaderDraftsAndInterruptedUploadsWithoutNewRequests()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
        using var client = await factory.SignedInAsync();
        var first = await Upload(client); first.EnsureSuccessStatusCode(); var draft = (await first.Content.ReadFromJsonAsync<AttachmentDto>())!;
        var readerResponse = await client.PostAsync($"/api/v1/attachments/{draft.Id}/document", null);
        readerResponse.EnsureSuccessStatusCode(); var reader = (await readerResponse.Content.ReadFromJsonAsync<AiNexus.Features.Knowledge.DocumentDto>())!;
        var second = await Upload(client); second.EnsureSuccessStatusCode(); var interrupted = (await second.Content.ReadFromJsonAsync<AttachmentDto>())!;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        await db.Set<Attachment>().Where(x => x.Id == draft.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, DateTimeOffset.UtcNow.AddDays(-15)));
        await db.Set<Attachment>().Where(x => x.Id == interrupted.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.StorageState, AttachmentStates.Pending).SetProperty(x => x.CreatedAt, DateTimeOffset.UtcNow.AddHours(-2)));
        await scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>().ReclaimAsync(CancellationToken.None);
        Assert.Empty(await db.Set<Attachment>().ToListAsync());
        var document = await db.Set<AiNexus.Features.Knowledge.KnowledgeDocument>().SingleAsync(x => x.Id == reader.Id);
        Assert.True(document.IsDeleted); Assert.Null(document.AttachmentId);
        Assert.Equal(0, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
        var root = scope.ServiceProvider.GetRequiredService<IOptions<AttachmentOptions>>().Value.StoragePath;
        Assert.Empty(Directory.GetFiles(root, "*.blob", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task LateWriterCannotLeaveUntrackedBytesAfterItsReservationHasBeenReclaimed()
    {
        FaultStorage? fault = null;
        await using var factory = new NexusFactory(services: services =>
        {
            services.RemoveAll<IAttachmentStorage>();
            services.AddSingleton<IAttachmentStorage>(sp => fault = new FaultStorage(new FileAttachmentStorage(sp.GetRequiredService<IOptions<AttachmentOptions>>(), sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>())));
        });
        using var client = await factory.SignedInAsync();
        fault!.BlockWrite = true;
        var uploading = Upload(client);
        await fault.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var reservation = await db.Set<Attachment>().AsNoTracking().SingleAsync();
        await db.Set<Attachment>().Where(x => x.Id == reservation.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, DateTimeOffset.UtcNow.AddHours(-2)));
        await scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>().ReclaimAsync(CancellationToken.None);
        Assert.Empty(await db.Set<Attachment>().ToListAsync());
        fault.FailDelete = true;
        fault.ContinueWrite.SetResult();
        Assert.Equal(HttpStatusCode.Conflict, (await uploading).StatusCode);
        var cleanup = await db.Set<Attachment>().AsNoTracking().SingleAsync();
        Assert.NotEqual(reservation.Id, cleanup.Id);
        Assert.Equal(reservation.StorageKey, cleanup.StorageKey);
        Assert.Equal(AttachmentStates.Deleting, cleanup.StorageState);
        Assert.Equal(600, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
        fault.FailDelete = false;
        await scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>().ReclaimAsync(CancellationToken.None);
        Assert.Empty(await db.Set<Attachment>().ToListAsync());
        var root = scope.ServiceProvider.GetRequiredService<IOptions<AttachmentOptions>>().Value.StoragePath;
        Assert.Empty(Directory.GetFiles(root, "*.blob", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(root, "*.upload", SearchOption.AllDirectories));
        Assert.Equal(0, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
    }

    [Fact]
    public async Task OrphanRecoveryPreservesTrackedAndRecentWritesAndRetriesFailedDeletion()
    {
        FaultStorage? fault = null;
        await using var factory = new NexusFactory(services: services =>
        {
            services.RemoveAll<IAttachmentStorage>();
            services.AddSingleton<IAttachmentStorage>(sp => fault = new FaultStorage(new FileAttachmentStorage(sp.GetRequiredService<IOptions<AttachmentOptions>>(), sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>())));
        });
        using var client = await factory.SignedInAsync();
        (await Upload(client)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var original = await db.Set<Attachment>().SingleAsync();
        var root = scope.ServiceProvider.GetRequiredService<IOptions<AttachmentOptions>>().Value.StoragePath;
        string PathFor(string key) => Path.Combine(root, key[..2], key.Substring(2, 2), key + ".blob");
        File.SetLastWriteTimeUtc(PathFor(original.StorageKey), DateTime.UtcNow.AddDays(-2));
        var stale = Guid.NewGuid().ToString("N"); var staged = Guid.NewGuid().ToString("N"); var fresh = Guid.NewGuid().ToString("N");
        foreach (var key in new[] { stale, staged, fresh }) await fault!.WriteAsync(key, new byte[] { 42 }, CancellationToken.None);
        File.Move(PathFor(staged), PathFor(staged) + ".upload");
        File.SetLastWriteTimeUtc(PathFor(stale), DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(PathFor(staged) + ".upload", DateTime.UtcNow.AddDays(-2));
        var lifecycle = scope.ServiceProvider.GetRequiredService<AttachmentLifecycle>();
        fault!.FailDelete = true;
        await lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.True(File.Exists(PathFor(stale))); Assert.True(File.Exists(PathFor(staged) + ".upload"));
        fault.FailDelete = false;
        await lifecycle.ReconcileAsync(CancellationToken.None);
        Assert.False(File.Exists(PathFor(stale))); Assert.False(File.Exists(PathFor(staged) + ".upload"));
        Assert.True(File.Exists(PathFor(original.StorageKey))); Assert.True(File.Exists(PathFor(fresh)));
        Assert.Equal(600, (await client.GetFromJsonAsync<AttachmentStorageDto>("/api/v1/attachments/storage"))!.UsedBytes);
    }

    [Fact]
    public void StorageRejectsWebsitePathsAndRelativePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "nexus-storage-root");
        var site = Path.Combine(root, "site");
        var attachments = Path.Combine(root, "data", "attachments");
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot("data", site));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(site, site));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(Path.Combine(site, "wwwroot", "files"), site));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(Path.Combine(root, "data", "..", "site", "files"), site));
        Assert.Equal(attachments, FileAttachmentStorage.ValidateRoot(attachments + Path.DirectorySeparatorChar, site));
        Assert.Equal(site + "-data", FileAttachmentStorage.ValidateRoot(site + "-data", site));
    }

    [WindowsFact]
    public void StorageComparesWindowsPathsWithoutCaseAndRejectsDriveRelativePaths()
    {
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(@"D:data\attachments", @"D:\site"));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(@"d:\SITE\wwwroot\files", @"D:\site"));
        Assert.Equal(@"D:\CoreProject\AiNexus\data\attachments", FileAttachmentStorage.ValidateRoot(@"D:\CoreProject\AiNexus\data\attachments", @"D:\CoreProject\AiNexus\site"));
    }

    private sealed class FaultStorage(IAttachmentStorage inner) : IAttachmentStorage
    {
        public bool FailWrite { get; set; }
        public bool FailDelete { get; set; }
        public bool BlockWrite { get; set; }
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WriteAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct)
        {
            if (BlockWrite) { WriteStarted.TrySetResult(); await ContinueWrite.Task.WaitAsync(ct); }
            await inner.WriteAsync(key, bytes, ct);
            if (FailWrite) throw new IOException("fixture write failure");
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public IAsyncEnumerable<string> StaleKeysAsync(DateTimeOffset before, CancellationToken ct) => inner.StaleKeysAsync(before, ct);
        public Task DeleteAsync(string key, CancellationToken ct) => FailDelete ? throw new IOException("fixture delete failure") : inner.DeleteAsync(key, ct);
        public Task VerifyAsync(CancellationToken ct) => inner.VerifyAsync(ct);
    }
}
