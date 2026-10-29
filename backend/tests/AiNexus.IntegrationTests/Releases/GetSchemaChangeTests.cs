using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Releases;
using AiNexus.Features.Admin.Releases.SchemaChanges;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

/// <summary>經端點在 LocalDB 實際呼叫 releases.usp_GetSchemaObject，檢查兩邊定義的對應。</summary>
public sealed class GetSchemaChangeTests
{
    [Fact]
    public async Task ExpectedAndActualDefinitionsAreReturnedSideBySide()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        await SnapshotAsync(factory);
        await HotfixProcedureAsync(factory, "operations", "usp_ListAdminUsers");
        await ExecuteAsync(factory, "CREATE TABLE [dbo].[Users_bak] ([Id] int NOT NULL);");

        var procedure = (await alice.GetFromJsonAsync<SchemaObjectDto>("/api/v1/admin/schema-changes/operations/usp_ListAdminUsers", TestContext.Current.CancellationToken))!;
        Assert.Equal("procedure", procedure.Type);
        Assert.DoesNotContain("學校端修正", procedure.Expected);
        Assert.Contains("學校端修正", procedure.Actual);

        var added = (await alice.GetFromJsonAsync<SchemaObjectDto>("/api/v1/admin/schema-changes/dbo/Users_bak", TestContext.Current.CancellationToken))!;
        Assert.Null(added.Expected);
        Assert.Contains("[Id] int NOT NULL", added.Actual);

        await AssertCodeAsync(await alice.GetAsync("/api/v1/admin/schema-changes/dbo/Missing", TestContext.Current.CancellationToken),
            HttpStatusCode.NotFound, ReleasesErrors.SchemaObjectNotFound.Code);
    }
}
