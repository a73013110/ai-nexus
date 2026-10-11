---
paths:
  - "docs/**"
  - "**/*.md"
---

# 文件寫法

- 繁體中文，先寫結論，條列優先；一份文件只講一件事，不超過 8 KB（`scripts/tests/Docs.Tests.ps1` 檢查）。
- 依讀者放：`architecture`（改後端的人）、`development`、`operations`、`features`、`frontend`、`decisions`（長期有效的「為什麼」）、`research`。
- 檔名小寫 kebab-case；ADR 為 `docs/decisions/<四位編號>-kebab-case.md`。
- 不重抄程式碼已表達的內容（欄位清單、端點清單）；連到檔案或 `openapi.json`。相對連結與反引號內的 repo 路徑必須存在（同一個測試檢查）。
- 改行為時同一個 PR 更新對應文件；過時的段落直接刪除。