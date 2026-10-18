namespace AiNexus.Features.Work.Repositories.Browsing;

/// <summary>程式庫清單與詳情共用的摘要；<c>Url</c> 只有在 owner/name 有效時才指向程式庫頁面，否則指向 Gitea 主機首頁。</summary>
public sealed record RepositoryDto(string FullName, string Description, bool Private, bool Archived, bool Empty, string DefaultBranch, string Language,
    int Stars, int Forks, int OpenIssues, int OpenPulls, DateTimeOffset? UpdatedAt, string Url, bool Pinned);
