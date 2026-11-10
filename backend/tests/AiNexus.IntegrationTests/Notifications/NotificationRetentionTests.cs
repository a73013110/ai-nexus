using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Platform.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Notifications;

public sealed class NotificationRetentionTests
{
    [Fact]
    public async Task OnlyReadOrDismissedNotificationsPastRetentionAreDeleted()
    {
        await using var f = new AppFactory();
        using var client = await f.SignedInAsync();
        var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var old = DateTimeOffset.UtcNow.AddDays(-100);
        var recent = DateTimeOffset.UtcNow.AddDays(-10);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new Notification { OwnerId = owner, EventKey = "old-read", Title = "舊的已讀", CreatedAt = old, ReadAt = old });
            db.Add(new Notification { OwnerId = owner, EventKey = "old-dismissed", Title = "舊的已移除", CreatedAt = old, DismissedAt = old });
            // 沒看過的不刪：先由「過期通知標為已讀」處理，下一輪才輪到它。
            db.Add(new Notification { OwnerId = owner, EventKey = "old-unread", Title = "舊的未讀", CreatedAt = old });
            db.Add(new Notification { OwnerId = owner, EventKey = "recent-read", Title = "最近的已讀", CreatedAt = recent, ReadAt = recent });
            await db.SaveChangesAsync();
        }

        Assert.Equal(2, await f.InScopeAsync((NotificationRetentionTask task) => task.DeleteExpiredAsync(CancellationToken.None)));

        using (var scope = f.Services.CreateScope())
        {
            var left = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.Select(x => x.EventKey).ToListAsync();
            Assert.Equal(new[] { "old-unread", "recent-read" }, left.Order());
        }
    }
}
