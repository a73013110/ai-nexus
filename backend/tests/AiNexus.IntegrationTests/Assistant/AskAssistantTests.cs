using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Features.Platform.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Assistant;

/// <summary>
/// 全站助理，以出勤的工具驗證：在出勤紀錄頁提問時，模型先拿到畫面那一份資料（範圍外的人不會出現）；
/// 模型支援工具時可以再呼叫工具，結果的編號接續、各自稽核。回答以 SSE 逐段送出。
/// </summary>
public sealed class AskAssistantTests
{
    private const string Questions = "/api/v1/assistant/questions";
    private const string Page = "attendance.records";

    private static JsonElement View(string from = "2026-10-19", string to = "2026-10-21")
        => JsonSerializer.SerializeToElement(new { from, to });

    /// <summary>bob 21 日沒刷卡也沒請假（未補足）；carol 都準時、沒有出勤紀錄的權限。hr 看全公司，bob 只看自己。</summary>
    private static async Task<RecordsSession> ArrangeAsync(bool tools = false)
    {
        var session = Create(services: tools
            ? collection => collection.PostConfigure<InferenceOptions>(options => options.Models[0].SupportsTools = true)
            : null);
        var hr = await session.PersonAsync("hr");
        var bob = await session.PersonAsync("bob");
        var carol = await session.PersonAsync("carol");
        await session.GrantAsync(hr, DataScopes.All);
        await session.GrantAsync(bob, DataScopes.Own);
        await session.HoldAsync(bob, "A1");
        await session.HoldAsync(carol, "A2");
        await session.PunchAsync("A1", At(19, 8, 30), At(19, 18, 0), At(20, 8, 30), At(20, 18, 0));
        await session.PunchAsync("A2", At(19, 8, 30), At(19, 18, 0), At(20, 8, 30), At(20, 18, 0), At(21, 8, 30), At(21, 18, 0));
        return session;
    }

    private static async Task<List<(string Name, AssistantEventDto Data)>> ReadEventsAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var events = new List<(string, AssistantEventDto)>();
        string? name = null;
        foreach (var line in (await response.Content.ReadAsStringAsync()).Split('\n'))
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal)) name = line[7..];
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
                events.Add((name!, JsonSerializer.Deserialize<AssistantEventDto>(line[6..], JsonSerializerOptions.Web)!));
        }
        return events;
    }

    [Fact]
    public async Task TheAnswerStreamsAfterThePageFactsTheModelWasGiven()
    {
        await using var session = await ArrangeAsync();
        using var hr = await session.Factory.SignedInAsync("hr");

        var events = await ReadEventsAsync(await hr.PostAsJsonAsync(Questions, new AssistantQuestion("誰還沒補足？", Page: Page, View: View())));

        var (name, grounding) = events[0];
        Assert.Equal("grounding", name);
        Assert.Equal("2026/10/19–10/21 · 2 人", grounding.Scope);
        Assert.False(grounding.Partial);
        Assert.Contains(grounding.Facts!, x => x.Label.StartsWith("bob", StringComparison.Ordinal) && x.Text.Contains("未補足 1 天", StringComparison.Ordinal));
        Assert.Equal("這是測試回答。", string.Concat(events.Where(x => x.Name == "delta").Select(x => x.Data.Text)));
        Assert.Equal(("done", false), (events[^1].Name, events[^1].Data.Truncated));

        // 交給模型的事實與送給畫面的依據是同一份、同樣的編號；模型不支援工具時不給工具。
        var prompt = session.Factory.Provider.LastMessages[^1].Content;
        Assert.Contains("[1] 查詢範圍：", prompt, StringComparison.Ordinal);
        Assert.EndsWith("問題：誰還沒補足？", prompt, StringComparison.Ordinal);
        Assert.Contains($"[{grounding.Facts!.Count}] ", prompt, StringComparison.Ordinal);
        Assert.Contains("「出勤紀錄」", session.Factory.Provider.LastMessages[0].Content, StringComparison.Ordinal);
        Assert.Empty(Assert.Single(session.Factory.Provider.OfferedTools));
    }

    [Fact]
    public async Task OnlyPeopleTheViewerCanSeeReachTheModel()
    {
        await using var session = await ArrangeAsync();
        using var bob = await session.Factory.SignedInAsync("bob");

        var events = await ReadEventsAsync(await bob.PostAsJsonAsync(Questions, new AssistantQuestion("大家的出勤如何？",
            History: [new("我有遲到嗎？", "沒有遲到 [3]。")], Page: Page, View: View())));

        Assert.DoesNotContain(events[0].Data.Facts!, x => x.Label.StartsWith("carol", StringComparison.Ordinal));
        Assert.DoesNotContain("carol", session.Factory.Provider.LastMessages[^1].Content, StringComparison.Ordinal);
        // 先前的輪次放在提問之前，依序是使用者與助理。
        Assert.Equal(["system", "user", "assistant", "user"], session.Factory.Provider.LastMessages.Select(x => x.Role));
    }

    [Fact]
    public async Task TheModelCallsAToolAndItsFactsContinueTheNumbering()
    {
        await using var session = await ArrangeAsync(tools: true);
        using var hr = await session.Factory.SignedInAsync("hr");
        session.Factory.Provider.ToolCalls.Enqueue([new("attendance_day_explanation", new JsonObject { ["date"] = "2026-10-21", ["person"] = "bob" })]);

        var events = await ReadEventsAsync(await hr.PostAsJsonAsync(Questions, new AssistantQuestion("bob 21 日為什麼未補足？", Page: Page, View: View())));

        Assert.Equal(["grounding", "tool", "grounding", "delta", "delta", "delta", "done"], events.Select(x => x.Name));
        Assert.Equal("查詢判定過程", events[1].Data.Tool);
        var (page, day) = (events[0].Data.Facts!, events[2].Data.Facts!);
        Assert.StartsWith("bob 10/21", day[0].Label, StringComparison.Ordinal);

        // 第二輪：工具結果接在模型的呼叫之後，編號從畫面資料之後接著編；最後一輪仍可呼叫工具，但模型直接回答了。
        var messages = session.Factory.Provider.LastMessages;
        Assert.Equal(["system", "user", "assistant", "tool"], messages.Select(x => x.Role));
        Assert.Contains($"[{page.Count + 1}] bob 10/21", messages[^1].Content, StringComparison.Ordinal);
        Assert.All(session.Factory.Provider.OfferedTools, x => Assert.Contains("attendance_day_explanation", x));

        // 畫面資料與工具各寫一筆稽核，記成出勤紀錄的使用。
        await session.WithDbAsync(async db =>
        {
            var audits = await db.AuditEvents.Where(x => x.Action == "attendance.assistant.read").OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.All(audits, x => Assert.Equal(Page, x.FeatureId));
            Assert.Contains("attendance_day_explanation", audits[1].DetailsJson, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ToolsTheViewerCannotSeeFailWithoutReachingTheModelAsData()
    {
        await using var session = await ArrangeAsync(tools: true);
        using var bob = await session.Factory.SignedInAsync("bob");
        session.Factory.Provider.ToolCalls.Enqueue([new("attendance_day_explanation", new JsonObject { ["date"] = "2026-10-21", ["person"] = "carol" })]);

        var events = await ReadEventsAsync(await bob.PostAsJsonAsync(Questions, new AssistantQuestion("carol 那天怎麼了？", Page: Page, View: View())));

        Assert.Equal(["grounding", "tool", "delta", "delta", "delta", "done"], events.Select(x => x.Name));
        Assert.StartsWith("查詢失敗", session.Factory.Provider.LastMessages[^1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidQuestionsAndViewsAreRejectedBeforeTheModelRuns()
    {
        await using var session = await ArrangeAsync();
        using var hr = await session.Factory.SignedInAsync("hr");

        await AssertCodeAsync(await hr.PostAsJsonAsync(Questions, new AssistantQuestion("  ")), HttpStatusCode.BadRequest, "assistant_question_invalid");
        await AssertCodeAsync(await hr.PostAsJsonAsync(Questions, new AssistantQuestion(new string('問', 501))), HttpStatusCode.BadRequest, "assistant_question_invalid");
        await AssertCodeAsync(await hr.PostAsJsonAsync(Questions, new AssistantQuestion("？", Page: Page, View: View("2026-10-21", "2026-10-19"))),
            HttpStatusCode.BadRequest, "attendance_records_range_invalid");
        Assert.Equal(0, session.Factory.Provider.Calls);
    }

    [Fact]
    public async Task WithoutAnyFeatureThatHasToolsThereIsNothingToAsk()
    {
        await using var session = await ArrangeAsync();
        // 所有人預設都有出勤紀錄（看自己）：拿掉預設授權，carol 就沒有任何帶工具的功能。
        await session.WithDbAsync(db => db.RoleGroupFeatures.Where(x => x.FeatureId == FeatureCatalog.AttendanceRecords && x.GroupId == "workspace").ExecuteDeleteAsync());
        using var carol = await session.Factory.SignedInAsync("carol");

        await AssertCodeAsync(await carol.PostAsJsonAsync(Questions, new AssistantQuestion("我有遲到嗎？", Page: Page, View: View())),
            HttpStatusCode.Forbidden, "assistant_unavailable");
        Assert.Equal(0, session.Factory.Provider.Calls);
    }

    [Fact]
    public async Task ThePageCanListTheModelsItMayUse()
    {
        await using var session = await ArrangeAsync();
        using var bob = await session.Factory.SignedInAsync("bob");

        // 模型清單開放給有助理工具的功能，不只對話。
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/api/v1/models")).StatusCode);
    }
}
