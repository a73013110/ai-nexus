# Gitea 程式庫工作區

提供程式庫清單、固定 commit 的檔案瀏覽、待處理議題、對話草稿、知識庫快照與背景 AI review。所有 Gitea 遠端操作都是 GET；不建立 issue、commit 或 PR、不推送修改。存取權由各使用者自己的權杖決定，AI Nexus 管理員不會自動取得私人程式庫內容。

## Commit review

「AI Review」進入時立即顯示操作，近期 commit、模型與歷史列表各自載入、各自重試。commit 載入中或失敗時仍能貼上 SHA；歷史列表不自動讀取報告，選取後才讀取明細。結果深連結直接顯示已授權讀取的報告，不等待 commit 選單、不重讀相同明細；歷史列表以一次資料庫查詢取得任務狀態。單一 commit 或區間的兩端均使用可搜尋的最近 30 個 commit 選單，也可展開貼上完整 40／64 位 SHA；相同起訖不可提交。單一 commit 比對父版本，區間為起點與終點的淨變更（兩點比較），不是逐一列出每個 commit。

目的可選 `review`（快速檢閱）、`summary`（變更摘要）、`typos`（內容誤植），預設 review。選模型及最多 2,000 字元關注事項後建立背景任務；關注事項可指定單一檔案。新結果採繁體中文結論、至多三個變更重點與三個有證據的 P1／P2 問題，說明合計最多 350 字（不含檔案位置）。不產生逐檔長報告、一般最佳實踐清單或修正程式碼；詳細查證可交給前沿模型。固定 diff／區段分析依需要展開，只有展開時才建立 Markdown 與 diff DOM。舊版長報告仍使用可聚焦的捲動區，另可展開完整內容。切換頁面仍繼續，結果深連結 `/repositories?review={id}`，完成／失敗／取消進入[通知](NOTIFICATIONS.md)。

建立時固定主機、SHA、diff、目的、分析／彙整／報告指令、UTF-8 byte 預算、輸出預留及模型設定 fingerprint。最多 256,000 bytes diff、60 來源區段，保留完整 Unicode 字元與來源。依實際輸出預留計算最多 14,000 bytes 輸入預算，相鄰小檔案合併批次，避免逐檔呼叫模型。預算內的變更只呼叫一次；大型變更先產生最多 140 字中間筆記，再依實際 bytes 分批彙整。中間筆記與整體報告分別最多 384／1,400 output tokens，仍受模型及個人額度上限限制；報告預留是完成短結果的安全空間，並非要求填滿。

最終回覆使用 provider 無關的 JSON 契約，後端驗證必要欄位、中文說明、項數、總字數與完整 finish reason，再轉成共用 Markdown。英文、過長、格式錯誤或截斷時，只補一次更精簡的生成；仍無效則明確失敗，保留分析 checkpoint，不把半份報告標為完成。補正的兩次用量合併顯示且各自保留計費紀錄。分析或中間彙整達輸出上限仍將報告標示不完整；中間彙整超預算則明確失敗，不悄悄裁切證據。二進位列為人工確認，不傳原始內容給文字模型，純二進位變更不呼叫模型。這是靜態初檢，不執行程式、不代表測試通過。

沿用 BackgroundJobWorker 的租約、取消、checkpoint；區段分析、中間彙整、最終報告均持久化，失敗／取消最多六次處理，重試只處理未完成步驟。結果 ordinal `0..slices-1` 是區段、`slices..` 是可重用的中間彙整、`-1` 是整體報告，沿用既有資料表。新快照 v3 使用精簡結果契約與合併批次；v1／v2 保留原本固定指令與結果，不自動重跑舊 review，頁面可展開舊報告或另建新 review。每次模型呼叫前後重新驗證 Gitea 及功能權限，模型設定變更拒絕沿用舊任務。共用 ModelTaskService 的額度、用量及價格快照；結果僅 owner 可讀，每次仍確認目前 Gitea 權限，不因保存 diff 繞過撤銷。

API 前綴 `/api/v1`：`GET /repositories/commits?repository={name}`、`GET /repositories/reviews?repository={name}`、`POST /repositories/reviews`、`GET /repositories/reviews/{id}`、`POST /repositories/reviews/{id}/cancel`／`retry`。建立 body `{ repository, commit, baseCommit, modelId, note, idempotencyKey, purpose }`，purpose 可省略。Detail 回傳 `{ review, sections, report, version }`，未完成／v1 的 report 為 null。key 為 GUID，owner scoped，同 key 同內容回原任務，不同內容（含 purpose）回 409；v1 預設目的的既有 key 可重用。

使用官方唯讀 API：[單一 commit diff](https://docs.gitea.com/api/operations/repo-download-commit-diff-or-patch/)、[區間比較](https://docs.gitea.com/api/operations/repo-compare-diff/) 的 `?output=diff`。舊版不支援 compare diff 時明確拒絕並提示確認版本，不把 JSON metadata 當程式碼送模型。

## 管理員設定

一般設定的 connector 與公文／校務 SQL sources 分開：

```json
{
  "Integrations": {
    "Connectors": {
      "Gitea": {
        "Enabled": true,
        "BaseUrl": "https://gitea.hanglong.com.tw/",
        "TimeoutSeconds": 10,
        "MaxFileBytes": 200000
      }
    }
  }
}
```

重啟後，「程式庫」對有 repositories 功能的角色顯示。預設基本工作區群組已授予此功能，但讀取仍要各自連線。公開部署範本保持 Disabled，本機已啟用你提供的 Gitea 位址；沒有任何共用管理者權杖。

BaseUrl 支援 Gitea 在子路徑部署；使用 HTTPS，HTTP 僅允許 loopback 本機測試。IIS 主機需要信任 Gitea 憑證，並可連到 `/api/v1`。網站登入頁可存取不代表 API 權限已通過。

## 使用者連線

在 Gitea 的「設定 → 應用程式」建立個人權杖，優先只授予 `read:user`、`read:repository`、`read:issue`，將權杖填入 AI Nexus「程式庫」連線視窗。不同 Gitea 版本的 scope／repo 範圍介面可能不同，以你主機的 `/api/swagger` 與 `/swagger.v1.json` 為準；不用發送權杖到聊天。[Gitea API 驗證與 scope](https://docs.gitea.com/development/api-usage/)

後端用權杖驗證 `/api/v1/user`，以 ASP.NET Data Protection 加密後存於 `workspace.RepositoryConnections`。保護目的同時綁定使用者 ID 與 Gitea BaseUrl；不同使用者／不同主機設定不能混用。畫面不會回傳權杖，不放在瀏覽器 localStorage，HttpClient 也不記錄授權標頭、不跟隨重新導向。IIS 的 `keys/` 同時保護登入 cookie 與 連線權杖，更新 app 必須保留；DPAPI 身分或 key ring 改變後可能需要重新連線。

「更新授權」替換加密權杖；「中斷連線」刪除自己的連線。Gitea 撤銷權杖後新讀取失敗，沒有自動切換共用帳號。連線、解除連線及匯入均留稽核。

## 檔案、草稿與索引

選擇程式庫時先把預設 branch 解析成 commit SHA，之後瀏覽與匯入均使用相同 SHA，不會在閱讀途中混入新的 main 內容。目錄最多 500 個項目；程式庫每頁 20 個，頁內搜尋有明確標示；議題列出最多 30 個開啟項目。

只讀上限內的 UTF-8 文字，不接受目錄、LFS、二進位檔或路徑跳脫。外部 API 回應受大小限制，不使用 Gitea 回傳的任意 download URL。

「帶入對話草稿」會再次確認權限並讀取固定版本，建立一個私人新對話，把來源 URL、commit 與文字放入輸入區。超過 6000 字只帶前段並提示；使用者可檢視後再送出，**不自動呼叫模型**。Google 模式送出時仍會將這份內容交給 Google；不希望離開內網時同時將對話與 embedding 設為 Ollama。

「匯入版本快照」只可選擇能編輯的知識庫，由後端重讀固定版本、沿用附件驗證與文件索引工作。相同使用者／知識庫／來源／commit／path 的重複匯入會重用未刪除的文件，同一 app 內以寫入鎖防止並行重複。IIS 使用一個 worker；未來部署多個 app 實例前，應改成跨實例的匯入唯一性／資料庫協調，不依賴記憶體鎖。

快照匯入後是 AiNexus 的獨立資料，依目的知識庫 ACL 保存，**不會同步 Gitea 的後續撤權、修改或刪除**。分享前須確認內容適合目的成員；中斷程式庫連線不會刪除已匯入的文件。來源識別存在 `knowledge.RepositoryImports`，文件仍有 commit／URL 可核對。

## 後續可用的延伸

優先做 README／規格／ADR 的增量知識索引、issue 的人工檢視摘要、程式版本差異分析。自動 webhook 匯入應先具備 HMAC 驗證、權限同步、revision／刪除標記與去重；直接寫 issue／PR 再增加獨立寫入 scope、預覽、確認與稽核。這些寫入與同步能力尚未開放，避免讓閱讀權杖變成修改帳號。

部署驗收時使用自己的唯讀權杖逐項確認連線、私人程式庫隔離、固定版本、草稿交接與匯入權限。無權杖的 API 403 只能證明主機有回應，不能據此判定登入或 API 已通過。
