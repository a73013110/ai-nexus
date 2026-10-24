---
paths:
  - "frontend/src/app/**"
  - "frontend/src/styles/**"
---

# 前端畫面一致性

改到哪個頁面、對話框或元件，就把它帶到現行規範；不做全系統盤點，但碰到的那一處要改完，並在回覆裡說明順手調整了什麼。

## 表單與對話框（[表單](../../docs/frontend/forms.md)）

動到含表單的檔案時，對照下列項目；不符合就在同一次修改調整：

- 兩組以上欄位用 `.form-sections` 分段（段落標題＋最多一句說明），不是一串 `<label>` 直接疊在 `.dialog-body`。
- 欄位間距只靠共用 gap：元件裡不寫欄位 margin，也不留沒內容時仍存在的空容器。
- 成對欄位用 `.form-row`；選填寫「（選填）」；placeholder 不重複標籤。
- 選一個對象（人、部門）選好後換成 `.form-choice`＋「更換」；多個用 `.form-chips`。
- 範本：`frontend/src/app/features/office/attendance/cards/assignment-dialog.ts`。

## 共用元件（[共用元件](../../docs/frontend/shared-components.md)）

- 寫 UI 前先看 `/admin/design` 的索引（`frontend/src/app/features/admin/design/design-page.ts`）與 `frontend/src/app/shared/ui/`，有就用。
- 正要寫的選擇器（選人、選部門、選檔案…）或列樣式在別處已有類似的：抽成共用元件，全站搜尋既有用法一次換完，刪掉舊樣式，e2e 選取器跟著改。
- 新共用元件同一次要有：`/admin/design` 範例（本機假資料、虛構姓名）、spec、文件一句用法；表單控制項加進 `frontend/eslint.config.js` 的 label 控制項清單。
