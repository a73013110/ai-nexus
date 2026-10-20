using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class AdminUserEmailTests
{
    [Fact]
    public async Task AnAdministratorCannotOverwriteOrClearADirectoryEmail()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var alice = await Login(admin, "alice");
        await AssertCodeAsync(await PutEmail(admin, alice.Id, "other@fixture.test"), HttpStatusCode.Conflict, "email_managed_by_directory");
        await AssertCodeAsync(await PutEmail(admin, alice.Id, null), HttpStatusCode.Conflict, "email_managed_by_directory");
        var user = await UserAsync(f, alice.Id);
        Assert.Equal(("Alice@fixture.test", EmailSources.Directory), (user.Email, user.EmailSource));
        Assert.Equal(["email_managed_by_directory", "email_managed_by_directory"], await ResultsAsync(f, alice.Id));
    }

    [Fact]
    public async Task AnAdministratorSetsAnUnverifiedEmailThatTheListShowsAndSearchesThenClearsIt()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        using var bobClient = f.CreateClient(); var bob = await Login(bobClient, "bob");

        (await PutEmail(admin, bob.Id, " Bob.Smith@Fixture.Test ")).EnsureSuccessStatusCode();
        var user = await UserAsync(f, bob.Id);
        Assert.Equal(("Bob.Smith@fixture.test", EmailSources.Admin), (user.Email, user.EmailSource));
        Assert.Null(user.EmailVerifiedAt);
        // 未驗證的地址不授予任何能力，所以不登出本人。
        Assert.Equal(0, user.SecurityVersion);
        Assert.Equal(HttpStatusCode.OK, (await bobClient.GetAsync("/api/v1/me")).StatusCode);

        var listed = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users?search=smith@fixture"))!;
        var row = Assert.Single(listed.Users);
        Assert.Equal((bob.Id, "Bob.Smith@fixture.test", EmailSources.Admin, (DateTimeOffset?)null), (row.Id, row.Email, row.EmailSource, row.EmailVerifiedAt));

        (await PutEmail(admin, bob.Id, "")).EnsureSuccessStatusCode();
        user = await UserAsync(f, bob.Id);
        Assert.Equal((null, null), (user.Email, user.EmailSource));
        Assert.Equal(["saved", "saved"], await ResultsAsync(f, bob.Id));
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var details = await db.AuditEvents.AsNoTracking().Where(x => x.Action == "admin.user_email").Select(x => x.DetailsJson).ToListAsync();
        // 快照只記遮罩後的地址（稽核攔截器還會再遮蔽像信箱的片段），不留完整地址。
        Assert.All(details, x => Assert.DoesNotContain("Bob.Smith", x));
        Assert.Contains(details, x => x!.Contains("\"source\":\"admin\""));
        // 沒有已驗證的舊信箱，不寄通知。
        Assert.False(await db.Set<OutboundMessage>().AnyAsync(x => x.Category == "account.security-notice"));
    }

    [Fact]
    public async Task ReplacingAVerifiedEmailNotifiesTheOldAddressAndRejectsAnotherUsersVerifiedAddress()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var bob = await Login(f.CreateClient(), "bob");
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == bob.Id).ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Email, "bob@fixture.test").SetProperty(x => x.EmailSource, EmailSources.User).SetProperty(x => x.EmailVerifiedAt, DateTimeOffset.UtcNow));

        await AssertCodeAsync(await PutEmail(admin, bob.Id, "alice@fixture.test"), HttpStatusCode.Conflict, "email_in_use");
        await AssertCodeAsync(await PutEmail(admin, bob.Id, "not an address"), HttpStatusCode.BadRequest, "email_invalid");
        // 同一地址不重設，本人的驗證保留。
        (await PutEmail(admin, bob.Id, "BOB@fixture.test")).EnsureSuccessStatusCode();
        Assert.NotNull((await UserAsync(f, bob.Id)).EmailVerifiedAt);
        Assert.False(await NoticeExistsAsync(f));

        (await PutEmail(admin, bob.Id, "robert@fixture.test")).EnsureSuccessStatusCode();
        var user = await UserAsync(f, bob.Id);
        Assert.Equal(("robert@fixture.test", EmailSources.Admin), (user.Email, user.EmailSource));
        Assert.Null(user.EmailVerifiedAt);
        using var check = f.Services.CreateScope();
        var notice = await check.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().AsNoTracking().SingleAsync(x => x.Category == "account.security-notice");
        Assert.Equal(("bob@fixture.test", bob.Id), (notice.RecipientAddress, notice.RecipientUserId));
        await Dispatcher(f).DispatchAsync(CancellationToken.None);
        var body = f.Mail.Sent.Last().Message.TextBody ?? "";
        Assert.Contains("r***t@fixture.test", body);
        Assert.Contains("系統管理員", body);
        Assert.DoesNotContain("robert@", body);
    }

    [Fact]
    public async Task OnlyUserAdministratorsCanSetEmails()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var bobClient = f.CreateClient(); var bob = await Login(bobClient, "bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await PutEmail(bobClient, bob.Id, "bob@fixture.test")).StatusCode);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        await AssertCodeAsync(await PutEmail(admin, Guid.NewGuid(), "x@fixture.test"), HttpStatusCode.NotFound, "user_not_found");
    }

    private static Task<HttpResponseMessage> PutEmail(HttpClient client, Guid id, string? email)
        => client.PutAsJsonAsync($"/api/v1/admin/users/{id}/email", new UserEmailRequest(email));

    private static async Task<User> UserAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static async Task<List<string>> ResultsAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents.AsNoTracking()
            .Where(x => x.ResourceId == id && x.Action == "admin.user_email").OrderBy(x => x.At).Select(x => x.Result!).ToListAsync();
    }

    private static async Task<bool> NoticeExistsAsync(AppFactory f)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().AnyAsync(x => x.Category == "account.security-notice");
    }
}
