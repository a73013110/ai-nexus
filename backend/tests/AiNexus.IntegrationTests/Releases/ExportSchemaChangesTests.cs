using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

public sealed class ExportSchemaChangesTests
{
    [Fact]
    public async Task ChangedProceduresAreRebuiltFromTheirCurrentDefinitionAndTablesAreOnlyDescribed()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        await SnapshotAsync(factory);
        await HotfixProcedureAsync(factory, "operations", "usp_ListAdminUsers");
        await ExecuteAsync(factory, "ALTER TABLE [organization].[Units] ADD [Note] nvarchar(20) NULL;");
        await ExecuteAsync(factory, "DROP VIEW [attendance].[vw_AttributedPunches];");

        var response = await alice.GetAsync("/api/v1/admin/schema-changes/export", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("ai-nexus-schema-changes.sql", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        // SSMS 要靠 BOM 才會用 UTF-8 開啟。
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var text = System.Text.Encoding.UTF8.GetString(bytes[3..]).ReplaceLineEndings("\n");
        Assert.Contains("DROP PROCEDURE IF EXISTS [operations].[usp_ListAdminUsers];\nGO", text);
        Assert.Contains("學校端修正", text);
        Assert.Contains("/* 資料表目前的定義（不是可執行的 DDL）：", text);
        Assert.Contains("[Note] nvarchar(20) NULL", text);
        Assert.Contains("-- removed view [attendance].[vw_AttributedPunches]", text);
        Assert.DoesNotContain("DROP VIEW", text);
    }
}
