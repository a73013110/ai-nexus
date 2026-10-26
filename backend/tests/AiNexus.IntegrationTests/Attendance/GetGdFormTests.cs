using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Features.Office.Attendance.Forms;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.RemoteFiles;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>出勤紀錄點單號開啟的表單：內容、流程、附件，以及與出勤紀錄相同的資料範圍。</summary>
public sealed class GetGdFormTests
{
    private const string Forms = "/api/v1/attendance-forms/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static DateTime Local(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0);

    private static async Task<GdFormDetailDto> GetAsync(HttpClient client, string desNo)
    {
        var response = await client.GetAsync(Forms + desNo);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<GdFormDetailDto>(Json))!;
    }

    [Fact]
    public async Task ALeaveFormShowsItsDetailsFlowAndAttachments()
    {
        var files = new FakeRemoteFiles(GdFileSource.SourceName);
        await using var session = Create(services: files.Register);
        await session.PersonAsync("bob");
        var bob = GdId("bob");
        session.Gd.AddDepartment(7, "資訊組");
        session.Gd.AddUser("G900", "carol", name: "林予安");
        session.Gd.Update("Sys_User", bob, "NAME = N'陳品睿'");
        session.Gd.AddMembership(7, bob, "T1", "專員");
        session.Gd.AddKind("G1", "喪假");
        session.Gd.AddLeave("L115102201", bob, "G1", Local(22, 8), Local(23, 17), "Decide");
        session.Gd.Update("Form_Leave", "L115102201",
            "LeaveDepNO = 7, LeaveTime = 16, SendDate = @sent, LeaveDesc = N'告別式', AttKind = N'訃文、', AttDesc = N'訃文後補', AgentDepNo = 7, AgentUserId = 'G900', BereaveType = '4'",
            new { sent = Local(20, 9, 30) });
        session.Gd.AddFlow("FLOW_HISTORY", "L115102201", "001", "7_" + bob, "待處理表單", "2026/10/20 09:30:00");
        session.Gd.AddFlow("FLOW_HISTORY", "L115102201", "002", "7_G900", "請假代理人", "2026/10/20 10:02:00", "同意代理");
        session.Gd.AddAttachment("L115102201", "L115102201001.pdf", "/attfile/115", "訃文.pdf", owner: bob);
        files.Files["/attfile/115/L115102201001.pdf"] = new(RemoteFileStatus.Found, "%PDF-1.4"u8.ToArray());
        using var client = await session.Factory.SignedInAsync("bob");

        var form = await GetAsync(client, "L115102201");

        Assert.Equal((GdFormKind.Leave, GdFormFlowOver.Decide, "喪假"), (form.Kind, form.FlowStatus, form.Title));
        Assert.Equal(new GdPerson("陳品睿", "資訊組", "專員"), form.Applicant);
        Assert.Equal(new GdPerson("林予安", "資訊組", null), form.Agent);
        Assert.Equal((At(22, 8), At(23, 17), (double?)16, (DateTimeOffset?)At(20, 9, 30)), (form.StartsAt, form.EndsAt, form.Hours, form.SubmittedAt));
        Assert.Equal(("告別式", "訃文後補"), (form.Reason, form.AttachmentNote));
        Assert.Equal("祖父母", form.Leave!.Relation);
        Assert.Equal(["訃文"], form.Leave.AttachmentKinds);
        Assert.Null(form.Travel);
        Assert.Empty(form.Segments);
        // 已核准的單只有歷程，沒有「目前」的關卡。
        Assert.Equal(["送出", "請假代理人"], form.Steps!.Select(x => x.Action));
        Assert.Equal("同意代理", form.Steps![1].Memo);

        var attachment = Assert.Single(form.Attachments!);
        Assert.Equal(("訃文.pdf", "application/pdf", false, "陳品睿"), (attachment.File.FileName, attachment.File.ContentType, attachment.File.IsImage, attachment.Uploader));
        // 前端只拿得到憑證：公文系統的路徑不在回應裡。
        Assert.DoesNotContain("attfile", await (await client.GetAsync(Forms + "L115102201")).Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var content = await client.GetAsync("/api/v1/remote-files/content?ticket=" + Uri.EscapeDataString(attachment.File.Ticket));
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("application/pdf", content.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["/attfile/115/L115102201001.pdf"], files.Reads);
    }

    [Fact]
    public async Task ACrossPeriodLeaveListsItsSegmentsAndAnInFlowFormItsWaitingSteps()
    {
        await using var session = Create();
        await session.PersonAsync("bob");
        var bob = GdId("bob");
        session.Gd.AddKind("A", "特休");
        session.Gd.AddLeave("L115101901", bob, "A", Local(19, 9), Local(30, 12), "Send", dayOfWeek: "1,3");
        session.Gd.AddLeaveGroup("L115101901", Local(21, 9), Local(21, 12), "3");
        session.Gd.AddLeaveGroup("L115101901", Local(19, 9), Local(19, 12), "1");
        session.Gd.AddFlow("FLOW_HISTORY", "L115101901", "001", "7_" + bob, "待處理表單", "2026/10/18 16:00:00");
        session.Gd.AddFlow("FLOW_CURRENT", "L115101901", "002", "7_G900", "串簽", "2026/10/18 16:00:05");
        session.Gd.AddFlow("FLOW_NEXT", "L115101901", "003", "7_G901", "決行");
        session.Gd.AddTravel("T115102001", bob, Local(20, 13), Local(20, 17), "教育訓練", "Send");
        session.Gd.Update("Form_Travel", "T115102001", "Place = N'台北', TravelDesc = N'高鐵', StudyMode = 1, StudyKind = '2'");
        session.Gd.Update("Sys_User", bob, "TelNum = '02-2345-6789', SubTelNum = '123'");
        using var client = await session.Factory.SignedInAsync("bob");

        var leave = await GetAsync(client, "L115101901");
        var travel = await GetAsync(client, "T115102001");

        Assert.Equal([new GdFormPeriod(At(19, 9), At(19, 12)), new GdFormPeriod(At(21, 9), At(21, 12))], leave.Segments);
        Assert.Equal([GdFlowStepState.Done, GdFlowStepState.Current, GdFlowStepState.Pending], leave.Steps!.Select(x => x.State));
        Assert.Empty(leave.Attachments!);
        Assert.Equal(new GdTravelDetail("台北", "高鐵", "行政人員研習", "02-2345-6789#123"), travel.Travel);
        Assert.Equal((GdFormKind.Travel, "教育訓練"), (travel.Kind, travel.Title));
    }

    [Fact]
    public async Task FormsOutsideTheRecordsScopeAreNotFound()
    {
        await using var session = Create();
        var bob = await session.PersonAsync("bob");
        await session.PersonAsync("alice");
        session.Gd.AddKind("A", "特休");
        session.Gd.AddLeave("L115102202", GdId("alice"), "A", Local(22, 9), Local(22, 12), "Decide");
        session.Gd.AddLeave("L115102203", GdId("bob"), "A", Local(22, 9), Local(22, 12), "Temp");
        session.Gd.AddLeave("L115102204", "G-nobody", "A", Local(22, 9), Local(22, 12), "Decide");
        using var client = await session.Factory.SignedInAsync("bob");

        // 別人的單、暫存的單、不存在或不像單號的，一律同樣的 404。
        foreach (var desNo in new[] { "L115102202", "L115102203", "L115102204", "L115102299", "L1", "L115.x" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Forms + desNo)).StatusCode);

        await session.GrantAsync(bob, DataScopes.All);
        Assert.Equal(GdFormKind.Leave, (await GetAsync(client, "L115102202")).Kind);
        // 對不到使用者的申請人只有全公司範圍看得到。
        Assert.Equal("L115102204", (await GetAsync(client, "L115102204")).DesNo);
    }

    [Fact]
    public async Task AnUnreachableGdIsUnavailable()
    {
        await using var session = Create(gdReachable: false);
        await session.PersonAsync("bob", mapped: false);
        using var client = await session.Factory.SignedInAsync("bob");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(Forms + "L115102201")).StatusCode);
    }
}
