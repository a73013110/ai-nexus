# AI Nexus

公司 AI 工作台：Angular 22、ASP.NET Core 10、SQL Server、AD 登入、Google AI／Ollama。以模組化單體與 lazy-loaded 前端維持清楚結構，資料層重用 EDoc 的 Dapper DbHelper 及 scoped EfHelper。

第一版包含個人對話、Markdown／程式碼、編輯與重新生成分支、停止／斷線恢復、角色→群組→功能授權、模型鎖定／名稱隱藏、思考選項、Context 預估、Markdown 匯出、明暗主題與減少動態。

工作台新增 12 項功能：文件／圖片分析、提示詞範本、對話指令、收藏、封存、標籤、內容搜尋、個人草稿、副本、JSON 文字備份、訊息尋找與快捷指令。操作見 [功能指南](docs/FEATURES.md)。

個人設定提供閱讀、外觀、對話操作、通知與用量；管理後台支援角色、功能群組、模型限制、日配額與異動稽核，詳見 [平台管理](docs/ADMINISTRATION.md)。

知識庫支援具名與群組授權、文件索引及回答來源；共用閱讀器提供 PDF 頁碼、縮放、原文核對與文字搜尋，掃描 PDF／圖片可辨識文字。背景任務保存進度並支援取消與重試。見 [知識庫與文件](docs/KNOWLEDGE.md)、[SQL 向量設計](docs/VECTOR_ARCHITECTURE.md)。

## 快速啟動

需要 Node 26.5.0、npm 11.6.1、.NET SDK 10.0.401 與 PowerShell 7.4+。在專案根目錄執行：

```powershell
./scripts/Restore.ps1
./scripts/Configure-Local.ps1
./scripts/Initialize-Database.ps1
./scripts/Start-Local.ps1
```

開啟 [本機工作台](http://localhost:5080/chat)。Configure 以遮蔽輸入保存既有 SQL 登入、AD 服務密碼與 Google key；不必另建 SQL login。Initialize 只在 AiNexus 不存在時建庫，並套用尚未完成的 migrations，既有資料保留。

**整合預覽只需一個 ASP.NET 程序**，已包含 build 後的 Angular。已有產物時用 `Start-Local.ps1 -SkipBuild`。開發時用 `./scripts/Start-Dev.ps1`，腳本同時管理 Angular 4200 與 API 5080、自動更新並以 Ctrl+C 一起停止。詳細指令與 port 選項見 [開發與執行](docs/DEVELOPMENT.md)。

## 設定與文件

| 文件                              | 內容                                                             |
| --------------------------------- | ---------------------------------------------------------------- |
| [參數設定](docs/CONFIGURATION.md) | SQL／AD／Google、設定優先順序、模型鎖定與名稱隱藏、思考能力      |
| [開發與執行](docs/DEVELOPMENT.md) | 一鍵啟動、建置／驗證、契約與 migration、文件版控規則             |
| [功能指南](docs/FEATURES.md)      | 12 項擴充的操作入口、快捷鍵、保存與備份規則                      |
| [文件與圖片](docs/ATTACHMENTS.md) | 格式、抽取方式、配額、模型能力與附件生命週期                     |
| [資料庫](docs/DATABASE.md)        | 七個業務 schema、資料表／索引／關聯、EDoc 分工、初始化與正式權限 |
| [授權](docs/ACCESS_CONTROL.md)    | 使用者→角色→群組→功能、預設 chat、撤銷與功能擴充                 |
| [架構](docs/ARCHITECTURE.md)      | 模組責任、推論生命週期、資料隔離與擴充邊界                       |
| [設計系統](docs/DESIGN_SYSTEM.md) | 三層 tokens、字級／密度、motion／可及性與調整方式                |
| [IIS 部署](deploy/iis/README.md)  | 單程序部署、LDAP／Windows、秘密來源、SSE 與交付驗收              |

本機一般參數放 `.local/config/appsettings.Local.json`，帳密與 key 放 `.local/secrets/appsettings.Secrets.json`；修改後重啟。`.local` 與 `artifacts` 整體忽略，機器環境／進度／排查筆記留在 `.local/notes`。Git 只保存 public defaults／examples、source、lockfiles、contracts、migrations 與長期文件。

預設 Google 模型為 `gemma-4-26b-a4b-it`，原生 SSE 與 minimal／high thinking。管理員可關閉模型選擇與隱藏名稱，API 會強制政策。Google 模式將目前分支的提問與上下文送往 Google；key 僅在後端。參考 [Google Gemma 官方說明](https://ai.google.dev/gemma/docs/core/gemma_on_gemini_api)。

## 驗證

```powershell
./scripts/Verify.ps1
./scripts/Test-Connections.ps1
```

Verify 使用獨立資料庫與測試 provider，Edge 測試也明確使用 fixture；不代表正式 AD／SQL／模型驗收。Test-Connections 使用本機實際設定，檢查 SQL／EDoc helpers／access seed、AD 服務 bind、模型串流及合成文件／圖片辨識，會使用模型配額。報告與截圖在 `artifacts`。

目前聊天部署設計要求單一 host／IIS worker；文件背景任務已具備 durable 租約與 checkpoint。JSON 文字備份不含附件原始檔；完整備份使用 SQL 備份。正式 IIS、區網雙帳號隔離、設備效能與備份還原按部署文件另行驗收。
