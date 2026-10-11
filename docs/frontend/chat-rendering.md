# 聊天渲染與前端請求

結論：長對話與長回答的成本只跟「畫面上看得到的部分」和「這一格新增的文字」有關；所有 Markdown 都經同一個安全邊界；啟動只等兩輪請求。改聊天畫面、Markdown、串流或 `ApiClient` 前先讀這份。

## 聊天版面

- 訊息、開場、輸入區、附件與提示共用 `.chat-column`，寬度只由 `--reading-width`（設定的「內容寬度」）決定，不另設輸入區寬度；`.chat-gutter` 統一側邊留白與捲軸保留空間。
- 新對話時標題、輸入框、起點依序置中；有訊息後輸入框固定在底部。超出高度時由內容區捲動，不把輸入框硬定在固定座標。
- 正文與輸入用閱讀 tokens（個人偏好），工具列與側欄用緊湊介面尺度。
- 生成狀態只由聊天上方一個 `role="status"` 播報；訊息區的 `GenerationIndicator` 只是視覺，避免重複朗讀。
- 隱藏模型名稱時，標頭與歷史都不補出實際模型。

## 長對話

- 除了最後一則，每則訊息都是 `content-visibility: auto`（`frontend/src/app/features/chat/chat-message.scss`）。離開畫面的訊息不排版、不繪製；`contain-intrinsic-size: auto` 記住實際高度，捲回時不跳動。
- 這會套用 paint containment，超出訊息框的東西會被裁掉。訊息內不要放絕對定位的浮層，要用 popover／top layer；焦點框靠左右的 `padding-inline` 留空間。
- 下一步若仍不夠，再做分頁或虛擬捲動；目前的「回到最新」、對話定位、搜尋都依賴所有訊息都在 DOM 中。
- 捲動跟隨每 frame 只排一次；使用者往上讀歷史時不強制捲到底。

## 串流

- SSE 增量經 `core/stream/frame-publisher.ts` 合併，每 32ms 最多一次更新；收到 terminal status 前立即 flush。
- `StreamingAnswer` 約 20fps 追上突發文字，不切開 UTF-16 surrogate；減少動態時直接顯示最新文字。
- 完成或停止後一律改用伺服器回傳的完整 Markdown 重新渲染。

## 串流 Markdown

`StreamingMarkdown`（`frontend/src/app/shared/markdown/streaming-markdown.ts`）把回答分成「已固定的區塊」和「尾段」，只有尾段每格重算：

| 情況 | 處理 |
| --- | --- |
| 後面已經有下一個頂層區塊 | 前面的區塊固定，保留 DOM |
| 尾段是很長的清單 | 已出現下一項的項目先固定；有序清單由 `start` 接續編號 |
| 尾段是未結束的程式碼區塊 | 不再解析 Markdown，只掃描新增的行找結束標記；內容以純文字分段顯示 |

- 串流中的畫面可能與完成後略有差異（例如鬆散清單的段落間距），完成後的重新渲染會修正。
- `completeStreamingInline` 只補齊畫面上未結束的粗體、行內程式碼與連結，不改動實際回答。
- 不完整的 Mermaid 區塊顯示原始碼，區塊結束後才繪圖。

## 程式碼高亮

- `markdown.ts` 只輸出跳脫後的程式碼，已知語言加上 `data-language`。
- `code-highlighting.ts` 用 IntersectionObserver 等區塊接近可視區，再延後載入 `code-highlight.ts`（highlight.js），每次最多處理約 8ms 就讓出主執行緒。
- 高亮結果只允許 `<span class>`（DOMPurify），不是另一個信任 HTML 的入口。新增語言在 `code-highlight.ts` 註冊。

## Markdown 安全邊界

- 所有閱讀介面（對話、成果、分享、使用者活動…）都用 `MarkdownView`，不另寫渲染。
- markdown-it 關閉 raw HTML；外部圖片只顯示描述；連結只允許 `http(s)` 與頁內錨點。
- 輸出經 DOMPurify allowlist 後才進入 Angular 的 trusted HTML（`renderMarkdown*`），以保留複製按鈕。其他 HTML 不得送進這個邊界。
- 完整的安全設定（含 CSP）見 [安全](../architecture/security.md)。

## Mermaid

- 只在有圖表時才延遲載入引擎（套件內的 `mermaid/dist/mermaid.esm.min.mjs`，已封裝 CommonJS 依賴，不需 `allowedCommonJsDependencies`）。
- `MarkdownView` 在安全解析後，把完成的 fence 掛到 renderer 自己產生的插槽，清單與引用中的圖表保留結構。
- 渲染序列化，避免共用設定與主題互相覆蓋；版本檢查與 destroy 擋掉遲到結果，Blob URL 在替換與離開時釋放。
- strict 模式、站點固定的 init 設定；輸出 SVG 再經 DOMPurify SVG allowlist，以 Blob 圖片顯示，沒有第二個 active SVG 的 trusted HTML 入口。CSP 因此只需為圖片加 `blob:`。
- 外觀集中在 `mermaid-theme.ts`：常用 classDef（`process`／`decision`／`error`／`result`）對應站點語意 token，壓掉模型常輸出的亮黃、粗框與陰影；其他自訂類別保留來源配色，原始碼不改寫。標籤對比不足 4.5:1 時改用黑／白字，下載沿用相同結果。
- 長圖在自己的畫布捲動，不撐寬頁面；錯誤時保留原始碼。
- `package.json` 的 `overrides` 把 Mermaid 間接依賴的 KaTeX 固定在已修正版本（[GHSA-238p-pmpm-9mq7](https://github.com/advisories/GHSA-238p-pmpm-9mq7)）。它不會替換 Mermaid 發行檔內已打包的數學引擎；升級 Mermaid 時重新檢查，上游修正後移除 override。不要接受 npm audit 建議的 Mermaid 降級。

## 啟動與請求

- 路由守衛確認登入狀態後，5 秒內的頁面直接沿用（`AuthService.requireLogin`），不再重抓 `/auth/session`。
- `WorkspaceSession.load` 同時抓帳號與個人設定。聊天在確認有權限後，同時抓模型與歷史；附件政策、標籤、知識庫也同時開始，但不阻擋輸入框。聊天一律重抓帳號，因為要知道是否有進行中的生成。
- 登入後的首頁不預先初始化聊天的模型、歷史與附件；由目的頁載入自己的資料。
- `ApiClient.get`：同一網址的 GET（未帶 headers／signal）若仍在進行，共用同一個請求，每個呼叫端各自解析一份。
- `ApiClient.reference`：模型目錄、網路搜尋狀態、附件政策 30 秒內重用。登入身分改變、401、或本瀏覽器送出任何非 GET 請求時清除。其他管理者的變更最多 30 秒後生效。
- `/api` 回應本身仍是 `no-store`，不做 HTTP 快取。

## 驗證

- `npm --prefix frontend run lint`、`npm --prefix frontend test`、`npm --prefix frontend run build`（看 initial total）；瀏覽器測試如 `streaming.spec.ts`、`mermaid.spec.ts`、`chat-layout.spec.ts`。
- 長對話與串流的實際手感要在瀏覽器確認：開一段很長的對話捲動、讓模型輸出長程式碼與長清單。
