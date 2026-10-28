# 組織

結論：`/admin/organization`（授權 `organization`，預設給 administrators）維護部門樹與人員所屬。部門只決定「在組織中的位置」與主管關係，用來計算資料範圍；能使用哪些功能仍由角色決定（[ADR 0017](../decisions/0017-organization-units-and-data-scopes.md)）。部門可手建，也可[從公文系統匯入](#從公文系統匯入)部門與主管；匯入的單位以公文系統為準，手建的單位與一般成員由管理員維護。不從 AD 同步。

頁首、單位編輯與公文系統匯入對話框右上角的問號可開啟操作導覽。

## 單位

- 樹狀，最多 8 層；同一個上層底下名稱不可重複（`organization_unit_name_duplicate`）。
- 新增時排到同層最後；用列的選單「上移／下移」調整同層順序。排序送出整層，期間有人改過這一層時拒絕（`organization_order_stale`），重新整理後再排。
- 編輯可換上層，整棵下層跟著搬、成員不變；不能移到自己或下層底下（`organization_unit_cycle`），搬過去超過 8 層也拒絕（`organization_depth_exceeded`）。
- **停用**：保留成員與歷史，但它與所有下層都不參與資料範圍，也不能再加入新成員（`organization_unit_disabled`）。不再使用的單位用停用，不刪除。
- **刪除**：必須沒有下層單位、也沒有成員（`organization_unit_not_empty`），不級聯。

## 首長單位

公司底下與各處同層的董事會、總經理室等，在單位編輯標為**首長單位**並給層級（1～9，數字越小越高），樹上標示「首長 N」。理由見 [ADR 0024](../decisions/0024-head-units.md)。

- 首長單位的主管管轄上層，以及上層底下層級不比它高的子單位與其下層：董事會 1、總經理室 2 時，董事長管全公司，總經理管總經理室與各處，不管董事會與稽核部。只影響主管範圍，成員範圍照單位樹。
- 上層的主管改由首長單位擔任：設定時取消上層的主管勾選（成員資格保留，稽核快照的 `parentManagerIds` 看得出取消了誰），之後也不能再勾選。
- 最上層不能設、層級 1～9（`organization_head_rank_invalid`）；同一個上層的層級不可重複（`organization_head_rank_duplicate`）；首長單位底下不能再有首長單位（`organization_head_unit_nested`）。
- 首長單位停用或沒有主管時不管轄，上層也不改回成員勾選。

## 成員

- 一人可屬多個單位，最多一個**主要所屬**；在某單位勾選主要所屬，會在同一次儲存取消此人在其他單位的主要所屬。
- **主管**：一個單位可有多位主管；主管關係只記在直屬單位，不自動成為下層的主管。這個勾選也是[主管範圍](../architecture/data-scopes.md#主管範圍)認定主管的唯一來源，取消勾選即失效；主管能另外看多少由各群組的授權決定。有[首長單位](#首長單位)的單位不能勾選主管（`organization_manager_by_head_unit`）。
- 「加入成員」開啟全部在職使用者的名單（`GET …/candidates`），依主要所屬分組、尚未分派部門的人在最前，可篩選並勾選多人一次加入；逐人寫入，每人一筆稽核。加入時不設主要所屬與主管，再於成員列勾選。
- 只能加入未刪除的帳號（`organization_user_not_found`）。帳號停用或刪除後所屬仍保留並標示「帳號已停用」，但不參與資料範圍，由管理員自行移出。
- 只記錄現況。「當時在哪個部門」由業務資料寫入時自己存單位 ID，組織不保留異動歷史。
- 使用者管理的檢視器顯示此人的所屬單位（唯讀，`identity.users` 或 `organization` 任一授權可讀）。

## 從公文系統匯入

頁首「從公文系統匯入」（動作 `organization.import`，預設給 administrators）讀 GD 的 `Sys_User_DEPT_BAS`，先預覽、確認後才套用。規則在 [`UnitImportPlan`](../../backend/src/AiNexus.Features/Admin/Organization/Imports/UnitImportPlan.cs)，連線與 GD 帳號權限見 [出勤與公文系統 GD](attendance-gd.md)。

- **對應**：單位以 `GdDepNo`（GD 的 `DEP_NO`）對應，重複匯入是更新不是新增。還沒綁定的部門若在同一個上層有同名、未綁定的手建單位，綁定它而不新增（同層本來就不能同名）。
- **以 GD 為準**：已綁定單位的名稱（去空白）、上層、啟用（`Disable`）與同層順序每次覆蓋。本地仍可編輯，但會被下一次匯入改回，所以樹上標示「公文」、編輯時提示。順序依 `OrderBy`、沒有值的排後、再依 `DEP_NO`，重新編號。
- **上層**：`PARENT_NO` 為 0、null 或不存在的編號是最上層。
- **GD 已沒有的部門**：停用，不刪除（成員與歷史引用保留）；重新出現時再啟用。手建單位不受影響。
- **略過、不失敗**：名稱空白或不合格式、同層同名（已有的單位優先，都是新的就留 `DEP_NO` 小的）、成環、超過八層（含掛在底下的手建單位）的列略過並列為問題；已綁定的維持原樣，新的不建立，無處可掛的下層一併略過。GD 一個部門都沒有時拒絕（`organization_import_empty`），避免把全部停用。
- **首長單位**：本地設定，匯入不覆寫；被匯入搬到別的上層時清除層級。有首長單位的上層不同步主管，GD 有設主管時列為問題。
- **主管**：`DEP_CHIEF` 與 `DEP_ACHIEF`（「、」或全形、半形逗號分隔）都設為主管，不分正副：兩者對資料範圍的意義相同（[主管範圍](../architecture/data-scopes.md#主管範圍)），預覽仍標示副主管。公文帳號經 `Sys_User.ACCOUNT` 對到未刪除使用者的 AD 帳號；公文帳號停用、對不到或對到多人的列為問題。不是成員的主管加入為成員（不設主要所屬）；GD 不再列為主管的人只取消主管，保留成員資格。停用的單位不同步主管。
- **勾選要套用的部門**：預覽以部門為單位列出（單位與主管的變動合在一起），未勾選的部門這次不動：已綁定的維持原樣（含 GD 已沒有的不停用）、新的不建立（其下層無處可掛也略過）、主管不同步，下次預覽仍會列出。本地已停用、套用會重新啟用的部門預設不勾，避免刻意停用的單位被打開。
- **預覽與套用一致**：預覽附內容的指紋。套用時重讀 GD、在行政交易內重讀組織並重算完整預覽，指紋不同（期間 GD 或組織有人改過）回 409 `organization_import_stale`，請重新預覽，不會套用沒看過的變更。
- GD 未設定、連不上或逾時、查詢被拒分別回 503 `organization_import_not_configured`、`organization_import_unavailable`、`organization_import_rejected`。
- 目前只讀用得到的欄位；登記桌 Email（`DEP_EMAIL`）、`UnitClass`、`Attach` 等日後需要時再加。

## 給其他模組

`OrganizationDirectory`（public）只回答現況：使用者所屬的有效單位、單位與所有下層，以及可組合進業務查詢的成員子查詢（`MemberIds`）。有效單位＝自己與所有上層都啟用。它也實作 AccessControl 的 `IUnitMembership`，[資料範圍](../architecture/data-scopes.md)的 `unit`、`unit-tree`、`managed`、`managed-tree` 由這裡計算（含首長單位的管轄）。

## 稽核

`organization.unit.created`／`.updated`／`.deleted`／`.reordered`／`.imported`、`organization.unit_member.updated`／`.deleted`，活動稽核的動作篩選選「組織（全部）」；成員異動的快照是該使用者的全部所屬，看得出主要所屬從哪裡移走。匯入只記數量（前後的單位、已匯入單位與主管數，排除的部門數與各類變動數），完整清單在預覽。
