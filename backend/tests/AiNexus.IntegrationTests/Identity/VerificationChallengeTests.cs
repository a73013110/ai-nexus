using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.SelfService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Identity;

public sealed class VerificationChallengeTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static AppFactory Factory(FakeTimeProvider clock) => new(clock: clock, seed: db => db.Users.Add(new User
    {
        Id = UserId, Sid = "managed:" + UserId, LocalAccount = "tester", DisplayName = "tester", AdEnabled = false, LastSeenAt = DateTimeOffset.UtcNow,
    }));

    [Fact]
    public async Task CodesAreStoredOnlyAsAHashAndAreSingleUse()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var f = Factory(clock);
        var issued = await IssueAsync(f, VerificationPurposes.EmailChange, "new@fixture.test");
        Assert.Matches("^[0-9]{6}$", issued.Code);
        var stored = await RowAsync(f, issued.Challenge.Id);
        Assert.Equal(32, stored.CodeHash.Length);
        Assert.Equal(VerificationChallenges.Hash(stored.Id, issued.Code), stored.CodeHash);
        Assert.DoesNotContain(issued.Code, Convert.ToHexString(stored.CodeHash), StringComparison.Ordinal);

        Assert.Equal("verification_expired", (await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.Reauthentication, issued.Code)).Error!.Code);
        var verified = await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.EmailChange, issued.Code);
        Assert.True(verified.IsSuccess);
        Assert.Equal("new@fixture.test", verified.Value!.Target);
        Assert.Equal("verification_expired", (await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.EmailChange, issued.Code)).Error!.Code);
    }

    [Fact]
    public async Task ConcurrentConfirmationsSucceedOnlyOnce()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var f = Factory(clock);
        var issued = await IssueAsync(f, VerificationPurposes.Reauthentication, null);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.Reauthentication, issued.Code)));
        Assert.Equal(1, results.Count(x => x.IsSuccess));
    }

    [Fact]
    public async Task CodesExpireAfterTheConfiguredMinutes()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var f = Factory(clock);
        var issued = await IssueAsync(f, VerificationPurposes.Reauthentication, null);
        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal("verification_expired", (await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.Reauthentication, issued.Code)).Error!.Code);
    }

    [Fact]
    public async Task FiveWrongAttemptsRevokeTheCode()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var f = Factory(clock);
        var issued = await IssueAsync(f, VerificationPurposes.Reauthentication, null);
        var wrong = issued.Code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 4; i++)
            Assert.Equal("verification_code_invalid", (await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.Reauthentication, wrong)).Error!.Code);
        Assert.Equal("verification_expired", (await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.Reauthentication, wrong)).Error!.Code);
        Assert.Equal("verification_expired", (await VerifyAsync(f, issued.Challenge.Id, VerificationPurposes.Reauthentication, issued.Code)).Error!.Code);
        var row = await RowAsync(f, issued.Challenge.Id);
        Assert.Equal(5, row.Attempts);
        Assert.NotNull(row.RevokedAt);
    }

    [Fact]
    public async Task ANewCodeRevokesTheOldOneAndIssuingIsRateLimited()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var f = Factory(clock);
        var first = await IssueAsync(f, VerificationPurposes.Reauthentication, null);
        Assert.Equal("verification_cooldown", (await TryIssueAsync(f, VerificationPurposes.Reauthentication)).Error!.Code);
        clock.Advance(TimeSpan.FromSeconds(61));
        var second = await IssueAsync(f, VerificationPurposes.Reauthentication, null);
        Assert.NotNull((await RowAsync(f, first.Challenge.Id)).RevokedAt);
        Assert.Equal("verification_expired", (await VerifyAsync(f, first.Challenge.Id, VerificationPurposes.Reauthentication, first.Code)).Error!.Code);
        for (var i = 0; i < 3; i++) { clock.Advance(TimeSpan.FromSeconds(61)); await IssueAsync(f, VerificationPurposes.Reauthentication, null); }
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal("verification_rate_limited", (await TryIssueAsync(f, VerificationPurposes.EmailChange)).Error!.Code);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.True((await TryIssueAsync(f, VerificationPurposes.EmailChange)).IsSuccess);
        Assert.NotEqual(first.Challenge.Id, second.Challenge.Id);
    }

    [Fact]
    public async Task TheDatabaseAllowsOnlyOneOpenCodePerPurpose()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var f = Factory(clock);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        VerificationChallenge Row() => new() { UserId = UserId, Purpose = VerificationPurposes.Reauthentication, CodeHash = new byte[32], CreatedAt = clock.GetUtcNow(), ExpiresAt = clock.GetUtcNow().AddMinutes(15) };
        db.AddRange(Row(), Row());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task<IssuedChallenge> IssueAsync(AppFactory f, string purpose, string? target)
    {
        var issued = await TryIssueAsync(f, purpose, target);
        Assert.True(issued.IsSuccess, issued.Error?.Code);
        return issued.Value!;
    }

    private static async Task<AiNexus.Platform.Errors.Result<IssuedChallenge>> TryIssueAsync(AppFactory f, string purpose, string? target = null)
    {
        using var scope = f.Services.CreateScope();
        var issued = await scope.ServiceProvider.GetRequiredService<VerificationChallenges>().IssueAsync(UserId, purpose, target, CancellationToken.None);
        if (issued.IsSuccess) await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        return issued;
    }

    private static async Task<AiNexus.Platform.Errors.Result<VerificationChallenge>> VerifyAsync(AppFactory f, Guid id, string purpose, string code)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<VerificationChallenges>().VerifyAsync(UserId, id, purpose, code, CancellationToken.None);
    }

    private static async Task<VerificationChallenge> RowAsync(AppFactory f, Guid id)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<VerificationChallenge>().AsNoTracking().SingleAsync(x => x.Id == id);
    }
}
