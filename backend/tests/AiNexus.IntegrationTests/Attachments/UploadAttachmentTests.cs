using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Attachments;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Attachments;

public sealed class UploadAttachmentTests
{
    [Fact]
    public async Task NewUploadsReclaimExpiredDraftsAndNeverRemoveLinkedHistory()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var draft = await Upload(client, "abandoned.txt", Encoding.UTF8.GetBytes("old draft"));
        var history = await Upload(client, "retained.txt", Encoding.UTF8.GetBytes("retained history"));
        var conversation = await CreateConversation(client);
        await RunWithAttachment(client, conversation.Id, history.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var files = await db.Set<Attachment>().ToListAsync();
            foreach (var file in files) file.CreatedAt = DateTimeOffset.UtcNow.AddDays(-15);
            await db.SaveChangesAsync();
        }
        await Upload(client, "current.txt", Encoding.UTF8.GetBytes("current"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/attachments/{draft.Id}")).StatusCode);
        Assert.Equal(Encoding.UTF8.GetBytes("retained history"), await client.GetByteArrayAsync($"/api/v1/attachments/{history.Id}/content"));
    }

    [Fact]
    public async Task AttachmentOwnershipProtectsMetadataDownloadDeletionAndGeneration()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var file = await Upload(alice, "private.txt", Encoding.UTF8.GetBytes("private"));
        var conversation = await CreateConversation(bob);
        foreach (var suffix in new[] { "", "/content" }) Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/attachments/{file.Id}{suffix}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostRun(bob, new(conversation.Id, "test-model", "steal", null, null, AttachmentIds: [file.Id]))).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages);
    }

    [Theory]
    [InlineData("fake.png", "not an image")]
    [InlineData("evil.svg", "<svg></svg>")]
    [InlineData("invalid.pdf", "not pdf")]
    [InlineData("empty.txt", " ")]
    public async Task InvalidFilesAreRejected(string name, string content)
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadResponse(client, name, Encoding.UTF8.GetBytes(content))).StatusCode);
    }

    [Fact]
    public async Task UploadRequiresCsrfAndRejectsOversizedFiles()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadResponse(client, "large.txt", new byte[4 * 1024 * 1024 + 1])).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadResponse(client, "private.txt", Encoding.UTF8.GetBytes("private"))).StatusCode);
    }
}
