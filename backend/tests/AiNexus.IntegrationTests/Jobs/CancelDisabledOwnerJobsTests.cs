using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Work.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Jobs;

public sealed class CancelDisabledOwnerJobsTests
{
    [Fact]
    public async Task DisablingAnAccountRequestsCancellationOfItsUnfinishedJobsOnly()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var aliceId = (await Login(admin, "alice")).Id;
        var testerId = await CreateUser(admin, LocalAccount());
        Guid queued, running, finished;
        using (var scope = f.Services.CreateScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<JobService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            queued = jobs.Enqueue(testerId, null, Guid.NewGuid(), "fixture", "排隊中").Id;
            running = jobs.Enqueue(testerId, null, Guid.NewGuid(), "fixture", "執行中").Id;
            var done = jobs.Enqueue(testerId, null, Guid.NewGuid(), "fixture", "已完成"); finished = done.Id;
            jobs.Enqueue(aliceId, null, Guid.NewGuid(), "fixture", "別人的");
            await db.SaveChangesAsync();
            await db.Set<BackgroundJob>().Where(x => x.Id == running).ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, RunStates.Running));
            await db.Set<BackgroundJob>().Where(x => x.Id == finished).ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, RunStates.Completed).SetProperty(x => x.ActiveKey, (string?)null));
        }

        // 只改姓名不算停用，工作照常。
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{testerId}", LocalAccount() with { DisplayName = "改名", Password = null })).EnsureSuccessStatusCode();
        Assert.Empty(await CancelledAsync());
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{testerId}", LocalAccount() with { Enabled = false, Password = null })).EnsureSuccessStatusCode();
        Assert.Equal(new[] { queued, running }.Order(), await CancelledAsync());

        async Task<Guid[]> CancelledAsync()
        {
            using var scope = f.Services.CreateScope();
            return [.. (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<BackgroundJob>().Where(x => x.CancelRequested).Select(x => x.Id).ToListAsync()).Order()];
        }
    }
}
