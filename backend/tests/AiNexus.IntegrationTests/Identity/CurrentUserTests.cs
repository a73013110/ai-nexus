using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Identity;

public sealed class CurrentUserTests
{
    [Fact]
    public async Task ExistingWindowsUsersAreResolvedWithoutTheIdentityWriteGate()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var gate = factory.Services.GetRequiredService<IdentityWriteLock>().Gate;
        await gate.WaitAsync();
        try { (await client.GetAsync("/api/v1/me").WaitAsync(TimeSpan.FromSeconds(10))).EnsureSuccessStatusCode(); }
        finally { gate.Release(); }

        // A user due a last-seen refresh still waits for the gate, then is updated.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));
        await gate.WaitAsync();
        Task<HttpResponseMessage> pending;
        try
        {
            pending = client.GetAsync("/api/v1/me");
            await Task.Delay(200);
            Assert.False(pending.IsCompleted);
        }
        finally { gate.Release(); }
        (await pending).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.SingleAsync(x => x.Account == "TEST\\alice")).LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
