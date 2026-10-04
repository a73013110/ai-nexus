import { test, expect, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { ApiFixture, settleEntrance } from "./fixtures";
import type { AdminCatalog, AuditEntry } from "../../frontend/src/app/core/api/types";

async function administration(page: Page) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  await fixture.attach(page);
  const bob = {
    id: randomUUID(),
    account: "AD\\bob",
    displayName: "王小明",
    lastSeenAt: "2026-10-04T00:00:00Z",
    roleIds: ["member"],
  };
  const actor = {
    id: fixture.userId,
    account: "AD\\admin",
    displayName: "測試使用者",
    lastSeenAt: "2026-10-04T00:00:00Z",
    roleIds: ["member", "administrator"],
  };
  const catalog: AdminCatalog = {
    roles: [
      {
        id: "member",
        name: "一般使用者",
        enabled: true,
        groupIds: ["workspace"],
        userCount: 2,
      },
      {
        id: "administrator",
        name: "平台管理員",
        enabled: true,
        groupIds: ["administrators"],
        userCount: 1,
      },
    ],
    groups: [
      {
        id: "workspace",
        name: "基本工作台",
        enabled: true,
        featureIds: ["chat"],
        policy: null,
      },
      {
        id: "administrators",
        name: "平台管理",
        enabled: true,
        featureIds: ["admin"],
        policy: null,
      },
    ],
    features: [
      {
        id: "chat",
        name: "AI 對話",
        route: "/chat",
        sortOrder: 10,
        enabled: true,
      },
      {
        id: "admin",
        name: "管理",
        route: "/admin",
        sortOrder: 90,
        enabled: true,
      },
    ],
    models: [
      {
        id: "fixture:8b",
        displayName: "測試模型",
        contextTokens: 8192,
        maxOutputTokens: 2048,
        reasoningEfforts: [],
        defaultReasoningEffort: "auto",
        supportsImages: true,
        supportsStreaming: true,
        supportsUsage: true,
      },
    ],
  };
  const audit: AuditEntry[] = [];
  await page.route("**/api/v1/admin/**", async (route: Route) => {
    const url = new URL(route.request().url()),
      path = url.pathname.replace("/api/v1/admin", ""),
      method = route.request().method();
    const json = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    if (path === "/catalog") return json(catalog);
    if (path === "/users") {
      const search = url.searchParams.get("search") || "";
      const users = [actor, bob].filter((x) =>
        (x.displayName + x.account).includes(search),
      );
      return json({ users, total: users.length, offset: 0 });
    }
    if (path.endsWith("/access"))
      return json({
        roles: [],
        groups: [],
        features: [{ id: "chat", name: "AI 對話", route: "/chat" }],
      });
    if (path === "/audit") return json(audit);
    if (path === "/usage")
      return json({
        users: 2,
        requests: 4,
        completed: 4,
        inputTokens: 1200,
        outputTokens: 600,
        requestsWithUsage: 4,
      });
    if (method === "PUT") {
      const body = route.request().postDataJSON();
      if (
        path === `/users/${actor.id}/roles` &&
        !body.roleIds.includes("administrator")
      )
        return json(
          {
            title: "此變更會撤銷你的管理權限。請先由另一位管理員處理。",
            code: "admin_lockout",
          },
          409,
        );
      if (path === `/users/${bob.id}/roles`) bob.roleIds = body.roleIds;
      if (path.startsWith("/groups/")) {
        const id = path.split("/").at(-1)!;
        const old = catalog.groups.find((x) => x.id === id);
        if (old) Object.assign(old, body);
        else catalog.groups.push({ id, ...body });
      }
      if (path.startsWith("/roles/")) {
        const id = path.split("/").at(-1)!;
        const old = catalog.roles.find((x) => x.id === id);
        if (old) Object.assign(old, body);
        else catalog.roles.push({ id, ...body, userCount: 0 });
      }
      audit.unshift({
        id: audit.length + 1,
        actor: "AD\\admin",
        action: path.includes("/groups/") ? "admin.group" : "admin.user_roles",
        resourceId: bob.id,
        result: "saved",
        at: "2026-10-04T00:00:00Z",
        detailsJson: JSON.stringify(body),
      });
      return route.fulfill({ status: 204 });
    }
    return json({}, 404);
  });
  await page.goto("/admin");
  await expect(
    page.getByRole("heading", { name: "平台管理", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("王小明", { exact: true })).toBeVisible();
  return { fixture, catalog, bob, audit };
}
test("administrators edit roles with effective access preview and an audit trail", async ({
  page,
}) => {
  const state = await administration(page);
  await page.getByRole("button", { name: "設定角色：王小明" }).click();
  const dialog = page.getByRole("dialog");
  await dialog
    .getByRole("checkbox", { name: "平台管理員", exact: true })
    .check();
  await expect(dialog.locator(".resource-badges")).toContainText("管理");
  await dialog.getByRole("button", { name: "儲存授權" }).click();
  await expect(dialog).not.toBeVisible();
  expect(state.bob.roleIds).toContain("administrator");
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
  await expect(page.getByText("調整使用者角色", { exact: true })).toBeVisible();
  await page.getByText("查看異動", { exact: true }).click();
  await expect(page.locator(".audit-details pre")).toContainText(
    "administrator",
  );
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-audit.png",
    fullPage: true,
  });
});
test("group model limits and self-lockout errors work on desktop and mobile", async ({
  page,
}) => {
  const state = await administration(page);
  await page
    .getByRole("button", { name: "功能群組與模型", exact: true })
    .click();
  await page.getByRole("button", { name: "新增群組" }).click();
  const dialog = page.getByRole("dialog");
  await dialog
    .getByRole("textbox", { name: "識別碼", exact: true })
    .fill("research");
  await dialog
    .getByRole("textbox", { name: "名稱", exact: true })
    .fill("研發工作台");
  await dialog.getByRole("checkbox", { name: "AI 對話", exact: true }).check();
  await dialog.getByRole("checkbox", { name: "限制可用模型" }).check();
  await dialog.getByRole("checkbox", { name: "測試模型", exact: true }).check();
  await dialog.getByRole("spinbutton", { name: "每日生成次數上限" }).fill("50");
  await dialog.getByRole("button", { name: "儲存授權" }).click();
  await expect(dialog).not.toBeVisible();
  expect(
    state.catalog.groups?.find((x) => x.id === "research")?.policy
      ?.dailyRequestLimit,
  ).toBe(50);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-groups.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "使用者", exact: true }).click();
  await page.getByRole("button", { name: "設定角色：測試使用者" }).click();
  await dialog
    .getByRole("checkbox", { name: "平台管理員", exact: true })
    .uncheck();
  await dialog.getByRole("button", { name: "儲存授權" }).click();
  await expect(dialog.getByRole("alert")).toContainText("撤銷你的管理權限");
  await dialog.getByRole("button", { name: "取消", exact: true }).click();
  await page.setViewportSize({ width: 375, height: 812 });
  await page.getByRole("searchbox", { name: "搜尋使用者" }).fill("王小明");
  await expect(
    page.getByRole("button", { name: "設定角色：王小明" }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-mobile.png",
    fullPage: true,
  });
});
test("members have no management navigation and direct routes show an access explanation", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/admin");
  await expect(page.getByRole("alert")).toContainText("沒有平台管理權限");
  await expect(
    page
      .getByRole("navigation", { name: "工作台功能" })
      .getByRole("link", { name: "管理", exact: true }),
  ).toHaveCount(0);
});
