using System.Net.Http.Json;
using AiNexus.Features.Admin.Releases.SchemaChanges;
using AiNexus.Features.Admin.Releases.Versions;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

/// <summary>經端點在 LocalDB 實際呼叫 releases.usp_ListSchemaChanges，檢查回傳欄位的對應與異動判定。</summary>
public sealed class ListSchemaChangesTests
{
    private const string Changes = "/api/v1/admin/schema-changes";

    [Fact]
    public async Task ObjectsChangedOnTheServerAfterTheReleaseAreListedByKind()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        // 沒有快照的庫無從比較，不能把整個結構都列成新增。
        Assert.Empty((await alice.GetFromJsonAsync<SchemaChangeDto[]>(Changes, TestContext.Current.CancellationToken))!);

        await SnapshotAsync(factory);
        Assert.True((await alice.GetFromJsonAsync<ReleaseStatusDto>("/api/v1/admin/release-status", TestContext.Current.CancellationToken))!.HasSnapshot);
        Assert.Empty((await alice.GetFromJsonAsync<SchemaChangeDto[]>(Changes, TestContext.Current.CancellationToken))!);

        await HotfixProcedureAsync(factory, "operations", "usp_ListAdminUsers");
        await ExecuteAsync(factory, "CREATE TABLE [dbo].[Users_bak] ([Id] int NOT NULL);");
        await ExecuteAsync(factory, "DROP VIEW [attendance].[vw_AttributedPunches];");
        await ExecuteAsync(factory, "ALTER TABLE [organization].[Units] ADD [Note] nvarchar(20) NULL;");

        var changes = (await alice.GetFromJsonAsync<SchemaChangeDto[]>(Changes, TestContext.Current.CancellationToken))!;
        Assert.Equal(
            ["added table dbo.Users_bak", "modified procedure operations.usp_ListAdminUsers", "modified table organization.Units", "removed view attendance.vw_AttributedPunches"],
            changes.Select(x => $"{x.Change} {x.Type} {x.SchemaName}.{x.Name}").Order(StringComparer.Ordinal));
        // 刪除只知道不見了，沒有時間；其他取 sys.objects 的時間。
        Assert.All(changes, x => Assert.Equal(x.Change == "removed", x.ChangedAt is null));
    }
}
