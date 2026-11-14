using System.Net.Http.Json;
using AiNexus.Features.Office.Flow.Approvals;
using AiNexus.Features.Work.Repositories.Access;

namespace AiNexus.IntegrationTests.Support;

/// <summary>簽核 API 的測試捷徑。</summary>
internal static class FlowApi
{
    internal const string Base = "/api/v1/flow";
    internal const string Definitions = "/api/v1/admin/flow-definitions";

    internal static async Task<FlowInstanceSummaryDto[]> TasksAsync(HttpClient client) => (await client.GetFromJsonAsync<FlowInstanceSummaryDto[]>($"{Base}/tasks"))!;

    internal static async Task<FlowInstanceDto> InstanceAsync(HttpClient client, Guid id) => (await client.GetFromJsonAsync<FlowInstanceDto>($"{Base}/instances/{id}"))!;

    internal static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid id, int step, bool approve, string? comment = null)
        => client.PostAsJsonAsync($"{Base}/instances/{id}/decision", new FlowDecisionRequest(step, approve, comment));

    internal static async Task ApproveAsync(HttpClient client, Guid id, int step)
    {
        var response = await DecideAsync(client, id, step, true);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>送出儲存庫權限申請，回傳簽核申請的 ID。</summary>
    internal static async Task<Guid> SubmitAsync(RepositoryAccessSession session, HttpClient applicant, string permission = "read")
    {
        var response = await session.RequestAsync(applicant, permission);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<RepositoryAccessRequestedDto>())!.InstanceId;
    }
}
