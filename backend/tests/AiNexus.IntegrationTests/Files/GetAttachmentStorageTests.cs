using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Files;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Files.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Files;

/// <summary>儲存上限的規則在 <c>files.udf_AttachmentStorage</c>（群組來源 <c>accesscontrol.udf_UserGroups</c>），這裡以實際資料庫逐一檢查每種來源。</summary>
public sealed class GetAttachmentStorageTests
{
    [Fact]
    public async Task LimitPrefersPersonalThenSmallestActiveGroupThenDefault()
    {
        await using var factory = new AppFactory();
        async Task<Guid> SignIn(string name)
        {
            using var client = await factory.SignedInAsync(name);
            return (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        }
        var alice = await SignIn("alice"); var bob = await SignIn("bob"); var carol = await SignIn("carol"); var dave = await SignIn("dave");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Role>().AddRange(new Role { Id = "probe", Name = "啟用" }, new Role { Id = "probe-off", Name = "停用", Enabled = false }, new Role { Id = "probe-null", Name = "未設上限" });
            db.Set<RoleGroup>().AddRange(
                new RoleGroup { Id = "probe-small", Name = "小" }, new RoleGroup { Id = "probe-large", Name = "大" },
                new RoleGroup { Id = "probe-disabled", Name = "停用群組", Enabled = false }, new RoleGroup { Id = "probe-tiny", Name = "停用角色的群組" },
                new RoleGroup { Id = "probe-null", Name = "未設上限" });
            db.Set<RoleGroupRole>().AddRange(
                new RoleGroupRole { RoleId = "probe", GroupId = "probe-small" }, new RoleGroupRole { RoleId = "probe", GroupId = "probe-large" },
                new RoleGroupRole { RoleId = "probe", GroupId = "probe-disabled" }, new RoleGroupRole { RoleId = "probe-off", GroupId = "probe-tiny" },
                new RoleGroupRole { RoleId = "probe-null", GroupId = "probe-null" });
            db.Set<GroupModelPolicy>().AddRange(
                new GroupModelPolicy { GroupId = "probe-small", StoredAttachmentLimitBytes = 3000 }, new GroupModelPolicy { GroupId = "probe-large", StoredAttachmentLimitBytes = 7000 },
                new GroupModelPolicy { GroupId = "probe-disabled", StoredAttachmentLimitBytes = 20 }, new GroupModelPolicy { GroupId = "probe-tiny", StoredAttachmentLimitBytes = 10 },
                new GroupModelPolicy { GroupId = "probe-null" });
            db.Set<UserRole>().AddRange(
                new UserRole { UserId = alice, RoleId = "probe-null" },
                new UserRole { UserId = bob, RoleId = "probe" },
                new UserRole { UserId = carol, RoleId = "probe" }, new UserRole { UserId = carol, RoleId = "probe-off" },
                new UserRole { UserId = dave, RoleId = "probe" });
            // 個人上限可以比群組寬；預約中與待清理的附件也算已用。
            (await db.Users.SingleAsync(x => x.Id == bob)).AttachmentLimitBytes = 5000;
            (await db.Users.SingleAsync(x => x.Id == dave)).Enabled = false;
            db.Set<Attachment>().AddRange(
                new Attachment { OwnerId = bob, FileName = "a.txt", ContentType = "text/plain", Size = 1000 },
                new Attachment { OwnerId = bob, FileName = "b.txt", ContentType = "text/plain", Size = 150, StorageState = AttachmentStates.Pending },
                new Attachment { OwnerId = bob, FileName = "c.txt", ContentType = "text/plain", Size = 50, StorageState = AttachmentStates.Deleting },
                new Attachment { OwnerId = carol, FileName = "d.txt", ContentType = "text/plain", Size = 3500 });
            await db.SaveChangesAsync();
        }

        using var read = factory.Services.CreateScope();
        var quota = read.ServiceProvider.GetRequiredService<AttachmentQuota>();
        var baseline = quota.DefaultLimitBytes;
        // 群組有政策列但沒設上限：仍走預設值。
        Assert.Equal(new AttachmentStorageDto(0, baseline, baseline, null, null, baseline, "default"), await quota.ForAsync(alice, CancellationToken.None));
        Assert.Equal(new AttachmentStorageDto(1200, 5000, 3800, 5000, 3000, baseline, "personal"), await quota.ForAsync(bob, CancellationToken.None));
        // 多個群組取最小值；停用的群組、停用角色帶來的群組都不算；超用時剩餘為 0。
        Assert.Equal(new AttachmentStorageDto(3500, 3000, 0, null, 3000, baseline, "group"), await quota.ForAsync(carol, CancellationToken.None));
        // 停用的使用者沒有有效群組。
        Assert.Equal(new AttachmentStorageDto(0, baseline, baseline, null, null, baseline, "default"), await quota.ForAsync(dave, CancellationToken.None));
    }
}
