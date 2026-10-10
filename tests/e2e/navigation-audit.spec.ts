import { test, expect, type Page } from "@playwright/test";
import { ApiFixture, expectViewportContained } from "./fixtures";

async function navigationWorkspace(page: Page) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.auditAccess = true;
  fixture.extraFeatures = [
    { id: "dashboard", name: "總覽", route: "/dashboard" },
    { id: "projects", name: "專案", route: "/projects" },
    { id: "knowledge", name: "知識庫", route: "/knowledge" },
    { id: "artifacts", name: "成果文件", route: "/artifacts" },
    { id: "shared", name: "分享", route: "/shared" },
    { id: "quality", name: "品質評測", route: "/quality" },
    { id: "repositories", name: "程式庫", route: "/repositories" },
    { id: "tasks", name: "背景任務", route: "/tasks" },
    { id: "integrations", name: "資料來源", route: "/integrations" },
    { id: "monitoring", name: "即時監控", route: "/admin/monitoring" },
    { id: "logs.query", name: "系統日誌", route: "/admin/logs" },
  ];
  await fixture.attach(page);
  await page.goto("/files");
  await expect(
    page.getByRole("heading", { name: "檔案庫", exact: true }),
  ).toBeVisible();
  return fixture;
}

test("compact navigation keeps its width and exposes every scroll boundary and focused destination", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 560 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await navigationWorkspace(page);
  await page.getByRole("button", { name: "收合側欄", exact: true }).click();
  const sidebar = page.locator(".workspace-sidebar");
  const viewport = sidebar.getByRole("region", {
    name: "工作區功能",
    exact: true,
  });
  const area = sidebar.locator("nx-scroll-area");
  const up = area.getByRole("button", {
    name: "向上捲動工作區功能",
    exact: true,
  });
  const down = area.getByRole("button", {
    name: "向下捲動工作區功能",
    exact: true,
  });
  await expect(sidebar).toHaveCSS("width", "52px");
  await expect(viewport).toHaveCSS("scrollbar-width", "none");
  await expect(up).not.toBeVisible();
  await expect(down).toBeVisible();
  const center = (await sidebar
    .getByRole("link", { name: "總覽", exact: true })
    .boundingBox())!;
  expect(Math.abs(center.x + center.width / 2 - 26)).toBeLessThan(1);
  const contentWidth = await viewport.evaluate((el) => el.clientWidth);
  await down.click();
  await expect(up).toBeVisible();
  await expect(down).toBeVisible();
  await viewport.focus();
  await page.keyboard.press("End");
  await expect(down).not.toBeVisible();
  await expect(up).toBeVisible();
  expect(await viewport.evaluate((el) => el.clientWidth)).toBe(contentWidth);
  await page.screenshot({
    path: "artifacts/screenshots/navigation-rail-bottom.png",
    animations: "disabled",
  });
  await page.keyboard.press("Home");
  await expect(up).not.toBeVisible();
  const audit = sidebar.getByRole("link", { name: "活動稽核", exact: true });
  await audit.focus();
  const bounds = (await viewport.boundingBox())!;
  const focused = (await audit.boundingBox())!;
  expect(focused.y).toBeGreaterThanOrEqual(bounds.y);
  expect(focused.y + focused.height).toBeLessThanOrEqual(
    bounds.y + bounds.height + 1,
  );
  await expect(audit).toBeFocused();
  await viewport.hover();
  await page.mouse.wheel(0, -1000);
  await expect(up).not.toBeVisible();
  await expect(audit).toBeFocused();
  await page.setViewportSize({ width: 1280, height: 1600 });
  await expect(up).not.toBeVisible();
  await expect(down).not.toBeVisible();
  await expect(viewport).toHaveAttribute("tabindex", "-1");
  expect(await viewport.evaluate((el) => el.clientWidth)).toBe(contentWidth);
  await expectViewportContained(page);
  await viewport.focus();
  await page.setViewportSize({ width: 1280, height: 360 });
  await expect(down).toBeVisible();
  await page.evaluate(
    () => (document.documentElement.dataset["theme"] = "dark"),
  );
  await page.screenshot({
    path: "artifacts/screenshots/navigation-rail-dark.png",
    animations: "disabled",
  });
  await expectViewportContained(page);
  await page.emulateMedia({ forcedColors: "active" });
  await expect(viewport).toHaveCSS("scrollbar-width", "auto");
});

test("short mobile drawers expose scrolling and preserve the focus escape route", async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 380 });
  await navigationWorkspace(page);
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  const sidebar = page.locator(".workspace-sidebar");
  await expect(sidebar).toHaveAttribute("role", "dialog");
  await expect(
    sidebar.getByRole("button", { name: "向下捲動工作區功能", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: "artifacts/screenshots/navigation-drawer-short.png",
    animations: "disabled",
  });
  await page.keyboard.press("Escape");
  await expect(sidebar).toHaveAttribute("aria-hidden", "true");
  await expect(
    page.getByRole("button", { name: "展開側欄", exact: true }),
  ).toBeFocused();
  await expectViewportContained(page);
});

test("audit-only access keeps deep-link filters and same-page links without requesting account administration", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.auditAccess = true;
  await fixture.attach(page);
  const queries: URL[] = [];
  const managementRequests: string[] = [];
  const traceId = "a".repeat(32);
  await page.route("**/api/v1/admin/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/v1/admin/audit/catalog")
      return route.fulfill({
        json: {
          features: [{ id: "audit", name: "活動稽核", route: "/admin/audit" }],
          models: [],
        },
      });
    if (url.pathname === "/api/v1/admin/audit") {
      queries.push(url);
      return route.fulfill({
        json: [
          {
            id: 1,
            actor: "reviewer",
            action: "identity.login",
            category: "authentication",
            result: "success",
            at: "2026-10-08T00:00:00Z",
            resourceId: null,
            detailsJson: null,
            traceId,
          },
        ],
      });
    }
    managementRequests.push(url.pathname);
    return route.fulfill({ status: 403, json: { code: "feature_forbidden" } });
  });
  await page.goto(
    `/admin/audit?category=authentication&search=reviewer&traceId=${traceId}#events`,
  );
  await expect(page).toHaveURL(
    new RegExp(
      `/admin/audit\\?category=authentication&search=reviewer&traceId=${traceId}#events$`,
    ),
  );
  await expect(
    page.getByRole("heading", { name: "活動稽核", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".audit-row")).toHaveCount(1);
  expect(queries[0].searchParams.get("traceId")).toBe(traceId);
  expect(queries[0].searchParams.get("category")).toBe("authentication");
  await expect(page.locator(".workspace-navigation a.current")).toHaveCount(1);
  await expect(page.locator(".workspace-navigation a.current")).toHaveAttribute(
    "href",
    "/admin/audit",
  );
  await page
    .locator(".workspace-navigation")
    .getByRole("link", { name: "活動稽核", exact: true })
    .click();
  await expect(
    page.getByRole("searchbox", { name: "搜尋稽核", exact: true }),
  ).toHaveValue("");
  await expect
    .poll(() => queries.at(-1)?.searchParams.has("traceId"))
    .toBe(false);
  expect(managementRequests).toEqual([]);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});

test("management access alone does not imply audit access or issue protected audit requests", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  await fixture.attach(page);
  const requests: string[] = [];
  await page.route("**/api/v1/admin/audit**", async (route) => {
    requests.push(route.request().url());
    return route.fulfill({ status: 403, json: { code: "feature_forbidden" } });
  });
  await page.goto("/admin/audit");
  await expect(page.getByRole("alert")).toContainText("需要活動稽核查閱權限");
  await expect(
    page.locator(".workspace-navigation a[href='/admin/audit']"),
  ).toHaveCount(0);
  await expect(page.locator(".audit-row")).toHaveCount(0);
  expect(requests).toEqual([]);
});
