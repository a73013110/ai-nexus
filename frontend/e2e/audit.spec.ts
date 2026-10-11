import { test, expect } from "@playwright/test";
import { ApiFixture, expectViewportContained } from "./fixtures";

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
