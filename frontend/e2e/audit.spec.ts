import { test, expect } from "@playwright/test";
import { ApiFixture, expectViewportContained, routeOf } from "./fixtures";

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
            action: "identity.session.signed_in",
            category: "authentication",
            result: "completed",
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
            action: "access-control.group.updated",
            category: "administration",
            result: "completed",
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

/** 瀏覽器時區的 `YYYY-MM-DD`；測試與瀏覽器跑在同一台機器、同一時區。 */
const localDay = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
const DAY = 86_400_000;

test("feature usage reports the period, opens a feature's trend and links to its audit trail", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.auditAccess = true;
  fixture.extraFeatures = [{ id: "audit.usage" }];
  await fixture.attach(page);
  const usageQueries: URL[] = [];
  const detailQueries: URL[] = [];
  const auditQueries: URL[] = [];
  await page.route("**/api/v1/admin/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/v1/admin/audit/usage") {
      usageQueries.push(url);
      return route.fulfill({
        json: {
          from: url.searchParams.get("from"),
          until: url.searchParams.get("until"),
          features: [
            {
              featureId: "chat",
              users: 12,
              events: 340,
              failures: 6,
              lastUsedAt: "2026-10-08T01:00:00Z",
            },
            // 目錄沒有的 ID 用原值當名稱；從未成功使用過的沒有最後使用時間。
            {
              featureId: "retired",
              users: 1,
              events: 0,
              failures: 2,
              lastUsedAt: null,
            },
          ],
        },
      });
    }
    if (url.pathname === "/api/v1/admin/audit/usage/chat") {
      detailQueries.push(url);
      return route.fulfill({
        json: {
          featureId: "chat",
          from: url.searchParams.get("from"),
          until: url.searchParams.get("until"),
          offset: Number(url.searchParams.get("offsetMinutes")),
          days: [
            { date: "2026-10-06", users: 4, events: 30 },
            { date: "2026-10-07", users: 7, events: 52 },
          ],
          users: [
            {
              userId: "5d0c9f4e-1b7a-4c1e-9a52-0f3e8d6b2a11",
              account: "TEST\alex",
              displayName: "林小安",
              events: 120,
              lastUsedAt: "2026-10-07T09:00:00Z",
            },
          ],
        },
      });
    }
    if (url.pathname === "/api/v1/admin/audit/catalog")
      return route.fulfill({ json: { features: [], models: [] } });
    if (url.pathname === "/api/v1/admin/audit") {
      auditQueries.push(url);
      return route.fulfill({ json: [] });
    }
    return route.fulfill({ status: 403, json: { code: "feature_forbidden" } });
  });
  await page.goto(routeOf("audit.usage"));
  await expect(
    page.getByRole("heading", { name: "功能使用量", exact: true }),
  ).toBeVisible();
  // 選單依種子放在活動稽核之後。
  await expect(
    page
      .locator(".workspace-navigation")
      .getByRole("link", { name: "功能使用量", exact: true }),
  ).toHaveAttribute("href", "/admin/audit/usage");

  // 預設 30 天：迄日是明天零時（含今天整天）。
  const span = (query: URL) =>
    Date.parse(query.searchParams.get("until")!) -
    Date.parse(query.searchParams.get("from")!);
  await expect.poll(() => usageQueries.length).toBe(1);
  expect(span(usageQueries[0])).toBe(30 * DAY);
  const until = new Date(usageQueries[0].searchParams.get("until")!);
  expect([until.getHours(), until.getMinutes()]).toEqual([0, 0]);
  expect(until.getTime()).toBeGreaterThan(Date.now());

  const summary = page.getByRole("list", { name: "功能使用量摘要" });
  await expect(summary.getByRole("listitem")).toHaveText([
    /有人使用的功能\s*2\s*個/,
    /操作次數\s*340\s*次/,
    /失敗或拒絕\s*8\s*次/,
  ]);
  const rows = page.locator(".usage-features tbody tr");
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0).getByRole("rowheader")).toContainText("對話");
  await expect(rows.nth(1).getByRole("rowheader")).toContainText("retired");
  // 只有失敗、沒有成功的功能失敗率是 100%，最後使用顯示「—」。
  await expect(rows.nth(1).locator("[data-label='失敗或拒絕']")).toHaveText(
    /2\s*100%/,
  );
  await expect(rows.nth(1).locator("[data-label='最後使用']")).toHaveText("—");

  await page
    .getByRole("group", { name: "統計期間" })
    .getByRole("button", { name: "7 天", exact: true })
    .click();
  await expect.poll(() => usageQueries.length).toBe(2);
  expect(span(usageQueries[1])).toBe(7 * DAY);
  expect(usageQueries[1].searchParams.get("until")).toBe(
    usageQueries[0].searchParams.get("until"),
  );

  await page.getByRole("button", { name: "檢視使用量：對話" }).click();
  const drawer = page.getByRole("dialog", { name: "功能使用量" });
  await expect(drawer.getByRole("heading", { name: "對話" })).toBeVisible();
  await expect(
    drawer.getByRole("img", { name: "每日使用人數，最高 7" }),
  ).toBeVisible();
  await expect(drawer.locator(".usage-people li")).toHaveText([
    /林小安\s*TEST\alex\s*120 次/,
  ]);
  // 明細與列表同一期間，以瀏覽器時區分日。
  const detail = detailQueries.at(-1)!;
  expect(detail.searchParams.get("from")).toBe(
    usageQueries[1].searchParams.get("from"),
  );
  expect(detail.searchParams.get("until")).toBe(
    usageQueries[1].searchParams.get("until"),
  );
  expect(Number(detail.searchParams.get("offsetMinutes"))).toBe(
    await page.evaluate(() => -new Date().getTimezoneOffset()),
  );

  // 帶到活動稽核：稽核頁的迄日含當天，所以是統計迄日的前一天。
  const from = localDay(new Date(usageQueries[1].searchParams.get("from")!));
  const lastDay = localDay(new Date(until.getTime() - DAY));
  await drawer.getByRole("link", { name: "查看這段期間的稽核紀錄" }).click();
  await expect(page).toHaveURL(
    `/admin/audit?feature=chat&from=${from}&until=${lastDay}`,
  );
  await expect(page.getByText("功能：對話")).toBeVisible();
  await expect
    .poll(() => auditQueries.at(-1)?.searchParams.get("feature"))
    .toBe("chat");

  await page.goto(routeOf("audit.usage"));
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(rows).toHaveCount(2);
  await expectViewportContained(page);
});

test("feature usage without audit access hides the audit link", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.extraFeatures = [{ id: "audit.usage" }];
  await fixture.attach(page);
  const auditRequests: string[] = [];
  await page.route("**/api/v1/admin/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/v1/admin/audit/usage")
      return route.fulfill({
        json: {
          from: url.searchParams.get("from"),
          until: url.searchParams.get("until"),
          features: [
            {
              featureId: "chat",
              users: 1,
              events: 1,
              failures: 0,
              lastUsedAt: "2026-10-08T01:00:00Z",
            },
          ],
        },
      });
    if (url.pathname === "/api/v1/admin/audit/usage/chat")
      return route.fulfill({
        json: {
          featureId: "chat",
          from: url.searchParams.get("from"),
          until: url.searchParams.get("until"),
          offset: 0,
          days: [],
          users: [],
        },
      });
    auditRequests.push(url.pathname);
    return route.fulfill({ status: 403, json: { code: "feature_forbidden" } });
  });
  await page.goto(routeOf("audit.usage"));
  await page.getByRole("button", { name: "檢視使用量：對話" }).click();
  const drawer = page.getByRole("dialog", { name: "功能使用量" });
  await expect(drawer.getByText("期間內沒有成功的操作。")).toBeVisible();
  await expect(drawer.getByRole("link")).toHaveCount(0);
  await expect(
    page.locator(".workspace-navigation a[href='/admin/audit']"),
  ).toHaveCount(0);
  expect(auditRequests).toEqual([]);
});
