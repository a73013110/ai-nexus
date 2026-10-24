# 共用元件

結論：先用現有元件；同一種互動或同一段 template＋樣式第二次出現，就抽成共用元件，並在同一次修改換掉所有既有用法、刪掉舊樣式，再放進 `/admin/design`。不做全系統盤點，但改到的功能要順手帶到現行做法。

## 先找有沒有

- 索引是 `/admin/design` 的側欄（[`design-page.ts`](../../frontend/src/app/features/admin/design/design-page.ts) 的 `INDEX`）；原始碼在 `frontend/src/app/shared/ui/`。
- 依賴某個模組 API 的共用元件放在該模組，例如選人的 [`person-picker.ts`](../../frontend/src/app/features/work/collaboration/person-picker.ts)。

## 什麼時候抽

- 同一種選擇（人、部門、檔案、角色……）或同一段列樣式在第二個地方出現。
- 正要複製另一個功能的 template 或 CSS class（例如 `.xxx-results`、`.xxx-user`）。
- 抽的時候用全站搜尋找既有用法（class 名、placeholder、相同的 `@for` 結構），一次換完；e2e 的選取器跟著改。

## 放哪裡

- 不依賴 API、不含業務規則：`shared/ui/`（一元件一檔，樣式放旁邊的 `.scss`，在 4 kB 預算內）。
- 需要某模組的 API 或查詢狀態：放該模組資料夾並由呼叫端以 `@features` 別名引用；查詢狀態由呼叫端建立後傳入（例：`directorySearch` → `PersonPicker` 的 `search`），元件只負責呈現與鍵盤。
- 共用元件不接管驗證、草稿或寫入；選取結果以 output 交給呼叫端。

## 現有的選擇器

- 選人 `PersonPicker`、從名單挑人 `PersonBrowser`、平面清單 `Select`、階層（單位、分群）`TreeSelect`；用法見 [表單](forms.md)「選擇對象」。

## 完成的條件

- `/admin/design` 有範例：一個 `DesignSection`，涵蓋空、載入、停用、錯誤等狀態；資料用本機假資料與虛構姓名，不呼叫 API。
- 有 spec 涵蓋鍵盤與無障礙名稱（combobox、listbox、option 等角色）。
- 是表單控制項時，加進 `frontend/eslint.config.js` 的 `label-has-associated-control` 清單。
- [表單](forms.md) 或 [共用介面模式](ui-patterns.md) 補一句何時用它。
