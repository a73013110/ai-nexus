# 系統日誌

「系統日誌」在 `/admin/logs`，回答「為什麼失敗、HTTP 狀態與耗時、例外與執行流程」。誰在何時做了什麼屬於 [活動稽核](ACTIVITY_AUDIT.md)。資料來自 `diagnostics.DiagnosticEvents`，架構見 [診斷日誌架構](../architecture/DIAGNOSTICS.md)，設定與故障排除見 [維運](../operations/DIAGNOSTICS.md)。

## 權限

| 功能 | 伺服器要求 |
| --- | --- |
| `logs.query` | 查詢摘要與健康資訊；初始只授予 administrators 群組 |
| `logs.detail` | 加上 `logs.query`，才能看受控屬性、UserId、例外型別與堆疊 |
| `logs.export` | 加上 `logs.query`，才能匯出摘要 CSV |

- `admin` 不隱含任何 `logs.*`；猜中查證代碼或 LogId 也不能繞過。每個 request 重新驗證 grant。
- 查詢與詳情每人每分鐘 60 次、匯出 2 次；伺服器限制範圍、筆數與逾時。沒有修改、刪除或無上限匯出的 API。
- 查詢、健康、詳情、匯出都先寫入獨立的讀取稽核再回傳；稽核失敗就拒絕操作，不會「成功但沒稽核」。

## 查詢

- 預設最近一天，可用 UTC 範圍、等級、模組（Category）、EventId／名稱、查證代碼、TraceId、JobId／RunId／OperationId、錯誤代碼、Instance 與模板文字篩選；最長範圍由 `Diagnostics.MaxQueryDays` 決定。
- 每頁 25／50／100 筆，預設新到舊；keyset cursor 以 Data Protection 保護，綁定操作者、全部篩選條件、時間範圍、排序與每頁筆數。
- 代碼與 trace 查詢走索引，不用全文檢索；模板文字的子字串搜尋最多 72 字且限一天，避免全表掃描。
- 同一 Trace／Job／Run 的關聯事件依時間最多顯示 50 筆；更長的流程用篩選與 cursor 繼續查，不要把 50 筆當成全部。
- 稽核詳情的「查證相關日誌」帶入同一 trace（缺少時用查證代碼）與事件前後五分鐘；日誌詳情也能反查同一 trace 的稽核。兩側連結依各自權限顯示。
- 匯出只含摘要欄位，範圍與筆數受 `MaxExportDays`／`MaxExportRows` 限制，並有公式注入防護。

## 用查證代碼追查

1. 使用者從錯誤提示旁複製 **NX** 代碼。**LOCAL** 代表伺服器沒有收到，先查網路與登入。
2. 有 `logs.query` 的人開「系統日誌」，設定發生時間（使用者時區），貼上代碼查詢；跨午夜或數日前的問題要擴大範圍。
3. 確認錯誤分類、模組、HTTP 狀態、Job／Run、Trace 與實例；有 `logs.detail` 才能點開詳情看遮罩後的型別、SQL 編號與 method stack。
4. 需要移交時，由有 `logs.export` 的人縮小範圍匯出 CSV，下載檔依組織規範保存。

## 例：全文索引不可用

hybrid 檢索的全文部分失敗（例如 SQL 30053）時改用 vector，記 Warning `2001/retrieval.degraded`，屬性含 RequestedMode=`hybrid`、ActualMode=`vector`、Reason、SqlNumber、IssueCode 與 Trace，HTTP 仍可能回 200。純 keyword 失敗回安全的 503 `fulltext_unavailable`。30010／30046／30053 屬於受控的全文不可用分類；SQL Server 本身的 ERRORLOG 需另查。

## 呈現

列表依使用者時區顯示，事件訊息在讀取時由模板展開，摘要權限看不到的參數顯示 `[omitted]`。原始模板保留在詳情的「受控屬性」分頁。查詢 API 回應不含未遮罩的秘密，管理員也一樣。
