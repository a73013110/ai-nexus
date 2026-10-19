# 程式碼 review

「程式庫」可把單一提交、提交區間或 Pull Request 交給 AI，在背景產生簡短的繁體中文檢閱或摘要。這是靜態初檢：不執行程式、不代表測試通過。連線與權限見 [Gitea 程式庫](gitea.md)。

## 操作

- **從哪裡開始**：提交詳情、區間變更（「AI 變更摘要」／「AI 檢閱」）、PR 詳情，或「AI 檢閱」分頁的「檢閱目前版本」。單一提交比對父版本；區間是兩點間的淨變更；PR 以 head 對合併基準，建立時固定成兩個 SHA。
- **目的**：`review`（快速檢閱，預設）找有證據的重大缺陷；`summary`（變更摘要）說明改了什麼與需要確認的影響。另可選模型（政策允許時）與最多 2,000 字的關注事項。
- **結果**：繁體中文結論、至多三個變更重點與三個有證據的 P1／P2 問題，說明合計最多 350 字。不產生逐檔長報告或修正程式碼。分段明細展開時才建立 Markdown 與 diff。
- **進度**：每個檢閱有自己的網址 `/work/repositories/{owner}/{name}/reviews/{id}`，執行中每 3 秒更新；完成、失敗或取消進入[通知](notifications.md)。通知與背景任務只知道 id，仍連到 `/work/repositories?review={id}`，頁面再轉到所屬程式庫。
- **貼回 PR**：PR 的檢閱完成後可「貼到 PR 留言」：先在對話框預覽並修改（最多 20,000 字），以使用者自己的 Gitea 帳號留言，需要權杖有 `write:issue`。每份檢閱只能貼一次，留言網址記在檢閱上。

## 固定內容與預算

建立時固定主機、SHA、diff、目的、分析／彙整／報告指令、UTF-8 byte 預算、輸出預留及模型設定 fingerprint。最多 256,000 bytes diff、60 來源區段，保留完整 Unicode 字元與來源。依實際輸出預留計算最多 14,000 bytes 輸入預算，相鄰小檔案合併批次，避免逐檔呼叫模型。預算內的變更只呼叫一次；大型變更先產生最多 140 字中間筆記，再依實際 bytes 分批彙整。中間筆記與整體報告分別最多 384／1,400 output tokens，仍受模型及個人額度上限限制；報告預留是完成短結果的安全空間，並非要求填滿。

## 結果驗證

最終回覆使用 provider 無關的 JSON 契約，後端驗證必要欄位、中文說明、項數、總字數與完整 finish reason，再轉成共用 Markdown。英文、過長、格式錯誤或截斷時，只補一次更精簡的生成；仍無效則明確失敗，保留分析 checkpoint，不把半份報告標為完成。補正的兩次用量合併顯示且各自保留計費紀錄。分析或中間彙整達輸出上限仍將報告標示不完整；中間彙整超預算則明確失敗，不悄悄裁切證據。二進位列為人工確認，不傳原始內容給文字模型，純二進位變更不呼叫模型。這是靜態初檢，不執行程式、不代表測試通過。

## 執行與權限

沿用 BackgroundJobWorker 的租約、取消、checkpoint；區段分析、中間彙整、最終報告均持久化，失敗／取消最多六次處理，重試只處理未完成步驟。結果 ordinal `0..slices-1` 是區段、`slices..` 是可重用的中間彙整、`-1` 是整體報告。快照只有目前版本（`ReviewSnapshot.Current`）；改變指令或結果契約時升版，舊版快照不再支援。每次模型呼叫前後重新驗證 Gitea 及功能權限，模型設定變更拒絕沿用舊任務。共用 ModelTaskService 的額度、用量及價格快照；結果僅 owner 可讀，每次仍確認目前 Gitea 權限，不因保存 diff 繞過撤銷。

## API

端點與欄位見 `contracts/openapi.json` 的 `/api/v1/repositories/{owner}/{name}/reviews` 與 `/api/v1/repositories/reviews/{id}`。建立時的 `idempotencyKey` 為 GUID、owner scoped：同 key 同內容回原任務，不同內容（含 purpose）回 409。

diff 來自官方唯讀 API：[單一 commit diff](https://docs.gitea.com/api/operations/repo-download-commit-diff-or-patch/)、[區間比較](https://docs.gitea.com/api/operations/repo-compare-diff/) 的 `?output=diff`。舊版 Gitea 不支援 compare diff 時明確拒絕，不把 JSON metadata 當程式碼送模型。
