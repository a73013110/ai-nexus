# 儲存庫權限

Gitea 儲存庫改為逐人授權：使用者在「儲存庫權限」頁申請讀取或寫入，經[簽核](flow.md)核准後，同步工作把帳號加為該儲存庫的協作者。設計與取捨見 [ADR 0037](../decisions/0037-repository-access-per-person.md)；閱讀程式碼仍用個人權杖，見 [Gitea](gitea.md)。

## 誰能做什麼

| 角色 | 看得到 | 能做 |
| --- | --- | --- |
| 一般使用者（`repositories.access`） | 開放申請、未封存、Gitea 還在的儲存庫，加上自己有權限或負責的 | 申請、申請變更（升級或延長）、放棄自己的權限 |
| 儲存庫負責人 | 自己負責的儲存庫的授權名單、審核中的申請、Gitea 上直接加的協作者 | 審核流程中的「儲存庫負責人」關卡；直接授予讀取或寫入、移除一般授權 |
| 系統管理員（`repositories.access.manage`） | 全部儲存庫 | 授予管理權限、以 Gitea 帳號授予（要寫理由）、設定負責人與是否開放申請、立即同步 |

- 授予與移除都記稽核並通知當事人；已有權限時，新的授予或核准以新內容取代。
- 申請前與授予前會用服務帳號確認 Gitea 有這個帳號（登入名＝AD 帳號）。沒有時回 `gitea_account_missing`：請對方先登入 Gitea 一次，或開啟 HLAD 的使用者同步。

## 同步

- 排程 `repositories.access-sync`（每 5 分鐘）是唯一寫入 Gitea 的地方：讀服務帳號所屬組織的儲存庫成目錄，把協作者調成與授權一致，處理到期，並記下平台沒有紀錄的協作者。核准、授予、移除後會立即排入一輪。
- 授權列的狀態：等待同步 → 已生效；失敗時滑過徽章可看原因，下一輪自動重試。已結束但還沒從 Gitea 移除的授權顯示「移除中」。
- 新發現的儲存庫預設開放申請；在 Gitea 找不到的標「Gitea 已找不到」，不再列給一般使用者。
- 「另由團隊授權」表示還有 Gitea 團隊能存取這個儲存庫，逐人授權對這些人沒有效果；同步不會代為移除團隊。
- 「Gitea 上直接加的協作者」：系統管理員可「轉成授權」納入管理（到期、覆核、稽核），或到 Gitea 移除。

## 設定

區段 `Repositories:Gitea`（[`GiteaOptions`](../../backend/src/AiNexus.Features/Work/Repositories/GiteaOptions.cs)）：

- `ServiceToken`（秘密）：服務帳號的權杖。空白時整個功能停用：頁面顯示未啟用，不同步、不能申請或授予。
- `ReapplyHours`（預設 24）：每隔幾小時把所有有效授權重寫一次，修正有人在 Gitea 手動改掉的權限。
- `ExpiryNoticeDays`（預設 7）：授權到期前幾天通知本人與負責人。

錯誤 `gitea_service_unauthorized`／`gitea_service_forbidden` 表示權杖失效或服務帳號不是組織 Owner，同步的執行紀錄（[背景作業](scheduling.md)）會留下原因。

## 上線步驟

1. 在 Gitea 建一個專用帳號，設為所有組織的 Owner；Owners 團隊只留最少的人。
2. 以該帳號建立權杖，只給 `write:repository`、`read:organization`、`read:user`，填入秘密設定的 `Repositories:Gitea:ServiceToken`，重新啟動。
3. 系統管理員在「儲存庫權限」按「立即同步」，確認儲存庫都列出，逐一設定負責人；不開放申請的關閉。
4. 在「簽核流程」確認 `儲存庫權限` 的關卡（預設：主管 → 儲存庫負責人）。
5. 公告請大家申請。團隊授權還在時，核准的協作者已生效但不影響工作。
6. 切換日在 Gitea 把儲存庫改為私有，並移除 Contributors、RD、Servers 等團隊對儲存庫的存取；部署用帳號改用部署金鑰，或由系統管理員以 Gitea 帳號授予。
7. 檢查各儲存庫的「Gitea 上直接加的協作者」，轉成授權或移除。

離職：帳號停用時不自動撤銷（[ADR 0019](../decisions/0019-account-disable-and-departure.md)），帳號覆核會列出「帳號已停用，仍有儲存庫權限」，由管理員移除。
