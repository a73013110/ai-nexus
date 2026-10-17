namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// 應用程式 DbContext 管理範圍之外的獨立 SQL Server 資料庫定義契約（如伺服器 master 系統資料庫、唯讀外部整合來源）。
/// 所屬模組宣告其連接的 <c>Databases</c> 設定鍵；連線則由 <see cref="DatabaseOptions"/> 統一建置。
/// </summary>
public interface ISqlDatabaseDefinition
{
    /// <summary><c>Databases</c> 區段下存放連線設定之設定鍵名稱。</summary>
    static abstract string Name { get; }

    /// <summary>同伺服器上欲替換開啟之資料庫名稱（如 <c>master</c>）；<see langword="null"/> 表示開啟設定鍵指定的資料庫。</summary>
    static virtual string? Catalog => null;

    /// <summary>若該模組僅對此資料庫進行查詢讀取，則此值應為 true，對應設定必須配置 <c>ReadOnly=true</c>，否則啟動時將阻斷。</summary>
    static virtual bool ReadOnly => false;
}
