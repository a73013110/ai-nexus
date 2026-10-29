# 腳本選單

`./scripts/Show-Scripts.ps1` 依分類列出入口腳本，填好參數、確認指令後執行；`-List` 只印清單，輸出被重導時也自動改印清單。

- 上下鍵選腳本，打字篩選（中英文都可以）；Enter 進參數畫面，Tab 套用 help 裡的範例，Enter 顯示完整指令，可以按 C 複製。
- 最近執行的腳本與參數記在 `.local/script-history.json`，下次排在最上面並帶入上次的參數。
- F2 是共用設定：資料庫伺服器、名稱與是否信任憑證寫入 `.local/config/appsettings.Local.json`，SQL 帳密寫入 `.local/secrets/appsettings.Secrets.json`（只有目前使用者可讀）。兩者都是網站連線用的同一組值，和 `Configure-Local.ps1` 寫的是同一處。
- 開啟有 `-Server`、`-Database` 參數的腳本時帶入共用設定；省略時會自己讀設定的參數（`Initialize-Database.ps1` 的 `-Server`）維持空白，以免改變連線方式。參數畫面、確認畫面與執行前都顯示「操作對象」，和共用設定不同時以警告色標示。
- `-Credential` 可選「使用已儲存的帳密」或「執行前輸入」：離開選單後才讀取或以 `Get-Credential` 詢問，指令一律顯示 `(Get-Credential)`，歷程只記來源。已儲存帳密且未設定 Windows 身分時預設使用已儲存的帳密。
- 用途、參數與範例都來自各腳本的 comment-based help；分類是 help 的 `.COMPONENT`，可用的值見 `Get-RepoScriptCategory`（`scripts/AiNexus/AiNexus.psm1`），新增入口腳本時要填，`scripts/tests` 會檢查。
- 中文環境：版面依顯示寬度排（中文佔 2 格），換行不切開英文單字、不讓全形標點落在行首；全形數字會轉成半形，清單參數也接受全形逗號，其他欄位出現全形英數或全形空白時提醒。需要 PowerShell 7（`pwsh`），在 Windows PowerShell 5.1 執行會被 `#requires` 擋下。
- `Verify.ps1`、`Build.ps1`、`Initialize-Database.ps1` 以 `Invoke-RepoSteps` 執行：每步顯示 `[n/總數]`，結束時（包括失敗）印出各步驟的結果、耗時與失敗原因，Windows Terminal 的分頁標題與工作列也會顯示進度。
