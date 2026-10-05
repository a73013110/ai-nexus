# 聊天連網搜尋

輸入區的「搜尋網路」預設關閉。開啟後只將本次提問送往搜尋服務，附件與歷史對話不送出；模型仍會使用你已授權的對話上下文。重新生成時搜尋原始提問。搜尋結果是有來源的參考摘要，不是瀏覽整個網站，也不代表模型已查證所有句子。

## 建議使用自架 SearXNG

SearXNG 不需要 Google AI API key；模型可完全使用 Ollama。它仍會向選定的網路搜尋引擎發送查詢，因此「地端模型」不表示搜尋本身離線。先在可由 IIS／後端主機存取的服務部署 SearXNG，並在其 `settings.yml` 開啟 JSON 格式：

```yaml
search:
  formats:
    - html
    - json
```

重啟 SearXNG 後，在 AI Nexus 的一般設定加入：

```json
{
  "Tools": {
    "WebSearch": {
      "Enabled": true,
      "Provider": "searxng",
      "Endpoint": "http://搜尋主機:8080/",
      "TimeoutSeconds": 10,
      "MaxResults": 5,
      "MaxDailyRequests": 100
    }
  }
}
```

使用 SearXNG 的 `/search?format=json`，若只允許 HTML，搜尋會失敗並提示設定問題。部署與 JSON 格式以 [SearXNG API](https://docs.searxng.org/dev/search_api.html)、[搜尋設定](https://docs.searxng.org/admin/settings/settings_search.html) 為準。若 IIS 與搜尋服務在不同主機，Endpoint 的 localhost 不能指向另一台主機。

## 選用 Brave Search

一般設定 `Provider=brave`，秘密檔設定：

```json
{ "Tools": { "WebSearch": { "ApiKey": "" } } }
```

在本機編輯器填入自己的 key，或使用 `Tools__WebSearch__ApiKey` 環境變數。後端固定使用 Brave 的 HTTPS API 與 `X-Subscription-Token`，key 不回傳前端。Endpoint 只適用 SearXNG。Brave 查詢限制 600 個字元／75 個單字，超過時在付費呼叫前拒絕，提示簡化。[Brave API](https://api-dashboard.search.brave.com/api-reference/web/search/post)、[驗證](https://api-dashboard.search.brave.com/documentation/guides/authentication)

有搜尋費用時，管理員設定供應商 `brave`／模型 `web-search` 的每次固定費率；自架 SearXNG 可設定內部成本或明確免費。沒有設定價格仍可搜尋，費用標示未知。

## 行為與驗收

- 服務未啟用或 Brave key 未填時，按鈕停用並說明原因；不會偷偷以其他供應商代替。
- 提問上限 2000 字元（Brave 更小），最多 8 個摘要，每段最多 600 字元。模型參考資料預留 3600 個保守 Context tokens，預覽不會真的呼叫搜尋。
- 保存標題、URL、摘要與取得時間；回覆下方可直接核對來源。這份清單是搜尋取得的來源，不是逐句引用命中追蹤。
- 搜尋摘要在系統指示中明確當成不受信任的參考資料；只允許實際提供的來源，沒有結果時必須說明。
- 過濾非 HTTP(S)、帶帳密、localhost／私人 IP 等來源連結，不抓取結果網站、不跟隨服務重新導向。外部 JSON 回應限制 1 MB。
- 每人每日配額以 UTC 零點重設。完成的相同提交只讀保存的結果；失敗同樣占配額，重試應以新的提交送出，避免不確定的付費請求被重複執行。

部署後用公開、無敏感內容的問題測試：按鈕關閉不會呼叫搜尋，開啟後可看到來源及搜尋費用；故意停掉搜尋服務應顯示錯誤並保留草稿。搜尋服務／上游引擎可能保存查詢，請依組織資料政策限制輸入。授權、用量與介面契約已有自動化測試，實際引擎結果仍需服務設定完成後驗收。
