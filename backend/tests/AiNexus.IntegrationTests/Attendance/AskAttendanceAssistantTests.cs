using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Platform.Assistant;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>出勤紀錄的頁內助理：查詢條件與表格相同，模型拿到的事實就是畫面那一份（範圍外的人不會出現）；回答以 SSE 逐段送出。</summary>
public sealed class AskAttendanceAssistantTests
{
    private const string Assistant = Records + "/assistant?from=2026-10-19&to=2026-10-21";

    /// <summary>bob 21 日沒刷卡也沒請假（未補足）；carol 都準時。hr 看全公司，bob 只看自己。</summary>
    private static async Task<RecordsSession> ArrangeAsync()
    {
        var session = Create();
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
    public async Task TheAnswerStreamsAfterTheFactsTheModelWasGiven()
    {
        await using var session = await ArrangeAsync();
        using var hr = await session.Factory.SignedInAsync("hr");

        var events = await ReadEventsAsync(await hr.PostAsJsonAsync(Assistant, new AssistantQuestion("誰還沒補足？")));

        var (name, grounding) = events[0];
        Assert.Equal("grounding", name);
        Assert.Equal("2026/10/19–10/21 · 2 人", grounding.Scope);
        Assert.False(grounding.Partial);
        Assert.Contains(grounding.Facts!, x => x.Label.StartsWith("bob", StringComparison.Ordinal) && x.Text.Contains("未補足 1 天", StringComparison.Ordinal));
        Assert.Equal("這是測試回答。", string.Concat(events.Where(x => x.Name == "delta").Select(x => x.Data.Text)));
        Assert.Equal(("done", false), (events[^1].Name, events[^1].Data.Truncated));

        // 交給模型的事實與送給畫面的依據是同一份、同樣的編號。
        var prompt = session.Factory.Provider.LastMessages[^1].Content;
        Assert.Contains("[1] 查詢範圍：", prompt, StringComparison.Ordinal);
        Assert.EndsWith("問題：誰還沒補足？", prompt, StringComparison.Ordinal);
        Assert.Contains($"[{grounding.Facts!.Count}] ", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyPeopleTheViewerCanSeeReachTheModel()
    {
        await using var session = await ArrangeAsync();
        using var bob = await session.Factory.SignedInAsync("bob");

        var events = await ReadEventsAsync(await bob.PostAsJsonAsync(Assistant, new AssistantQuestion("大家的出勤如何？",
            History: [new("我有遲到嗎？", "沒有遲到 [3]。")])));

        Assert.DoesNotContain(events[0].Data.Facts!, x => x.Label.StartsWith("carol", StringComparison.Ordinal));
        Assert.DoesNotContain("carol", session.Factory.Provider.LastMessages[^1].Content, StringComparison.Ordinal);
        // 先前的輪次放在提問之前，依序是使用者與助理。
        Assert.Equal(["system", "user", "assistant", "user"], session.Factory.Provider.LastMessages.Select(x => x.Role));
    }

    [Fact]
    public async Task InvalidQuestionsAndFiltersAreRejectedBeforeTheModelRuns()
    {
        await using var session = await ArrangeAsync();
        using var hr = await session.Factory.SignedInAsync("hr");

        await AssertCodeAsync(await hr.PostAsJsonAsync(Assistant, new AssistantQuestion("  ")), HttpStatusCode.BadRequest, "assistant_question_invalid");
        await AssertCodeAsync(await hr.PostAsJsonAsync(Assistant, new AssistantQuestion(new string('問', 501))), HttpStatusCode.BadRequest, "assistant_question_invalid");
        await AssertCodeAsync(await hr.PostAsJsonAsync(Records + "/assistant?from=2026-10-21&to=2026-10-19", new AssistantQuestion("？")),
            HttpStatusCode.BadRequest, "attendance_records_range_invalid");
        Assert.Equal(0, session.Factory.Provider.Calls);
    }

    [Fact]
    public async Task ThePageCanListTheModelsItMayUse()
    {
        await using var session = await ArrangeAsync();
        using var bob = await session.Factory.SignedInAsync("bob");

        // 模型清單開放給有頁內助理的頁面，不只對話。
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/api/v1/models")).StatusCode);
    }
}
