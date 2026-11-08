using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Features.Work.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Work.Sharing.Management;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Notifications;

public sealed class ListNotificationsTests
{
    [Fact]
    public async Task NotificationPagingHandlesEqualTimestampsAndReadThroughNeverCrossesAccountsOrNewEvents()
    {
        await using var factory = new AppFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var other = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 55; i++) db.Add(new Notification { OwnerId = owner, EventKey = "test:" + i, Type = "task.completed", Title = "完成", TargetKind = "task", TargetId = Guid.NewGuid(), CreatedAt = now });
            db.Add(new Notification { OwnerId = other, EventKey = "private", Title = "私人", CreatedAt = now }); await db.SaveChangesAsync();
        }
        var page = (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        Assert.Equal(50, page.Items.Count); Assert.Equal(55, page.Unread); Assert.True(page.HasMore);
        var next = (await alice.GetFromJsonAsync<NotificationPageDto>($"/api/v1/notifications?before={page.Items[^1].Id}"))!;
        Assert.Equal(5, next.Items.Count); Assert.False(next.HasMore); Assert.Equal(55, page.Items.Concat(next.Items).Select(x => x.Id).Distinct().Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"/api/v1/notifications?before={page.Items[0].Id}")).StatusCode);
        (await bob.PostAsync($"/api/v1/notifications/{page.Items[0].Id}/read", null)).EnsureSuccessStatusCode();
        (await bob.DeleteAsync($"/api/v1/notifications/{page.Items[0].Id}")).EnsureSuccessStatusCode();
        Assert.Equal(55, (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Add(new Notification { OwnerId = owner, EventKey = "newer", Title = "新通知", CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        (await alice.PostAsJsonAsync("/api/v1/notifications/read", new ReadNotificationsRequest(now))).EnsureSuccessStatusCode();
        Assert.Equal(1, (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications?unread=true"))!.Unread);
        Assert.Equal(1, (await bob.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread);
        alice.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/v1/notifications/read", new ReadNotificationsRequest(now))).StatusCode);
    }

    [Fact]
    public async Task CompletedGenerationsAndSharesPublishDurableDeduplicatedTypedNotifications()
    {
        await using var factory = new AppFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "回答完成後通知"); await WaitForTerminal(owner, run.Id);
        var notifications = (await owner.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        var completion = Assert.Single(notifications.Items); Assert.Equal("conversation.completed", completion.Type); Assert.Equal(new("conversation", conversation.Id), completion.Target);
        // 自己送出的提問完成只留紀錄，不計入未讀。
        Assert.NotNull(completion.ReadAt); Assert.Equal(0, notifications.Unread);
        var user = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var response = await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user])); response.EnsureSuccessStatusCode();
        var share = (await response.Content.ReadFromJsonAsync<ShareDto>())!;
        var inbox = (await reader.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        var received = Assert.Single(inbox.Items);
        Assert.Equal(new("share", share.Id), received.Target); Assert.Null(received.ReadAt); Assert.Equal(1, inbox.Unread);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var row = await db.Notifications.SingleAsync(x => x.Id == completion.Id);
        await scope.ServiceProvider.GetRequiredService<NotificationService>().PublishAsync(
            new NotificationMessage(row.OwnerId, row.EventKey, row.Type, row.Severity, row.Title, row.Body, row.TargetKind, row.TargetId), CancellationToken.None);
        await db.SaveChangesAsync(); Assert.Equal(2, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task OnlyWhatNeedsAttentionStaysUnreadUntilSeenSupersededOrExpired()
    {
        await using var factory = new AppFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var other = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        Guid task = Guid.NewGuid(), share = Guid.NewGuid();
        async Task PublishAsync(Guid to, string key, string severity, string kind, Guid id, bool own)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<NotificationService>().PublishAsync(
                new NotificationMessage(to, key, "test", severity, "標題", "內容", kind, id) { OwnAction = own }, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        async Task<int> UnreadAsync(HttpClient client) => (await client.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread;

        // 自己發起的：成功不提醒，失敗提醒；重試成功後前一次的失敗跟著改為已讀。
        await PublishAsync(owner, "job:1", "success", "task", Guid.NewGuid(), own: true);
        Assert.Equal(0, await UnreadAsync(alice));
        await PublishAsync(owner, "job:2:1", "error", "task", task, own: true);
        Assert.Equal(1, await UnreadAsync(alice));
        await PublishAsync(owner, "job:2:2", "success", "task", task, own: true);
        Assert.Equal(0, await UnreadAsync(alice));

        // 別人給的：提醒到打開目標為止；只影響自己的通知。
        await PublishAsync(owner, "share:a", "info", "share", share, own: false);
        await PublishAsync(other, "share:b", "info", "share", share, own: false);
        Assert.Equal(1, await UnreadAsync(alice));
        (await alice.PostAsync($"/api/v1/notifications/targets/share/{share}/read", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, await UnreadAsync(alice)); Assert.Equal(1, await UnreadAsync(bob));

        // 放超過一週的未讀自動改為已讀。
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Notifications.Where(x => x.OwnerId == other)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, DateTimeOffset.UtcNow - NotificationExpiryTask.UnreadFor - TimeSpan.FromHours(1)));
        }
        Assert.Equal(1, await factory.InScopeAsync((NotificationExpiryTask task) => task.ExpireAsync(CancellationToken.None)));
        Assert.Equal(0, await UnreadAsync(bob));
    }
}
