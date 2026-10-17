namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// DbContext 管理範圍之外的獨立 SQL Server 資料庫定義（如 master 系統資料庫、唯讀的外部整合來源）。
/// 所屬模組宣告它使用的 <c>Databases</c> 設定鍵，連線字串統一由 <see cref="DatabaseOptions"/> 組裝。
/// </summary>
public interface ISqlDatabaseDefinition
{
    /// <summary><c>Databases</c> 區段下存放連線設定之設定鍵名稱。</summary>
    static abstract string Name { get; }

    /// <summary>同伺服器上欲替換開啟之資料庫名稱（如 <c>master</c>）；<see langword="null"/> 表示開啟設定鍵指定的資料庫。</summary>
    static virtual string? Catalog => null;

    /// <summary>模組只對此資料庫做查詢時設為 true；對應設定必須有 <c>ReadOnly=true</c>，否則啟動時會阻斷。</summary>
    static virtual bool ReadOnly => false;
}
