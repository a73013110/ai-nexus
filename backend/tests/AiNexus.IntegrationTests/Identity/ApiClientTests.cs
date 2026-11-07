using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.ApiClients;
using AiNexus.Features.Admin.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class ApiClientTests
{
    private const string Verify = "/api/v1/credential-verifications";

    [Fact]
    public async Task AKeyVerifiesAdCredentialsWithoutCsrfOrCreatingAnAccount()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var admin = await f.SignedInAsync();
        var issued = await CreateAsync(admin, "公文系統");
        Assert.Matches("^ainexus_[A-Za-z0-9_-]{43}$", issued.Key);
        Assert.Equal(issued.Key[^4..], issued.Client.KeyHint);

        using var system = KeyClient(f, issued.Key);
        using var response = await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("FIXTURE\\bob", "fixture-password"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var verified = (await response.Content.ReadFromJsonAsync<VerifiedCredentialDto>())!;
        Assert.Equal(new VerifiedCredentialDto("S-1-5-21-fixture-bob", "bob", "bob", null), verified);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 只回答帳密是誰：不在 AiNexus 建立帳號。
        Assert.False(await db.Users.AnyAsync(x => x.Sid == verified.Sid));
        var audit = await db.AuditEvents.SingleAsync(x => x.Action == IdentityAudit.CredentialsVerified.Name);
        Assert.Equal(issued.Client.Id, audit.ResourceId);
        Assert.Equal("completed", audit.Result);
        var listed = (await ListAsync(admin)).Single();
        Assert.NotNull(listed.LastUsedAt);
        Assert.Equal(1, listed.RecentCalls);

        // 外部系統頁「查看呼叫紀錄」帶的條件：稽核看得到呼叫的系統名稱，遮罩後仍保留。
        var calls = (await admin.GetFromJsonAsync<AuditDto[]>(
            $"/api/v1/admin/audit?action={IdentityAudit.CredentialsVerified.Name}&search={issued.Client.Id}"))!;
        using var details = JsonDocument.Parse(Assert.Single(calls).DetailsJson!);
        Assert.Equal("公文系統", details.RootElement.GetProperty("client").GetString());
        Assert.Equal("bob", details.RootElement.GetProperty("account").GetString());
    }

    [Fact]
    public async Task WrongPasswordsAndWrongKeysAnswerDifferentCodes()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var admin = await f.SignedInAsync();
        var issued = await CreateAsync(admin, "公文系統");
        using var system = KeyClient(f, issued.Key);
        await AssertCodeAsync(await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "wrong")), HttpStatusCode.UnprocessableEntity, "credentials_rejected");
        await AssertCodeAsync(await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "")), HttpStatusCode.UnprocessableEntity, "credentials_rejected");
        using (var scope = f.Services.CreateScope())
        {
            // 失敗的呼叫也記下是哪個系統；網站登入的稽核不帶這個欄位。
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var failures = await db.AuditEvents.Where(x => x.Action == IdentityAudit.CredentialsVerified.Name).Select(x => x.DetailsJson!).ToListAsync();
            Assert.Equal(2, failures.Count);
            Assert.All(failures, json =>
            {
                using var failure = JsonDocument.Parse(json);
                Assert.Equal("公文系統", failure.RootElement.GetProperty("client").GetString());
            });
            Assert.All(await db.AuditEvents.Where(x => x.Action == IdentityAudit.SignedIn.Name).Select(x => x.DetailsJson!).ToListAsync(),
                json => Assert.DoesNotContain("client", json, StringComparison.Ordinal));
        }

        // 格式正確但不存在的金鑰，與格式錯誤的值回同一個代碼。
        using var unknown = KeyClient(f, "ainexus_" + new string('A', 43));
        using var rejected = await unknown.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "fixture-password"));
        await AssertCodeAsync(rejected, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal("Bearer", rejected.Headers.WwwAuthenticate.Single().Scheme);
        using var anonymous = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "fixture-password"))).StatusCode);
    }

    [Fact]
    public async Task KeysAndSessionsDoNotCrossOver()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var admin = await f.SignedInAsync();
        var issued = await CreateAsync(admin, "公文系統");
        // 登入的管理員不能借 cookie 呼叫金鑰端點，金鑰也不能呼叫一般端點。
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "fixture-password"))).StatusCode);
        using var system = KeyClient(f, issued.Key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await system.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await system.GetAsync("/api/v1/admin/api-clients")).StatusCode);
    }

    [Fact]
    public async Task ReissuingOrDeletingRevokesTheKeyImmediately()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var admin = await f.SignedInAsync();
        var first = await CreateAsync(admin, "公文系統");
        using var reissue = await admin.PostAsJsonAsync($"/api/v1/admin/api-clients/{first.Client.Id}/keys", new ApiClientKeyRequest(first.Client.RowVersion));
        reissue.EnsureSuccessStatusCode();
        var second = (await reissue.Content.ReadFromJsonAsync<IssuedApiClientKeyDto>())!;
        Assert.NotEqual(first.Key, second.Key);

        var request = new CredentialVerificationRequest("alice", "fixture-password");
        using var old = KeyClient(f, first.Key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.PostAsJsonAsync(Verify, request)).StatusCode);
        using var current = KeyClient(f, second.Key);
        Assert.Equal(HttpStatusCode.OK, (await current.PostAsJsonAsync(Verify, request)).StatusCode);

        // 用舊版本重新產生（另一位管理員同時操作）不會讓剛拿到的金鑰失效。
        await AssertCodeAsync(await admin.PostAsJsonAsync($"/api/v1/admin/api-clients/{first.Client.Id}/keys", new ApiClientKeyRequest(first.Client.RowVersion)),
            HttpStatusCode.Conflict, "admin_write_conflict");

        (await admin.DeleteAsync($"/api/v1/admin/api-clients/{first.Client.Id}?rowVersion={second.Client.RowVersion}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await current.PostAsJsonAsync(Verify, request)).StatusCode);
        Assert.Empty(await ListAsync(admin));
    }

    [Fact]
    public async Task NamesAreUniqueAndEditingKeepsTheKey()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var admin = await f.SignedInAsync();
        var gd = await CreateAsync(admin, "公文系統", "王小明 #1234");
        var pm = await CreateAsync(admin, "專案管理");
        Assert.Equal("王小明 #1234", gd.Client.Contact);
        Assert.True(gd.Client.Enabled);
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/admin/api-clients", new ApiClientCreateRequest(" 公文系統 ")), HttpStatusCode.Conflict, "api_client_name_taken");
        await AssertCodeAsync(await UpdateAsync(admin, pm.Client, "公文系統", null, true), HttpStatusCode.Conflict, "api_client_name_taken");

        (await UpdateAsync(admin, gd.Client, "公文系統（GD）", "  ", true)).EnsureSuccessStatusCode();
        var edited = (await ListAsync(admin)).Single(x => x.Id == gd.Client.Id);
        Assert.Equal("公文系統（GD）", edited.Name);
        Assert.Null(edited.Contact);
        using var system = KeyClient(f, gd.Key);
        Assert.Equal(HttpStatusCode.OK, (await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "fixture-password"))).StatusCode);
    }

    [Fact]
    public async Task DisablingRejectsTheKeyUntilEnabledAgain()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var admin = await f.SignedInAsync();
        var issued = await CreateAsync(admin, "公文系統");
        using var system = KeyClient(f, issued.Key);
        var request = new CredentialVerificationRequest("alice", "fixture-password");
        Assert.Equal(HttpStatusCode.OK, (await system.PostAsJsonAsync(Verify, request)).StatusCode);
        await AssertCodeAsync(await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("alice", "wrong")), HttpStatusCode.UnprocessableEntity, "credentials_rejected");

        (await UpdateAsync(admin, issued.Client, "公文系統", null, false)).EnsureSuccessStatusCode();
        var disabled = (await ListAsync(admin)).Single();
        Assert.False(disabled.Enabled);
        // 不論驗證結果都算一次呼叫。
        Assert.Equal(2, disabled.RecentCalls);
        // 停用與不存在的金鑰回應相同。
        using var rejected = await system.PostAsJsonAsync(Verify, request);
        await AssertCodeAsync(rejected, HttpStatusCode.Unauthorized, "authentication_required");

        (await UpdateAsync(admin, disabled, "公文系統", null, true)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await system.PostAsJsonAsync(Verify, request)).StatusCode);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.Action == IdentityAudit.ApiClientUpdated.Name && x.ResourceId == issued.Client.Id));
    }

    [Fact]
    public async Task ManagingExternalSystemsRequiresItsOwnFeature()
    {
        await using var f = new AppFactory(administrators: ["alice"]);
        using var member = await f.SignedInAsync("bob");
        await AssertCodeAsync(await member.GetAsync("/api/v1/admin/api-clients"), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PostAsJsonAsync("/api/v1/admin/api-clients", new ApiClientCreateRequest("公文系統")), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    [Fact]
    public async Task FailuresThroughTheApiCountTowardTheWebLoginThrottle()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var issued = await CreateAsync(admin, "公文系統");
        using var system = KeyClient(f, issued.Key);
        for (var i = 0; i < IdentityModule.LoginFailuresPerAccount; i++)
            await AssertCodeAsync(await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("bob", "wrong")), HttpStatusCode.UnprocessableEntity, "credentials_rejected");

        using var limited = await system.PostAsJsonAsync(Verify, new CredentialVerificationRequest("bob", "fixture-password"));
        await AssertCodeAsync(limited, HttpStatusCode.TooManyRequests, "login_rate_limited");
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        // 換個入口不能多猜幾輪：網站登入同一帳號也被擋。
        using var web = f.CreateClient(); await Csrf(web);
        await AssertCodeAsync(await web.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("bob", "fixture-password")), HttpStatusCode.TooManyRequests, "login_rate_limited");
    }

    private static HttpClient KeyClient(AppFactory f, string key)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private static async Task<IssuedApiClientKeyDto> CreateAsync(HttpClient admin, string name, string? contact = null)
    {
        using var response = await admin.PostAsJsonAsync("/api/v1/admin/api-clients", new ApiClientCreateRequest(name, contact));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssuedApiClientKeyDto>())!;
    }

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient admin, ApiClientDto client, string name, string? contact, bool enabled)
        => admin.PutAsJsonAsync($"/api/v1/admin/api-clients/{client.Id}", new ApiClientUpdateRequest(name, contact, enabled, client.RowVersion));

    private static async Task<ApiClientDto[]> ListAsync(HttpClient admin) => (await admin.GetFromJsonAsync<ApiClientDto[]>("/api/v1/admin/api-clients"))!;
}
