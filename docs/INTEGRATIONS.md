# 公文與校務系統唯讀整合

本版提供公文／校務兩個 Dapper adapter、資料來源狀態、參數化搜尋、逐筆本文／狀態／版本／簽核歷程，以及帶入聊天草稿與匯入個人成果快照。呼叫使用既有 EDoc `IDbHelper<TDb>`／`DbHelper<TDb>`；外部 SQL 與 AI Nexus EF 寫入分開，不持有資料庫交易等待外部查詢。

**目前未設定兩套來源連線，也未宣稱已完成正式連線驗證。** 預設停用且沒有來源群組授權。程式、關係式權限測試、SQL 參數邊界與瀏覽器操作使用測試資料驗證；實際來源 view 需原系統管理員確認，不能從登入／選單權限推定所有資料都可公開。

## 已分析的既有程式

| 來源與檔案                                                         | 觀察與設計影響                                                                                                                                                             |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GDWEB_HOTAI `src/gdbean/query/Qry_ReceiveReg.java:91`              | `Doc_vwInOutDetail` 提供 `DesNo`、`DesNumber`、`AccDesDate`、`DesMain`、`ChargeName`、`ChargeDepName`、`DocKind`；報表依部門與子部門過濾。報表查詢不直接當作通用閱讀授權。 |
| GDWEB_HOTAI `src/gdbean/Frm_InOut/Receive_Bean.java`               | 使用 `userId`、`userDepNo` 與代理公文／部門資訊；代理與承辦關係需要逐筆映射。                                                                                              |
| GDWEB_HOTAI `src/gdbean/flow/PrivilegeFlow_Bean.java:46`           | `CoerceFlowRoute.CoerceDepUserID` 使用部門與人員組合；這是流程／強制路徑設定，不當作所有文件的閱讀權。                                                                     |
| GDWEB_HOTAI `src/model/frm/FrmService.java:53`                     | 用印流程涉及 `Sys_Seal`、狀態與圖片；本版不呼叫改狀態或核章程式。                                                                                                          |
| GDWEB_HOTAI `src/gdbean/db/SafeRsBean.java`                        | 有參數化 query／queryProc；部分舊查詢仍有字串組合，新 adapter 使用固定 SQL 與參數。                                                                                        |
| HL_Mvc_MEIHO `WebApp.Models/DbContextFactory.cs`                   | EF6 與 Dapper，分 School／Auth／GDR 三個 context。adapter 使用另建的唯讀 SQL 連線，不能拿 EF EntityClient 連線字串直接當 SqlClient 字串。                                  |
| HL_Mvc_MEIHO `WebApp/Sql/Auth/Webfunction_roles.sql`               | 帳號群組 `T_Account_Group`、角色功能 `T_WebFunctionRole`、個別帳號 `T_WebFunctionAcc` 與 `T_WebFunction.IsRun`。授權 view 應明確檢查有效帳號、角色／個別授權狀態。         |
| HL_Mvc_MEIHO `WebApp.Services/s_Menu.cs:188`                       | 依 `loginAccount` 篩選功能，另有 `CP_ReviewAccount`／`CP_ReviewAccountFirst` 的審查者例外。選單可用不代表所有學生列可讀。                                                  |
| HL_Mvc_MEIHO `WebApp.Models/DBModels/RGP_Edu01.cs`、`RGP_Edu06.cs` | 可辨識單位 code/name 及班級結構。第一階段先核准規範／表單、單位資料；不暴露學生、成績、健康及帳號密碼欄位。                                                                |

原始位置分別為 `D:\JavaProject\GDWEB_HOTAI` 與 `D:\NetProject\HL_Mvc_MEIHO`。未修改這兩個專案，未搬入原始機敏設定。AD SID、完整 DOMAIN\account／UPN 與舊人員代碼的對應應由來源端維護，不能只截取帳號尾碼。

## 設定步驟

1. 原系統 DBA 準備兩個 `nexus` view，契約見 [authorized-views.sql](../db/integrations/authorized-views.sql)。範本故意不回傳任何列；先根據原系統資料列授權替換內容並驗證撤權、代理、停用、機密、學生範圍等案例。
2. 建立專用唯讀 SQL login/user，只給 `nexus_reader` 對兩個 view 的 SELECT。不給來源全庫 `db_datareader`、原始表 SELECT、寫入或簽核 procedure 權限。`ApplicationIntent=ReadOnly` 只是連線意圖，不能取代真正的 SQL 權限。
3. 在 `.local/config/appsettings.Local.json` 的 `Integrations.Sources.Gdweb.Database`／`Meiho.Database` 設定 Server、Name、TrustServerCertificate、ConnectTimeoutSeconds；帳密在 `.local/secrets/appsettings.Secrets.json` 的相同位置填 User／Password。進階使用者仍可在秘密檔填 `ConnectionStrings.LegacyGdweb`／`LegacyMeiho` 完整 SqlClient 字串，非空時優先於分項設定。
4. 在一般設定的 `Integrations.Sources.Gdweb`／`Meiho` 設定 `Transport=sql`、`Enabled=true`、`AclContractConfirmed=true`、`AllowedGroupIds`、`CommandTimeoutSeconds`（2–30 秒）及 `MaxResults`（1–50）。空群組不會授權任何人，平台管理員也不能繞過。未來 API adapter 可沿用來源識別與 ACL 契約；目前設定其他 transport 會明確顯示未支援。
5. 「平台管理 → 角色群組」將 `資料來源` 功能授予需要的群組。此功能初始只提供給平台管理員，來源權限仍須另外設定。重新啟動後查詢，測試帳號應只有原系統核准的列。

```json
{
  "Integrations": {
    "Sources": {
      "Gdweb": {
        "Enabled": false,
        "Transport": "sql",
        "AclContractConfirmed": false,
        "AllowedGroupIds": [],
        "CommandTimeoutSeconds": 10,
        "MaxResults": 30,
        "Database": {
          "Server": "",
          "Name": "",
          "TrustServerCertificate": false,
          "ConnectTimeoutSeconds": 10
        }
      },
      "Meiho": {
        "Enabled": false,
        "Transport": "sql",
        "AclContractConfirmed": false,
        "AllowedGroupIds": [],
        "CommandTimeoutSeconds": 10,
        "MaxResults": 30,
        "Database": {
          "Server": "",
          "Name": "",
          "TrustServerCertificate": false,
          "ConnectTimeoutSeconds": 10
        }
      }
    }
  }
}
```

兩個來源 marker 強制加密與唯讀意圖，連線 timeout 預設 10 秒。Production 也可明確設定 `TrustServerCertificate=true` 使用自簽 SQL 憑證，連線仍加密。API 不回傳連線字串，失敗記錄僅保留來源代碼與例外類型。

## View 契約與查詢

`AuthorizedRecords` 必須對每個允許的 AD SID／完整帳號／RecordId 提供唯一一列。欄位為 ActorSid、ActorAccount、RecordId（160）、RecordKind、Title（120）、Status、Revision（160）、ModifiedAt（datetimeoffset）、Body（最多 16,000 字元）。公文 kind=`document`；校務第一階段 kind=`reference` 或 `organization`。Revision 必須在本文／可讀內容修改時改變。不得把用戶密碼、憑證或非核准欄位放到 Body。

`AuthorizedRecordHistory` 提供相同身分與 RecordId 的歷程，包含 EventId、At、Kind、Actor、Description、Revision，**必須套用與主資料相同的來源授權**。最多顯示最近 100 筆，超過會標示可至原系統閱讀。讀取歷程後再確認主資料 grant 與版本。

SQL 是固定的 SELECT，查詢字、類型、身分、識別碼與筆數都是參數；LIKE 的 `%`、`_`、`[` 等會跳脫。沒有任意 SQL、使用者指定 Actor、來源端修改／簽核入口，也沒有讓模型產生或執行 SQL 的工具。

「帶入對話」建立個人對話及草稿，使用者確認後再送出，來源本文作為不可信 JSON 資料。超出訊息長度時不截斷，請分段使用。「儲存個人快照」再查一次授權與版本，保存當時文字至私人成果，附來源識別與版本資訊；`content.SourceReferences` 保存來源索引。後續來源修改／撤權不會遠端刪除已合法保存的快照，使用者後續分享成果仍是明確的獨立操作。

## 接續向量整合

本版不自動將外部資料複製到共用知識庫。下一階段需依來源類別建立可重驗的 live ACL、版本同步與刪除／撤權清理，並在向量候選生成前套用來源授權。先做公文規範與校務政策，再擴充原有逐列權限的學生查詢。詳見 [VECTOR_ARCHITECTURE.md](VECTOR_ARCHITECTURE.md)。
