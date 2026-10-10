using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Conversations;

public sealed class ImportConversationTests
{
    [Fact]
    public async Task JsonBackupRecreatesTheTreeWithoutProviderIds()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var original = await CreateConversation(client);
        await WaitForTerminal(client, (await CreateRun(client, original.Id, "backup prompt")).Id);
        var backup = (await client.GetFromJsonAsync<ConversationBackup>($"/api/v1/conversations/{original.Id}/export"))!;
        var importedResponse = await client.PostAsJsonAsync("/api/v1/conversations/import", backup);
        importedResponse.EnsureSuccessStatusCode();
        var imported = (await importedResponse.Content.ReadFromJsonAsync<ConversationDto>())!;
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{imported.Id}"))!;
        Assert.Equal(2, detail.Messages.Count);
        Assert.DoesNotContain(detail.Messages, x => backup.Messages.Any(y => y.Id == x.Id));
        Assert.All(detail.Messages, x => Assert.Null(x.ModelId));
        Assert.NotNull(imported.ActiveLeafId);
    }

    [Fact]
    public async Task ImportRejectsCyclicAndCrossTreeParents()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var backup = new ConversationBackup(1, "bad", "", [], second, [new(first, second, "user", "x", "completed", DateTimeOffset.UtcNow, []), new(second, first, "assistant", "y", "completed", DateTimeOffset.UtcNow, [])]);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/conversations/import", backup)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
    }
}
