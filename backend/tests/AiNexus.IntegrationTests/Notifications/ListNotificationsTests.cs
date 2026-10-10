using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Notifications;
using AiNexus.Features.Persistence;
using AiNexus.Features.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Notifications;

public sealed class ListNotificationsTests
{
    [Fact]
    public async Task NotificationPagingHandlesEqualTimestampsAndReadThroughNeverCrossesAccountsOrNewEvents()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var other = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            for (var i = 0; i < 55; i++) db.Add(new WorkspaceNotification { OwnerId = owner, EventKey = "test:" + i, Type = "task.completed", Title = "完成", TargetKind = "task", TargetId = Guid.NewGuid(), CreatedAt = now });
            db.Add(new WorkspaceNotification { OwnerId = other, EventKey = "private", Title = "私人", CreatedAt = now }); await db.SaveChangesAsync();
        }
        var page = (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        Assert.Equal(50, page.Items.Count); Assert.Equal(55, page.Unread); Assert.True(page.HasMore);
        var next = (await alice.GetFromJsonAsync<NotificationPageDto>($"/api/v1/notifications?before={page.Items[^1].Id}"))!;
        Assert.Equal(5, next.Items.Count); Assert.False(next.HasMore); Assert.Equal(55, page.Items.Concat(next.Items).Select(x => x.Id).Distinct().Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"/api/v1/notifications?before={page.Items[0].Id}")).StatusCode);
        (await bob.PostAsync($"/api/v1/notifications/{page.Items[0].Id}/read", null)).EnsureSuccessStatusCode();
        (await bob.DeleteAsync($"/api/v1/notifications/{page.Items[0].Id}")).EnsureSuccessStatusCode();
        Assert.Equal(55, (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>(); db.Add(new WorkspaceNotification { OwnerId = owner, EventKey = "newer", Title = "新通知", CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        (await alice.PostAsJsonAsync("/api/v1/notifications/read", new ReadNotificationsRequest(now))).EnsureSuccessStatusCode();
        Assert.Equal(1, (await alice.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications?unread=true"))!.Unread);
        Assert.Equal(1, (await bob.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Unread);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/v1/notifications/read", new ReadNotificationsRequest(now))).StatusCode);
    }

    [Fact]
    public async Task CompletedGenerationsAndSharesPublishDurableDeduplicatedTypedNotifications()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "回答完成後通知"); await WaitForTerminal(owner, run.Id);
        var notifications = (await owner.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!;
        var completion = Assert.Single(notifications.Items); Assert.Equal("conversation.completed", completion.Type); Assert.Equal(new("conversation", conversation.Id), completion.Target);
        var user = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var response = await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [user])); response.EnsureSuccessStatusCode();
        var share = (await response.Content.ReadFromJsonAsync<ShareDto>())!;
        var received = Assert.Single((await reader.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications"))!.Items);
        Assert.Equal(new("share", share.Id), received.Target);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.ChangeTracker.Clear();
        var row = await db.Set<WorkspaceNotification>().SingleAsync(x => x.Id == completion.Id);
        await scope.ServiceProvider.GetRequiredService<NotificationService>().PublishAsync(row.OwnerId, row.EventKey, row.Type, row.Severity, row.Title, row.Body, row.TargetKind, row.TargetId, CancellationToken.None);
        await db.SaveChangesAsync(); Assert.Equal(2, await db.Set<WorkspaceNotification>().CountAsync());
    }
}
