# IIS 區網交付操作

本文件供正式交付審閱；部署前需完成目標主機與雙帳號驗收。參數見 [CONFIGURATION](../../docs/CONFIGURATION.md)，物件見 [DATABASE](../../docs/DATABASE.md)。

1. 由主機管理員確認正式 IIS／AD 網域、內部 DNS 名稱與受信任 HTTPS 憑證。安裝 .NET 10 Hosting Bundle 及 IIS Windows Authentication 功能。
2. 執行 `scripts/Build.ps1`，將 artifacts/publish 作為應用程式部署目錄；Angular 已包含在 wwwroot。使用 publish 產生的 web.config 與 in-process hosting，獨立 application pool 設為 No Managed Code，關閉 32-bit。
3. 此應用採單一記憶體排程器：maximum worker processes 必須 1，禁止 web garden、雙應用實例與重疊 recycle。更新時先停止提交／停止或等待生成，再用 app_offline.htm 或受控 stop/start。設定 Disable Overlapped Recycle，避免舊／新程序同時使用同一 GPU 與資料庫。
4. 選定 AD 驗證模式：目前預設 AdAuthentication.Mode=Ldap，IIS 啟用 Anonymous Authentication，讓登入頁與 auth/session 可存取，Windows Authentication 停用；ASP.NET Core 以 LDAP 驗證後簽發 cookie，其他 API 仍要求登入。若選 Windows 模式，則 Anonymous 停用、Windows Authentication 啟用，並確認公司 intranet／瀏覽器整合驗證政策及 proxy Negotiate 連線親和性。
5. 設定 `LocalConfigPath` 與 `SecretsConfigPath` 指向 publish／wwwroot 外的受 ACL 保護檔案，或用部署系統注入環境變數。涵蓋 SQL、AD、Inference 模型政策與正式 AllowedHosts。環境為 Production、HTTPS 綁定正確；SQL 使用受信任憑證且 TrustServerCertificate=false。設定 `DataProtection.KeyRingPath` 為 application pool 身分可寫的持久目錄，啟用該身分的 Load User Profile；Windows DPAPI 依執行身分保護金鑰，避免更新目錄時遺失登入 session。設定與 key ring 備份須保護 ACL；換身分或主機的 DPAPI 還原需另規劃。LDAP 需可連到 StartTLS 389／LDAPS 636 且信任 AD 憑證。
6. 由 DBA 審閱 db/migrations.sql 後在 AiNexus 執行。應用需各業務 schema 必要 DML，以及 access 主檔 SELECT／UserRoles INSERT；授權編輯由受控管理登入處理。正式不開啟 ApplyMigrationsOnStartup。
7. Google 模式允許後端 HTTPS 連 generativelanguage.googleapis.com，key 留在後端；依公司政策設定模型選擇／名稱顯示與核准 profiles。改回 Ollama 時，只允許後端存取指定服務，模型清單與 thinking 能力以實際裝置確認。

IIS hosting、Hosting Bundle 與 application pool 參考 [Microsoft 官方文件](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0)，Windows 驗證參考 [Windows Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth?view=aspnetcore-10.0)。

## 上線前實測

附件／文字備份需要較大的 request body，API host 上限為 10 MB。IIS 額外的 request filtering 必須至少允許該大小；可將以下節點合併到 publish `web.config` 的 `system.webServer`，保留原有 `aspNetCore`／handler 設定。每檔、單則及個人配額仍由應用程式檢查，詳見 [附件設定](../../docs/ATTACHMENTS.md)。

```xml
<security>
  <requestFiltering>
    <requestLimits maxAllowedContentLength="10485760" />
  </requestFiltering>
</security>
```

- 以至少兩個真 AD 帳號，確認名稱映射與彼此對話／run／SSE 隔離。
- 實測文件、圖片、預覽與下載；另一個帳號不能讀取附件。確認 Google 外送文件／圖片符合部署單位設定的使用範圍。
- 用另一台區網電腦測試 HTTPS、多輪、停止、重試、重新整理與分頁關閉後還原歷史。
- 確認 SSE 沒有被 IIS／額外 proxy compression、buffering 或 idle timeout 延遲；10 秒 heartbeat 必須持續，跨 180 秒生成也能收到狀態。API 已 DisableBuffering，IIS 設定仍需實測。不要加入 application response compression 壓縮 text/event-stream。
- 驗證 bounded queue、每人一個 active run、多使用者提交與取消。先維持單 GPU worker，不啟用雙生成。
- 設定 idle timeout／啟動策略，避免程序在背景生成中被無預期停止；確認 recycle 後舊 active run 明確變為 failed/server_restarted。
- 實測模型首字、吞吐、GPU／記憶體與中文品質，再決定 context／輸出上限。未知總長度不顯示完成百分比。
- 完成 SQL 備份與獨立還原演練、對話／audit 保留政策後才算交付完成。

診斷入口：/health/live 僅代表程序存活；經驗證的 /api/v1/status 才反映 storage 與 scheduler。me、models 顯示實際可用狀態。HTTP 503 不會自動降級假使用者或測試模型。API 預設不記完整 prompt／回答／連線字串；問題回應包含 traceId。
