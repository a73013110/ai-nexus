using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiNexus.IntegrationTests.Support;

/// <summary>錯誤回應的共用斷言：狀態碼與 problem 的 <c>code</c> 一起比對，不看提示文字（提示由 PublicErrorCatalog 決定）。</summary>
internal static class ProblemAssertions
{
    internal static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
