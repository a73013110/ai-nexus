# 外部資料庫

結論：別的系統的資料庫（目前是公文系統 GD）一律經 Platform 的 [`ExternalDatabase<TDatabase>`](../../backend/src/AiNexus.Platform/Data/External/ExternalDatabase.cs) 存取：連不上時回傳 null 讓呼叫端降級、失敗分類、只在狀態轉換時記日誌、自動出現在監控。為什麼直連資料庫、為什麼這樣分層見 [ADR 0020](../decisions/0020-external-databases.md)。

## 分層

| 層 | 放什麼 | 例 |
| --- | --- | --- |
| Platform | 通用存取、監控、註冊，寫一次 | `ExternalDatabase<T>`、`ExternalDatabaseMonitoring<T>`、`AddExternalDatabase<T>()` |
| 系統模組 `Features/Platform/<系統>/` | 資料庫定義，以及**多個模組共用**的該系統知識 | `GdDatabase`、`GdAccounts`（使用者 ↔ 公文帳號） |
| 使用端模組 | 自己的業務 SQL | 出勤的 `LeaveSheetReader`、`GdCalendarWriter`；組織的 `GdUnitSource` |

使用端不注入 `ISqlDatabase<T>`，只用 `ExternalDatabase<T>`，否則降級與監控會漏掉那條路。

## 新增一個外部資料庫

1. 定義與模組，放在 `Features/Platform/<系統>/`：

   ```csharp
   public sealed class ErpDatabase : IExternalDatabaseDefinition
   {
       private ErpDatabase() { }
       public static string Name => "Erp";                 // Databases:Erp，監控 Id erp
       public static string DisplayName => "ERP 系統";      // 監控卡片、流量相依的名稱
       public static string Dependents => "採購單查詢";      // 監控摘要：「……暫時無法使用」
       public static bool ReadOnly => true;                // 只讀時宣告，設定必須 ReadOnly=true
   }

   public sealed class ErpModule : IFeatureModule
   {
       public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddExternalDatabase<ErpDatabase>();
   }
   ```

2. `FeatureModules.AddFeatures` 加 `Add<ErpModule>(builder)`。
3. `appsettings.json` 的 `Databases` 加 `Erp` 一項（伺服器、名稱、逾時、`ReadOnly`），帳密放秘密檔；欄位見 [設定與秘密](../development/configuration.md)。
4. 使用端注入 `ExternalDatabase<ErpDatabase>`，SQL 寫在使用端。
5. 在該系統建專用帳號，只給用到的物件權限；清單寫在該功能的文件（GD 見 [出勤與公文系統 GD](../features/attendance-gd.md#連線與帳號權限)）。

不必寫：設定區段、Options 類別、監控來源、日誌事件、連線字串。

## 存取規則

- **讀取**：`QueryAsync`／`ReadAsync`。結果為 null（或 `Failure` 有值）表示不可用，與空清單（沒有資料）不同；呼叫端決定降級（顯示「無法取得」）還是拒絕（回 503）。`ReadAsync` 帶原因，給要告訴使用者「未設定」「被拒」的呼叫端。
- **長 `IN` 清單**：`QueryInAsync`／`ReadInAsync` 每 500 個鍵一批（SQL Server 一個命令最多 2100 個參數），任何一批失敗整個回 null。
- **寫入**：需要跨命令交易時用 `OpenAsync` 自己管理連線與交易；先看 `Configured`、`ReadOnly`。這條路不更新可用狀態，失敗由呼叫端記日誌。寫入範圍寫在 SQL，不靠帳號權限。
- **逾時**：`Databases:<Name>:CommandTimeoutSeconds` 寫進連線字串，所有命令（含 `OpenAsync` 開的連線）沿用；外部系統設短一點，讓頁面早點降級。
- **例外**：呼叫端取消與程式錯誤（欄位型別對不上等）照常拋出，不算對方的失敗。

## 失敗分類與監控

| 分類 | 條件 | 監控 |
| --- | --- | --- |
| 未設定 | `Databases:<Name>` 不完整 | idle，不探測 |
| 連不上 | 網路、主機、登入、資料庫離線等 | failing，成功一次即恢復 |
| 逾時 | 用戶端逾時（-2）或探測超過 3 秒 | failing |
| 查詢被拒 | 207、208、229、230、4104：表或欄位不存在、沒有權限 | degraded，最後一次被拒後 1 小時恢復 |

- 狀態只在記憶體，重啟後歸零。監控讀取時若 30 秒內沒有任何存取，先 `SELECT 1` 探測；只在有人看監控時發生，有人在用時不多打對方。
- `SELECT 1` 測不出某張表被拒，所以被拒以時間窗恢復。「寫入」一欄只依 `ReadOnly` 顯示，不探測寫入權限。
- 日誌 3407 不能用、3408 恢復、3411 查詢被拒，`Database` 欄位是設定鍵（[日誌事件](log-events.md)）。
