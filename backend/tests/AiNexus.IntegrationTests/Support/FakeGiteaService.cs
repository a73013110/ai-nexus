using AiNexus.Features.Work.Repositories.Access;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 服務帳號看到的 Gitea：組織 hanglong 下的儲存庫、各庫的團隊與協作者、存在的帳號。同步任務寫入的協作者留在這裡，測試直接檢查。
/// 預設有 hanglong/nexus（團隊 Owners）與帳號 alice、bob、carol、dave、deploy-bot。
/// </summary>
internal sealed class FakeGiteaService : IGiteaServiceClient
{
    public const string ServiceAccount = "ainexus-service";
    private readonly Lock gate = new();
    private readonly Dictionary<long, GiteaRepository> repositories = [];
    private readonly Dictionary<string, List<string>> teams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> collaborators = new(StringComparer.OrdinalIgnoreCase);

    public FakeGiteaService()
    {
        AddRepository(1, "nexus");
        Users.UnionWith(["alice", "bob", "carol", "dave", "deploy-bot", ServiceAccount]);
    }

    public HashSet<string> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>設定後每個呼叫都丟這個錯誤（模擬 Gitea 離線或權杖失效）。</summary>
    public Error? Failure { get; set; }

    public void AddRepository(long id, string name, string owner = "hanglong", params string[] repositoryTeams)
    {
        lock (gate)
        {
            repositories[id] = new(id, owner, name, $"{name} 說明", true, false);
            teams[$"{owner}/{name}"] = ["Owners", .. repositoryTeams];
            collaborators.TryAdd($"{owner}/{name}", new(StringComparer.OrdinalIgnoreCase));
        }
    }

    public void RemoveRepository(long id)
    {
        lock (gate) repositories.Remove(id);
    }

    /// <summary>某庫目前的協作者與權限。</summary>
    public IReadOnlyDictionary<string, string> CollaboratorsOf(string fullName)
    {
        lock (gate) return new Dictionary<string, string>(collaborators[fullName], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>直接在 Gitea 上加協作者（不經過 AI Nexus），模擬未登記的存取。</summary>
    public void AddCollaboratorDirectly(string fullName, string login, string permission)
    {
        lock (gate) collaborators[fullName][login] = permission;
    }

    public Task<string> AccountAsync(CancellationToken ct) => Run(() => ServiceAccount);

    public Task<IReadOnlyList<string>> OrganizationsAsync(CancellationToken ct)
        => Run<IReadOnlyList<string>>(() => [.. repositories.Values.Select(x => x.Owner).Distinct()]);

    public Task<IReadOnlyList<GiteaRepository>> RepositoriesAsync(string organization, CancellationToken ct)
        => Run<IReadOnlyList<GiteaRepository>>(() => [.. repositories.Values.Where(x => x.Owner == organization).OrderBy(x => x.Id)]);

    public Task<IReadOnlyList<string>> TeamsAsync(string owner, string name, CancellationToken ct)
        => Run<IReadOnlyList<string>>(() => [.. teams[$"{owner}/{name}"]]);

    public Task<IReadOnlyList<string>> CollaboratorsAsync(string owner, string name, CancellationToken ct)
        => Run<IReadOnlyList<string>>(() => [.. Repository(owner, name).Keys]);

    public Task<string> PermissionAsync(string owner, string name, string login, CancellationToken ct)
        => Run(() => Repository(owner, name).GetValueOrDefault(login, "read"));

    public Task SetCollaboratorAsync(string owner, string name, string login, string permission, CancellationToken ct) => Run(() =>
    {
        if (!Users.Contains(login)) throw new ExternalServiceException(Error.Conflict("gitea_account_missing"));
        return Repository(owner, name)[login] = permission;
    });

    public Task RemoveCollaboratorAsync(string owner, string name, string login, CancellationToken ct) => Run(() => Repository(owner, name).Remove(login));

    public Task<bool> UserExistsAsync(string login, CancellationToken ct) => Run(() => Users.Contains(login));

    private Dictionary<string, string> Repository(string owner, string name)
        => collaborators.TryGetValue($"{owner}/{name}", out var found) ? found : throw new ExternalServiceException(Error.NotFound("gitea_not_found"));

    private Task<T> Run<T>(Func<T> call)
    {
        lock (gate)
        {
            if (Failure is { } failure) throw new ExternalServiceException(failure);
            return Task.FromResult(call());
        }
    }
}
