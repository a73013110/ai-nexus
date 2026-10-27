using System.Data;
using System.Globalization;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Projects;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Work.Dashboard;
using AiNexus.Features.Work.Repositories;
using AiNexus.Platform.Dashboard;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Dashboard;

/// <summary>
/// 總覽每個來源一支 procedure（<see cref="DashboardProcedure"/>）。欄位與 C# record 只在執行時對應，所以逐支檢查參數與結果集形狀，
/// 再走一次 API 確認 owner 篩選、項目只列檢視者、軟刪除與檢視者時區的日界。
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
        ["repositories.usp_GetRepositoriesDashboard"] = string.Join('|', "Connections:Int32,Running:Int32", Days, Shares, "Id:Guid,Repository:String,Commit:String,CreatedAt:DateTimeOffset"),
        ["sharing.usp_GetSharingDashboard"] = string.Join('|', "Open:Int32,Expiring:Int32,Received:Int32", Days, Shares, "Id:Guid,Title:String,Kind:String,CreatedAt:DateTimeOffset,ExpiresAt:DateTimeOffset"),
        ["notifications.usp_GetNotificationsDashboard"] = string.Join('|', "Unread:Int32,Errors:Int32", Days, "Id:Guid,Title:String,Body:String,Severity:String,IssueCode:String,TargetKind:String,TargetId:Guid,CreatedAt:DateTimeOffset"),
        ["websearch.usp_GetWebSearchDashboard"] = string.Join('|', "Failed:Int32", Days),
        ["quality.usp_GetQualityDashboard"] = string.Join('|', "Up:Int32,Down:Int32,Evaluations:Int32,Retrievals:Int32,Running:Int32", Days),
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

    [Fact]
    public async Task OwnerScopeSoftDeletesViewerItemsAndViewerDaysFollowTheSharedRules()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.PostConfigure<GiteaOptions>(o => o.Enabled = true));
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var a = await Me(alice); var b = await Me(bob);
        // 檢視者在 UTC+8：期間是當地 9/30 與 10/1 兩天，From 含、Until 不含。
        var from = DateTimeOffset.Parse("2026-09-29T16:00:00Z", CultureInfo.InvariantCulture);
        var until = from.AddDays(2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var live = new Conversation { OwnerId = a, Title = "留著的對話", UpdatedAt = from };
            var deleted = new Conversation { OwnerId = a, Title = "已刪除的對話", UpdatedAt = from.AddHours(1), IsDeleted = true };
            var theirs = new Conversation { OwnerId = b, Title = "別人的對話", UpdatedAt = from.AddHours(2) };
            db.AddRange(live, deleted, theirs);
            foreach (var at in new[] { from.AddSeconds(-1), from, from.AddDays(1).AddSeconds(-1), from.AddDays(1), until })
                db.Add(new Message { ConversationId = live.Id, Role = "user", CreatedAt = at });
            db.AddRange(new Message { ConversationId = deleted.Id, Role = "user", CreatedAt = from.AddDays(1) },
                new Message { ConversationId = theirs.Id, Role = "user", CreatedAt = from });
            foreach (var (owner, gone) in new[] { (a, false), (a, true), (b, false) })
            {
                var artifact = new Resource { OwnerId = owner, Kind = Artifact.Kind, Name = gone ? "已刪除的成果" : "成果", IsDeleted = gone };
                var project = new Resource { OwnerId = owner, Kind = Project.Kind, Name = gone ? "已刪除的專案" : "專案", IsDeleted = gone, CreatedAt = from, UpdatedAt = from };
                db.AddRange(artifact, new Artifact { Id = artifact.Id }, project, new Project { Id = project.Id });
            }
            var review = new BackgroundJob { OwnerId = a, Kind = "repository-review", Status = RunStates.Running, CreatedAt = from, UpdatedAt = from };
            db.AddRange(review, new BackgroundJob { OwnerId = b, Kind = "evaluation", Status = RunStates.Failed, CreatedAt = from, UpdatedAt = from },
                new RepositoryReview { OwnerId = a, JobId = review.Id, Repository = "hanglong/nexus", Commit = new string('a', 40), IdempotencyKey = "dashboard", CreatedAt = from });
            await db.SaveChangesAsync();
        }
        const string Window = "from=2026-09-29T16:00:00Z&until=2026-10-01T16:00:00Z&offsetMinutes=480";

        var personal = await Read(alice, Window);
        var chat = Section(personal, "chat");
        Assert.Equal(1, Value(chat, "conversations"));
        Assert.Equal(["2026-09-30:2", "2026-10-01:1"], chat.Series.Single(x => x.Key == "questions").Points.Select(x => $"{x.Date}:{x.Value}"));
        Assert.Equal(["留著的對話"], chat.Items.Select(x => x.Title));
        Assert.Equal(1, Value(Section(personal, "artifacts"), "artifacts"));
        Assert.Equal(["成果"], Section(personal, "artifacts").Items.Select(x => x.Title));
        Assert.Equal(new double?[] { 1, 1 }, Values(Section(personal, "projects"), "projects", "created"));
        Assert.Equal(new double?[] { 1, 0 }, Values(Section(personal, "tasks"), "active", "failed"));
        Assert.Equal(1, Value(Section(personal, "repositories"), "running"));
        Assert.Equal("aaaaaaa", Assert.Single(Section(personal, "repositories").Items).Detail);

        // 平台範圍指定 bob：數字換成 bob 的，項目仍是檢視者（alice）自己的。
        var selected = await Read(alice, $"scope=platform&ownerId={b}&{Window}");
        chat = Section(selected, "chat");
        Assert.Equal(new double?[] { 1, 1 }, Values(chat, "conversations", "questions"));
        Assert.Equal(["留著的對話"], chat.Items.Select(x => x.Title));
        Assert.Equal(["成果"], Section(selected, "artifacts").Items.Select(x => x.Title));
        Assert.Equal(new double?[] { 0, 1 }, Values(Section(selected, "tasks"), "active", "failed"));
        Assert.Single(Section(selected, "tasks").Items);
        Assert.Equal(new double?[] { 0, 0 }, Values(Section(selected, "repositories"), "running", "reviews"));
        Assert.Single(Section(selected, "repositories").Items);

    }

    private static async Task<Guid> Me(HttpClient client) => (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;

    private static async Task<DashboardDto> Read(HttpClient client, string query) => (await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?" + query))!;

    private static DashboardSectionDto Section(DashboardDto dashboard, string id) => dashboard.Sections.Single(x => x.Id == id);

    private static double? Value(DashboardSectionDto section, string metric) => section.Metrics.Single(x => x.Key == metric).Value;

    private static double?[] Values(DashboardSectionDto section, params string[] metrics) => [.. metrics.Select(x => Value(section, x))];
}
