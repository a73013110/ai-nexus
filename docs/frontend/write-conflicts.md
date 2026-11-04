# 版本衝突

結論：管理端的修改與刪除帶讀到的版本（[行政寫入](../architecture/administrative-writes.md)），期間有人改過時回 409 `admin_write_conflict`；前端看 `action()` 回傳的 `stale`（底層是 `@core/errors/safe-errors` 的 `isStaleWrite`），說明一律用 `STALE_WRITE_MESSAGE`。

- **寫入與寫入後的重讀期間，停用會帶版本的動作**：重讀回來前手上的版本還是舊的，再送只會衝突自己。例如 [`OrganizationStore`](../../frontend/src/app/features/admin/organization/organization-store.ts) 的 `busy`。排序不帶版本，可以繼續。
- **對話框衝突時關閉**：草稿的版本已過期，再送也一樣被拒。對話框的動作 `reset()` 後關閉，由 store 的 `stale()` 在頁內 Notice 說明並重讀，不用 toast，也不留在對話框裡讓人再按一次。例如 [`UnitDialog`](../../frontend/src/app/features/admin/organization/unit-dialog.ts)。
- **分頁表單衝突時留在原地**：錯誤放分頁並重讀，內容沒變的草稿留著，換上新版本就能再送。例如 [`AdminUserStore`](../../frontend/src/app/features/admin/identity/users/admin-user-store.ts)。
- **同一次儲存送兩個端點**：後送的帶前一個加一後的版本，成功的帳號寫入一定加一，不必為此多讀一次；中途失敗時重讀拿到實際的版本。
