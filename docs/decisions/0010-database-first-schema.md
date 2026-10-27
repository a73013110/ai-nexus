# 0010 資料庫優先：結構以開發庫為準

- **狀態**：採用（2026-10-18）
- **決定**：
  - 資料庫結構的唯一修改入口是**開發庫**：DBA 照舊在 SSMS 物件總管直接改表、procedure、view。
  - repo 的 `database/<資料庫>/` 只放 `scripts/Export-Database.ps1` 的匯出結果（SqlPackage Extract，每物件一檔），沒有人手改；一個 SDK-style SQL 專案 glob 這些檔案，`dotnet build` 產出 `.dacpac`。沒有人需要打開這個專案。
  - **不再使用 EF migration**：移除 `Migrations/`、`__EFMigrationsHistory` 與 migration 產生的發布腳本。EF Core 只做對應（[ADR 0011](0011-data-access-ef-dapper-procedures.md)）。
  - 發版以 dacpac 對目標庫產生差異報告與升級 SQL，DBA 審核後執行；種子資料、一次性資料修正、全文索引另有手寫腳本，依編號在結構前後執行。
- **修訂**（2026-10-27）：正式機不能放部署腳本、也不能裝 SqlPackage，所以發版改為手動：以 Visual Studio 結構描述比對產生升級 SQL，手寫腳本由 `Publish-IIS.ps1` 依執行順序編號放進套件（[資料庫建置與權限](../operations/database-setup.md#部署)）；漂移偵測改為定期手動比對。
- **修訂**：[ADR 0005](0005-sqlite-tests-sqlserver-opt-in.md)（測試改用從 dacpac 建立的 SQL Server，procedure 無法在 SQLite 執行；已由 [ADR 0015](0015-integration-tests-on-localdb.md) 取代）。

## 原因

- 團隊的資料庫負責人與程式負責人分開，結構調整一向在 SSMS 完成；程式再維護一套 migration，兩邊必然不一致。
- 複雜邏輯寫在 procedure，能在客戶端立即更新，不必等程式走完整發版流程。
- dacpac 讓比對只需要一個資料庫：「這版應有的結構」是檔案，目標是客戶庫。Visual Studio 結構描述比對可直接選 `.dacpac` 當來源，DBA 不必再準備新舊兩個庫。
- 匯出結果進版控後，每次結構變更都有作者、原因與程式改動在同一個 PR；`git diff` 直接看出改了哪個物件。

## 流程

```
開發庫（SSMS 修改）
  → Export-Database.ps1 匯出到 database/App/（PR 作者執行，只 stage 與本次有關的檔案）
  → dotnet build database/App → App.dacpac（引用錯誤在 build 時失敗）
  → Publish-IIS.ps1：套件帶 App.dacpac 與依序編號的手寫 SQL（Data/<版本>/、FullText/、Seed/）
  → 發版：結構描述比對 App.dacpac 與目標庫 → DBA 審核升級 SQL → 依編號在前後執行手寫 SQL
```

- 環境：開發庫＋PROD；大專案加 UAT。所有環境都用同一個 dacpac 比對，不會出現兩份不同的升級腳本。
- **漂移偵測**：定期以已發版 dacpac 對 PROD 做結構描述比對（[做法](../operations/database-verification.md#漂移偵測)）；多出的差異＝客戶端熱修尚未回收。開發庫則每晚和 repo 比對，抓「改了沒匯出」。只通知，不自動提交。
- **客戶端熱修**：允許直接改 procedure，但必須在開發庫做同樣修改並匯出提交；漂移報告為零才算結案。

## 資料夾

```
database/
  App/                 主資料庫（名稱依角色，fork 後不必改）
    App.sqlproj        設定 DSP、collation、RCSI；匯出不含資料庫層級選項
    <schema>/Tables|Views|Stored Procedures|Functions|Synonyms/…   匯出產生
    FullText/          全文目錄與索引：匯出腳本移入 Catalogs/、Indexes/，其餘手寫；偵測不到全文元件時跳過
    Seed/              手寫：基礎資料，可重複執行的 MERGE
    Data/<版本>/       手寫：一次性資料修正（pre-／post- 前綴決定在結構更新前或後執行）
  References/          外部資料庫的 dacpac（view 透過 synonym 引用外部庫時 build 需要）
```

匯出腳本只替換它產生的檔案，保留 `FullText/` 的手寫檔、`Seed/`、`Data/` 與 sqlproj；這三個資料夾不進 dacpac，schema 也不能取這些名稱。

## 發版參數

- `BlockOnPossibleDataLoss=True`：刪欄位、縮短長度等可能遺失資料時整份中止；確定要刪的那次由 DBA 審核後單次放行，或先以 `Data/<版本>/pre-*.sql` 搬移資料。
- `DropObjectsNotInSource=False`：熱修多出的物件不在發版時刪除，由漂移偵測列出。
- `GenerateSmartDefaults=False`：新增 NOT NULL 欄位卻沒有預設值時失敗，交給人決定。
- 忽略權限、使用者、角色、登入、檔案群組與資料行順序；不寫入資料庫選項與 collation（由建庫步驟負責）。

## 已知限制（PoC 實測）

- 全文索引無法用 `ExcludeObjectTypes` 排除，所以由匯出腳本移出到 `FullText/`；沒有全文元件的環境才能部署。
- view 透過 synonym 讀外部庫時，必須在 `References/` 放對方的 dacpac 才能 build；procedure 只有警告，可抑制。
- 不同資料庫匯出的欄位說明語句順序不同；匯出腳本排序後才不會產生只有順序差異的 diff。
- 本機測試用 LocalDB（SQL Server 2025 Express）可建出完整結構，但沒有全文元件。

## 何時重新評估

團隊改以程式主導結構（例如改由開發者自行維護資料表），或需要在應用程式啟動時自動升級結構時。
