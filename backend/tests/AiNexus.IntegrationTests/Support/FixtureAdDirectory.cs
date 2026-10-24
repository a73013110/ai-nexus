using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 測試用 AD 目錄（虛構人員）：alice、bob 與 <see cref="FixtureAdAuthenticator"/> 同一個 SID，匯入後再登入會對到同一位使用者；
/// 其他人只能匯入、不能登入。測試可以改名、停用、移除人員或讓目錄暫時無法連線。節點與人數的規則與 LDAP 實作相同（人數含子節點）。
/// </summary>
public sealed class FixtureAdDirectory : IAdDirectory
{
    public const string Root = "DC=fixture,DC=internal";
    public const string Research = "OU=研發處," + Root;
    public const string Platform = "OU=平台部," + Research;
    public const string Users = "CN=Users," + Root;
    public const string Carol = "S-1-5-21-1000-2000-3000-1101";
    public const string Dave = "S-1-5-21-1000-2000-3000-1102";
    public const string Erin = "S-1-5-21-1000-2000-3000-1103";
    private readonly Lock gate = new();
    private readonly (string Dn, string Kind)[] nodes =
    [
        ("OU=資訊處," + Root, DirectoryNodeKinds.OrganizationalUnit),
        (Research, DirectoryNodeKinds.OrganizationalUnit),
        (Platform, DirectoryNodeKinds.OrganizationalUnit),
        ("OU=業務處," + Root, DirectoryNodeKinds.OrganizationalUnit),
        (Users, DirectoryNodeKinds.Container),
        ("CN=Computers," + Root, DirectoryNodeKinds.Container),
    ];
    private readonly List<DirectoryPerson> people =
    [
        new("S-1-5-21-fixture-alice", "CN=alice,OU=資訊處," + Root, "alice", "alice", "Alice@Fixture.TEST", "資訊處", "工程師", false),
        new("S-1-5-21-fixture-bob", "CN=bob,OU=資訊處," + Root, "bob", "bob", null, "資訊處", null, false),
        new(Carol, "CN=林可蘿," + Platform, "carol.lin", "林可蘿", "carol.lin@example.test", "研發處", "資深工程師", false, "carol.lin@fixture.internal"),
        new(Dave, "CN=吳大衛," + Platform, "dave.wu", "吳大衛", "dave.wu@example.test", "研發處", null, true),
        new(Erin, "CN=陳艾琳,OU=業務處," + Root, "erin.chen", "陳艾琳", "erin.chen@example.test", "業務處", "專員", false),
    ];

    /// <summary>true 時每次查詢都像服務帳號 bind 失敗一樣拋出 <c>directory_unavailable</c>。</summary>
    public bool Unavailable { get; set; }

    public bool Writable => false;

    /// <summary>以 SID 取代目錄中的人（例如改名、停用）；SID 不存在時新增。</summary>
    public void Put(DirectoryPerson person)
    {
        lock (gate) { people.RemoveAll(x => x.Sid == person.Sid); people.Add(person); }
    }

    public DirectoryPerson Get(string sid) { lock (gate) return people.Single(x => x.Sid == sid); }

    public void Remove(string sid) { lock (gate) people.RemoveAll(x => x.Sid == sid); }

    public Task<DirectoryBranch?> ChildrenAsync(string dn, CancellationToken ct)
    {
        Fail();
        if (!Exists(dn)) return Task.FromResult<DirectoryBranch?>(null);
        lock (gate)
        {
            var children = nodes.Where(x => Parent(x.Dn).Equals(dn, StringComparison.OrdinalIgnoreCase))
                .Select(x => new DirectoryNode(x.Dn, DirectoryNames.NameOf(x.Dn), x.Kind, Count(x.Dn), nodes.Any(y => DirectoryNames.Below(y.Dn, x.Dn))))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            return Task.FromResult<DirectoryBranch?>(new(Count(dn), children));
        }
    }

    public Task<DirectoryListing?> PeopleAsync(string dn, bool subtree, string? query, int limit, CancellationToken ct)
    {
        Fail();
        if (!Exists(dn)) return Task.FromResult<DirectoryListing?>(null);
        lock (gate)
        {
            var found = people.Where(x => subtree ? DirectoryNames.Within(x.Dn, dn) : Parent(x.Dn).Equals(dn, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(query) || new[] { x.DisplayName, x.AccountName, x.Upn, x.Email, x.Department, x.Title }
                    .Any(value => value?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            return Task.FromResult<DirectoryListing?>(new([.. found.Take(limit)], found.Count > limit));
        }
    }

    public Task<IReadOnlyList<DirectoryPerson>> FindAsync(IReadOnlyCollection<string> sids, CancellationToken ct)
    {
        Fail();
        lock (gate) return Task.FromResult<IReadOnlyList<DirectoryPerson>>([.. people.Where(x => sids.Contains(x.Sid, StringComparer.OrdinalIgnoreCase))]);
    }

    public Task<DirectoryPerson?> FindAccountAsync(string accountName, CancellationToken ct)
    {
        Fail();
        lock (gate) return Task.FromResult(people.FirstOrDefault(x => x.AccountName.Equals(accountName.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>完整屬性由清單欄位推得，其他屬性是固定的虛構值；停用的人帶停用位元。</summary>
    public Task<DirectoryPersonDetail?> DetailAsync(string sid, CancellationToken ct)
    {
        Fail();
        lock (gate)
        {
            var x = people.FirstOrDefault(p => p.Sid.Equals(sid, StringComparison.OrdinalIgnoreCase));
            var created = new DateTimeOffset(2025, 1, 6, 1, 0, 0, TimeSpan.Zero);
            return Task.FromResult(x is null ? null : new DirectoryPersonDetail(x.Sid, null, x.Dn, x.AccountName, x.DisplayName, null, null, x.Upn, x.Email, null,
                null, "示範公司", x.Department, x.Title, null, null, null, null, 0x200 | (x.Disabled ? 2 : 0), null, created, false, null, null, 0, null, null, null,
                null, created, created, ["研發群組"], false));
        }
    }

    private bool Exists(string dn) => dn.Equals(Root, StringComparison.OrdinalIgnoreCase) || nodes.Any(x => x.Dn.Equals(dn, StringComparison.OrdinalIgnoreCase));

    private int Count(string dn) => people.Count(x => DirectoryNames.Within(x.Dn, dn));

    // 測試資料的 DN 沒有跳脫的逗號，第一個逗號之後就是上層。
    private static string Parent(string dn) => dn[(dn.IndexOf(',', StringComparison.Ordinal) + 1)..];

    private void Fail()
    {
        if (Unavailable) throw new ExternalServiceException(Error.Unavailable("directory_unavailable"), "fixture directory is offline");
    }
}
