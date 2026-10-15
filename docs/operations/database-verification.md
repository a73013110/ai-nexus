# 結構差異與部署後驗證

給 DBA 與發布人員：`Publish-IIS.ps1 -FromMigration` 除了[拆檔 SQL](iis-deployment.md#給-dba-的拆檔-sql)，還產生每個物件的定義差異與預期結構；DBA 執行完 SQL 後以 `Verify-Database.ps1` 比對正式庫的實際結構。取代手動維護新舊兩個資料庫再用 Visual Studio 結構描述比對。

為什麼要比實際結構：逐檔執行可能漏檔，但每個 migration 的最後一檔照樣寫入 `__EFMigrationsHistory`，之後同 migration 的檔都會被略過，網站啟動檢查（只看 history）也會通過。

## 發布時產生

```text
<套件>\
├─ Verify-Database.ps1
└─ database\
   ├─ manifest.md、schema\、data\                  ← 拆檔 SQL
   ├─ diff\manifest.md                             ← 新增／刪除／修改的物件清單
   ├─ diff\knowledge.Tables.EmbeddingProfiles.diff ← 每個變動物件一份 unified diff
   └─ expected.dacpac                              ← 發布版本的完整結構
```

- 主機指令 `db diff`（`AiNexus.Host --help`）在 `Databases:Nexus` 的伺服器建立暫存庫 `AiNexus_Snapshot_<隨機>`（與 `db init` 一樣建庫後開 RCSI），migrate 到 `-FromMigration` 後以 SqlPackage 逐物件匯出，再 migrate 到最新匯出一次並存成 `expected.dacpac`，逐檔比對。暫存庫結束時一定刪除，失敗或 Ctrl+C 也一樣；刪除失敗會印出庫名請 DBA 手動 DROP。
- 帳號沿用開發機 `.local` 的 `Databases:Nexus`，需要建庫權限，與 `db init` 相同。參考伺服器用與正式機同版（SQL Server 2025）的開發或建置伺服器，**不要指向正式機**；runtime 與部署帳號都不需要建庫權限。
- SqlPackage 版本固定在 repo 的 `.config/dotnet-tools.json`，發布腳本先 `dotnet tool restore`。密碼經暫存的 response file 傳給 SqlPackage，不出現在命令列。
- SqlPackage 每次匯出同一物件內的批次順序不同、欄位會對齊，比對前先排序批次並合併連續空白，所以 diff 只反映定義變動；它是給人看的，不是升級 SQL。
- 發布機連不到 SQL Server 時加 `-SkipSchemaDiff`：只產生拆檔 SQL，套件沒有 `diff\`、`expected.dacpac` 與驗證腳本。

## 部署後驗證

```powershell
pwsh -NoProfile -File 'D:\Packages\AiNexus\Verify-Database.ps1' -Server 'sql.company.internal' `
  -DacpacPath 'D:\Packages\AiNexus\database\expected.dacpac'
```

- 以 SqlPackage `DeployReport` 比對，只讀結構、不改資料庫。省略 `-Credential` 時用目前 Windows 身分；帳號需要 VIEW DEFINITION（部署帳號即可）。自簽憑證加 `-TrustServerCertificate`。
- 結束碼 0 表示一致；1 列出差異物件與讓正式庫符合預期所需的動作（Create／Alter／Drop），通常是漏跑的檔；`-ReportPath` 保留完整 XML。
- 忽略欄位順序、使用者／登入／角色／權限、schema 擁有者、檔案群組與檔案、資料庫選項（RCSI 由網站啟動時檢查）。正式庫**多出**的物件也算差異，所以漏跑的 DROP 也抓得到。
- **全文不比對**：參考伺服器缺全文元件時 `InitialCreate` 會略過全文索引，正式機也可能沒有，兩邊不同不代表漏檔。報告中的全文目錄／索引只列為「略過」；正式機的全文索引用主機指令 `verify sql` 確認。`diff\manifest.md` 會註明參考伺服器有沒有全文元件。
- 補救漏檔：該 migration 已寫入 history，重跑漏掉的檔會整檔略過。請 DBA 在維護時段手動執行該檔 `IF NOT EXISTS` 包住的語句，再重新驗證。

## 安裝 SqlPackage

- 開發機：`./scripts/Restore.ps1` 會一起還原。
- 正式主機或 DBA 工作站：腳本依序找 `-SqlPackagePath`、PATH 上的 `sqlpackage`、`Program Files\Microsoft SQL Server\170`（或 `160`）`\DAC\bin`。都找不到時失敗並列出安裝方式，不會略過：有 .NET SDK 就 `dotnet tool install --global microsoft.sqlpackage`（版本見訊息）；只有 Hosting Bundle 的 IIS 主機下載 [SqlPackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-download) 的 Windows zip，以 `-SqlPackagePath` 指定其中的 `SqlPackage.exe`。也可以在能連正式 SQL 的 DBA 工作站執行。
