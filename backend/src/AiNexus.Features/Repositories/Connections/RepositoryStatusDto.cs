namespace AiNexus.Features.Repositories.Connections;

/// <summary>連線狀態。<c>Connected</c> 只在儲存的主機與目前設定的主機相同時為 true；換了主機就必須重新連線。</summary>
public sealed record RepositoryStatusDto(bool Available, bool Connected, string BaseUrl, string? Login, DateTimeOffset? ConnectedAt);
