namespace AiNexus.Features.Platform.Gd;

/// <summary>公文系統的一個人在某個部門的顯示資料；查不到（帳號已刪、部門已撤）的欄位是 null。</summary>
public sealed record GdPerson(string? Name, string? Unit, string? Title);

/// <summary>
/// 公文系統「人」的 SQL 片段，表單與流程共用，免得各自寫一份姓名、部門與職稱的對照。
/// 職稱跟著部門走（<c>Sys_User_DEPT</c> 一人一部門一列，<c>TitleCode</c> 對 <c>Sys_User_Title</c>），所以要同時給帳號與部門。
/// 流程的 <c>PERSON_ID</c> 是「部門_公文帳號」（GD 以 <c>CAST(DEP_NO AS varchar) + '_' + ID</c> 組成），用 <see cref="DepNoOf"/>、<see cref="UserIdOf"/> 拆開。
/// </summary>
public static class GdPeople
{
    /// <summary>「部門_帳號」的帳號部分；沒有底線時整個字串就是帳號。</summary>
    public static string UserIdOf(string column) => $"SUBSTRING({column}, CHARINDEX('_', {column}) + 1, 50)";

    /// <summary>「部門_帳號」的部門編號；沒有底線或不是數字時為 NULL。</summary>
    public static string DepNoOf(string column) => $"TRY_CAST(LEFT({column}, NULLIF(CHARINDEX('_', {column}), 0) - 1) AS int)";

    /// <summary>
    /// <c>OUTER APPLY (...) alias</c> 的內容，欄位是 <c>Name</c>、<c>Unit</c>、<c>Title</c>。<paramref name="userId"/>、<paramref name="depNo"/> 是外層的運算式（欄位或上面兩個片段），
    /// 由程式組成、不含使用者輸入。帳號不存在時仍回一列（全為 NULL），讓部門名稱照樣查得到。
    /// </summary>
    public static string Apply(string userId, string depNo) => $"""
        SELECT u.[NAME] AS Name, d.[DEP_NAME] AS Unit, t.[TitleName] AS Title
        FROM (SELECT 1 AS One) x
        LEFT JOIN [dbo].[Sys_User] u ON u.[ID] = {userId}
        LEFT JOIN [dbo].[Sys_User_DEPT_BAS] d ON d.[DEP_NO] = {depNo}
        OUTER APPLY (SELECT TOP 1 ud.[TitleCode] FROM [dbo].[Sys_User_DEPT] ud WHERE ud.[ID] = {userId} AND ud.[DEP_NO] = {depNo}) ud
        LEFT JOIN [dbo].[Sys_User_Title] t ON t.[TitleID] = ud.[TitleCode]
        """;

    /// <summary>空白字串當成沒有；GD 的 char 欄位會補尾端空白。</summary>
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static GdPerson? Person(string? name, string? unit, string? title)
        => Text(name) is null && Text(unit) is null ? null : new(Text(name), Text(unit), Text(title));
}
