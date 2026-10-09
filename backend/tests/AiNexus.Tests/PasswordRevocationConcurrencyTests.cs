using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AiNexus.Features.Identity.Users;

namespace AiNexus.Tests;

public sealed class PasswordRevocationConcurrencyTests
{
    [Fact]
    public async Task AStaleLoginOrHashUpgradeCannotOverwriteAConcurrentPasswordReset()
    {
        var id = Guid.NewGuid();
        await using var factory = new NexusFactory(seed: db => db.Users.Add(new NexusUser {
            Id = id, Sid = "managed:" + id, Account = "tester", DisplayName = "tester", LocalAccount = "TESTER",
            LocalEnabled = true, AdEnabled = false, PasswordHash = "old-format-hash", SecurityVersion = 1
        }));
        using var loginScope = factory.Services.CreateScope(); using var resetScope = factory.Services.CreateScope();
        var loginDb = loginScope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var resetDb = resetScope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var stale = await loginDb.Users.SingleAsync(x => x.Id == id);
        var reset = await resetDb.Users.SingleAsync(x => x.Id == id);
        reset.PasswordHash = "new-password-hash"; reset.SecurityVersion++;
        await resetDb.SaveChangesAsync();
        stale.PasswordHash = "upgrade-of-old-password-hash"; stale.LastSeenAt = DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loginDb.SaveChangesAsync());
        await resetDb.Entry(reset).ReloadAsync();
        Assert.Equal("new-password-hash", reset.PasswordHash); Assert.Equal(2, reset.SecurityVersion);
    }
}
