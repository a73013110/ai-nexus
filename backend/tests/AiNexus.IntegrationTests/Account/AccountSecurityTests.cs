using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Features.Platform.Account.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Account;

public sealed class AccountSecurityTests
{
    private const string Strong = "藍色貓咪 喜歡 下午三點的陽光";

    [Fact]
    public async Task SensitiveChangesRequireAFreshPasswordStepUp()
    {
        await using var f = new AppFactory(ldap: true);
        using var client = f.CreateClient();
        var me = await Login(client, "bob");
        var security = (await client.GetFromJsonAsync<AccountSecurityDto>("/api/v1/settings/account"))!;
        Assert.True(security.ReauthenticationRequired);
        Assert.Equal(["password"], security.ReauthMethods);
        Assert.Equal("ad", security.CurrentMethod);

        await AssertCodeAsync(await client.PutAsJsonAsync("/api/v1/settings/account/local-login", new LocalLoginRequest("bob", Strong)), HttpStatusCode.Forbidden, "reauthentication_required");
        await AssertCodeAsync(await client.PostAsJsonAsync("/api/v1/settings/account/reauthentications", new ReauthenticationRequest("wrong password")), HttpStatusCode.BadRequest, "reauthentication_failed");
        await ReauthenticateAsync(client);
        Assert.False((await client.GetFromJsonAsync<AccountSecurityDto>("/api/v1/settings/account"))!.ReauthenticationRequired);

        var changed = await PutLocalLoginAsync(client, new LocalLoginRequest("bob", Strong));
        Assert.True(changed.Security.LocalLogin.Enabled);
        Assert.Equal("bob", changed.Security.LocalLogin.Account);
        var user = await UserAsync(f, me.Id);
        Assert.NotNull(user.PasswordChangedAt);
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(["account.local_login_enabled", "account.reauthentication", "account.reauthentication"], await ActionsAsync(f, me.Id));
        // 剛建立的本地密碼可以登入（bob 剛通過 AD 驗證，符合新鮮度規則）。
        Assert.Equal(me.Id, (await Login(f.CreateClient(), "bob", "local", Strong)).Id);
    }

    [Fact]
    public async Task ASecurityChangeKeepsThisSessionAndSignsOutTheOthers()
    {
        await using var f = new AppFactory(ldap: true);
        using var current = f.CreateClient(); using var other = f.CreateClient();
        await Login(current, "bob"); await Login(other, "bob");
        var stale = current.DefaultRequestHeaders.GetValues("X-CSRF-Token").Single();
        await ReauthenticateAsync(current);
        Assert.NotEqual(HttpStatusCode.OK, (await Send(current, stale, HttpMethod.Post, "/api/v1/settings/account/reauthentications", new ReauthenticationRequest("fixture-password"))).StatusCode);
        await PutLocalLoginAsync(current, new LocalLoginRequest("bob", Strong));
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/v1/me")).StatusCode);
        await AssertCodeAsync(await other.GetAsync("/api/v1/me"), HttpStatusCode.Unauthorized, "authentication_required");
    }

    [Fact]
    public async Task AnEmailIsVerifiedWithACodeSentToTheNewAddressAndTheOldAddressIsNotified()
    {
        await using var f = new AppFactory(ldap: true, services: services => services.PostConfigure<SelfServiceOptions>(x => x.CodeCooldownSeconds = 0));
        using var client = f.CreateClient();
        var me = await Login(client, "bob");
        await ReauthenticateAsync(client);
        var code = await RequestEmailAsync(f, client, "bob@fixture.test");
        await AssertCodeAsync(await client.PostAsJsonAsync($"/api/v1/settings/account/email-changes/{code.Id}/confirmation", new VerificationCodeConfirmation(Wrong(code.Secret))), HttpStatusCode.BadRequest, "verification_code_invalid");
        var status = (await client.GetFromJsonAsync<VerificationCodeDto>($"/api/v1/settings/account/verification-codes/{code.Id}"))!;
        Assert.Equal((4, "sent", "b***b@fixture.test"), (status.AttemptsLeft, status.Delivery, status.MaskedTarget));
        var confirmed = await ConfirmAsync(client, code);
        Assert.Equal(("bob@fixture.test", "user"), (confirmed.Security.Email, confirmed.Security.EmailSource));
        Assert.NotNull(confirmed.Security.EmailVerifiedAt);
        await AssertCodeAsync(await client.PostAsJsonAsync($"/api/v1/settings/account/email-changes/{code.Id}/confirmation", new VerificationCodeConfirmation(code.Secret)), HttpStatusCode.Conflict, "verification_expired");

        var next = await RequestEmailAsync(f, client, "robert@fixture.test");
        await ConfirmAsync(client, next);
        Assert.Equal("robert@fixture.test", (await UserAsync(f, me.Id)).Email);
        using var scope = f.Services.CreateScope();
        var notice = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().AsNoTracking().SingleAsync(x => x.Category == "account.security-notice");
        Assert.Equal("bob@fixture.test", notice.RecipientAddress);
        // 驗證碼信是敏感信：寄出後內文即刪除，紀錄看不到驗證碼。
        Assert.Null(await ContentAsync(f, (await StatusMessageAsync(f, code.Id))!.Value));
    }

    [Fact]
    public async Task DirectoryEmailsCannotBeChangedButCanReceiveStepUpCodes()
    {
        await using var f = new AppFactory(ldap: true);
        using var client = f.CreateClient();
        await Login(client, "alice");
        var security = (await client.GetFromJsonAsync<AccountSecurityDto>("/api/v1/settings/account"))!;
        Assert.False(security.EmailEditable);
        Assert.Equal(["password", "code"], security.ReauthMethods);
        var sent = await client.PostAsJsonAsync("/api/v1/settings/account/verification-codes", new VerificationCodeRequest("reauthentication"));
        var issued = (await sent.Content.ReadFromJsonAsync<VerificationCodeDto>())!;
        var secret = await DeliveredCodeAsync(f);
        var response = await client.PostAsJsonAsync("/api/v1/settings/account/reauthentications", new ReauthenticationRequest(null, issued.Id, secret));
        response.EnsureSuccessStatusCode();
        UseToken(client, (await response.Content.ReadFromJsonAsync<AccountSecurityChangeDto>())!.CsrfToken!);
        await AssertCodeAsync(await client.PostAsJsonAsync("/api/v1/settings/account/email-changes", new EmailChangeRequest("other@fixture.test")), HttpStatusCode.Conflict, "email_managed_by_directory");
    }

    [Fact]
    public async Task StepUpAttemptsAreRateLimitedPerUser()
    {
        await using var f = new AppFactory(ldap: true);
        using var client = f.CreateClient();
        await Login(client, "bob");
        for (var i = 0; i < 10; i++)
            await AssertCodeAsync(await client.PostAsJsonAsync("/api/v1/settings/account/reauthentications", new ReauthenticationRequest("wrong password")), HttpStatusCode.BadRequest, "reauthentication_failed");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/v1/settings/account/reauthentications", new ReauthenticationRequest("fixture-password"))).StatusCode);
    }

    [Fact]
    public async Task TheOnlySignInMethodCannotBeDisabledAndDisablingTheCurrentOneSignsOut()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        await CreateUser(admin, LocalAccount());
        using var local = f.CreateClient(); await Login(local, "tester", "local");
        await ReauthenticateAsync(local, Password);
        await AssertCodeAsync(await local.DeleteAsync("/api/v1/settings/account/local-login"), HttpStatusCode.Conflict, "local_login_required");

        using var bob = f.CreateClient(); await Login(bob, "bob");
        await ReauthenticateAsync(bob);
        await PutLocalLoginAsync(bob, new LocalLoginRequest("bobby", Strong));
        using var bobLocal = f.CreateClient(); await Login(bobLocal, "bobby", "local", Strong);
        await ReauthenticateAsync(bobLocal, Strong);
        var response = await bobLocal.DeleteAsync("/api/v1/settings/account/local-login");
        response.EnsureSuccessStatusCode();
        Assert.True((await response.Content.ReadFromJsonAsync<AccountSecurityChangeDto>())!.SignedOut);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bobLocal.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bob.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task WindowsRequestsAreAlreadyVerifiedAndWeakPasswordsAreRejected()
    {
        await using var f = new AppFactory();
        using var client = await f.SignedInAsync();
        Assert.False((await client.GetFromJsonAsync<AccountSecurityDto>("/api/v1/settings/account"))!.ReauthenticationRequired);
        await AssertCodeAsync(await client.PutAsJsonAsync("/api/v1/settings/account/local-login", new LocalLoginRequest("alice", "alice-password-1")), HttpStatusCode.BadRequest, "password_weak");
        await AssertCodeAsync(await client.PutAsJsonAsync("/api/v1/settings/account/local-login", new LocalLoginRequest("alice", "short")), HttpStatusCode.BadRequest, "invalid_password");
        (await client.PutAsJsonAsync("/api/v1/settings/account/local-login", new LocalLoginRequest("alice", Strong))).EnsureSuccessStatusCode();
    }

    private sealed record IssuedCode(Guid Id, string Secret);

    private static async Task ReauthenticateAsync(HttpClient client, string password = "fixture-password")
    {
        var response = await client.PostAsJsonAsync("/api/v1/settings/account/reauthentications", new ReauthenticationRequest(password));
        response.EnsureSuccessStatusCode();
        UseToken(client, (await response.Content.ReadFromJsonAsync<AccountSecurityChangeDto>())!.CsrfToken!);
    }

    private static async Task<AccountSecurityChangeDto> PutLocalLoginAsync(HttpClient client, LocalLoginRequest request)
    {
        var response = await client.PutAsJsonAsync("/api/v1/settings/account/local-login", request);
        response.EnsureSuccessStatusCode();
        var changed = (await response.Content.ReadFromJsonAsync<AccountSecurityChangeDto>())!;
        UseToken(client, changed.CsrfToken!);
        return changed;
    }

    private static async Task<IssuedCode> RequestEmailAsync(AppFactory f, HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/settings/account/email-changes", new EmailChangeRequest(email));
        response.EnsureSuccessStatusCode();
        var issued = (await response.Content.ReadFromJsonAsync<VerificationCodeDto>())!;
        return new(issued.Id, await DeliveredCodeAsync(f));
    }

    private static async Task<AccountSecurityChangeDto> ConfirmAsync(HttpClient client, IssuedCode code)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/settings/account/email-changes/{code.Id}/confirmation", new VerificationCodeConfirmation(code.Secret));
        response.EnsureSuccessStatusCode();
        var changed = (await response.Content.ReadFromJsonAsync<AccountSecurityChangeDto>())!;
        UseToken(client, changed.CsrfToken!);
        return changed;
    }

    /// <summary>寄出佇列中的信，取最後一封的 6 位數驗證碼。</summary>
    private static async Task<string> DeliveredCodeAsync(AppFactory f)
    {
        await Dispatcher(f).DispatchAsync(CancellationToken.None);
        return Regex.Match(f.Mail.Sent.Last().Message.TextBody ?? "", @"\b\d{6}\b").Value;
    }

    private static string Wrong(string code) => code == "000000" ? "111111" : "000000";

    private static async Task<Guid?> StatusMessageAsync(AppFactory f, Guid challengeId)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<VerificationChallenge>().Where(x => x.Id == challengeId).Select(x => x.MailMessageId).SingleAsync();
    }

    private static void UseToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", token);
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string token, HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-Token", token);
        var saved = client.DefaultRequestHeaders.GetValues("X-CSRF-Token").Single();
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        try { return await client.SendAsync(request); }
        finally { client.DefaultRequestHeaders.Add("X-CSRF-Token", saved); }
    }

    private static async Task<User> UserAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static async Task<List<string>> ActionsAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents.AsNoTracking()
            .Where(x => x.OwnerId == id && x.Action.StartsWith("account.")).Select(x => x.Action).OrderBy(x => x).ToListAsync();
    }
}
