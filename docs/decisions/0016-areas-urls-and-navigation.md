# 0016 區、網址、頁面目錄與選單

- **狀態**：採用（2026-10-18），取代 [ADR 0009](0009-module-names-and-data-driven-navigation.md)。
- **實作**：第一步（區資料夾、模組搬移與改名）已完成；網址、頁面目錄與選單尚未開始，這些部分現行程式仍是 0009 的做法。每一步落地時同步更新對應文件。
- **決定**：
  - 程式與網址共用「區 → 模組 → 頁面」三層；權限 ID 與 API 不帶區。
  - 頁面與動作的目錄只定義在程式碼；資料庫只存授權與選單。
  - 選單是獨立的資料樹，節點可以是分群、頁面或外部連結。
  - 資料列層級的權限見 [ADR 0017](0017-organization-units-and-data-scopes.md)。

## 三層命名

| 層 | 後端 | 前端 | 網址 | 權限 ID |
|---|---|---|---|---|
| 區 | `Features/Admin/` | `features/admin/` | `/admin` | 不帶 |
| 模組 | `Features/Admin/AccessControl/` | `features/admin/access-control/` | `/admin/access-control` | 不帶 |
| 頁面 | 能力子資料夾 | 同名子資料夾 | `/admin/access-control/roles` | `access-control.roles` |
| 動作 | 不另設 | 不另設 | 不另設 | `access-control.roles.maintain` |

- **區只是資料夾，不是依賴邊界**：模組名全域唯一，`ModuleBoundaryTests` 改以「區.模組」辨識模組，依賴規則不變。區根目錄只放模組資料夾。
- **schema 仍＝模組名小寫**：SQL schema 無法巢狀，模組名全域唯一讓它成立；`AiNexus.Data/App/<Module>/` 與 `database/App/<schema>/` 也不加區。
- **權限 ID**：`<module>.<page>[.<action>]`，模組用 kebab-case。不帶區，模組換區時授權資料不必改。
- **API**：維持 `/api/v1/<resource>`。API 是給程式呼叫的資源合約，不隨介面重組改版。
- **網址**：`/<area>/<module>[/<page>][/<id>]`。
  - 單頁模組省略頁面段；頁內分頁、篩選用 query string，不再加段。
  - ID 段格式要與頁面段可區分（數字或 GUID）。
  - 根路由例外只有登入、個人設定與首頁轉址（`/` → `/work/dashboard`）。
  - 模組換區時加一條前端轉址。

## 區

| 區 | 名稱 | 模組 |
|---|---|---|
| `work` | AI 工作台 | Dashboard、Chat、Conversations、Projects、Files、Knowledge、Artifacts、Prompts、Sharing、Collaboration、Repositories、Jobs |
| `staff` | 員工服務 | 規劃中：Attendance（出勤打卡）、Leave（請假）等公司內部自助服務 |
| `admin` | 系統管理 | Identity、AccessControl、Organization、Operations、Audit、Diagnostics、Monitoring、Integrations、Quality、Billing、Design（只有前端） |
| `platform` | 基礎能力 | Account、Inference、WebSearch、Notifications；沒有選單頁，不出現在網址 |

- 區依「頁面主要給誰用」決定，總數維持 3～8 個；新增區要修訂本 ADR。
- 員工服務用 `staff` 而不是 `hr`：打卡、請假、報修、會議室都是員工自助服務，不只人事。

## 模組改名與重組

| 現在 | 改為 | 原因 |
|---|---|---|
| Attachments | Files | 使用者看到的是「檔案庫」，網址 `/work/files` |
| Library | Prompts | 只放提示範本，Library 看不出內容 |
| Administration | 拆散 | 「管理」是區，不是模組：Users → Identity，Roles、Navigation → AccessControl，ModelPolicies → Inference，Retrieval → Knowledge，Usage 與營運設定 → Operations，管理端讀對話 → Conversations |
| （新增） | Organization | 部門樹，見 ADR 0017 |

例外：使用者清單與洞察、管理目錄、群組儲存與平台用量放 Operations，而不是 Identity 或 AccessControl。這些回應內嵌 Inference、Billing、Files、Account 的 DTO，放進下層模組會成環，而已發布的 API 合約不改。

保留原名：Jobs（背景工作；Tasks 留給日後的待辦）、Diagnostics（日誌之外還有前端問題回報與管線監控）。

選單上的「身分與權限」是分群（資料），底下放 Identity、AccessControl、Organization 三個模組的頁面；顯示名稱不必等於模組名。

## 頁面目錄

- 後端 `FeatureCatalog` 取代 `FeatureIds`，每個頁面與動作只宣告一次：ID、區、模組、網址、預設名稱、圖示、支援的資料範圍。經 OpenAPI 輸出給前端，型別同現在的 `FeatureId`。
- 前端每個模組一份 `<module>.routes.ts`，路由以 `data: { feature }` 指向目錄，由同一個守衛檢查；`app.routes.ts` 每個區只有一行 `loadChildren`。
- 測試雙向比對：目錄 ↔ Angular 路由、目錄 ↔ Seed。
- `accesscontrol.Features` 只留 Id、Enabled 與名稱覆寫，給授權外鍵用；route、圖示、分群移出。
- 麵包屑由目錄推得（區 > 模組 > 頁面），與選單無關。

## 選單

`accesscontrol.NavigationItems` 取代 `NavigationGroups` 與 `Features.GroupId`，是一棵樹，節點三種：

- `group`：分群，可巢狀。
- `page`：指向目錄裡的頁面；沒有授權就不顯示。
- `link`：外部系統（公文、客服），存 URL 與是否另開視窗。可指定一個只授權、沒有頁面的功能（例如 `links.document-system`）控制誰看得到；不指定則所有登入者可見。

規則：

- 同一頁面可出現在多個位置；麵包屑跟網址走，不會有歧義。
- 外部連結只接受 `https://`，主機須在設定的允許清單內。選單是管理員可編輯的資料，不能成為 `javascript:` 或釣魚入口；URL 不帶使用者資料。
- 沒有可見節點的分群不顯示；只剩一個可見節點的分群攤平。
- 側欄預設展開兩到三層；模組內的頁面用模組內導覽。另有 Ctrl+K 搜尋（含外部連結）、我的最愛與最近使用。

## 原因

- 模組到數十個後，平鋪資料夾難以瀏覽；區讓每一層維持在十來個。
- 網址跟程式走、不跟選單走：選單常調整，網址卻被書籤、通知與稽核紀錄引用。
- 區很少換；換的代價是搬資料夾加一條轉址，權限與 API 不受影響。
- 同一個網址目前寫在 `app.routes.ts`、Seed 與 `FeatureIds` 三處，頁面到數百個時必然不一致。
- 0009 不加大類層的前提是模組少於約 40 個，預期規模會超過。

## 何時重新評估

某個區需要獨立部署，或單一區超過約 20 個模組時。
