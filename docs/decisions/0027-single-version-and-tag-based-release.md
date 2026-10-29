# 0027 單一版號與以 git tag 為基準的發版

- **狀態**：提議（2026-10-29）
- **決定**：
  - 全專案一條版號 `YYYY.MM.N`（N 每月從 0 起算，每次發版加一），不分前端、後端、資料庫。一個客戶一個專案，不依客戶分版號。
  - 版號只存在 annotated tag（`v2026.11.4`），不寫在任何檔案；tag 說明以 trailer `Components: app, database` 記這次發了哪些部分。
  - 元件依路徑判定：`database/` 是資料庫；`backend/`、`frontend/`、`contracts/` 是程式（前後端同一次建置、同一個 IIS 套件，不分開）。
  - 唯一入口 `Release.ps1`（取代 `Publish-IIS.ps1`）：不帶參數只列狀態，`-Publish` 發版。
  - 資料庫更新包由「上一個含資料庫的 tag」與目前 commit 各建一份 dacpac 比對產生；不保存 dacpac，也不維護對應客戶版本的鏡像庫。

## 為什麼用 annotated tag

- 它是獨立物件，記錄建立者、時間與說明，再指向 commit；lightweight tag 只是指向 commit 的名字，放不了元件清單。
- `git describe` 預設只認 annotated tag，開發中的建置可推得 `2026.11.4-dev.3+a1b2c3d`；`git push --follow-tags` 也只推 annotated tag。
- 打了不移動：tag 清單就是發版紀錄，不需要 force push。

## 為什麼單一版號

- 程式與資料庫可以分開發（程式要跑內規流程、procedure 可立即更新），但同一條序列能直接比新舊：學校顯示「程式 v2026.11.3、資料庫 v2026.11.5」，一眼看出資料庫先發了兩次。分兩條序列要的資訊，Components 已經記到。
- 日期版號：內部網站沒有給他人引用的相容承諾，SemVer 的主次版沒有意義；年月直接看出新舊。
- 不用 Nerdbank.GitVersioning：它以 commit 數算最後一碼，適合每次 build 都要遞增的安裝套件；網站要的是「哪一次發版」。
- 鏡像庫（客戶版本的本機副本）要人工同步，和客戶庫一樣會漏，而且不在 git、無法審核；客戶版本改由 tag 重建。

## 開發者情境

| 情境 | 做法 |
| --- | --- |
| 改了 SQL | 照舊 `Export-Database.ps1` 後 commit（[ADR 0010](0010-database-first-schema.md)） |
| 看要發什麼 | `Release.ps1`：各元件自上次發版的變更（程式列 `feat`／`fix`，資料庫列物件）、開發庫尚未匯出的物件、下一個版號 |
| 發版 | `Release.ps1 -Publish`：有變更的元件都放入；程式還在跑內規流程時加 `-Only Database` |
| 目標庫落後不只一版（UAT 與 PROD 不同步、某版沒上） | 加 `-From v2026.11.3`，填目標庫版本頁顯示的版本 |
| 舊版出問題、master 已有未發的功能 | 從 tag 開 `hotfix/*` 分支修正，在分支上 `-Publish`，再合回 master |
| 本機重現客戶的資料庫 | `Initialize-Database.ps1 -Version v2026.11.3` 在 LocalDB 建臨時庫 |

`-Publish` 要求工作目錄乾淨、位於 master 或 `hotfix/*`；演練通過才建立 tag，確認後推送。產出 `artifacts/releases/v2026.11.4/`：

```text
README.md        執行順序（先資料庫、後程式）與手動步驟
app/             IIS 套件；根目錄 version.json（版本、commit、建置時間）
database/
  update.sql     唯一要執行的檔案（SSMS，SQLCMD 模式）
  changes.md     變更清單，給人審核
  compare.scmp   VS 結構描述比對：兩份 dacpac 與比對選項已設定
  rollback.sql   只含 procedure、function、view 的反向腳本；資料表回復一律用備份
  manual/        database/Scripts/ 在這段期間新增的一次性資料修正
```

## update.sql

1. **前置檢查**：目標庫記錄的版本必須等於比對起點，否則停止並提示改用 `-From`（建了 tag 卻沒上線也在這裡擋下，不必刪 tag）；上次更新後有手動異動的物件列出並停止，確認後才繼續（[ADR 0028](0028-schema-drift-and-version-display.md)）。
2. **結構**：sqlpackage 以兩份 dacpac 產生，選項同 ADR 0010 的發版參數。
3. **全文、種子資料**：可重跑，沿用現有包裝。
4. **記錄**：寫入版本與預期快照。

- 一個檔案取代原本六個步驟：不會漏跑；VS 圖形介面只用來審核。
- 演練：LocalDB 以起點 dacpac 建庫、執行 update.sql，再和目前的 dacpac 比對，必須沒有差異；預期快照也取自這個庫。
- 起點 dacpac 以 `git worktree` 取出 tag 建置，不動工作目錄；SDK 由 `global.json` 固定，結果依 commit 快取在 `.local/`。

## 一次性資料修正

- 移除 `Data/<版本>/` 與 `-Release`：開發者不必事先猜版號。
- 腳本提交到 `database/Scripts/<yyyy-MM-dd>-<描述>.sql` 經 PR 審核；發版依 git 範圍帶入 `manual/`，由工程師判斷執行。複雜的資料更新仍由工程師處理，但不會漏掉，也留得下紀錄。

## 版本流向

- `scripts/AiNexus` 模組以單一函式從 `git describe` 推得版號，`Build.ps1` 與 `Release.ps1` 共用；沒有 tag 時為 `0.0.0-dev+<commit>`。
- 後端以 `-p:Version`、`-p:SourceRevisionId` 寫入 InformationalVersion（診斷日誌已讀取）；前端以 `ng build --define` 注入 `APP_VERSION`。
- 移除 `frontend/package.json` 未使用的 `version`。

## 影響

採用後修訂 ADR 0010（流程、`Data/`、漂移偵測），改寫 [資料庫建置與權限](../operations/database-setup.md)、[結構描述比對](../operations/database-verification.md)、[IIS 部署](../operations/iis-deployment.md)、[IIS 更新](../operations/iis-upgrade.md)。

## 何時重新評估

一個專案要服務多個客戶，或正式機改為可以執行部署工具（可改由 SqlPackage 直接對目標庫比對）時。
