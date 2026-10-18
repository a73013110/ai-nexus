namespace AiNexus.Features.Work.Repositories;

/// <summary>Gitea 標籤的名稱與顏色（<c>#rrggbb</c>，格式不合時為空字串）。</summary>
public sealed record RepositoryLabelDto(string Name, string Color);
