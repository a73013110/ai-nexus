# 0011 EF Core、Dapper 與 procedure 的分工

- **狀態**：提議（2026-10-18）
- **決定**：EF Core 負責簡單的單表讀寫；列表、報表、多表查詢用 SQL（procedure 或 `.sql` 檔）經 Dapper 執行；授權留在程式。
- **修訂**：[ADR 0003](0003-single-dbcontext.md)（改為每個讀寫資料庫一個 DbContext；主資料庫內仍只有一個）。

## 分工

| 情境 | 用 |
|---|---|
| 單表或單一聚合的新增、修改、刪除；依主鍵讀取；簡單篩選 | EF Core |
| 列表、報表、搜尋、多表 join 或彙總 | Dapper：procedure，或放在 use case 旁的 `.sql` 嵌入資源 |
| 需要在客戶端熱修的業務邏輯 | procedure |
| 授權（誰能看、能改什麼） | 程式（`ICurrentUser`、資源 ACL）；procedure 只接收 `@UserId` 等參數並據此過濾 |

- 不在 C# 字串裡寫長 SQL。只有一兩行的簡單 SQL 可內嵌；其餘放 `<UseCase>.sql`，與 use case 同名、同資料夾。
- procedure 名稱＝`usp_`＋use case 名稱，放在擁有該 use case 的模組 schema，例如 `ListAdminUsers` → `administration.usp_ListAdminUsers`。從程式搜尋名稱即可找到兩端。
- 規則選不出來時，選讀起來最短的那個。

## 原因

- 團隊對 SQL 與 Dapper 比 LINQ 熟；多表查詢寫成 LINQ（例如目前的 `ListAdminUsers`）要分好幾次查詢再在記憶體組合，讀起來吃力，效能也不如一次 join。
- 單表 CRUD 寫成 procedure 只增加維護點；EF 的變更追蹤與並行控制（concurrency token）在寫入時仍然有用。

## 讀可跨模組，寫有歸屬

- SQL 查詢可以 join 任何 schema 的表；查詢不經過 domain 規則，限制它只會讓報表變得更難寫。
- **寫入只由擁有該 schema 的模組執行**；其他模組要改資料，呼叫對方的 `public` 服務或發 domain event（[模組邊界](../architecture/module-boundaries.md)）。

## 必須知道的陷阱

- **procedure 寫入不會觸發 `SaveChanges` 攔截器**：稽核紀錄與 domain event 都不會自動發生。需要稽核或副作用的寫入走 EF；以 procedure 寫入時，由 procedure 自己寫稽核表，程式明確發出事件。
- procedure 回傳欄位與 DTO 只在執行時對應：每支被程式呼叫的 procedure 至少要有一個整合測試。
- `ISqlDatabase<AppDbContext>` 與 EF 共用連線與交易，EF 寫入和 procedure 呼叫可以在同一個交易提交或回復。

## 多個資料庫

- 每個讀寫資料庫各一個 DbContext、各一個 SQL 專案，名稱依資料庫角色或名稱（`AppDbContext`、`ErpDbContext`）。唯讀外部來源維持 `ISqlDatabaseDefinition`，只走 Dapper。
- 一個 use case 只寫一個資料庫。
- 跨庫讀取寫在 procedure 或 view 中，透過本庫的 `syn_` synonym 引用，各環境資料庫名稱不同時只改 synonym。
- 兩個庫必須一起寫的少數情況：寫成一支 procedure，在單一交易內透過 synonym 寫另一庫（同一 instance 不升級 DTC）。**不在 C# 開兩個連線再以 `TransactionScope` 包起來**，那會升級成分散式交易。
- EF 不做跨庫查詢。

## EF 的角色

- 實體與欄位必須與資料庫一致：測試以 dacpac 建立的資料庫比對 EF 模型（表、欄位、型別、長度、nullable），不一致即失敗。
- 應用程式啟動時做同樣的唯讀比對：EF 對應到的欄位缺少或型別不同就停止啟動；資料庫多出的欄位只警告。資料庫可以先於程式加欄位，DBA 不必等程式發版。
- EF 組態只寫對應需要的內容（表、主鍵、長度、關聯、owned type、query filter），不重複宣告索引、約束與說明；這些只存在資料庫。
