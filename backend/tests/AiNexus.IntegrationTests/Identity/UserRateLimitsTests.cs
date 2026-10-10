using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Chat;

namespace AiNexus.IntegrationTests.Identity;

public sealed class UserRateLimitsTests
{
    [Fact]
    public async Task SendingIsLimitedPerUserWithRetryAfter()
        => await AssertPerUserLimitAsync("/api/v1/runs", ChatModule.SendsPerMinute, client => client.PostAsJsonAsync("/api/v1/runs", new { }));

    [Fact]
    public async Task UploadingIsLimitedPerUserWithRetryAfter()
        => await AssertPerUserLimitAsync("/api/v1/attachments", AttachmentsModule.UploadsPerMinute, client => client.PostAsync("/api/v1/attachments", new StringContent("")));

    // The limiter runs before binding, so requests the endpoint then rejects still count.
    private static async Task AssertPerUserLimitAsync(string path, int permits, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        await using var factory = new NexusFactory();
        var alice = await factory.SignedInAsync();
        for (var i = 0; i < permits; i++)
        {
            using var allowed = await send(alice);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }
        using var limited = await send(alice);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero, path + " must say when to retry.");
        Assert.Equal("rate_limited", (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var bob = await factory.SignedInAsync("bob");
        using var other = await send(bob);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, other.StatusCode);
    }
}
