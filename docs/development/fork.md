# fork 成新產品

公版以 `dotnet new` 範本（`.template.config/template.json`）fork：專案名、namespace 與產品識別一次換掉，不必維護改名腳本。範本不帶進 fork，這份文件原樣保留，記錄來源與手動要改的地方。命名原則見 [ADR 0013](../decisions/0013-data-project-and-template-naming.md)。

## 步驟

從乾淨的 clone 安裝範本：工作目錄裡的 `node_modules`、`.local/` 等即使已排除，安裝時仍會被掃描。

```powershell
git clone --depth 1 <公版 repository> D:\Temp\ai-nexus-template
dotnet new install D:\Temp\ai-nexus-template
dotnet new ainexus -n Contoso -o D:\GitProject\contoso --display-name 'Contoso 工作台'
dotnet new uninstall D:\Temp\ai-nexus-template

Set-Location D:\GitProject\contoso
git init
./scripts/Restore.ps1
./scripts/Initialize-Database.ps1 -Server '(localdb)\MSSQLLocalDB'   # 或請 DBA 以發布套件建庫
./scripts/Configure-Local.ps1
./scripts/Verify.ps1
./scripts/Start-Local.ps1
```

- `-n` 用 PascalCase 單字，它會成為專案名、namespace、資料庫名與 IIS 集區名。`--display-name` 省略時用 `-n`。
- 新庫建好後，結構改由新產品自己的開發庫匯出，流程見 [資料庫](../architecture/database.md)；其餘第一次啟動的設定見 [開發與執行](development.md#第一次啟動)。

## 範本替換的字樣

| 公版寫法 | 換成（`-n Contoso`） | 出現在 |
|---|---|---|
| `AiNexus` | `Contoso` | 專案、namespace、檔名、資料庫名、IIS 集區與路徑、cookie |
| `ainexus`／`AINEXUS` | `contoso`／`CONTOSO` | 外部來源的 schema 與角色、測試環境變數 |
| `ai-nexus` | `contoso` | npm 套件、Angular 專案、localStorage、匯出檔名、problem type URN、metric 前綴 |
| `AI Nexus`／`AI NEXUS` | `--display-name` 的值／大寫 | 畫面、文件標題、匯出文件 |

產品識別只能用這五種寫法（[CLAUDE.md](../../CLAUDE.md) 命名規範）；單獨的 `Nexus` 範本無法安全替換。

- **帶產品名**：cookie、localStorage、metric、環境變數、資料庫與 IIS 名稱。cookie 不分 port，同一台機器開發兩個產品時同名 session cookie 會互相覆蓋；其餘在同一台主機、同一個 collector 或同一個 SQL 執行個體上也會衝突。
- **不帶產品名**：型別、函式、設定鍵（`Databases:App`）、header（`X-CSRF-Token`）、claim 與 auth scheme。它們只在一個應用程式內有意義，fork 後不必改。

## 範本不處理、fork 後手動改

- **品牌圖形**：`frontend/public/brand.svg`、`frontend/src/app/shared/graphics/brand-logo.ts`（標誌外框）、聊天訊息的 `N` 標記；Angular selector 前綴 `nx` 是內部前綴，可保留。
- **組織設定**：`appsettings.json` 的 AD 網域、Gitea 網址與資料庫伺服器，見 [設定與秘密](configuration.md)。
- **用不到的整合**：公文系統 GD（[出勤與公文系統 GD](../features/attendance-gd.md)）等外部系統的功能依需要移除。
- `docs/decisions/` 保留公版的決策紀錄，裡面的舊型別名是歷史，不必改。
