# 前端共用邊界

結論：業務 UI、store 與 `*-api.ts` 放 `features`；全站單例放 `core`；沒有業務狀態的通用程式放 `shared`。依賴方向由 ESLint 檢查，所有請求經過型別安全的 `ApiClient`，讀取一律用 `apiResource()`。

## 資料夾責任

路徑相對於 `frontend/src/app`。

| 位置 | 責任 |
| --- | --- |
| `core/api` | 產生的 OpenAPI 型別、`ApiClient`（JSON／multipart／SSE）、`apiResource()`、CSRF 與登入失效 |
| `core/auth` | 路由 guard、帳號世代與 `WorkspaceSession`、身分橫幅、功能名稱 |
| `core/errors` | 安全錯誤訊息、查證代碼、前端例外回報與未處理錯誤提示 |
| `core/layout` | 版面 shell（`FeaturePage`、側欄、導覽、首頁路徑）；唯一可以組合 features 的 core 資料夾 |
| `core/preferences` | 主題、帳號設定、本機草稿、跨頁新對話草稿交接 |
| `core/monitoring`、`core/stream` | 瀏覽器在線狀態；SSE 解析與生成串流 |
| `features/*` | 按路由載入的頁面、該功能的 `*-api.ts`、store 與業務元件；被多個功能共用的業務元件也留在 features（例：`features/workspace` 的 `ConversationActions`） |
| `shared/ui` | 無業務狀態的通用元件（Select、ActionMenu、ConfirmDialog、Notice…） |
| `shared/markdown` | Markdown 解析與檢視、串流 Markdown、Mermaid、程式碼區塊與語法上色 |
| `shared/browser` | ViewScope、autosize、拖放／貼圖、下載、複製、日期格式、popover 定位、未儲存提示 |
| `shared/graphics` | 不含認證／業務依賴的數學（傅立葉取樣／DFT、標誌輪廓） |
| `frontend/src/styles` | 跨頁共用樣式；規則見 [設計系統](DESIGN_SYSTEM.md#樣式載入) |

## 依賴方向

- 由 [`frontend/eslint.config.js`](../../frontend/eslint.config.js) 檢查：`shared` 不可引用 `features`；`core` 除了 `core/layout` 不可引用 `features`；`features` 之間可以互相引用。
- 需要登入的頁面都是 `app.routes.ts` 裡同一個 shell route 的子路由，登入檢查只宣告一次；新頁面加在 children 裡。
- 頁面使用 Signals、OnPush 與 zoneless。
- 新對話草稿交接只留在記憶體，綁定帳號世代與 conversation ID，讀取一次；不把來源全文放在 URL、history state 或 localStorage。

## 呼叫 API

- 一律透過 `core/api/api-client.ts` 的 `ApiClient`，第一個參數是 `contracts/openapi.json` 裡的路徑（含 `/api/v1`），`path`、`query`、`body` 與回傳值的型別都由 `schema.ts` 推導；路徑或欄位寫錯是編譯錯誤。
- 只有各功能的 `<feature>-api.ts`（與 `core`）可以注入 `ApiClient`，ESLint 會擋其他檔案；這些服務只是把操作命名，不宣告回傳型別、不手寫 DTO；元件與 store 呼叫這些服務，不自己組網址。
- DTO 直接用 `schema.ts` 匯出的後端名稱（`ConversationDto`、`CreateRunRequest`），不另取別名，從前端名稱就能搜到後端 record。
- 串流與下載用 `ApiClient.open` 取得原始 `Response`；上傳用 `ApiClient.upload`（OpenAPI 不描述 multipart body，只描述回應）。
- 瀏覽器自己載入的連結（`<a href>`、`<img src>` 的檔案內容）用 `apiHref` 產生，同樣受型別檢查。
- 表單狀態（例如日誌篩選）不是 DTO，可以在功能內宣告；送出前轉成合約的 query 型別。
- 請求共用與參考資料快取見 [聊天渲染](CHAT_RENDERING.md#啟動與請求)。

## 讀取與寫入

- 讀取一律用 `core/api/api-resource.ts` 的 `apiResource()`：它等帳號載入、檢查功能授權、換帳號時重來並丟掉晚到的回應，把失敗轉成審核過的訊息。全站的載入、錯誤與重試因此是同一種寫法：`loading()` 顯示第一次讀取，`error()` 放進 Notice，重試按鈕呼叫 `reload()`。
- 依路由或選擇讀取時，`params` 回傳目前的請求；回傳 `undefined` 表示不讀。換請求時舊值會保留到新值回來，所以畫面上若可能出現別筆資料，loader 回傳 `{ id, data }`，再用 computed 比對目前的 id（例：`projects-page.ts`、`admin-user-inspector.ts`）。
- 需要輪詢的讀取用 `poll`（例：任務、知識庫處理中的文件），分頁隱藏時暫停。
- 對話框每次開啟都要重讀時，用一個開啟次數的 signal 當 `params`（例：`price-book.ts`）。可編輯的表單值用 `linkedSignal` 從讀到的資料衍生，讀到新資料時自動重設（例：`settings-page.ts`）。
- 寫入是元件或 store 的方法：成功後把伺服器回傳的值寫進 `value`，或呼叫 `reload()`；寫入錯誤放在自己的 signal，與讀取錯誤合成同一個 `error`。寫入仍用 ViewScope 的 guard 忽略離開頁面或換帳號後才回來的結果。
- 寫入失敗時保留使用者的輸入（例：`NameDialog`、`TextSourceEditor`）；防止覆蓋他人變更靠伺服器的 conditional update／concurrency token，不靠前端判斷。
- 大頁面拆成元件；同頁多個元件共用的讀取放在頁面提供（`providers`）的 store，例如 `AdminStore`（管理頁的目錄與使用者）與 `LogFilterState`（日誌篩選）。
- 例外：以下是有自己生命週期的狀態機，保留為 store，不改成 `apiResource()`：
  - 聊天：`ChatStore` 與 `ChatModels`、`ChatHistory`、`ChatRun`、`ChatDraft`、`DraftAttachments`、`KnowledgeSelection`（串流、草稿、分支與送出佇列互相牽動）。整理對話的寫入在 `ChatConversations`。
  - `NotificationStore`：輪詢後合併、保留已讀並發出瀏覽器通知；登入世代切換時清除。
  - `MonitoringStore`：即時事件串流與重連。
  - `DocumentViewer`：PDF 多階段載入與渲染。
  - 登入與 `AuthService`、`WorkspaceSession`：其他讀取都依賴它們。
