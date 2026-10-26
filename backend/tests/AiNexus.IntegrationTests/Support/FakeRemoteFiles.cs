using AiNexus.Platform.RemoteFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 取代真正的遠端檔案來源（例如連公文系統網站的 <c>GdFileSource</c>）：以 <see cref="Name"/> 註冊，<see cref="Files"/> 是位置對應的結果，
/// 沒列到的位置回 NotFound；<see cref="Reads"/> 記下被讀過的位置。
/// </summary>
internal sealed class FakeRemoteFiles(string name) : IRemoteFileSource
{
    public string Name { get; } = name;

    public Dictionary<string, RemoteFileRead> Files { get; } = new(StringComparer.Ordinal);

    public List<string> Reads { get; } = [];

    public Task<RemoteFileRead> ReadAsync(string reference, CancellationToken ct)
    {
        lock (Reads) Reads.Add(reference);
        return Task.FromResult(Files.GetValueOrDefault(reference, RemoteFileRead.NotFound));
    }

    /// <summary>移除所有已註冊的來源，只留這一個，測試不會連到外部網站。</summary>
    public void Register(IServiceCollection services)
    {
        services.RemoveAll<IRemoteFileSource>();
        services.AddSingleton<IRemoteFileSource>(this);
    }
}
