# Gitea 程式庫工作區

用每個人自己的 Gitea 權杖閱讀程式碼、提交紀錄、議題與 Pull Request，帶入對話、匯入知識庫或交給 [AI 檢閱](code-review.md)。對 Gitea 的唯一寫入是「把 AI 檢閱貼成 PR 留言」，需要使用者另外授予 `write:issue` 並逐次確認；其餘都是 GET，不建立 commit、議題或 PR。AI Nexus 管理員不會因此取得任何人的私人程式庫。

## 管理員設定

在機器的一般設定開啟（鍵、範圍與預設在 `GiteaOptions` 與 `appsettings.json`，預設 `Enabled=false`）：

```json
{
  "Repositories": {
    "Gitea": {
      "Enabled": true,
      "BaseUrl": "https://gitea.hanglong.com.tw/"
    }
  }
}
```

重啟後，「程式庫」對有 repositories 功能的角色顯示；預設 `workspace` 群組已授予，但讀取仍要各自連線，沒有共用管理者權杖。

BaseUrl 可在子路徑；必須 HTTPS，HTTP 只允許 loopback。IIS 主機要信任 Gitea 憑證並連得到 `/api/v1`；網站登入頁可開不代表 API 可用。

## 使用者連線

在 Gitea「設定 → 應用程式」建立個人權杖，授予 `read:user`、`read:repository`、`read:issue`；要把檢閱貼回 PR 才加 `write:issue`。scope 介面依 Gitea 版本而異，以主機的 `/api/swagger` 為準。[Gitea API 驗證與 scope](https://docs.gitea.com/development/api-usage/)

後端用權杖驗證 `/api/v1/user`，以 Data Protection 加密存於 `repositories.RepositoryConnections`，保護目的綁定使用者 ID 與 BaseUrl。權杖不回傳畫面、不進瀏覽器儲存，HttpClient 不記錄授權標頭、不跟隨重新導向。IIS 的 `keys/` 同時保護登入 cookie 與權杖，更新 app 必須保留；key ring 改變後要重新連線。

「更新授權」替換權杖；「中斷連線」刪除自己的連線，已匯入的文件與檢閱紀錄保留。Gitea 拒絕權杖（401）、權限不足（403）或找不到（404）各有錯誤代碼與提示，不顯示泛用失敗訊息。連線、中斷與匯入留稽核。

## 瀏覽

- **列表**：伺服器端搜尋使用者能讀的所有程式庫（每頁 20 個，依更新時間）；最多釘選 30 個，存在 `repositories.RepositoryPins`，顯示在列表上方。
- **版本**：分支、標籤或 commit 由網址 `?ref=` 指定，進入時解析成 commit SHA；之後的資料夾、檔案、提交紀錄都讀這個 SHA，閱讀途中不混入新推送。
- **程式碼**：資料夾附 README；檔案有行號與語法上色，Markdown 可切換預覽與原始碼。只讀 `MaxFileBytes` 內的 UTF-8 文字，不接受 LFS、二進位或路徑跳脫。
- **提交紀錄**：從固定版本往前，每頁 30 個、依日期分組。選兩個提交即開啟區間（含較舊那一個的變更），網址為 `compare/{base}...{head}`；單一提交比對第一個父版本。diff 超過 256,000 bytes 不顯示也不能交給 AI，改到 Gitea 查看。
- **議題與 PR**：依開啟／關閉／全部篩選，議題可搜尋；詳情含內文與最多 100 則留言。PR 的「查看變更」比較合併基準到 head。

## 帶入對話

在檔案列勾選（可跨資料夾，預設最多 `MaxChatFiles`=20 個）或在議題／PR 詳情按「帶入對話」。後端以目前權限重讀固定版本，打包成**一個 Markdown 附件**，建立私人新對話並放入草稿；**不自動送出**。非文字或過大的檔案略過並列在附件裡；總長超過 `MaxChatCharacters` 直接拒絕，請少選或改用匯入知識庫。PR 在長度允許時附上 diff。

附件內容會隨對話送給所選模型；不希望程式碼離開內網時，將對話與 embedding 都設為 Ollama。

## 匯入知識庫

只能從資料夾匯入（不提供單檔匯入），目標是自己能編輯的知識庫。建立時用 Gitea 遞迴樹列出固定版本下的文字檔（副檔名白名單、`MaxFileBytes` 內，最多 `MaxImportFiles`=200 個），存在 `repositories.RepositoryFolderImports`，交給背景任務「程式庫匯入」逐檔處理。每個檔案匯入前重新確認 Gitea、功能與知識庫權限；之前匯入過的同一來源與 commit 會重用文件（`repositories.RepositoryImports`），所以重試從斷點繼續。

匯入後是 AI Nexus 的獨立資料，依知識庫 ACL 保存，**不同步 Gitea 之後的撤權、修改或刪除**；分享前確認內容適合該知識庫的成員。

## 尚未開放

自動 webhook 同步、建立議題或 PR、在 PR 上逐行留言都還沒有做。webhook 要先有 HMAC 驗證、權限同步、revision 與刪除標記；其他寫入要有獨立 scope、預覽、確認與稽核。

部署驗收時用自己的權杖逐項確認連線、私人程式庫隔離、固定版本、帶入對話、資料夾匯入與 PR 留言。無權杖的 API 403 只能證明主機有回應。
