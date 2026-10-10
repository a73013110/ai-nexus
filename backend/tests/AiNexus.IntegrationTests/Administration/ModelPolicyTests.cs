using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Audit;
using AiNexus.Features.Chat;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Administration;

public sealed class ModelPolicyTests
{
    [Fact]
    public async Task ModelDenialAppliesToCatalogContextAndRunAndHiddenNamesStayHidden()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], inference: options => options.ShowModelNames = false);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("基本工作區", true, ["chat"], new([])))).EnsureSuccessStatusCode();
        Assert.Empty((await bob.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "test", "model-1"))).StatusCode);
        var conversation = await CreateConversation(bob);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostRun(bob, new(conversation.Id, "model-1", "test", null, null))).StatusCode);
        Assert.DoesNotContain("test-model", await (await bob.GetAsync("/api/v1/settings/model-policy")).Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnrestrictedGroupsGrantAllModelsAndBudgetsAcrossCatalogContextChatAndTasks(bool savedPolicy)
    {
        await using var factory = new NexusFactory(administrators: ["alice"],
            seed: db =>
            {
                db.Add(new GroupModelPolicy { GroupId = BuiltInAccess.WorkspaceGroup, AllowedModelsJson = "[\"test-model\"]", DailyTokenLimitsJson = "{\"test-model\":0}" });
                if (savedPolicy) db.Add(new GroupModelPolicy { GroupId = AdministrationConfiguration.Group });
                db.SaveChanges();
            },
            inference: options => options.Models.Add(new() { Id = "not-approved", DisplayName = "另一模型" }));
        using var admin = await factory.SignedInAsync();
        var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var effective = (await admin.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Null(effective.AllowedModelIds);
        Assert.All(effective.Models, model => { Assert.Null(model.DailyTokenLimit); Assert.Equal("unlimited", model.Source); });
        Assert.Equal(2, (await admin.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models.Count);
        (await admin.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "準備", "not-approved"))).EnsureSuccessStatusCode();
        var conversation = await CreateConversation(admin);
        var accepted = await PostRun(admin, new(conversation.Id, "not-approved", "開始回答", null, null));
        accepted.EnsureSuccessStatusCode();
        await WaitForTerminal(admin, (await accepted.Content.ReadFromJsonAsync<RunDto>())!.Id);
        var task = await factory.Services.GetRequiredService<ModelTaskService>().GenerateAsync(owner, "transform", "短文", "改寫", CancellationToken.None, model: "test-model");
        Assert.NotEmpty(task.Value!.Text);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest(["test-model"], new Dictionary<string, long> { ["test-model"] = 0 }))).EnsureSuccessStatusCode();
        Assert.Equal("test-model", Assert.Single((await admin.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "禁止", "not-approved"))).StatusCode);
        var quota = await factory.Services.GetRequiredService<ModelTaskService>().GenerateAsync(owner, "ocr", "短文", "辨識", CancellationToken.None);
        Assert.Equal("model_token_quota", quota.Error?.Code);
    }

    [Fact]
    public async Task RestrictedGroupsAccumulateAndRevocationRemovesOnlyThatGroupsModelsAndBudgets()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], inference: options => options.Models.Add(new() { Id = "not-approved", DisplayName = "另一模型" }));
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("工作區", true, ["chat"], new(["test-model"], new Dictionary<string, long> { ["test-model"] = 1000, ["not-approved"] = 1 })))).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.AddRange(new RoleGroup { Id = "specialists", Name = "專業工作區" },
                new RoleGroupRole { RoleId = BuiltInAccess.MemberRole, GroupId = "specialists" },
                new GroupModelPolicy { GroupId = "specialists", AllowedModelsJson = "[\"test-model\",\"not-approved\"]", DailyTokenLimitsJson = "{\"test-model\":2000,\"not-approved\":3000}" });
            await db.SaveChangesAsync();
        }
        var effective = (await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Equal(2, effective.AllowedModelIds!.Count);
        Assert.Equal(2000, effective.Models.Single(x => x.ModelId == "test-model").DailyTokenLimit);
        Assert.Equal(3000, effective.Models.Single(x => x.ModelId == "not-approved").DailyTokenLimit);
        Assert.Equal(2, (await bob.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models.Count);
        (await bob.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "準備", "not-approved"))).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            (await db.Set<RoleGroup>().SingleAsync(x => x.Id == "specialists")).Enabled = false;
            await db.SaveChangesAsync();
        }
        effective = (await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Equal("test-model", Assert.Single(effective.AllowedModelIds!));
        Assert.Equal(1000, effective.Models.Single(x => x.ModelId == "test-model").DailyTokenLimit);
        Assert.Equal("test-model", Assert.Single((await bob.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "禁止", "not-approved"))).StatusCode);
        var denied = await factory.Services.GetRequiredService<ModelTaskService>().GenerateAsync(owner, "transform", "短文", "改寫", CancellationToken.None, model: "not-approved");
        Assert.Equal("model_group_forbidden", denied.Error?.Code);
    }

    [Fact]
    public async Task PersonalBudgetsOverrideGroupsAndPersonalModelsNarrowGroupGrants()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], inference: options => options.Models.Add(new() { Id = "not-approved", Provider = "google", ProviderModelId = "not-approved", DisplayName = "另一模型" }));
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("工作區", true, ["chat"], new(["test-model"], new Dictionary<string, long> { ["test-model"] = 1000, ["not-approved"] = 500 })))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest(["test-model", "not-approved"], new Dictionary<string, long> { ["test-model"] = 2000 }))).EnsureSuccessStatusCode();
        var policy = (await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Equal("test-model", Assert.Single(policy.AllowedModelIds!));
        Assert.Equal(2000, policy.Models.Single(x => x.ModelId == "test-model").DailyTokenLimit);
        Assert.Equal("personal", policy.Models.Single(x => x.ModelId == "test-model").Source);
        Assert.Null(policy.Models.Single(x => x.ModelId == "not-approved").DailyTokenLimit);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest())).EnsureSuccessStatusCode();
        policy = (await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Equal(1000, policy.Models.Single(x => x.ModelId == "test-model").DailyTokenLimit);
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit"))!;
        Assert.Contains(audit, x => x.Action == "admin.user_model_policy" && x.DetailsJson!.Contains("2000"));
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest())).StatusCode);
    }
}
