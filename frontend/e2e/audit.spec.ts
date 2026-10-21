import { test, expect } from "@playwright/test";
import { ApiFixture, expectViewportContained } from "./fixtures";

test("audit-only access keeps deep-link filters and same-page links without requesting account administration", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.auditAccess = true;
  await fixture.attach(page);
  // 預設釘選不含稽核；釘上它才測得到「點目前頁面的側邊欄連結」會清掉篩選。
  await page.addInitScript(
    (key) => localStorage.setItem(key, JSON.stringify(["chat", "audit"])),
    `ai-nexus.pins.${fixture.userId}`,
  );
  const queries: URL[] = [];
  const managementRequests: string[] = [];
  const traceId = "a".repeat(32);
  await page.route("**/api/v1/admin/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/v1/admin/audit/catalog")
      return route.fulfill({
        json: {
          features: [
            {
              id: "audit",
              name: "活動稽核",
              route: "/admin/audit",
              icon: "audit",
            },
          ],
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
  // 已釘選的稽核在釘選列與全部功能各標一次，兩個都指向同一頁。
  const current = page.locator(".workspace-navigation a.current");
  await expect(current).toHaveCount(2);
  for (const link of await current.all())
    await expect(link).toHaveAttribute("href", "/admin/audit");
  await page
    .getByRole("list", { name: "釘選功能", exact: true })
    .getByRole("link", { name: "活動稽核", exact: true })
    .click();
  await expect(
    page.getByRole("searchbox", { name: "搜尋", exact: true }),
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
  await expect(page.getByRole("alert")).toContainText("需要「活動稽核」權限");
  await expect(
    page.locator(".workspace-navigation a[href='/admin/audit']"),
  ).toHaveCount(0);
  await expect(page.locator(".audit-row")).toHaveCount(0);
  expect(requests).toEqual([]);
});

test("recorded feature changes read under their menu group", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.auditAccess = true;
  await fixture.attach(page);
  await page.route("**/api/v1/admin/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/v1/admin/audit/catalog")
      return route.fulfill({
        json: {
          // 伺服器依功能目錄的宣告順序列出；不在清單中的 ID 由前端以目錄補上名稱。
          features: [
            { id: "chat", name: "對話", route: "/work/chat", icon: "chat" },
            {
              id: "audit",
              name: "活動稽核",
              route: "/admin/audit",
              icon: "audit",
            },
          ],
          models: [],
          // 完整預設選單上的位置；不在選單裡的目錄功能歸「未放入選單」，不在目錄的歸「其他」。
          navigationSections: [
            { id: "work", path: ["AI 工作台"], featureIds: ["chat"] },
            {
              id: "system.observability",
              path: ["系統管理", "監控與紀錄"],
              featureIds: [
                "diagnostics.logs",
                "diagnostics.logs.detail",
                "diagnostics.logs.export",
                "audit",
              ],
            },
          ],
        },
      });
    if (url.pathname === "/api/v1/admin/audit")
      return route.fulfill({
        json: [
          {
            id: 7,
            actor: "AD\\admin",
            action: "admin.group",
            category: "administration",
            result: "saved",
            at: "2026-10-08T00:00:00Z",
            resourceId: "auditors",
            detailsJson: JSON.stringify({
              before: { featureIds: ["chat"] },
              // 快照的順序不是目錄順序，也可能有已移除的功能。
              after: {
                featureIds: [
                  "retired",
                  "diagnostics.logs.export",
                  "audit",
                  "chat",
                ],
              },
            }),
          },
        ],
      });
    return route.fulfill({ status: 403, json: { code: "feature_forbidden" } });
  });
  await page.goto("/admin/audit");
  await page.getByRole("button", { name: /^檢視稽核：/ }).click();
  const [before, after] = [
    "nx-feature-summary >> nth=0",
    "nx-feature-summary >> nth=1",
  ].map((selector) =>
    page
      .getByRole("dialog", { name: "稽核詳情" })
      .locator(".audit-feature-change")
      .locator(selector),
  );
  await expect(before.locator(".feature-category-label")).toHaveText([
    "AI 工作台",
  ]);
  // 依選單分組、組內依選單順序（動作接在上層頁面之後），不論快照順序；已移除的 ID 放最後。
  await expect(after.locator(".feature-category-label")).toHaveText([
    "AI 工作台",
    "系統管理 › 監控與紀錄",
    "其他",
  ]);
  await expect(after.locator(".resource-badges span")).toHaveText([
    "對話",
    "日誌匯出",
    "活動稽核",
    "retired",
  ]);
});
