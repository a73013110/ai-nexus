using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.Operations;
using AiNexus.Features.Platform.Account;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Operations;

public sealed class GetAdminUsageTests
{
    [Fact]
    public async Task StoredOriginalsCountOnlyReadyAttachments()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Attachments.AddRange(
                new Attachment { OwnerId = owner, FileName = "ready.txt", ContentType = "text/plain", Size = 10 },
                new Attachment { OwnerId = owner, FileName = "uploading.txt", ContentType = "text/plain", Size = 100, StorageState = AttachmentStates.Pending },
                new Attachment { OwnerId = owner, FileName = "removing.txt", ContentType = "text/plain", Size = 1000, StorageState = AttachmentStates.Deleting });
            await db.SaveChangesAsync();
        }

        var usage = (await admin.GetFromJsonAsync<AdminUsageDto>("/api/v1/admin/usage"))!;

        Assert.Equal(1, usage.StoredFiles);
        Assert.Equal(10, usage.StoredBytes);
    }
}
