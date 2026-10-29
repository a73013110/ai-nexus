# 版本與結構異動

結論：所有人在使用者選單底部看得到目前版本；有 `releases` 權限的人（預設 administrators）在「版本與結構異動」頁看程式與資料庫的版本對照、正式機上不在這一版裡的結構異動與部署歷史。為什麼這樣設計見 [ADR 0028](../decisions/0028-schema-drift-and-version-display.md)，發版見 [ADR 0027](../decisions/0027-single-version-and-tag-based-release.md)。

## 所有使用者

- 使用者選單的「關於」：版本、發布日期、修訂與本版更新（`feat` 列為新功能、`fix` 列為修正）。開發中執行為 `v0.0.0-dev`，沒有更新說明。
- 網站更新後，開著的舊分頁在下一次 API 回應時跳出「已更新到 v…」提示與「重新整理」；同一分頁同一版只提示一次，不自動重新整理。

## 版本頁

- **版本對照**：程式、資料庫，以及程式需要的最低資料庫版本。資料庫低於最低版本時標示不相容，監控的「資料庫結構」卡片同時為 failing；開發中的程式不檢查。
- **結構異動**：與這一版應有結構不同的物件（新增、修改、移除）。選取後並排顯示「這一版」與「正式機目前」的定義，離變更較遠的相同行預設收起。資料表顯示的是欄位與約束的說明，不是可執行的 DDL。
- **下載 .sql**：異動物件目前的定義（procedure、view、function、trigger 可直接執行；資料表只附說明；移除的只留註解）。套用到開發庫後匯出提交，處理方式見 [手動異動](../operations/database-verification.md#手動異動)。每人每分鐘 2 次。
- **部署歷史**：程式在新版本第一次啟動時自動記錄（只有發布包，IIS 回收不重複記錄）；資料庫由 `update.sql` 記錄。

## 通知與監控

- 每天檢查一次；出現新的一組異動時，通知有版本頁權限的人，點選前往版本頁。同一組異動不重複通知。
- 監控的「資料庫結構」卡片：不相容 failing、有異動 degraded、還沒有快照（尚未以 `update.sql` 部署）idle、一致 healthy；每 10 分鐘重算。

## 稽核

查看清單、查看單一物件與下載 `.sql` 都記入[活動稽核](activity-audit.md)（`releases.schema_changes.read`、`releases.schema_object.read`、`releases.schema_changes.exported`）。
