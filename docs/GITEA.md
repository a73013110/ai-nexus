# Gitea 程式庫工作區

第一版提供自己的程式庫清單、固定 commit 的檔案瀏覽、待處理議題、對話草稿與知識庫版本快照。所有遠端操作都是 GET；不建立 issue、commit 或 PR，不推送修改。Gitea 的存取權由各使用者自己的權杖決定，AI Nexus 管理員不會自動取得私人程式庫內容。

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
