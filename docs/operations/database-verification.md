# 部署後結構驗證

給 DBA 與發布人員：結構部署後，以 `Verify-Database.ps1` 比對正式庫的實際結構與發布套件的 `database\App.dacpac`（由 `database/App` 建置，這一版預期的結構）。

為什麼要比實際結構：升級 SQL 可能漏跑或被改過，正式機也可能有尚未回收的熱修；網站啟動不檢查完整結構。

## 執行

```powershell
pwsh -NoProfile -File 'D:\Packages\AiNexus\Verify-Database.ps1' -Server 'sql.company.internal' `
  -DacpacPath 'D:\Packages\AiNexus\database\App.dacpac'
```

- 以 SqlPackage `DeployReport` 比對，只讀結構、不改資料庫。省略 `-Credential` 時用目前 Windows 身分；帳號需要 VIEW DEFINITION（部署帳號即可）。自簽憑證加 `-TrustServerCertificate`。
- 結束碼 0 表示一致；1 列出差異物件與讓正式庫符合預期所需的動作（Create／Alter／Drop）；`-ReportPath` 保留完整 XML。
- 忽略欄位順序、使用者／登入／角色／權限、schema 擁有者、檔案群組與檔案、資料庫選項（RCSI 由網站啟動時檢查）。正式庫**多出**的物件也算差異。
- **全文不比對**：全文目錄與索引不在 dacpac，正式機也可能沒有全文元件。報告中的全文物件只列為「略過」；正式機的全文索引用主機指令 `verify sql` 確認。

## 安裝 SqlPackage

- 開發機：`./scripts/Restore.ps1` 會一起還原。
- 正式主機或 DBA 工作站：腳本依序找 `-SqlPackagePath`、PATH 上的 `sqlpackage`、`Program Files\Microsoft SQL Server\170`（或 `160`）`\DAC\bin`。都找不到時失敗並列出安裝方式，不會略過：有 .NET SDK 就 `dotnet tool install --global microsoft.sqlpackage`（版本見訊息）；只有 Hosting Bundle 的 IIS 主機下載 [SqlPackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-download) 的 Windows zip，以 `-SqlPackagePath` 指定其中的 `SqlPackage.exe`。也可以在能連正式 SQL 的 DBA 工作站執行。
