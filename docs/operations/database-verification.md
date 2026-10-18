# 結構驗證與漂移偵測

給 DBA 與發布人員：以 `Verify-Database.ps1` 比對資料庫的實際結構與已發版的 `database\App.dacpac`（由 `database/App` 建置，這一版預期的結構）。同一個比對有兩個用途：

- **部署後驗證**：[部署](database-setup.md#部署)完成後執行一次，確認升級 SQL 沒有漏跑或被改過。
- **漂移偵測**：平時排程對 PROD 執行。有差異＝客戶端熱修尚未回收：在開發庫做同樣修改、匯出並提交，下一版發版後差異歸零才算結案（[ADR 0010](../decisions/0010-database-first-schema.md)）。

網站啟動時只比對 EF 讀寫的表與欄位（見 [資料庫](../architecture/database.md#改結構)）；索引、procedure、view 與多出的物件要靠這個腳本。

## 執行

```powershell
pwsh -NoProfile -File 'D:\Packages\AiNexus\Verify-Database.ps1' -Server 'sql.company.internal' `
  -DacpacPath 'D:\Packages\AiNexus\database\App.dacpac' -OutputPath 'D:\Packages\AiNexus\logs\drift'
```

- 以 SqlPackage `DeployReport` 比對，只讀結構、不改資料庫。省略 `-Credential` 時用目前 Windows 身分；帳號需要 VIEW DEFINITION。自簽憑證加 `-TrustServerCertificate`。
- 結束碼 0 表示一致；1 列出差異物件與讓目標符合 dacpac 所需的動作：Create＝目標缺少、Drop＝目標多出（熱修新增的 procedure、索引、表）、Alter＝定義不同（熱修改過的 procedure、欄位）。
- `-OutputPath` 寫出 `DeployReport.xml` 與 `Report.md`（時間、目標、dacpac 雜湊、差異與警示），排程時把結束碼 1 與 `Report.md` 接到通知；只通知，不自動修正。
- 漂移偵測要用**目前在 PROD 上的那一版**套件的 dacpac，不是 repo 最新版；否則尚未發版的變更也會被列為差異。
- 忽略欄位順序、使用者／登入／角色／權限、schema 擁有者、檔案群組與檔案、資料庫選項（RCSI 由網站啟動時檢查）。
- **全文不比對**：全文目錄與索引不在 dacpac，目標也可能沒有全文元件。報告中的全文物件只列為「略過」；全文索引用主機指令 `verify sql` 確認。

## 安裝 SqlPackage

- 開發機：`./scripts/Restore.ps1` 還原 repo 的 dotnet 工具；在 repo 根目錄執行時自動使用。
- 正式主機或 DBA 工作站：腳本依序找 `-SqlPackagePath`、PATH 上的 `sqlpackage`、`Program Files\Microsoft SQL Server\170`（或 `160`）`\DAC\bin`。都找不到時失敗並列出安裝方式，不會略過：有 .NET SDK 就 `dotnet tool install --global microsoft.sqlpackage`（版本見訊息）；只有 Hosting Bundle 的 IIS 主機下載 [SqlPackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage-download) 的 Windows zip，以 `-SqlPackagePath` 指定其中的 `SqlPackage.exe`。也可以在能連目標 SQL 的 DBA 工作站執行。`Deploy-Database.ps1` 用同樣的找法。
