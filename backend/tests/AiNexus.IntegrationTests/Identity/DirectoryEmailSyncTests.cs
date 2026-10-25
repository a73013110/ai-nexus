using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class DirectoryEmailSyncTests
{
    [Fact]
    public async Task LdapLoginStoresTheDirectoryMailAsVerifiedAndRecordsTheDirectoryCheck()
    {
        await using var f = new AppFactory(ldap: true);
        using var client = f.CreateClient();
        var me = await Login(client, "alice");
        var user = await UserAsync(f, me.Id);
        Assert.Equal("Alice@fixture.test", user.Email);
        Assert.Equal(EmailSources.Directory, user.EmailSource);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotNull(user.DirectoryVerifiedAt);
        Assert.Null((await UserAsync(f, (await Login(f.CreateClient(), "bob")).Id)).Email);
    }

    [Fact]
    public async Task TheDirectoryDoesNotOverwriteAnAddressTheUserVerified()
    {
        await using var f = new AppFactory(ldap: true);
        var id = (await Login(f.CreateClient(), "alice")).Id;
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == id).ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Email, "own@fixture.test").SetProperty(x => x.EmailSource, EmailSources.User)
                .SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddHours(-1)));
        await Login(f.CreateClient(), "alice");
        var user = await UserAsync(f, id);
        Assert.Equal("own@fixture.test", user.Email);
        Assert.Equal(EmailSources.User, user.EmailSource);
    }

    [Theory]
    [InlineData(@"FIXTURE\alice")]
    [InlineData("alice@fixture.internal")]
    [InlineData(" alice ")]
    public async Task DirectoryLoginAcceptsTheDomainPrefixOrSuffix(string account)
    {
        await using var f = new AppFactory(ldap: true);
        Assert.Equal("alice", (await Login(f.CreateClient(), account)).Account);
    }

    private static async Task<User> UserAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }
}
