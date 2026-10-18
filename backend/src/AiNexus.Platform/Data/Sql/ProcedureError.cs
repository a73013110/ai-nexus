using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// procedure 回報預期內失敗的約定：<c>THROW 50000, N'&lt;error_code&gt;', 1;</c>，訊息就是模組錯誤代碼（例如 <c>price_exists</c>）。
/// 其他錯誤號碼是程式或資料錯誤，照常往上拋。
/// </summary>
public static class ProcedureError
{
    public const int Number = 50000;

    /// <summary>預期內失敗的錯誤代碼；不是 <see cref="Number"/> 的錯誤回傳 null。</summary>
    public static string? Code(Exception error) => error is SqlException { Number: Number } sql ? sql.Message : null;
}
