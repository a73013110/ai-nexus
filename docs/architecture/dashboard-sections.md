# 總覽區塊

結論：功能要出現在總覽，就在自己的模組實作 `IDashboardSource` 並註冊；前端不改也會以通用卡片顯示。需要特殊圖表時才在前端加專屬 widget。

## 後端：一個模組一個來源

1. 在模組根目錄新增 `<Module>Dashboard.cs`（`internal sealed class`），實作 [`IDashboardSource`](../../backend/src/AiNexus.Platform/Dashboard/IDashboardSource.cs)：`Feature` 是決定是否顯示的功能 grant。總覽對每個人都一樣，不加管理員專用的區塊；平台層級的監控與帳號狀況放在各自的管理頁。
2. 在模組的 `AddServices` 呼叫 `services.AddDashboardSource<XxxDashboard>()`。
3. 數字由一支 procedure `<schema>.usp_Get<Module>Dashboard` 一次往返取回，以 [`DashboardProcedure.QueryDashboardAsync`](../../backend/src/AiNexus.Platform/Dashboard/DashboardProcedure.cs) 呼叫；schema 是擁有該模組的 schema，模組沒有自己的 schema 時用主要資料表的（Chat → `conversations`）。檔頭與排版見 [SQL 物件](sql-objects.md)。
4. 用 [`DashboardSection`](../../backend/src/AiNexus.Platform/Dashboard/DashboardSection.cs) 組出內容：`Count`（目前數量）、`Period`（依查詢期間）、`Size`、`Daily`、`Share`、`Item`。範例：[`JobsDashboard`](../../backend/src/AiNexus.Features/Work/Jobs/JobsDashboard.cs) 與 `jobs.usp_GetJobsDashboard`。
5. 在 `backend/tests/AiNexus.IntegrationTests/Dashboard/DashboardProcedureTests.cs` 的 `Shapes` 登記結果集形狀，在 `GetDashboardTests.cs` 或模組自己的測試驗證數字口徑，並重產合約（`Export-Contracts.ps1`）。

procedure 的寫法：

- 參數固定為 `@Owner`（NULL＝全平台）、`@Actor`、`@From`、`@Until`、`@OffsetMinutes`，沒用到也要宣告；來源自己的參數（例如 `TimeProvider` 的現在時間）接在後面。時間一律由程式傳入。
- 結果集依序：計數一列 → 每日 `(Day, Value)` → 分佈 `([Key], Value)` → 最近項目；沒有的省略，其餘順序不變。COUNT 對應 `int`、位元組 `long`、平均 `double?`，欄位名稱與順序要和 C# record 一致。
- 每日序列一律 `CROSS APPLY [dbo].[udf_DashboardDay](時間, @From, @Until, @OffsetMinutes)`：同時套用期間並依檢視者的 UTC 位移切日，所有區塊的日界才一致。
- 軟刪除要自己寫（`Resources.IsDeleted`、`Conversations.IsDeleted`）：procedure 不經過 EF 的 `SoftDelete` filter。
- 依 `@Owner` 篩選的查詢加 `OPTION (RECOMPILE)`，個人範圍才會用 `OwnerId` 索引，不沿用全平台的掃描計畫。
- 只回數字與原始值；文字、色調、顯示名稱、哪些指標出現與授權都留在 C#。

規則：

- `DashboardScope.Owner` 為 `null` 代表整個平台；依它過濾擁有者。`Item` 一律只列 `Actor` 自己的資料，平台範圍也一樣。
- 只放使用者自己的工作與用量。設定或服務狀態（「已啟用／待設定」、檢索方式、外部服務可用性）放在管理頁，所以 builder 沒有 `Fact`，合約中的 `facts` 一律為空；依賴的服務未設定、沒有可數的資料時回傳 `null`，不顯示空卡片。
- 未知值傳 `null`，不要傳 0；錯誤內容比照原功能遮罩（例：失敗通知只顯示查證訊息）。
- 標題留空時用該功能在 `accesscontrol.Features` 的名稱與路由；不屬於任何頁面的區塊（通知）傳空字串路由。
- 帶 `Danger` 且大於 0 的指標會讓區塊進入 attention 狀態；`ActiveWhen` 標出進行中的工作，主視覺據此呈現。
- 來源共用同一個請求的連線，依序執行；每個來源一次往返，只回彙總與少量項目，不要載入整張表。

`IDashboardSource` 放在 `backend/src/AiNexus.Platform/Dashboard`：它和 `IFeatureModule` 一樣是組合各模組的擴充點，各層模組都要實作，放進任何業務模組都會造成依賴循環。

## 前端：通用卡片與專屬 widget

- 每個區塊自動成為 `section:<id>` 卡片（`SectionWidget`）。
- 專屬 widget 寫在 `frontend/src/app/features/work/dashboard/widgets/`，從 `DashboardStore` 讀資料，再加進 [`dashboard-widgets.ts`](../../frontend/src/app/features/work/dashboard/widgets/dashboard-widgets.ts) 的清單與預設順序。
- 區塊圖示在 `dashboard-format.ts` 的 `SECTION_ICONS`；未列出的用通用圖示。
- 卡片外觀由 `tokens.scss` 共用的 `--tile-*` 變數決定，widget 只用 token 與這些變數；頭條數字要能在最窄的卡片裡縮小或縮寫，不能溢出。
- 移除區塊或 widget 時，把它的 id（區塊為 `section:<id>`）加進 `dashboard-layout.ts` 的 `RETIRED`，讓舊的已存版面讀取時直接略過。
