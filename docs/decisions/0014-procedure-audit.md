# 0014 procedure 的稽核

- **狀態**：已由 [0023 稽核規範](0023-audit-standard.md) 取代（2026-10-27）
- **原決定**：procedure 自己寫稽核，與業務異動放在同一個交易。稽核情境（擁有者、操作者、TraceId、時間）由程式在呼叫時以 `sp_set_session_context` 帶入，procedure 透過 `audit.udf_AuditContext()` 與 `audit.usp_WriteAuditEvent` 寫入。

## 為什麼取代

- 稽核改成每個請求都由請求稽核寫一筆（0023）。如果 procedure 再自己寫，同一個操作會有兩筆，或必須逐支協調由誰來寫。
- session context 是第二套情境來源，要另外維護；功能、資料主體、來源 IP 這些新欄位也得在兩邊各實作一次。
- 原本「成功由 procedure 寫，失敗由程式在回滾後寫」的分工，在 0023 裡由 `AuditTrail` 統一處理：成功的紀錄與業務異動同一個交易，失敗的紀錄在回滾後補寫。

不採用觸發程序、SQL Server Audit 與 Temporal table 的理由不變，見 0023。
