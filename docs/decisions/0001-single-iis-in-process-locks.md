# 0001 單一 IIS 執行個體與程序內鎖

- **狀態**：採用（2026-10）
- **決定**：正式環境只跑一個 IIS 執行個體、一個 worker（不開 web garden、不重疊回收）。同一程序內的互斥用 `KeyedAsyncLock`（`AiNexus.Platform/Threading`）依對話或資源分鍵；每人一個進行中的生成與配額靠 `Users` 列鎖在交易內保證。

## 原因

- 使用者是單一學校，一台主機的容量足夠；多台需要的分散式鎖、共用佇列與共用 key ring 會讓維運成本倍增。
- 程序內鎖依鍵分開，不同對話或資源互不等待，沒有全域瓶頸。
- 需要跨程序正確性的地方已經用 SQL 處理：配額與附件容量用 row lock，生成用 executor 租約，背景工作用 fenced lease，所以本機與 IIS 共用資料庫時不會互相中止生成。

## 代價

- 聊天排程、模型容量（`MaxConcurrency`）與 Gitea 匯入的去重只在程序內有效，沒有跨主機的 GPU 限制或全域持久佇列。
- 回收或重啟會中斷正在生成的回答（租約到期後保留部分輸出，可重新生成）。

## 何時重新評估

要同時跑多台伺服器時：程序內鎖改為資料庫併發控制（rowversion 或條件更新），匯入改成資料庫層的預約與唯一約束，監控的 presence 改成分散式 adapter，Data Protection key ring 改為共用。見 [生成與背景任務](../architecture/GENERATION.md)。
