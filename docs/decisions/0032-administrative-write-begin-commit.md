# 0032 行政寫入改為明確交易、rowversion，並依規則宣告最小的鎖

- **狀態**：採用，逐模組遷移（2026-11-02）；卡片與持有期間先行，其他模組暫留 `ExecuteAsync`。
- **決定**：
  - handler 以 `write.BeginAsync(目標)` 開始、`transaction.CommitAsync()` 寫入，取代 callback；沒提交就離開一律回滾。
  - 使用者編輯的表加 `RowVersion`，寫入時帶回讀到的版本，不符回 409 `admin_write_conflict`。
  - 預設不取鎖。只有唯一索引與 rowversion 擋不住的跨列規則，才以 `AuditTarget.Locking(...)` 宣告資源層級的鎖。
  - 新路徑不再於交易內重查權限；`RequireFeature` 在同一請求已查過。
- **修訂**：[ADR 0031](0031-administrative-write-scoped-locks.md)（模組鎖不再是預設）、[ADR 0001](0001-single-iis-in-process-locks.md)（`PunchImportGate` 的取代方式）。

## 原因

- **callback 不直覺**：`SaveChanges` 藏在框架裡，只看 handler 看不出何時寫入；提前回傳的流程也難讀。明確的提交讀起來就是業務邏輯加上提交，與 `TransactionScope.Complete()` 同一種習慣。
- **`await using var` 而不是區塊**：提交當下就放掉交易、鎖與連線，using 結束得晚也不佔資源；區塊只多一層巢狀，開始到提交一樣長。
- **鎖解決不了覆蓋**：使用者看舊畫面改資料時，鎖只會讓兩筆排隊，後者仍蓋掉前者。只有版本比對擋得住，而且單列更新有了它就不需要鎖。
- **模組鎖的成本全部人付**：一張卡的指派會擋住另一張卡與整個匯入。多數寫入只碰一列或有唯一索引，不必排隊；需要互斥的規則其實都能寫出範圍（這張卡的期間、匯入與卡片的關係）。
- **rowversion 用 `long` 對應**：JSON 與查詢字串可直接傳，前端是 `number`，不必處理 Base64。八位元組的值實際不會超過 2^53。

## 卡片怎麼判斷

| 寫入 | 鎖 | 為什麼 |
| --- | --- | --- |
| 改備註、隱藏 | 無 | 單列，`RowVersion` 就夠 |
| 新增卡片 | 匯入鎖 | 匯入會自動建卡 |
| 刪除卡片 | 這張卡＋匯入鎖 | 刪除要確認沒有持有期間與刷卡，兩者分別來自指派與匯入 |
| 新增、修改、刪除持有期間 | 這張卡 | 同一張卡的期間不可重疊，沒有約束擋得住 |

## 代價

- **開發者要判斷鎖**：遷移每個模組時都要列出跨列規則與範圍，判斷方式見 [行政寫入](../architecture/administrative-writes.md)。判斷錯會變成規則被繞過，所以規則要有並行測試。
- **漏提交只在執行期才擋得到**：回成功卻沒提交時 `AuditTrail` 拋例外（503），不會回成功卻沒存，但編譯期抓不到。
- **提交以外的衝突接不到**：body 內的 `ExecuteUpdate` 或中途 `SaveChanges` 撞上版本時，例外不經 `CommitAsync`，會變成 503。全面遷移前要決定做法，遷移期間這類寫入留在 `ExecuteAsync`。
- **權限撤銷類模組不能照搬**：帳號與存取控制要在同一交易內檢查 lockout，遷移時另行設計，目前留在 `ExecuteAsync`。
- **開始到提交的長度靠約定**：型別擋不住有人在中間放耗時工作，只能在 production 由 `admin_write.held_too_long` 發現。影響範圍限於宣告的鎖與一條連線，所以不為此改回 callback。
- **遷移期間兩套並存**：舊路徑的鎖與權限行為不變。全部遷移後移除 `ExecuteAsync` 與模組鎖。

## 何時重新評估

某條宣告的鎖常逾時回 `admin_write_busy`：先把耗時工作移出交易，再縮小範圍。
