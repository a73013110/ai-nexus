using AiNexus.Data.App;
using AiNexus.Features.Office.Flow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Flow;

public sealed class FlowSeedTests
{
    // 沒有流程設定的種類送出申請會回 flow_definition_missing：新增種類時要一併在 Seed/flow.sql 加預設流程。
    [Fact]
    public async Task EveryRegisteredKindHasASeededFlowWithValidRules()
    {
        await using var factory = new AppFactory();
        using var scope = factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<FlowCatalog>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var steps = await db.FlowDefinitionSteps.AsNoTracking().ToListAsync();

        Assert.NotEmpty(catalog.Kinds);
        foreach (var kind in catalog.Kinds)
        {
            var own = steps.Where(x => x.Kind == kind.Kind).ToList();
            Assert.True(own.Count > 0, $"{kind.Kind} 沒有預設流程。");
            Assert.All(own, x => Assert.True(catalog.Rule(x.ApproverRule)?.AllowedFor(kind.Kind) == true, $"{kind.Kind} 第 {x.StepNumber} 關的規則 {x.ApproverRule} 不可用。"));
        }
    }
}
