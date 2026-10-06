# AI Nexus

AI 工作區：Angular 22、ASP.NET Core 10、MSSQL、AD 與 Google AI／Ollama 多供應商路由。模組化單體與前端 lazy routes 維持清楚結構，資料層保留 EDoc 的 Dapper DbHelper／scoped EfHelper，風格集中於三層 tokens。

聊天包含文件／圖片分析、掃描 PDF OCR、Markdown／程式碼、訊息分支、停止／斷線恢復、範本、搜尋、收藏／封存／標籤、本機草稿、文字備份與快捷指令。模型、思考與 Context 位於輸入區，支援鎖定模型與隱藏名稱；標題可雙擊修改，角色及問答定位清楚區分。

| 工作區         | 能力                                                                            | 文件                              |
| -------------- | ------------------------------------------------------------------------------- | --------------------------------- |
| 總覽           | 空間流程與狀態、日期篩選、每次／全對話／使用者費用、價格版本與 CSV              | [費用](docs/BILLING.md)           |
| 連網搜尋       | 手動開啟、SearXNG／Brave、來源與時間、配額、搜尋費用                            | [搜尋](docs/WEB_SEARCH.md)        |
| 程式庫         | Gitea 唯讀檔案／議題、固定 commit 快照、單一／區間 commit 的背景 AI review | [Gitea](docs/GITEA.md) |
| 個人設定       | 當頁設定視窗、主題、12–24px 閱讀／密度、操作、通知、草稿及用量                  | [功能指南](docs/FEATURES.md)      |
| 平台管理       | 角色／群組／功能、個人／群組逐模型 token 政策、個別用量與唯讀對話、前後差異稽核 | [管理](docs/ADMINISTRATION.md)    |
| 知識庫／閱讀器 | ACL、索引／OCR、SQL 向量檢索、引用及原文核對                                    | [知識庫](docs/KNOWLEDGE.md)       |
| 檔案庫         | 對話／知識／專案原檔、自動保存、搜尋篩選、重用與大型預覽                        | [檔案庫](docs/FILES.md)           |
| 成果文件       | 共用編輯、不可變版本、段落工具、Word／PDF                                       | [成果](docs/ARTIFACTS.md)         |
| 專案           | 共用指示、文件、範本及成果，提問仍屬個人                                        | [專案](docs/PROJECTS.md)          |
| 分享           | 具名收件人、版本快照、附件授權、到期／撤銷                                      | [分享](docs/SHARING.md)           |
| 品質評測       | 私人回饋、固定題庫、模型／指令比較、設定指紋、人工評分                          | [品質](docs/QUALITY.md)           |
| 背景任務       | 持久進度、租約／checkpoint、停止及重試                                          | [知識庫](docs/KNOWLEDGE.md)       |
| 通知 | 共用通知中心、未讀、進度／分享事件、前往內容、瀏覽器通知 | [規格](docs/NOTIFICATIONS.md) |
| 資料來源       | 公文／校務唯讀 adapter、歷程、私人成果及聊天草稿                                | [整合](docs/INTEGRATIONS.md)      |
| 介面元件       | 管理員檢視實際元件、主題、鍵盤／動畫及 tokens 匯出                              | [設計系統](docs/DESIGN_SYSTEM.md) |

介面與統計的共同定義見 [工作區名詞](docs/TERMINOLOGY.md)。

## 快速啟動

需要 Node 26.5.0、npm 11.6.1、.NET SDK 10.0.401、PowerShell 7.4+，在專案根目錄執行：

```powershell
./scripts/Restore.ps1
./scripts/Configure-Local.ps1
./scripts/Initialize-Database.ps1
dotnet dev-certs https --trust
./scripts/Start-Local.ps1
```

開啟 [本機工作區](https://localhost:5080/)，預設進入總覽。Configure 以遮蔽輸入保存既有 SQL 登入、AD 服務密碼及 Google key。Initialize 只在 AiNexus 不存在時建庫；InitialCreate 針對空庫，後續增量 migrations 依版本升級；不會刪除現有 DB。升級先停止舊 host，再套用 migrations。模型與 snapshot、物件描述均會檢查。HTTPS 憑證只需在開發機首次建立與信任。

**整合預覽只執行一個 ASP.NET 程序**，同時提供 Angular 產物與 API；已有 build 用 `./scripts/Start-Local.ps1 -SkipBuild`。熱更新用 `./scripts/Start-Dev.ps1`，管理 Angular 4200／API 5080，以 Ctrl+C 一起停止。見 [開發與執行](docs/DEVELOPMENT.md)。

## 設定與結構

一般參數在 .local/config/appsettings.Local.json，秘密在 .local/secrets/appsettings.Secrets.json，修改後重啟。.local／artifacts 整體忽略，進度／排查放 .local/notes。Git 保存 source、public defaults／examples、lockfiles、contracts、migrations 及長期文件。

| 文件                                       | 內容                                                           |
| ------------------------------------------ | -------------------------------------------------------------- |
| [參數](docs/CONFIGURATION.md)              | SQL／AD／Google、設定順序、鎖定／隱藏及模型能力                |
| [開發](docs/DEVELOPMENT.md)                | 啟動、build／驗證、契約、migration、版控                       |
| [功能](docs/FEATURES.md)                   | 工作區、聊天操作、快捷鍵及保存                                 |
| [附件](docs/ATTACHMENTS.md)                | 格式、OCR、配額及檔案生命週期                                  |
| [資料庫](docs/DATABASE.md)                 | 13 個 schema、物件／關聯、初始化、SQL 權限                     |
| [授權](docs/ACCESS_CONTROL.md)             | 功能 grant、預設角色、撤銷及擴充                               |
| [網站安全](docs/SECURITY.md)               | HTTPS／Cookie、CSRF、安全標頭、檔案隔離與部署驗收              |
| [架構](docs/ARCHITECTURE.md)               | 模組、共用邊界、隔離、推論與 durable jobs                      |
| [設計](docs/DESIGN_SYSTEM.md)              | tokens、字級、主題、共用元件及動畫                             |
| [向量](docs/VECTOR_ARCHITECTURE.md)        | 實作路徑、公文／校務資料、ACL 與 ANN 評估                      |
| [本地 AI](docs/LOCAL-AI.md)                | Ollama、16 GB GPU 的模型規劃與向量化優先順序                   |
| [Embedding 比較](docs/EMBEDDING_MODELS.md) | BGE-M3／Qwen、768／1024、query profile 與本地 Recall／MRR 工具 |
| [資源操作](docs/FEATURE_LIFECYCLE.md)      | 各功能的增刪修、歷史保存及操作權限                             |
| [IIS](deploy/iis/README.md)                | 單程序部署、AD、秘密、SSE 與驗收                               |

Google 與 Ollama 可同時啟用，各自排程、容量與故障隔離；預設路由為 google/gemma-4-26b-a4b-it，key 只在後端。Google 模式將此次需要的文字／圖片／上下文送往 Google，embedding 與聊天模型獨立。登入頁的傅立葉動畫使用自有 N 輪廓與 DFT，支援跳過／重播、手機及減少動態，表單全程可用。

## 驗證與部署邊界

```powershell
./scripts/Verify.ps1
./scripts/Test-Connections.ps1
./scripts/Test-SqlCapabilities.ps1
```

Verify 使用獨立資料庫、test provider 與 Edge fixtures；真實 AD／SQL／模型另用 Test-Connections，會使用模型配額。Test-SqlCapabilities 檢查實際 SQL、原生向量及精確 cosine。報告／截圖在 artifacts，提交前 stage 後執行 `./scripts/Test-Repository.ps1`。

每個 IIS app 使用一個 worker；聊天 executor 租約避免共用 SQL 的實例互相中止生成，但排程與模型容量仍在各程序內。首次升級須停止所有舊 host。文件及評測已有 durable 租約／checkpoint。原檔存站外，預設每人 5 GB，個人 override 優先於群組。完成回答顯示並保存耗時與 tokens。JSON 文字備份不含原檔，完整備份須包含同一時點的 SQL 與附件目錄，見 [備份與還原](docs/BACKUP.md)。公文／校務 adapter 與授權 view 契約已準備，實際連線／view 仍需設定；正式 IIS、來源 ACL、區網隔離、效能及備份還原需實機驗收。
