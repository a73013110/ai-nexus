# 程式碼 review

「程式庫」的「AI Review」對單一 commit 或區間建立背景任務，產生簡短的繁體中文初檢報告。這是靜態初檢：不執行程式、不代表測試通過，也不修改 Gitea。連線與權限見 [Gitea 程式庫](gitea.md)。

## 操作

「AI Review」進入時立即顯示操作，近期 commit、模型與歷史列表各自載入、各自重試。commit 載入中或失敗時仍能貼上 SHA；歷史列表不自動讀取報告，選取後才讀取明細。結果深連結直接顯示已授權讀取的報告，不等待 commit 選單、不重讀相同明細；歷史列表以一次資料庫查詢取得任務狀態。單一 commit 或區間的兩端均使用可搜尋的最近 30 個 commit 選單，也可展開貼上完整 40／64 位 SHA；相同起訖不可提交。單一 commit 比對父版本，區間為起點與終點的淨變更（兩點比較），不是逐一列出每個 commit。

目的可選 `review`（快速檢閱）、`summary`（變更摘要）、`typos`（內容誤植），預設 review。選模型及最多 2,000 字元關注事項後建立背景任務；關注事項可指定單一檔案。新結果採繁體中文結論、至多三個變更重點與三個有證據的 P1／P2 問題，說明合計最多 350 字（不含檔案位置）。不產生逐檔長報告、一般最佳實踐清單或修正程式碼；詳細查證可交給前沿模型。固定 diff／區段分析依需要展開，只有展開時才建立 Markdown 與 diff DOM。舊版長報告仍使用可聚焦的捲動區，另可展開完整內容。切換頁面仍繼續，結果深連結 `/repositories?review={id}`，完成／失敗／取消進入[通知](notifications.md)。

## 固定內容與預算

建立時固定主機、SHA、diff、目的、分析／彙整／報告指令、UTF-8 byte 預算、輸出預留及模型設定 fingerprint。最多 256,000 bytes diff、60 來源區段，保留完整 Unicode 字元與來源。依實際輸出預留計算最多 14,000 bytes 輸入預算，相鄰小檔案合併批次，避免逐檔呼叫模型。預算內的變更只呼叫一次；大型變更先產生最多 140 字中間筆記，再依實際 bytes 分批彙整。中間筆記與整體報告分別最多 384／1,400 output tokens，仍受模型及個人額度上限限制；報告預留是完成短結果的安全空間，並非要求填滿。

## 結果驗證

最終回覆使用 provider 無關的 JSON 契約，後端驗證必要欄位、中文說明、項數、總字數與完整 finish reason，再轉成共用 Markdown。英文、過長、格式錯誤或截斷時，只補一次更精簡的生成；仍無效則明確失敗，保留分析 checkpoint，不把半份報告標為完成。補正的兩次用量合併顯示且各自保留計費紀錄。分析或中間彙整達輸出上限仍將報告標示不完整；中間彙整超預算則明確失敗，不悄悄裁切證據。二進位列為人工確認，不傳原始內容給文字模型，純二進位變更不呼叫模型。這是靜態初檢，不執行程式、不代表測試通過。

## 執行與權限

沿用 BackgroundJobWorker 的租約、取消、checkpoint；區段分析、中間彙整、最終報告均持久化，失敗／取消最多六次處理，重試只處理未完成步驟。結果 ordinal `0..slices-1` 是區段、`slices..` 是可重用的中間彙整、`-1` 是整體報告，沿用既有資料表。新快照 v3 使用精簡結果契約與合併批次；v1／v2 保留原本固定指令與結果，不自動重跑舊 review，頁面可展開舊報告或另建新 review。每次模型呼叫前後重新驗證 Gitea 及功能權限，模型設定變更拒絕沿用舊任務。共用 ModelTaskService 的額度、用量及價格快照；結果僅 owner 可讀，每次仍確認目前 Gitea 權限，不因保存 diff 繞過撤銷。

## API

API 前綴 `/api/v1`：`GET /repositories/commits?repository={name}`、`GET /repositories/reviews?repository={name}`、`POST /repositories/reviews`、`GET /repositories/reviews/{id}`、`POST /repositories/reviews/{id}/cancel`／`retry`。建立 body `{ repository, commit, baseCommit, modelId, note, idempotencyKey, purpose }`，purpose 可省略。Detail 回傳 `{ review, sections, report, version }`，未完成／v1 的 report 為 null。key 為 GUID，owner scoped，同 key 同內容回原任務，不同內容（含 purpose）回 409；v1 預設目的的既有 key 可重用。

使用官方唯讀 API：[單一 commit diff](https://docs.gitea.com/api/operations/repo-download-commit-diff-or-patch/)、[區間比較](https://docs.gitea.com/api/operations/repo-compare-diff/) 的 `?output=diff`。舊版不支援 compare diff 時明確拒絕並提示確認版本，不把 JSON metadata 當程式碼送模型。
