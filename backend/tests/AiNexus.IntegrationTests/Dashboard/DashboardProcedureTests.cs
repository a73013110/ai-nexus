using System.Data;
using System.Globalization;
using AiNexus.Platform.Dashboard;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Dashboard;

/// <summary>
/// 總覽每個來源一支 procedure（<see cref="DashboardProcedure"/>）。欄位與 C# record 只在執行時對應，所以逐支檢查參數與結果集形狀。
/// </summary>
public sealed class DashboardProcedureTests
{
    private const string Days = "Day:DateTime,Value:Int32";
    private const string Shares = "Key:String,Value:Int32";
    private const string Resources = "Id:Guid,Name:String,UpdatedAt:DateTimeOffset";

    /// <summary>procedure 與它的結果集：每個結果集是「欄位:CLR 型別」，以 | 分隔；第一個一定是一列計數。</summary>
    private static readonly Dictionary<string, string> Shapes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["conversations.usp_GetChatDashboard"] = string.Join('|', "Conversations:Int32,Favorites:Int32,Archived:Int32,Generating:Int32,Completed:Int32,Failed:Int32,Cancelled:Int32,LatencyMilliseconds:Double", Days, Shares, "Id:Guid,Title:String,UpdatedAt:DateTimeOffset,IsFavorite:Boolean"),
        ["artifacts.usp_GetArtifactsDashboard"] = string.Join('|', "Artifacts:Int32,InProjects:Int32,FromChat:Int32", Days, Resources),
        ["files.usp_GetFilesDashboard"] = string.Join('|', "Files:Int32,Bytes:Int64", Days, Shares),
        ["jobs.usp_GetJobsDashboard"] = string.Join('|', "Queued:Int32,Running:Int32,Failed:Int32,Completed:Int32", Days, Shares, "Id:Guid,Label:String,Kind:String,Stage:String,Status:String,UpdatedAt:DateTimeOffset"),
        ["knowledge.usp_GetKnowledgeDashboard"] = string.Join('|', "Collections:Int32,Documents:Int32,Ready:Int32,Failed:Int32,Pages:Int32,Chunks:Int32", Days, Resources),
        ["projects.usp_GetProjectsDashboard"] = string.Join('|', "Projects:Int32,Archived:Int32,Templates:Int32,Shared:Int32", Days, Resources),
    };

    public static TheoryData<string> Procedures => [.. Shapes.Keys];

    [Theory]
    [MemberData(nameof(Procedures))]
    public async Task ProcedureTakesTheSharedParametersAndReturnsTheResultSetsItsSourceReads(string procedure)
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var connection = db.Database.GetDbConnection();
            var declared = (await connection.QueryAsync<string>(
                "SELECT p.name FROM sys.parameters AS p WHERE p.object_id = OBJECT_ID(@procedure) ORDER BY p.parameter_id", new { procedure })).ToList();
            Assert.Equal(["@Owner", "@Actor", "@From", "@Until", "@OffsetMinutes"], declared.Take(5));
            var from = DateTimeOffset.Parse("2026-09-29T16:00:00Z", CultureInfo.InvariantCulture);
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["@Actor"] = Guid.NewGuid(), ["@From"] = from, ["@Until"] = from.AddDays(30), ["@OffsetMinutes"] = 480, ["@Now"] = from, ["@ExpiringBefore"] = from.AddDays(7),
            };
            // 全平台（@Owner NULL）與單一擁有者各跑一次：兩種範圍的結果集形狀相同，計數都只有一列。
            foreach (Guid? owner in new Guid?[] { null, Guid.NewGuid() })
            {
                var parameters = new DynamicParameters();
                parameters.Add("Owner", owner, DbType.Guid);
                foreach (var name in declared.Skip(1)) parameters.Add(name[1..], values[name]);
                var sets = new List<string>();
                var countRows = 0;
                await using (var reader = await connection.ExecuteReaderAsync(procedure, parameters, commandType: CommandType.StoredProcedure))
                {
                    do
                    {
                        sets.Add(string.Join(',', Enumerable.Range(0, reader.FieldCount).Select(i => $"{reader.GetName(i)}:{reader.GetFieldType(i).Name}")));
                        var rows = 0;
                        while (await reader.ReadAsync()) rows++;
                        if (sets.Count == 1) countRows = rows;
                    } while (await reader.NextResultAsync());
                }
                Assert.Equal(Shapes[procedure].Split('|'), sets);
                Assert.Equal(1, countRows);
            }
        });
    }

    [Fact]
    public async Task EveryDashboardProcedureInTheDatabaseHasItsShapeChecked()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var found = await db.Database.GetDbConnection().QueryAsync<string>(
                "SELECT SCHEMA_NAME(p.schema_id) + N'.' + p.name FROM sys.procedures AS p WHERE p.name LIKE N'usp[_]Get%Dashboard'");
            Assert.Equal(Shapes.Keys.Order(StringComparer.OrdinalIgnoreCase), found.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        });
    }
}
