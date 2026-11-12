# 源碼掃描

## 打包與上傳

`./scripts/Export-SourceArchive.ps1` 把原始碼打包成根目錄的 `ai-nexus-source-<日期>-<commit>[-dirty].zip`，上傳後手動刪除（已在 `.gitignore`）。

- 預設打包工作目錄的現況：已追蹤的檔案（含未提交的修改）加上未被忽略的新檔，檔名帶 `-dirty` 表示含未提交的變更；`-Committed` 只打包 HEAD。
- 排除文件、`.claude`、圖示與測試（測試裡的假帳密只會變成誤報，需要時加 `-IncludeTests`），保留 lockfile 供 SCA 使用。

## 判定原則

- 先看資料流是否真的可被利用；可利用就修程式，修法以讓程式更好為準（共用、移除重複、收斂邊界），不為了避開規則改名或加無作用的設定。
- 判為不可利用時，在掃描平台留下理由與佐證（下表的程式位置或測試），下次掃描沿用。
- 新出現的同類結果先對照下表；不符合表中的條件，就當成新問題調查。

## 已知的規則誤判

| 規則 | 為什麼不可利用 | 佐證 |
|---|---|---|
| Use Of Hardcoded Password | 規則只看識別字含 password；被標的值是畫面文字（「AD 密碼」「永不過期」）、稽核欄位標籤或錯誤代碼的說明，不是密碼 | `passwordLabelFor`（`frontend/src/app/features/admin/identity/login-methods.ts`）；錯誤目錄由後端產生 |
| Client Server Empty Password | 表單模型的初始值與送出後清空；空密碼不會送出（前端 `required`），後端也以 401 `invalid_credentials` 拒絕並稽核 | `login-page.ts` 的 `clearPassword`、`user-account-fields.ts` 的 `savedAccountDraft` |
| Client Privacy Violation | 輸出的是欄位標籤，不是使用者輸入的密碼 | 同上 `passwordLabelFor` |
| Angular Usage of Unsafe DOM Sanitizer | 只有兩個已審查的信任邊界，ESLint 擋其他位置 | [網站安全](../architecture/security.md#回應與瀏覽器邊界) |
| HttpOnlyCookies In Config | `system.web/httpCookies` 只作用於舊 ASP.NET；ASP.NET Core 由全站 cookie 原則強制 | `TransportSecurity`、`WebSecurityTests` |
| Unencrypted Web Config File | `web.config` 不含秘密；秘密在部署機的 `appsettings.Secrets.json` | [設定與秘密](configuration.md) |
| Insufficient Logging of Exceptions | 掃描器不認得 `[LoggerMessage]` 產生的方法，catch 內直接呼叫也照樣標記；確認 catch 內有 `[LoggerMessage]` 呼叫或轉成 `Result` 錯誤即可 | `GdCalendarEdit.CommitAsync` 的 3410；[日誌事件](../architecture/log-events.md) |
