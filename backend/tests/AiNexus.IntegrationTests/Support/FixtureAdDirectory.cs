using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 測試用 AD 目錄（虛構人員）：alice、bob 與 <see cref="FixtureAdAuthenticator"/> 同一個 SID，匯入後再登入會對到同一位使用者；
/// 其他人只能匯入、不能登入。測試可以改名、停用、移除人員或讓目錄暫時無法連線。比對規則與 LDAP 篩選相同（前綴＋顯示名稱包含）。
/// </summary>
public sealed class FixtureAdDirectory : IAdDirectory
{
    public const string Carol = "S-1-5-21-1000-2000-3000-1101";
    public const string Dave = "S-1-5-21-1000-2000-3000-1102";
    public const string Erin = "S-1-5-21-1000-2000-3000-1103";
    private readonly Lock gate = new();
    private readonly List<DirectoryPerson> people =
    [
        new("S-1-5-21-fixture-alice", "alice", "alice", "Alice@Fixture.TEST", "資訊處", "工程師", "資訊處", false),
        new("S-1-5-21-fixture-bob", "bob", "bob", null, "資訊處", null, "資訊處", false),
        new(Carol, "carol.lin", "林可蘿", "carol.lin@example.test", "研發處", "資深工程師", "研發處 › 平台部", false),
        new(Dave, "dave.wu", "吳大衛", "dave.wu@example.test", "研發處", null, "研發處 › 平台部", true),
        new(Erin, "erin.chen", "陳艾琳", "erin.chen@example.test", "業務處", "專員", "業務處", false),
    ];

    /// <summary>true 時每次查詢都像服務帳號 bind 失敗一樣拋出 <c>directory_unavailable</c>。</summary>
    public bool Unavailable { get; set; }

    /// <summary>以 SID 取代目錄中的人（例如改名、停用）；SID 不存在時新增。</summary>
    public void Put(DirectoryPerson person)
    {
        lock (gate) { people.RemoveAll(x => x.Sid == person.Sid); people.Add(person); }
    }

    public DirectoryPerson Get(string sid) { lock (gate) return people.Single(x => x.Sid == sid); }

    public void Remove(string sid) { lock (gate) people.RemoveAll(x => x.Sid == sid); }

    public Task<DirectorySearchResult> SearchAsync(string keyword, int limit, CancellationToken ct)
    {
        Fail();
        lock (gate)
        {
            var found = people.Where(x => Starts(x.AccountName, keyword) || Starts(x.Email, keyword) || Starts(x.Department, keyword)
                || x.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            return Task.FromResult(new DirectorySearchResult([.. found.Take(limit)], found.Count > limit));
        }
    }

    public Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct)
    {
        Fail();
        lock (gate) return Task.FromResult<IReadOnlyList<DirectoryPerson>>([.. people.Where(x => sids.Contains(x.Sid, StringComparer.OrdinalIgnoreCase))]);
    }

    private void Fail()
    {
        if (Unavailable) throw new ExternalServiceException(Error.Unavailable("directory_unavailable"), "fixture directory is offline");
    }

    private static bool Starts(string? value, string keyword) => value?.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) == true;
}
