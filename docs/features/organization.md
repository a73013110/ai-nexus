# 組織

結論：`/admin/organization`（授權 `organization`，預設給 administrators）維護部門樹與人員所屬。部門只決定「在組織中的位置」與主管關係，用來計算資料範圍；能使用哪些功能仍由角色決定（[ADR 0017](../decisions/0017-organization-units-and-data-scopes.md)）。部門與所屬由管理員維護，不從 AD 同步。

## 單位

- 樹狀，最多 8 層；同一個上層底下名稱不可重複（`organization_unit_name_duplicate`）。
- 新增時排到同層最後；用列的選單「上移／下移」調整同層順序。排序送出整層，期間有人改過這一層時拒絕（`organization_order_stale`），重新整理後再排。
- 編輯可換上層，整棵下層跟著搬、成員不變；不能移到自己或下層底下（`organization_unit_cycle`），搬過去超過 8 層也拒絕（`organization_depth_exceeded`）。
- **停用**：保留成員與歷史，但它與所有下層都不參與資料範圍，也不能再加入新成員（`organization_unit_disabled`）。不再使用的單位用停用，不刪除。
- **刪除**：必須沒有下層單位、也沒有成員（`organization_unit_not_empty`），不級聯。

## 成員

- 一人可屬多個單位，最多一個**主要所屬**；在某單位勾選主要所屬，會在同一次儲存取消此人在其他單位的主要所屬。
- **主管**：一個單位可有多位主管；只看直屬單位，不自動延伸到下層。
- 只能加入未刪除的帳號（`organization_user_not_found`）。帳號停用或刪除後所屬仍保留並標示「帳號已停用」，但不參與資料範圍，由管理員自行移出。
- 只記錄現況。「當時在哪個部門」由業務資料寫入時自己存單位 ID，組織不保留異動歷史。
- 使用者管理的檢視器顯示此人的所屬單位（唯讀，`identity.users` 或 `organization` 任一授權可讀）。

## 給其他模組

`OrganizationDirectory`（public）只回答現況：使用者所屬的有效單位、單位與所有下層、單位的有效成員或主管，以及可組合進業務查詢的成員子查詢（`MemberIds`）。有效單位＝自己與所有上層都啟用。它也實作 AccessControl 的 `IUnitMembership`，[資料範圍](../architecture/data-scopes.md)的 `unit`、`unit-tree` 由這裡計算。

## 稽核

`admin.organization_unit`、`admin.organization_unit_delete`、`admin.organization_order`、`admin.organization_member`、`admin.organization_member_delete`，在活動稽核以「組織異動」篩選；成員異動的快照是該使用者的全部所屬，看得出主要所屬從哪裡移走。測試身分只能讀取。
