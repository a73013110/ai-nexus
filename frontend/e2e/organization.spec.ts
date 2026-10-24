import { test, expect, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  expectViewportContained,
  routeOf,
  settleEntrance,
  toasts,
} from "./fixtures";
import type {
  UnitDto,
  UnitImportPreviewDto,
  UnitMemberDto,
} from "../src/app/core/api/schema";

/** 組織 API 的記憶體替身：只做頁面需要的形狀與兩個錯誤代碼，規則本身由後端整合測試涵蓋。 */
async function organization(page: Page, canImport = false) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = [
    { id: "organization" },
    ...(canImport ? [{ id: "organization.import" }] : []),
  ];
  await fixture.attach(page);
  const company = randomUUID(),
    it = randomUUID(),
    hr = randomUUID();
  const bob = { id: randomUUID(), account: "AD\\bob", displayName: "王小明" };
  const units: UnitDto[] = [
    {
      id: company,
      parentId: null,
      name: "總公司",
      enabled: true,
      sortOrder: 10,
      memberCount: 0,
      gdDepNo: null,
    },
    {
      id: it,
      parentId: company,
      name: "資訊處",
      enabled: true,
      sortOrder: 10,
      memberCount: 0,
      gdDepNo: null,
    },
    {
      id: hr,
      parentId: company,
      name: "人資處",
      enabled: false,
      sortOrder: 20,
      memberCount: 0,
      gdDepNo: null,
    },
  ];
  const members = new Map<string, UnitMemberDto[]>();
  const writes: { method: string; path: string; body: unknown }[] = [];
  const count = () => {
    for (const unit of units)
      unit.memberCount = members.get(unit.id)?.length ?? 0;
  };
  await page.route(
    "**/api/v1/admin/organization-units**",
    async (route: Route) => {
      const request = route.request();
      const path = new URL(request.url()).pathname.replace(
        "/api/v1/admin/organization-units",
        "",
      );
      const method = request.method();
      const body = request.postDataJSON() as Record<string, unknown> | null;
      if (method !== "GET") writes.push({ method, path, body });
      if (method === "GET" && path === "")
        return route.fulfill({ json: units });
      if (path === "/import" && method === "GET")
        return route.fulfill({ json: importPreview(company, it) });
      if (method === "GET" && path === "/candidates")
        return route.fulfill({
          json: [
            {
              id: bob.id,
              account: bob.account,
              displayName: bob.displayName,
              primaryUnitId: null,
            },
          ],
        });
      if (path === "/import" && method === "POST") {
        // 套用：總公司綁定公文系統的部門 1（取消勾選時不動），資訊處改名。
        const excluded = (body!["excluded"] as number[]) ?? [];
        if (!excluded.includes(1)) units[0].gdDepNo = 1;
        units[1].gdDepNo = 2;
        units[1].name = "資訊部";
        return route.fulfill({
          json: {
            created: 0,
            adopted: excluded.includes(1) ? 0 : 1,
            updated: 1,
            disabled: 0,
            managersAdded: 1,
            managersRemoved: 0,
            issues: 1,
          },
        });
      }
      if (method === "POST") {
        const unit: UnitDto = {
          id: randomUUID(),
          parentId: (body!["parentId"] as string | null) ?? null,
          name: body!["name"] as string,
          enabled: body!["enabled"] as boolean,
          sortOrder: 100,
          memberCount: 0,
          gdDepNo: null,
        };
        units.push(unit);
        return route.fulfill({ json: unit });
      }
      if (method === "PUT" && path === "/order") {
        (body!["unitIds"] as string[]).forEach((id, index) => {
          units.find((unit) => unit.id === id)!.sortOrder = (index + 1) * 10;
        });
        return route.fulfill({ status: 204 });
      }
      const [, id, , userId] = path.split("/");
      if (method === "GET")
        return route.fulfill({ json: members.get(id) ?? [] });
      if (method === "DELETE" && !userId) {
        if (
          units.some((unit) => unit.parentId === id) ||
          members.get(id)?.length
        )
          return route.fulfill({
            status: 409,
            json: { title: "conflict", code: "organization_unit_not_empty" },
          });
        units.splice(
          units.findIndex((unit) => unit.id === id),
          1,
        );
        return route.fulfill({ status: 204 });
      }
      if (method === "PUT" && userId) {
        const list = members.get(id) ?? [];
        const existing = list.find((item) => item.userId === userId);
        const next = {
          userId,
          account: bob.account,
          displayName: bob.displayName,
          active: true,
          isPrimary: body!["isPrimary"] as boolean,
          isManager: body!["isManager"] as boolean,
        };
        members.set(
          id,
          existing
            ? list.map((item) => (item === existing ? next : item))
            : [...list, next],
        );
        count();
        return route.fulfill({ status: 204 });
      }
      if (method === "DELETE" && userId) {
        members.set(
          id,
          (members.get(id) ?? []).filter((item) => item.userId !== userId),
        );
        count();
        return route.fulfill({ status: 204 });
      }
      return route.fulfill({
        status: 404,
        json: { title: "not found", code: "not_found" },
      });
    },
  );
  return { units, members, writes, ids: { company, it, hr }, bob };
}

const tree = (page: Page) => page.locator(".unit-list .unit-name > span");

function importPreview(company: string, it: string): UnitImportPreviewDto {
  return {
    fingerprint: "a".repeat(64),
    sourceUnits: 3,
    units: [
      {
        gdDepNo: 1,
        unitId: company,
        action: "Adopt",
        path: ["總公司"],
        previousPath: null,
        enabled: true,
        fields: [],
      },
      {
        gdDepNo: 2,
        unitId: it,
        action: "Update",
        path: ["總公司", "資訊部"],
        previousPath: ["總公司", "資訊處"],
        enabled: true,
        fields: ["Name"],
      },
    ],
    managers: [
      {
        gdDepNo: 2,
        unitId: it,
        path: ["總公司", "資訊部"],
        userId: randomUUID(),
        account: "AD\\carol",
        displayName: "陳小華",
        added: true,
        deputy: true,
        joinsUnit: true,
      },
    ],
    issues: [{ gdDepNo: 9, name: null, code: "NameMissing", gdUserId: null }],
  };
}

test("組織頁以樹狀列出單位，可新增下層、排序並維護成員", async ({ page }) => {
  const state = await organization(page);
  await page.goto(routeOf("organization"));
  await expect(
    page.getByRole("heading", { name: "組織單位", level: 1 }),
  ).toBeVisible();
  await expect(tree(page)).toHaveText(["總公司", "資訊處", "人資處"]);
  await expect(page.locator(".unit-row.is-inactive")).toHaveCount(1);

  // 下移「資訊處」：送出整層的新順序。
  await page
    .getByRole("button", { name: "單位操作：資訊處", exact: true })
    .click();
  await page.getByRole("menuitem", { name: "下移" }).click();
  await expect(tree(page)).toHaveText(["總公司", "人資處", "資訊處"]);
  expect(state.writes.at(-1)).toEqual({
    method: "PUT",
    path: "/order",
    body: {
      parentId: state.ids.company,
      unitIds: [state.ids.hr, state.ids.it],
    },
  });

  // 新增下層：上層預先選好，建立後選取新單位。
  await page
    .getByRole("button", { name: "總公司 › 資訊處", exact: true })
    .click();
  await page.getByRole("button", { name: "新增下層", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "新增下層單位：資訊處" });
  await dialog.getByRole("textbox", { name: "名稱" }).fill("系統部");
  await dialog.getByRole("button", { name: "建立單位" }).click();
  await expect(dialog).toBeHidden();
  await expect(
    toasts(page).filter({ hasText: "已新增「系統部」" }),
  ).toBeVisible();
  expect(state.writes.at(-1)?.body).toEqual({
    parentId: state.ids.it,
    name: "系統部",
    enabled: true,
  });
  await expect(tree(page)).toHaveText(["總公司", "人資處", "資訊處", "系統部"]);

  // 建立後選取的是新單位；回到資訊處加入成員並設為主要所屬與主管。
  await expect(
    page.getByRole("heading", { name: "系統部", level: 2 }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "總公司 › 資訊處", exact: true })
    .click();
  await page.getByRole("button", { name: "加入成員", exact: true }).click();
  const members = page.getByRole("dialog", { name: "加入成員" });
  await members.getByRole("checkbox", { name: /王小明/ }).check();
  await members.getByRole("button", { name: "加入 1 人" }).click();
  await expect(members).toBeHidden();
  await expect(
    toasts(page).filter({ hasText: "已將 1 人加入「資訊處」" }),
  ).toBeVisible();
  const row = page.locator(".person-row").filter({ hasText: "王小明" });
  await row.getByRole("checkbox", { name: "主要所屬" }).check();
  await expect(row.getByRole("checkbox", { name: "主要所屬" })).toBeChecked();
  await row.getByRole("checkbox", { name: "主管" }).check();
  await expect
    .poll(() => state.members.get(state.ids.it)?.[0])
    .toMatchObject({
      isPrimary: true,
      isManager: true,
    });

  // 仍有成員的單位不能刪除：顯示伺服器的錯誤提示。
  await page
    .getByRole("button", { name: "單位操作：資訊處", exact: true })
    .click();
  await page.getByRole("menuitem", { name: "刪除" }).click();
  await page
    .getByRole("dialog", { name: "刪除「資訊處」？" })
    .getByRole("button", { name: "刪除" })
    .click();
  await expect(page.getByText(/單位仍有下層單位或成員/)).toBeVisible();

  // 移出成員需確認。
  await row.getByRole("button", { name: "將 王小明 移出單位" }).click();
  await page
    .getByRole("dialog", { name: /移出「資訊處」/ })
    .getByRole("button", { name: "移出" })
    .click();
  await expect(page.locator(".person-row")).toHaveCount(0);
});

test("停用的單位不能加入成員，編輯時上層不列出自己與下層", async ({ page }) => {
  await organization(page);
  await page.goto(routeOf("organization"));
  await page
    .getByRole("button", { name: "總公司 › 人資處", exact: true })
    .click();
  await expect(page.getByText(/單位已停用/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "加入成員", exact: true }),
  ).toHaveCount(0);

  await page.getByRole("button", { name: "總公司", exact: true }).click();
  await page.getByRole("button", { name: "編輯", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "編輯單位：總公司" });
  await dialog.getByRole("combobox", { name: "上層單位" }).click();
  await expect(
    page.getByRole("tree", { name: "上層單位" }).getByRole("treeitem"),
  ).toHaveText(["最上層"]);
  await page.keyboard.press("Escape");
  await dialog.getByRole("button", { name: "關閉單位編輯" }).click();
  await expect(dialog).toBeHidden();
});

test("手機版單位樹與成員改為上下排列，不產生橫向捲動", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await organization(page);
  await page.goto(routeOf("organization"));
  await settleEntrance(page);
  await page
    .getByRole("button", { name: "總公司 › 資訊處", exact: true })
    .click();
  await expectViewportContained(page);
});

test("從公文系統匯入先預覽變動與問題，只套用勾選的部門", async ({ page }) => {
  const state = await organization(page, true);
  await page.goto(routeOf("organization"));
  await page
    .getByRole("button", { name: "從公文系統匯入", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "從公文系統匯入" });
  // 一個部門一個勾選項，單位與主管的變動寫在同一列說明。
  const departments = dialog.getByRole("group", { name: "要套用的部門" });
  await expect(
    departments.getByText(
      "名稱依公文系統更新；原為 總公司 › 資訊處；陳小華 設為主管（副主管）並加入為成員",
    ),
  ).toBeVisible();
  await expect(dialog.getByText("名稱空白，略過")).toBeVisible();
  // 預覽本身不寫入；取消勾選的部門以 excluded 送出，與指紋一起。
  expect(state.writes).toEqual([]);
  await departments
    .getByRole("checkbox", { name: "總公司", exact: true })
    .uncheck();
  await dialog.getByRole("button", { name: "套用 1 個部門" }).click();
  await expect(dialog).toBeHidden();
  expect(state.writes.at(-1)).toEqual({
    method: "POST",
    path: "/import",
    body: { fingerprint: "a".repeat(64), excluded: [1] },
  });
  await expect(
    toasts(page).filter({ hasText: "已從公文系統匯入：單位更新 1 個" }),
  ).toBeVisible();
  await expect(tree(page)).toHaveText(["總公司", "資訊部", "人資處"]);
  await expect(
    page.locator(".unit-row").filter({ hasText: "公文" }),
  ).toHaveCount(1);

  await page
    .getByRole("button", { name: "總公司 › 資訊部", exact: true })
    .click();
  await expect(page.getByText(/來自公文系統（部門編號 2）/)).toBeVisible();
});

test("沒有匯入權限時不顯示匯入按鈕", async ({ page }) => {
  await organization(page);
  await page.goto(routeOf("organization"));
  await expect(
    page.getByRole("heading", { name: "組織單位", level: 1 }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "從公文系統匯入" }),
  ).toHaveCount(0);
});
