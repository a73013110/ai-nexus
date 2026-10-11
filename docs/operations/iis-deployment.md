# IIS 部署

- 正式環境是單一 Windows IIS 網站、in-process、**一個 worker**，環境固定 Production；前後端在同一個套件，主機不需要 Node.js。
- 設定、金鑰與資料都放在 app 之外，更新時只替換 `app`。路徑以 `D:\CoreProject\AiNexus` 為例。
- 首次部署順序：確認空資料庫 → [外部設定](iis-configuration.md) → 複製新 app → [站外目錄、ACL 與集區](iis-site.md) → [初始化資料庫](iis-site.md#空資料庫初始化) → [部署驗收](iis-verification.md) → 啟動網站。日後更新見 [更新與回復](iis-upgrade.md)。
- 相關維運文件：[備份與還原](backup.md)、[診斷日誌維運](diagnostics.md)。

## 部署目錄與各檔用途

```text
D:\CoreProject\AiNexus\
├─ app\                         ← IIS 網站實體路徑，只指這一層
│  ├─ AiNexus.Host.dll / deps.json / runtimeconfig.json / 依賴套件
│  ├─ appsettings.json          ← 發版預設值，不放真實帳密
│  ├─ web.config                ← ANCM、Production、外部設定位置
│  └─ wwwroot\                  ← 已編譯 Angular；不需在主機啟動 npm
├─ config\
│  ├─ appsettings.Production.json  ← SQL／AD 位址、Host、模型與限額
│  └─ appsettings.Secrets.json     ← SQL 帳密、AD 服務密碼、Google key
├─ keys\                        ← cookie／antiforgery 的 Data Protection 金鑰
├─ data\attachments\            ← 原檔持久儲存，不隨 app 發版替換
├─ data\diagnostics\            ← 每日／大小輪替 JSONL、SQL補送 checkpoint
├─ logs\                        ← 暫時開啟的 ANCM 啟動日誌
└─ app.previous-時間\            ← 選用的上一版檔案，不對外提供
```

不要把 IIS 指到整個 `AiNexus` 根目錄，也不要把 config／keys／data 放入 app 或映射成 IIS 虛擬目錄。`keys` 不是 AD 角色資料；角色與授權在 SQL。更新 app 時保留 keys，可避免所有人因金鑰遺失而被登出。Windows 使用執行身分的 DPAPI 加密金鑰，因此須固定 application pool 身分並載入 user profile。更換主機／身分不能只複製加密後的 XML 就假設可解密；單台可重新建立 key ring 並讓使用者重新登入，多台負載平衡另需規劃共用金鑰、憑證保護與會話路由。

## 主機先備條件

使用支援 .NET 10 的 Windows Server，先啟用 IIS，再安裝 **.NET 10 Hosting Bundle x64**（只裝 SDK 或 Runtime 不足以保證有 ASP.NET Core Module V2）。若 Hosting Bundle 早於 IIS 安裝，重新修復安裝。檢查 `dotnet --list-runtimes` 是否包含 `Microsoft.NETCore.App 10.x` 與 `Microsoft.AspNetCore.App 10.x`。安裝／修復後安排 IIS 重啟。Hosting Bundle、ANCM 與 in-process 的要求見 [Microsoft IIS 指南](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)。

主機需可連 SQL `192.168.2.95` 的實際 TCP port、AD 的 LDAPS 636 或 LDAP StartTLS 389，以及選用的 Ollama 主機 11434。Ollama 使用 localhost 只適用 IIS 和 GPU 在同台。Google 模式另需可連官方 HTTPS 443；完全本地模式不需 Google key。

網站需公司信任的 HTTPS 憑證、正確 DNS 與 IIS HTTPS binding。這是**網站**憑證，與 SQL 自簽憑證放行分開。Production 的登入與 CSRF cookie 固定 Secure；只用 HTTP 即使看得到頁面，也無法正確維持登入。不要以切 Development 解決。

## 在開發機產生完整套件

```powershell
Set-Location D:\GitProject\ai-nexus
pwsh -NoProfile -File scripts/Verify.ps1
pwsh -NoProfile -File scripts/Publish-IIS.ps1 -DataRoot 'D:\CoreProject\AiNexus\data' -SkipBuild -PublishDirectory artifacts/verification
```

套件放 `artifacts/iis/<時間>/`，只含部署需要的東西：app／config 範本／空 keys／logs、`Verify-IIS.ps1`，以及發布當下產生的 `migrations.sql`（idempotent，給 DBA 審閱；與 app 出自同一份原始碼），不預設攜帶秘密。文件不打包，請看 repo。`-DataRoot` 是正式主機的站外資料根目錄（必填），套件 config 的 `Attachments.StoragePath`、`Diagnostics.Directory` 會設為其下的 `attachments`、`diagnostics`。輸出的 `app` 才是發版成品；已包含前端與後端，IIS 主機不用 Node.js。`PublishDirectory` 預設仍為 `artifacts/publish`；執行完整 Verify 後應明確封裝其 `artifacts/verification` 產物。

若是在受控環境製作含本機設定的內部移轉套件，可以使用 `-IncludeLocalConfig`；這會複製秘密，套件必須全程受 ACL 保護並在移轉完成後依公司政策清理。預設不複製現有 key ring。`-DestinationPath` 指**全新且空的套件 app 目錄**，不是正在運行的網站；腳本拒絕覆蓋非空目錄。既有 config／keys 也不會被這個封裝流程覆蓋。
