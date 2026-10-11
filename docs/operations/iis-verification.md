# IIS 部署驗收

## 分層驗收：確定設定正確

先執行套件附的靜態檢查（appcmd 檢查需管理員權限）：

```powershell
pwsh -NoProfile -File 'D:\Packages\AiNexus\Verify-IIS.ps1' `
  -AppPath 'D:\CoreProject\AiNexus\app' -AppPool AiNexus `
  -BaseUrl 'https://你的實際主機名稱'
```

它檢查必要檔案、Production、外部設定位置、金鑰及站外原檔目錄、Host、集區與 HTTPS session；不會顯示秘密，也不聲稱讀得到檔案就代表集區身分可讀。

再做 **SQL／設定與原檔 IO 驗證**，不啟動背景 workers、不登入 AD、不產生 AI 回答（僅讀取模型清單）：

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet 'D:\CoreProject\AiNexus\app\AiNexus.Host.dll' `
  --contentRoot 'D:\CoreProject\AiNexus\app' `
  --LocalConfigPath '..\config\appsettings.Production.json' `
  --SecretsConfigPath '..\config\appsettings.Secrets.json' `
  verify deployment
```

輸出應有 `environment=Production`、`sqlConnected=true`、`pendingMigrations=0`、`providerAvailable=true`、`availableModelCount>0`、`sqlEncrypted=true`、`trustsSqlCertificate=true`、`adConfigured=true`、各 providers 狀態、模型數、keyRingPath、正確 attachmentStoragePath 與 attachmentStorageWritable=true、`ready=true`。SQL 版本落後或其他基線會輸出原因並退出，模型服務或指定模型不可用會列出 `modelNotice`。檢查會讀取各 provider 模型清單，不產生回答，並寫入／讀取／刪除一個短暫合成原檔 probe；`ready` 是平台狀態，個別群組仍需模型授權。切換 provider 或模型後，於管理頁確認各群組允許的模型，既有白名單不會自動清空或放寬。退出碼 0 才通過。此指令用**目前維運 shell 身分**讀檔，IIS 身分仍需實際網站驗證。

輸出也顯示 embedding provider／維度、webSearchEnabled 與 giteaEnabled，並驗證這些 optional tools 的參數範圍。`ready=true` 只表示核心設定、SQL 與原檔 probe 通過，不代表 Gitea token、搜尋 API 或 embedding 品質已實測通過。

最後用新的無痕瀏覽器驗收：

1. 直接開 `/chat`、`/projects`：未登入會到 `/login?returnUrl=...`，沒有先出現私人工作區。
2. AD 登入後能開對話，重新整理仍登入；登出後私人 URL 再次要求登入。
3. 短回答與「圖解傅立葉轉換」能串流到完成；停止／重新生成都保留歷史。
4. 上傳合成文字檔及圖片，實際模型能力正確；文字模型不應允許圖片。
5. 網路面板 `/api/v1/status` 是 ready；匿名的 `/health/ready` 回 200（資料庫可連線，SQL 停止時回 503），`/health/live` 只代表程序存活。負載平衡或監控探測用 `/health/ready`；它不檢查模型服務。
6. 一般帳號不可開管理 API；管理員查看對話會留稽核。專案／知識權限隔離正常。
7. 回收集區後重新登入／生成可用；config／keys／logs／data 的 URL 無法讀取。
8. 啟用 Google 與 Ollama 時，兩個使用者可同時使用不同 provider；停用其中一台服務仍可選其餘模型，沒有自動 provider fallback。
9. 上傳後 SQL 沒有 bytes、站外出現 opaque .blob；已用／上限／剩餘一致，草稿計入，多處重用不重複計算。管理個人上限調高能超過群組上限，null 恢復繼承。引用中的原檔刪除回應 409，移除引用及刪檔後實體檔與 metadata 消失。
10. 完成、取消及失敗回答可展開耗時／排隊／執行與 tokens，重新整理仍保留；管理者同樣可讀並留下稽核。
11. 總覽在個人／平台範圍、日期與不同幣別間正確切換；先設定測試模型價格，再確認單次／全對話金額及管理 CSV。平台查閱應有稽核。
12. 啟用 Gitea 後用唯讀個人 token 連線、讀取固定 commit 檔案並帶入草稿；回收集區後仍可解密 token。`keys` 同時保護此 token，不能在更新時清空。
13. 如啟用連網搜尋，先完成 [SearXNG／Brave 設定](../features/web-search.md)，再測 opt-in、來源與每日配額；未設定時入口停用。自架搜尋仍會向外部搜尋引擎送出公開提問。
14. 若切到本地 embedding，先確認 Ollama 可達、指定模型已安裝且回傳維度正確，再重新索引合成文件及檢查引用；不要把核心 ready 當成向量驗收。

如需真實 AI、AD TLS 與 EF／Dapper 寫入探測，再於受控環境跑主機指令 `verify connections --output <logs內檔案>`。該工具會發送合成資料並呼叫真實模型；與上面的 `verify deployment` 模型清單及原檔 IO 探測分開執行。主機指令清單用 `dotnet AiNexus.Host.dll --help` 查看；結束碼 0 成功、1 失敗、2 用法錯誤。
