using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Account;
using AiNexus.Features.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Jobs;

public sealed class JobLifecycleTests
{
    [Fact]
    public async Task JobsAreOwnerScopedCancellationIsDurableAndRetryRequiresCsrf()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var collection = await CreateCollection(alice); var document = await AddDocument(alice, collection.Resource.Id, "Cancellation sample document.");
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/jobs/{document.JobId}/cancel", null)).StatusCode);
        (await alice.PostAsync($"/api/v1/jobs/{document.JobId}/cancel", null)).EnsureSuccessStatusCode(); await Process(factory);
        Assert.Equal("cancelled", (await alice.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!.Status);
        Assert.Equal(0, factory.Embeddings.Calls);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsync($"/api/v1/jobs/{document.JobId}/retry", null)).StatusCode);
    }

    /// <summary>
    /// 同一對象同一種工作只能有一筆進行中（<c>ActiveKey</c> 的 filtered unique index）；結束的工作清掉 <c>ActiveKey</c> 後可以再排入。
    /// EF 組態不宣告索引，所以只有對真實資料庫才驗證得到。
    /// </summary>
    [Fact]
    public async Task DatabaseAllowsOneActiveJobPerSubjectAndKind()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var subject = Guid.NewGuid();
        BackgroundJob first;
        using (var scope = factory.Services.CreateScope())
        {
            first = scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(owner, null, subject, "fixture", "第一筆");
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(owner, null, subject, "fixture", "重複");
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync());
            Assert.Equal(2601, Assert.IsType<SqlException>(error.InnerException).Number);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<BackgroundJob>().Where(x => x.Id == first.Id).ExecuteUpdateAsync(x => x.SetProperty(j => j.ActiveKey, (string?)null));
            scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(owner, null, subject, "fixture", "再排入");
            await db.SaveChangesAsync();
        }
    }
}
