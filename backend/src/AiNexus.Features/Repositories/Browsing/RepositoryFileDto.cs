namespace AiNexus.Features.Repositories.Browsing;

/// <summary>讀取到的文字檔內容；<c>Commit</c> 是讀取時固定的 commit，不是分支名稱。</summary>
public sealed record RepositoryFileDto(string Commit, string Path, string Text, long Size, string Url);
