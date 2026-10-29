# 結構描述比對與異動偵測

給 DBA 與發布人員。資料庫的升級 SQL 由 `Release.ps1` 產生（[部署](database-setup.md#部署)）；Visual Studio「結構描述比對」只用來審核，正式機的手動異動由網站偵測。

網站啟動時另外比對 EF 讀寫的表與欄位，不一致就拒絕啟動（[資料庫](../architecture/database.md#改結構)）。

## 審核更新包

需要 Visual Studio 的「資料儲存和處理」工作負載（SSDT）。

1. 開啟發布包的 `database\compare.scmp`：來源是這一版的 dacpac、目標是起點的 dacpac，選項與 `update.sql` 相同。檔案記的是產生時的絕對路徑；套件搬到別台後，改選 `database\compare\` 下的兩個檔案。
2. 按「比較」，看到的就是 `update.sql` 第 2 步會做的事；不要從這裡「更新」或「產生指令碼」。
3. 物件清單與資料表欄位的增減也寫在 `changes.md`，PR 或發版前不開 VS 也能看。

選項由 `scripts/AiNexus` 的 `Get-RepoSqlPackageOption` 決定，建庫與更新共用；要改時改那裡，`update.sql`、`compare.scmp`、`Initialize-Database.ps1` 會一起變。

## 部署後驗證

`update.sql` 最後一步列出與這一版不同的物件；沒有結果才表示目標庫和這一版完全一致。之後隨時可在 SSMS 執行：

```sql
EXEC [releases].[usp_ListSchemaChanges];   -- modified／added／removed 與時間
EXEC [releases].[usp_GetSchemaObject] N'operations', N'usp_ListAdminUsers';   -- 應有與實際的定義本文
```

## 手動異動

- 比對的是「這一版應有的結構」（`update.sql` 寫入的快照，取自發版時的演練庫），所以更新前就存在的客戶端修改也會列出。判定與時間見 [ADR 0028](../decisions/0028-schema-drift-and-version-display.md)。
- 網站的[版本頁](../features/releases.md)列出同樣的清單、並排差異，並可下載異動物件目前定義的 `.sql`。
- 有異動＝熱修尚未回收：在開發庫做同樣修改、以 `scripts/Export-Database.ps1` 匯出並提交，下一版更新後清單歸零才算結案。不在正式機自動修正。
- 下一次 `update.sql` 的前置檢查遇到異動會停止，避免覆蓋尚未回收的修改。
- 不比對：權限、使用者、統計、全文索引（改用主機指令 `verify sql`）與欄位說明；`database/App/export-exclusions.txt` 列出的表也略過。
