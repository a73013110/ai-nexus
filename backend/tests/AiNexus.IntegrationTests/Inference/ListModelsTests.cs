using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Inference;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Inference;

public sealed class ListModelsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyCatalogDistinguishesUnavailableProfilesFromGroupRestrictions(bool profileInstalled)
    {
        await using var factory = new NexusFactory(
            seed: db => { db.Set<AiNexus.Features.AccessControl.GroupModelPolicy>().Add(new() { GroupId = "workspace", AllowedModelsJson = "[\"retired-provider-model\"]" }); db.SaveChanges(); },
            inference: options => { if (!profileInstalled) { options.Models[0].Id = "not-installed"; options.DefaultModelId = "not-installed"; } });
        using var client = await factory.SignedInAsync();
        var catalog = (await client.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!;
        Assert.Empty(catalog.Models);
        Assert.True(catalog.ProviderAvailable);
        Assert.Equal(profileInstalled
            ? "你的群組目前沒有可用模型，請由管理員確認群組允許的模型與目前服務設定。"
            : "系統指定的模型尚未就緒，請由管理員確認模型設定。", catalog.Notice);
        Assert.Null(catalog.Policy.DefaultModelId);
    }

    [Fact]
    public async Task MissingLockedDefaultNeverFallsBackToAnotherModel()
    {
        await using var factory = new NexusFactory(inference: options =>
        {
            options.AllowModelSelection = false;
            options.DefaultModelId = "missing";
            options.Models.Add(new() { Id = "missing", DisplayName = "尚未安裝" });
        });
        using var client = await factory.SignedInAsync();
        Assert.Empty((await client.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models);
        var conversation = await CreateConversation(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(conversation.Id, null, "hello", null, null))).StatusCode);
        Assert.Equal(0, factory.Provider.Calls);
    }

    [Fact]
    public async Task CatalogOnlyContainsInstalledApprovedModels()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var catalog = await client.GetFromJsonAsync<ModelsDto>("/api/v1/models");
        Assert.Equal("test-model", Assert.Single(catalog!.Models).Id);
        var conversation = await CreateConversation(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(conversation.Id, "not-approved", "hello", null, null))).StatusCode);
    }
}
