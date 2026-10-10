using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Monitoring;

namespace AiNexus.IntegrationTests.Monitoring;

public sealed class HeartbeatPresenceTests
{
    [Fact]
    public async Task PresenceIsAuthenticatedCsrfProtectedAndMonitorIsSeparatelyGranted()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(Guid.NewGuid(), "chat", "active"))).StatusCode);
        using var bob = await factory.SignedInAsync("bob"); using var alice = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/monitoring")).StatusCode);
        var tab = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "chat", "active"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "/chat/private-title?secret=1", "active"))).StatusCode);
        var snapshot = (await alice.GetFromJsonAsync<MonitoringSnapshot>("/api/v1/admin/monitoring"))!;
        Assert.Equal("bob", Assert.Single(snapshot.Sessions).DisplayName);
        bob.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/presence", new PresenceRequest(tab, "chat", "active"))).StatusCode);
    }
}
