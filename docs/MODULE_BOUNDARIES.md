# 模組邊界

- 模組之間可以互相依賴，但**不可形成循環**。目標是能由下往上讀懂、測試與修改，不是零依賴。
- `ModuleBoundaryTests.Module_dependencies_have_no_cycles` 以 NetArchTest 計算 `AiNexus.Features` 各模組命名空間之間的依賴；出現循環時列出最短路徑，以及每一段由哪些型別造成。
- `Persistence`、`Configuration` 是共用基礎設施，不算模組：`NexusDbContext` 會引用所有模組的實體設定，各模組也都使用它。

## 目前的分層

上層可以使用下層，反過來不行。測試只檢查沒有循環，下表是現況的概略位置，方便判斷新程式碼該放哪裡：

| 層 | 模組 | 說明 |
| --- | --- | --- |
| 身分 | AccessControl、Identity | 授權資料與登入；不依賴其他業務模組 |
| 基礎能力 | Inference、Notifications、Monitoring、Diagnostics、Library | 模型、通知、監控；Inference 只依賴身分與授權 |
| 共用資料 | Operations、Collaboration、Conversations | 背景任務、資源 ACL、對話與訊息 |
| 資料延伸 | Attachments、Billing、WebSearch、Artifacts | 掛在對話或資源上的檔案、計費、搜尋、成果 |
| 內容 | Knowledge、Projects、Quality、Repositories、Integrations | 組合上述資料的功能 |
| 組合 | Chat、Sharing、Account、Administration、Dashboard | 一次用到多個模組的工作流程與管理介面 |

`Chat` 負責把對話、附件、專案、知識與網路搜尋組成一次提問（Context、執行、SSE），也負責整段對話的讀取、匯出、複製與刪除；這些端點沿用 `Inference`、`Conversations` 的 OpenAPI tag。`Account` 是登入者本人的資料、偏好、設定與用量。

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

## 不要做的事

- 不要為了通過測試把型別搬進 `Persistence` 或 Platform。`Persistence` 只放 `NexusDbContext` 本身就要處理的東西（例如它對應並遮罩的 `AuditEvent`）。
- 不要用 `IServiceProvider` 以字串或反射解析其他模組的服務來繞過檢查。
