# 前端共用邊界

> 由架構文件移出，F6 會與其他前端文件合併。


| 位置                | 責任                                                                                                               |
| ------------------- | ------------------------------------------------------------------------------------------------------------------ |
| core/api            | 產生的 OpenAPI 型別、型別安全的 `ApiClient`（JSON／multipart／SSE）、CSRF／登入失效／安全錯誤                      |
| core/auth           | 帳號世代與 WorkspaceSession；功能頁不為導覽載入聊天歷史                                                            |
| core/preferences    | 主題、帳號設定、本機草稿、跨頁新對話草稿交接                                                                       |
| features            | 按路由載入的 UI、feature API 與業務 store；不持續擴大 ChatStore                                                    |
| shared/ui           | Select、ActionMenu、ConfirmDialog、InlineTitle、Markdown、JobProgress；FeaturePage／導覽與資源授權 UI 共用基礎服務 |
| shared/browser      | ViewScope、autosize、拖放／貼圖、下載、複製、文字選取、popover 定位與未儲存提示                                    |
| shared/graphics     | 不含認證／業務依賴的數學，例如傅立葉取樣／DFT                                                                      |
| tokens.scss／styles | 三層 tokens、嵌套主題、分區樣式與統一減少動態規則                                                                  |

頁面使用 Signals、OnPush 與 zoneless。ViewScope 管生命週期與延遲回應的帳號檢查；同頁切換資源還需自己的 request version。離開／換帳號取消訂閱或忽略舊回應。新對話交接只留在記憶體，綁定帳號世代與 conversation ID，讀取一次；不把來源全文放在 URL、history state 或 localStorage。

共享 UI 的 DOM ID 每個實例唯一；浮層使用原生 top layer，避免 dialog／捲動區裁切。管理員元件頁 /design 以正式元件及本機範例檢查主題、鍵盤、停用、確認與有限階段動畫。見 [設計系統](DESIGN_SYSTEM.md)。

`InfoPopover` 統一單次／全對話費用的焦點、Esc 與邊界定位；StorageUsage／RunTimingDisplay 共用容量與耗時呈現；`TrendChart` 使用同一份資料提供 SVG、鍵盤游標與文字表格。Dashboard 的流向圖只呈現真實資源／索引／生成狀態，與後端查詢分離。圖示沿用同一個 Lucide renderer，工作區與快捷指令各有獨立語意。

WorkspaceLayout／WorkspaceSidebar 管所有路由的圖示欄、手機 overlay 與通知入口；NotificationStore 在登入世代切換時清除資料。ConversationActions 共用歷史及工具列操作，MessageContent／AttachmentList／DocumentViewer 共用聊天與唯讀分享呈現。TokenUsageChart 共用日期／模型統計及既有 SVG，不另增圖表依賴。NameDialog 及 TextSourceEditor 保留失敗輸入，服務端仍以 conditional update／concurrency token 防止覆蓋。

## 呼叫 API

- 一律透過 `core/api/api-client.ts` 的 `ApiClient`，第一個參數是 `contracts/openapi.json` 裡的路徑（含 `/api/v1`），`path`、`query`、`body` 與回傳值的型別都由 `schema.ts` 推導；路徑或欄位寫錯是編譯錯誤。
- 各功能的 `<feature>-api.ts` 只是把操作命名，不宣告回傳型別、不手寫 DTO；元件與 store 呼叫這些服務，不自己組網址。
- DTO 直接用 `schema.ts` 匯出的後端名稱（`ConversationDto`、`CreateRunRequest`），不另取別名，從前端名稱就能搜到後端 record。
- 串流與下載用 `ApiClient.open` 取得原始 `Response`；上傳用 `ApiClient.upload`（OpenAPI 不描述 multipart body，只描述回應）。
- 瀏覽器自己載入的連結（`<a href>`、`<img src>` 的檔案內容）用 `apiHref` 產生，同樣受型別檢查。
- 表單狀態（例如日誌篩選）不是 DTO，可以在功能內宣告；送出前轉成合約的 query 型別。
