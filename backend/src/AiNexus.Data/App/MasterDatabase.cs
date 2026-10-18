using AiNexus.Platform.Data.Sql;

namespace AiNexus.Data.App;

/// <summary>
/// Nexus 伺服器之 <c>master</c> 系統資料庫連線定義。
/// </summary>
/// <remarks>
/// 只在系統初始化（如 <see cref="DatabaseInitializer"/>）時使用，用來檢查或建立 <c>AiNexus</c> 資料庫。
/// 業務運行期間絕不連線 master。
/// </remarks>
public sealed class MasterDatabase : ISqlDatabaseDefinition
{
    private MasterDatabase() { }

    public static string Name => AppDbContext.DatabaseName;
    public static string? Catalog => "master";
}
