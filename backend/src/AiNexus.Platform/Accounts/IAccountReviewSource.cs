namespace AiNexus.Platform.Accounts;

/// <summary>
/// 帳號待辦的一項：某位使用者因帳號停用（離職）或復職，需要管理員判斷處理的事（ADR 0019）。
/// <paramref name="Code"/> 是穩定代碼 <c>&lt;module&gt;.&lt;snake_case&gt;</c>；<paramref name="Feature"/> 是處理頁的功能 ID，沒有處理頁時為 null，
/// 檢視者沒有該功能時不給連結；<paramref name="Query"/> 是接在處理頁網址後的查詢字串（不含 <c>?</c>）。
/// </summary>
public sealed record AccountReviewItem(Guid UserId, string Code, string Title, string? Detail = null, string? Feature = null, string? Query = null);

/// <summary>一個來源這次讀到的待辦。<paramref name="Notice"/> 不為 null 表示這次沒能完整檢查（例如目錄無法連線），畫面要明確顯示，不能當成沒有待辦。</summary>
public sealed record AccountReviewReading(IReadOnlyList<AccountReviewItem> Items, string? Notice = null);

/// <summary>
/// 帳號待辦的來源，由擁有該資料的模組實作：待辦是查詢結果、不另存狀態，條件成立就出現，處理完就消失。
/// 和 <c>IDashboardSource</c> 一樣是各層模組都要實作的組合擴充點，放在 Platform 才不會造成依賴循環。
/// 來源共用同一個請求的 DbContext、依序讀取；只回有待辦的使用者，不要載入整張使用者表的資料。
/// </summary>
public interface IAccountReviewSource
{
    Task<AccountReviewReading> ReadAsync(CancellationToken ct);
}

public static class AccountReviewSourceRegistration
{
    /// <summary>註冊帳號待辦來源（Scoped）。</summary>
    public static IServiceCollection AddAccountReviewSource<TSource>(this IServiceCollection services) where TSource : class, IAccountReviewSource
        => services.AddScoped<IAccountReviewSource, TSource>();
}
