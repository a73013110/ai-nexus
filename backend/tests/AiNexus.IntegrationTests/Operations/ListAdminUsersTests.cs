using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Inference;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Admin.Operations;

namespace AiNexus.IntegrationTests.Operations;

public sealed class ListAdminUsersTests
{
    private const string Procedure = "operations.usp_ListAdminUsers";

    [Fact]
    public async Task ProcedureListMatchesTheServicesItReplacesForRolesUsageConversationsAndStorage()
    {
        var audits = new ProcedureAudits();
        await using var factory = new AppFactory(administrators: ["alice"], services: audits.Register);
        using var admin = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob"); using var carol = await factory.SignedInAsync("carol"); using var dave = await factory.SignedInAsync("dave");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var carolId = (await carol.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var daveId = (await dave.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync(x => x.Id == bobId)).AttachmentLimitBytes = 5000;
            (await db.Users.SingleAsync(x => x.Id == daveId)).DeletedAt = DateTimeOffset.UtcNow;
            db.Set<Attachment>().Add(new() { OwnerId = bobId, FileName = "a.txt", ContentType = "text/plain", Size = 1200 });
            db.Set<ModelInvocation>().AddRange(
                new() { OwnerId = bobId, Kind = "evaluation", ModelId = "test-model", Status = "completed", InputTokens = 600, OutputTokens = 200, DurationMilliseconds = 50 },
                new() { OwnerId = bobId, Kind = "ocr", ModelId = "test-model", Status = "failed" },
                new() { OwnerId = bobId, Kind = "transform", ModelId = "test-model", Status = "completed", InputTokens = 900, OutputTokens = 500, CreatedAt = DateTimeOffset.UtcNow.AddDays(-40) });
            db.Conversations.AddRange(new Conversation { OwnerId = bobId }, new Conversation { OwnerId = bobId, IsDeleted = true });
            // carol 經由啟用的角色與群組取得群組儲存上限；停用的群組不算。
            db.Set<Role>().Add(new() { Id = "storage-probe", Name = "儲存測試" });
            db.Set<RoleGroup>().AddRange(new RoleGroup { Id = "storage-probe", Name = "儲存測試" }, new RoleGroup { Id = "storage-off", Name = "停用", Enabled = false });
            db.Set<RoleGroupRole>().AddRange(new RoleGroupRole { RoleId = "storage-probe", GroupId = "storage-probe" }, new RoleGroupRole { RoleId = "storage-probe", GroupId = "storage-off" });
            db.Set<GroupModelPolicy>().AddRange(new GroupModelPolicy { GroupId = "storage-probe", StoredAttachmentLimitBytes = 7000 }, new GroupModelPolicy { GroupId = "storage-off", StoredAttachmentLimitBytes = 10 });
            db.Set<UserRole>().Add(new() { UserId = carolId, RoleId = "storage-probe" });
            await db.SaveChangesAsync();
        }

        var page = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users"))!;
        Assert.Equal([@"TEST\alice", @"TEST\bob", @"TEST\carol"], page.Users.Select(x => x.Account));
        Assert.Equal(3, page.Total);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = page.Users.Select(x => x.Id).ToArray();
            var quota = scope.ServiceProvider.GetRequiredService<AttachmentQuota>();
            var usage = (await scope.ServiceProvider.GetRequiredService<UsageReports>().ByOwnersAsync(ids, CancellationToken.None)).ToDictionary(x => x.OwnerId, x => x.Usage);
            foreach (var user in page.Users)
            {
                Assert.Equal(await quota.ForAsync(user.Id, CancellationToken.None), user.Storage);
                Assert.Equal(usage.GetValueOrDefault(user.Id) ?? new(0, 0, 0, 0, 0, 0, 0), user.Activity!.Usage);
                Assert.Equal(await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).CountAsync(x => x.OwnerId == user.Id), user.Activity.Conversations);
                Assert.Equal(await db.Set<UserRole>().Where(x => x.UserId == user.Id).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync(), user.RoleIds);
            }
        }
        var bobRow = page.Users.Single(x => x.Id == bobId);
        Assert.Equal(new UsageTotalsDto(2, 1, 1, 0, 600, 200, 1, 50, 1), bobRow.Activity!.Usage);
        Assert.Equal(2, bobRow.Activity.Conversations);
        Assert.Equal(new AttachmentStorageDto(1200, 5000, 3800, 5000, null, bobRow.Storage!.DefaultLimitBytes, "personal"), bobRow.Storage);
        var carolRow = page.Users.Single(x => x.Id == carolId);
        Assert.Equal(("group", 7000L), (carolRow.Storage!.LimitSource, carolRow.Storage.LimitBytes));
        Assert.Contains("storage-probe", carolRow.RoleIds);
        Assert.Equal("default", page.Users.Single(x => x.Account == @"TEST\alice").Storage!.LimitSource);

        var found = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users?search=BO"))!;
        Assert.Equal((1, bobId), (found.Total, Assert.Single(found.Users).Id));
        Assert.Equal(0, (await admin.GetFromJsonAsync<AdminUsersDto>($"/api/v1/admin/users?search={Uri.EscapeDataString("%")}"))!.Total);
        var beyond = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users?offset=100"))!;
        Assert.Equal((3, 100), (beyond.Total, beyond.Offset)); Assert.Empty(beyond.Users);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/users?offset=-1")).StatusCode);
        audits.AssertReadOnly(Procedure);
    }
}
