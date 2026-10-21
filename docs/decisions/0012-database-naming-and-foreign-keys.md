# 0012 資料庫命名、schema 與外鍵

- **狀態**：採用（2026-10-18）
- **決定**：沿用團隊既有的小寫物件前綴，補齊約束前綴；一個模組一個 schema；同一資料庫內的核心關聯一律設外鍵。

## Schema

- schema 表達**歸屬與權限**，不表達物件種類：一個業務模組一個 schema，名稱＝模組名小寫（[ADR 0009](0009-module-names-and-data-driven-navigation.md)）。不開 `sp`、`views` 這類 schema，物件種類由前綴表達。
- 物件放在擁有它的模組 schema；跨 schema 的 join 與外鍵沒有限制（同一資料庫）。
- SQL 一律寫 `schema.物件`，不依賴預設 schema。
- 權限以 schema 授予，例如 `GRANT EXECUTE ON SCHEMA::identity TO <應用程式帳號>`。
- `dbo` 不放業務物件，只放跨模組共用的 `udf_`、`udt_`、`tvp_`。
- fork 後保留平台模組的 schema（`identity`、`accesscontrol`…），業務表開自己的 schema。

## 物件命名

| 物件 | 規則 | 例 |
|---|---|---|
| 資料表 | PascalCase 複數，無前綴 | `identity.Users` |
| 欄位 | PascalCase | `LastSeenAt` |
| 預存程序 | `usp_<動詞><名詞>`，與 use case 同名 | `operations.usp_ListAdminUsers` |
| 函式 | `udf_` | `dbo.udf_SplitIds` |
| 使用者定義型別／資料表值參數 | `udt_`／`tvp_` | `dbo.tvp_IdList` |
| 觸發程序 | `tr_<表>_<時機>` | `tr_Users_AfterUpdate` |
| 檢視 | `vw_` | `billing.vw_DailyUsage` |
| 同義字 | `syn_<來源庫><物件>` | `integrations.syn_GdOrders` |
| 主鍵 | `pk_<表>` | `pk_Users` |
| 外鍵 | `fk_<子表>_<父表>`；同一對表多條時 `fk_<子表>_<欄位>` | `fk_UserRoles_Users` |
| 索引 | `ix_<表>_<欄位>[_<欄位>]` | `ix_Users_LastSeenAt` |
| 唯一索引 | `ux_<表>_<欄位>` | `ux_Users_LocalAccount` |
| Check | `ck_<表>_<欄位或規則>` | `ck_Users_AttachmentLimitBytes` |
| Default | `df_<表>_<欄位>` | `df_Users_Enabled` |

- 約束名稱在 schema 內必須唯一，所以帶表名。
- 唯一性用唯一索引（`ux_`），不用 UNIQUE constraint：唯一索引可以加篩選條件，命名也只需一套。
- **`df_`、`ck_` 一定要命名**。SSMS 設計工具只填預設值時，SQL Server 會產生隨機名稱，每個環境都不同；改到該欄位時會被刪除重建，結構比對也較難判讀。
- 每張表與欄位都要有說明（`MS_Description`），由 DBA 在 SSMS 維護；測試檢查漏寫。procedure、function、view 的說明寫在檔頭，不加 `MS_Description`（[SQL 物件](../architecture/sql-objects.md#檔頭)）。

## 外鍵

同一資料庫內的核心關聯一律設外鍵，動作用 `NO ACTION`（不連鎖刪除），刪除由程式或 procedure 明確處理。

- **原因**：DBA 在 SSMS 直接改資料、procedure 直接寫表，都會繞過程式驗證；外鍵是唯一能擋住孤兒資料的地方。
- **成本**：寫入時對父表主鍵多一次索引查詢，成本很小。SQL Server **不會自動替外鍵欄位建索引**，要自己建 `ix_`，否則刪除父列時會掃描子表。
- **不設外鍵的情況**：
  - 高寫入量的日誌、事件、稽核表（例如 `RunEvents`、`DiagnosticEvents`、`AuditEvents`）。
  - 跨資料庫參照（無法設）。
  - 刻意保留已刪除對象識別碼的歷史資料。
- 批次匯入時可以先 `NOCHECK CONSTRAINT`，完成後必須以 `WITH CHECK CHECK CONSTRAINT` 重新驗證；否則約束會變成不受信任（`is_not_trusted`），最佳化器不再使用它。

## 何時重新評估

團隊改用其他命名慣例，或某類表的寫入量讓外鍵檢查成為可量測的瓶頸時。
