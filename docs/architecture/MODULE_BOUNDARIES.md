# 模組邊界

- 模組之間可以互相依賴，但**不可形成循環**。目標是能由下往上讀懂、測試與修改，不是零依賴。
- `ModuleBoundaryTests.Module_dependencies_have_no_cycles` 以 NetArchTest 計算 `AiNexus.Features` 各模組命名空間之間的依賴；出現循環時列出最短路徑，以及每一段由哪些型別造成。
- `Persistence` 是共用基礎設施，不算模組：`NexusDbContext` 自動套用各模組的實體設定，跨模組外鍵與 SQLite 差異（`CrossModuleRelationships`、`SqliteModel`）引用各模組的實體，各模組也都使用它。設定的繫結（`InferenceSettings` 等）放在擁有該設定的模組。
- 每個模組資料夾的根目錄有 `<Module>Module.cs`（唯一的註冊點）；有預期失敗時也有 `<Module>Errors.cs`。兩者都由 `ModuleBoundaryTests` 檢查。

## 目前的分層

上層可以使用下層，反過來不行。測試只檢查沒有循環，下表是現況的概略位置，方便判斷新程式碼該放哪裡：

| 層 | 模組 | 說明 |
| --- | --- | --- |
| 身分 | AccessControl、Identity | 授權資料與登入；不依賴其他業務模組 |
| 基礎能力 | Inference、Notifications、Monitoring、Diagnostics、Library | 模型、通知、監控；Inference 只依賴身分與授權 |
| 共用資料 | Jobs、Audit、Collaboration、Conversations | 背景任務、稽核查詢、資源 ACL、對話與訊息 |
| 資料延伸 | Attachments、Billing、WebSearch、Artifacts | 掛在對話或資源上的檔案、計費、搜尋、成果 |
| 內容 | Knowledge、Projects、Quality、Repositories、Integrations | 組合上述資料的功能 |
| 組合 | Chat、Sharing、Account、Administration、Dashboard | 一次用到多個模組的工作流程與管理介面 |

`Chat` 負責把對話、附件、專案、知識與網路搜尋組成一次提問（Context、執行、SSE），也負責整段對話的讀取、匯出、複製與刪除；這些端點沿用 `Inference`、`Conversations` 的 OpenAPI tag。

整段對話的四個端點刻意不放在 `Conversations`：讀取的 `MessageDto` 帶有附件、引用、計費與網路來源（分屬 Attachments、Knowledge、Billing、WebSearch），刪除與複製要處理附件連結與配額鎖，而這些模組都依賴 `Conversations` 的實體。放進 `Conversations` 會形成循環，用介面反轉也得改動 API 合約或把其他模組的 DTO 搬進來。`Conversations` 只管對話本身（建立、列表、改名、標籤、分支、設定、匯入）。`Account` 是登入者本人的資料、偏好、設定與用量。

## 新增依賴造成循環時

依序考慮，選第一個可行的：

1. **型別放錯模組**：把型別移到真正擁有該行為的模組。例：模型政策屬於 Inference、個人設定屬於 Account、檔案庫清單需要知識文件而放在 Knowledge。
2. **下層定義介面、上層實作**：下層需要上層的資料或動作，但不需要知道是誰。例：
   - `IActiveUsers`（AccessControl 讀啟用中的使用者，Identity 實作）
   - `ISignInGrant`（Identity 登入時套用授權，Administration 的首次管理員實作）
   - `IModelCallMeter`（Inference 的模型任務計費，Billing 實作）
   - `IPrivateReaders`（Attachments 清理草稿時辨識私人閱讀文件，Knowledge 實作）
   - `ServiceModel`（Knowledge 登記向量模型，Inference／Billing 用來顯示名稱與價格目標）
3. **domain event**：「A 發生後 B 跟著處理」，A 不需要結果。例：`UserSignedOut` 由 Monitoring 移除線上連線；`ContainerDeleted` 由 Conversations、Artifacts 解除專案連結。寫法見 [後端撰寫慣例](BACKEND_CONVENTIONS.md)。
4. **只是參數**：下層方法不要收上層的 request 型別，改收自己定義的參數。例：`ConversationTurn` 取代 `CreateRunRequest`。

另外兩個固定做法：

- 兩端分屬不同模組的外鍵設定放在 `Persistence/CrossModuleRelationships.cs`；模組自己的實體設定只參照自己的實體。資料庫約束不變。
- 共用的識別字放在下層模組，例如管理功能的 `FeatureIds.Admin`／`Policies.Admin` 在 AccessControl，`AdministrationConfiguration` 引用它。編譯期常數不會產生依賴，但放在下層才不會誤導讀者。

## 直接呼叫或 domain event

- **直接呼叫**：需要對方的回傳值、要依結果決定 HTTP 回應或是否繼續（查詢、授權、配額、排程任務、計費預約），或是對方的檢查必須在自己寫入之前完成。
- **domain event**：「A 發生後 B 要跟著處理」，A 不需要知道結果，B 的寫入要和 A 同一個交易，例如刪除對話／成果時撤銷分享、刪除專案時解除成果的專案連結。只有在依賴方向能因此反轉、避免循環時才改；如果呼叫端仍為了查詢依賴對方，改成事件沒有好處。
- 事件是發布模組裡過去式命名的 `public sealed record`，實作 `IDomainEvent`，放在引發它的 slice 檔（如 `DeleteConversation.cs` 的 `ConversationDeleted`）。訂閱模組實作 `IDomainEventHandler<T>`，在自己的 `AddServices` 以 `AddDomainEventHandler<TEvent, THandler>()` 註冊；訂閱方引用發布方，不可反過來，也不可因此形成循環。
- 發布端注入 scoped `DomainEvents`，在呼叫 `SaveChangesAsync` 前 `Raise(...)`。`SaveChangesAsync` 寫入前，`DomainEventInterceptor` 先分派所有待處理事件（handler 再引發的事件也會處理，最多 `DomainEvents.MaxRounds` 輪），`AuditEventInterceptor` 再整理稽核列，所以 handler 新增的 entity 和稽核列與發布端一起儲存。沒有交易時會自動開一個交易包住分派與寫入。
- handler 在同一個 `NexusDbContext` 與交易內、發布端的變更寫入之前執行：可以追蹤 entity、使用 `ExecuteUpdate`／`ExecuteDelete`（立即在目前交易執行），但不可呼叫 `SaveChanges` 或自行開關交易。handler 不保證先後順序，彼此不可依賴。
- `Raise` 之後到 `SaveChangesAsync` 之間不要提早 return；分派失敗會清掉待處理事件。同步的 `SaveChanges` 遇到待處理事件會丟例外。
- 需要在交易提交後非同步處理的副作用（外部呼叫、檔案刪除）不用 domain event，交給既有的 durable job。

## 不要做的事

- 不要為了通過測試把型別搬進 `Persistence` 或 Platform。`Persistence` 只放 `NexusDbContext` 本身就要處理的東西（例如它對應並遮罩的 `AuditEvent`）。
- 不要用 `IServiceProvider` 以字串或反射解析其他模組的服務來繞過檢查。
