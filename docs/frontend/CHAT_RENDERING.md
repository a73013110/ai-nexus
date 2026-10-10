# 聊天渲染與前端請求

結論：長對話與長回答的成本只跟「畫面上看得到的部分」和「這一格新增的文字」有關；啟動只等兩輪請求。改聊天畫面、Markdown、串流或 `ApiClient` 前先讀這份。

## 長對話

- 除了最後一則，每則訊息都是 `content-visibility: auto`（`frontend/src/app/features/chat/chat-message.scss`）。離開畫面的訊息不排版、不繪製；`contain-intrinsic-size: auto` 記住實際高度，捲回時不跳動。
- 這會套用 paint containment，超出訊息框的東西會被裁掉。訊息內不要放絕對定位的浮層，要用 popover／top layer；焦點框靠左右的 `padding-inline` 留空間。
- 下一步若仍不夠，再做分頁或虛擬捲動；目前的「回到最新」、對話定位、搜尋都依賴所有訊息都在 DOM 中。

## 程式碼高亮

- `markdown.ts` 只輸出跳脫後的程式碼，已知語言加上 `data-language`。
- `code-highlighting.ts` 用 IntersectionObserver 等區塊接近可視區，再延後載入 `code-highlight.ts`（highlight.js），每次最多處理約 8ms 就讓出主執行緒。
- 高亮結果只允許 `<span class>`（DOMPurify），不是另一個信任 HTML 的入口。新增語言在 `code-highlight.ts` 註冊。

## 串流 Markdown

`StreamingMarkdown`（`shared/markdown/streaming-markdown.ts`）把回答分成「已固定的區塊」和「尾段」，只有尾段每格重算：

| 情況                          | 處理                                                                      |
| ----------------------------- | ------------------------------------------------------------------------- |
| 後面已經有下一個頂層區塊      | 前面的區塊固定，保留 DOM                                                  |
| 尾段是很長的清單（> 1200 字） | 已出現下一項的項目先固定；有序清單由 `start` 接續編號                     |
| 尾段是未結束的程式碼區塊      | 不再解析 Markdown，只掃描新增的行找結束標記；內容以純文字每 24 行一段顯示 |

- 串流中的畫面可能與完成後略有差異（例如鬆散清單的段落間距）；完成或停止後一律改用伺服器回傳的完整內容重新渲染。
- `completeStreamingInline` 只補齊畫面上未結束的粗體、行內程式碼與連結，不改動實際回答。
- 不完整的 Mermaid 區塊顯示原始碼，區塊結束後才繪圖。

## Markdown 安全邊界

- markdown-it 關閉 raw HTML；外部圖片只顯示描述；連結只允許 `http(s)` 與頁內錨點。
- 輸出經 DOMPurify allowlist 後才進入 Angular 的 trusted HTML（`renderMarkdown*`），以保留複製按鈕。其他 HTML 不得送進這個邊界。

## 啟動與請求

- 路由守衛確認登入狀態後，5 秒內的頁面直接沿用（`AuthService.requireLogin`），不再重抓 `/auth/session`。
- `WorkspaceSession.load` 同時抓帳號與個人設定。聊天在確認有權限後，同時抓模型與歷史；附件政策、標籤、知識庫也同時開始，但不阻擋輸入框。聊天一律重抓帳號，因為要知道是否有進行中的生成。
- `ApiClient.get`：同一網址的 GET 若仍在進行，共用同一個請求，每個呼叫端各自解析一份。
- `ApiClient.reference`：模型目錄、網路搜尋狀態、附件政策 30 秒內重用。登入身分改變、401、或本瀏覽器送出任何非 GET 請求時清除。其他管理者的變更最多 30 秒後生效。
- `/api` 回應本身仍是 `no-store`，不做 HTTP 快取。

## 樣式載入

- 元件樣式放在元件旁的 `.scss`，使用預設的 Emulated 封裝，隨元件（多半隨 lazy 路由）載入；刪掉元件就刪掉它的樣式。不使用 `ViewEncapsulation.None`，也不用 `::ng-deep`。
- 全域 `styles.scss` 只留 tokens、base、捲軸、按鈕、版面框架、共用 `ui-*`／resource／dialog 模式、Markdown 與動效。這些規則套在多個元件或封裝碰不到的內容上：
  - Markdown 由 `innerHTML` 產生，元件屬性加不上去。
  - 投影進共用元件的內容（`ng-content`）帶的是宣告端的屬性，接收端碰不到；Notice、EmptyState 的這類規則放 `styles/projected.scss`，表格內容的規則在 `styles/data-workspace.scss`。
  - keyframes 留在 `styles/motion.scss`：元件內宣告的 keyframes 會被改名，e2e 也以名稱檢查動畫。
- driver.js 的覆寫是獨立的 `driver-tour.css`（`angular.json` 的 `inject: false`），導覽啟動時才和 driver.js 樣式一起載入。
- 跨元件調整：
  - 祖先或 host 狀態用 `:host(.x)`、`:host-context(.x)`（例：側欄收合 `is-compact`、設定頁嵌在對話框裡）。
  - 父層要改子元件內部，由子元件開 custom property 或 input（例：`--inline-title-max-width`、`--view-switch-button-*`、`--selection-control-padding`、`Select` 的 `appearance`）。
  - 父層可以直接排版子元件的 host 元素（例：`.composer-bottom nx-composer-controls`）。
- 每個元件樣式檔要在 `anyComponentStyle` 預算（4 kB）內；超過時拆成子元件或用 `styleUrls` 分檔，不提高預算。

## 驗證

- `npm --prefix frontend run lint`、`npm --prefix frontend test`、`npm --prefix frontend run build`（看 initial total）。
- 長對話與串流的實際手感要在瀏覽器確認：開一段很長的對話捲動、讓模型輸出長程式碼與長清單。
