using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.ModelTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Inference;

public sealed class ModelInvocationRetentionTests
{
    [Fact]
    public async Task InvocationsPastRetentionAreDeleted()
    {
        await using var f = new AppFactory();
        using var client = await f.SignedInAsync();
        var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new ModelInvocation { OwnerId = owner, Kind = "ocr", ModelId = "google/expired", Status = "completed", CreatedAt = DateTimeOffset.UtcNow.AddDays(-401) });
            db.Add(new ModelInvocation { OwnerId = owner, Kind = "ocr", ModelId = "google/kept", Status = "completed", CreatedAt = DateTimeOffset.UtcNow.AddDays(-399) });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await f.InScopeAsync((ModelInvocationRetentionTask task) => task.DeleteExpiredAsync(CancellationToken.None)));

        using (var scope = f.Services.CreateScope())
            Assert.Equal(["google/kept"], await scope.ServiceProvider.GetRequiredService<AppDbContext>().ModelInvocations.Select(x => x.ModelId).ToListAsync());
    }

    /// <summary>用量與花費報表讀這張表，保存期的下限不能短於報表查得到的最長期間，否則較早的日子會少算。</summary>
    [Fact]
    public void RetentionCannotBeShorterThanTheLongestReportPeriod()
    {
        var now = DateTimeOffset.UtcNow;
        var days = 1;
        while (SpendPeriod.Create(now.AddDays(-(days + 1)), now, null, now).IsSuccess) days++;
        Assert.False(SpendPeriod.Create(now.AddDays(-(days + 1)), now, null, now).IsSuccess);

        Assert.True(Valid(days));
        Assert.False(Valid(days - 1));

        static bool Valid(int retention)
        {
            var options = new InferenceOptions { InvocationRetentionDays = retention };
            var results = new List<ValidationResult>();
            Validator.TryValidateProperty(options.InvocationRetentionDays,
                new ValidationContext(options) { MemberName = nameof(InferenceOptions.InvocationRetentionDays) }, results);
            return results.Count == 0;
        }
    }
}
