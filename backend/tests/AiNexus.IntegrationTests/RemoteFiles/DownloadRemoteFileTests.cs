using System.Net;
using AiNexus.Features.Platform.RemoteFiles;
using AiNexus.Platform.RemoteFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.RemoteFiles;

/// <summary>以憑證取遠端檔案：憑證限發給的使用者、有效期間內，內容與錯誤由來源決定，回應帶預覽或下載的標頭。</summary>
public sealed class DownloadRemoteFileTests
{
    private const string Content = "/api/v1/remote-files/content?ticket=";

    private sealed class Scenario : IAsyncDisposable
    {
        public FakeRemoteFiles Files { get; } = new("test");
        public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 22, 2, 0, 0, TimeSpan.Zero));
        public AppFactory Factory { get; }

        public Scenario() => Factory = new AppFactory(clock: Clock, services: Files.Register);

        /// <summary>發給 <paramref name="account"/> 一張憑證（跳過發憑證端的權限判斷，那是呼叫端的事）。</summary>
        public async Task<string> TicketAsync(string account, string reference, string fileName)
        {
            var user = await OrganizationApi.UserIdAsync(Factory, account);
            var file = Factory.Services.GetRequiredService<RemoteFileTickets>().Issue(new RemoteFileReference(Files.Name, reference), fileName, user);
            return Uri.EscapeDataString(file.Ticket);
        }

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    [Fact]
    public async Task AValidTicketStreamsTheFileInlineOrAsADownload()
    {
        await using var s = new Scenario();
        s.Files.Files["a"] = new(RemoteFileStatus.Found, [0x89, 0x50, 0x4E, 0x47]);
        using var client = await s.Factory.SignedInAsync("alice");
        var ticket = await s.TicketAsync("alice", "a", "相片.png");

        var inline = await client.GetAsync(Content + ticket);
        var download = await client.GetAsync(Content + ticket + "&download=true");

        Assert.Equal(HttpStatusCode.OK, inline.StatusCode);
        Assert.Equal("image/png", inline.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], await inline.Content.ReadAsByteArrayAsync());
        // 內嵌預覽不帶 Content-Disposition；兩者都套上傳檔案的沙箱 CSP。
        Assert.Null(inline.Content.Headers.ContentDisposition);
        Assert.Contains("sandbox", inline.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("相片.png", download.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task TicketsOfOtherUsersExpiredOrTamperedWithAreNotFound()
    {
        await using var s = new Scenario();
        s.Files.Files["a"] = new(RemoteFileStatus.Found, [1]);
        using var bob = await s.Factory.SignedInAsync("bob");
        using var alice = await s.Factory.SignedInAsync("alice");
        var ticket = await s.TicketAsync("alice", "a", "a.pdf");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(Content + ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync(Content + ticket[..^4] + "AAAA")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync(Content + new string('A', 2001))).StatusCode);

        // 預設 30 分鐘到期。
        s.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync(Content + ticket)).StatusCode);
        Assert.Empty(s.Files.Reads);
    }

    [Theory]
    [InlineData(RemoteFileStatus.NotFound, HttpStatusCode.NotFound, "remote_file_not_found")]
    [InlineData(RemoteFileStatus.TooLarge, HttpStatusCode.RequestEntityTooLarge, "remote_file_too_large")]
    [InlineData(RemoteFileStatus.Unavailable, HttpStatusCode.ServiceUnavailable, "remote_file_unavailable")]
    public async Task SourceFailuresBecomePublicErrors(RemoteFileStatus status, HttpStatusCode expected, string code)
    {
        await using var s = new Scenario();
        s.Files.Files["a"] = new(status);
        using var client = await s.Factory.SignedInAsync("alice");

        var response = await client.GetAsync(Content + await s.TicketAsync("alice", "a", "a.pdf"));

        Assert.Equal(expected, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
