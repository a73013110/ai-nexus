import { test, expect, type Route } from "@playwright/test";
import { ApiFixture, featureOf } from "./fixtures";

const calendarId = "team@example.test";
const run = {
  id: "11111111-1111-1111-1111-111111111111",
  calendarId,
  triggerKind: "scheduled",
  requestedById: null,
  requestedByName: null,
  startedAt: "2026-11-08T12:47:29Z",
  finishedAt: "2026-11-08T12:47:41Z",
  status: "succeeded",
  sourceCount: 120,
  createdCount: 0,
  updatedCount: 63,
  deletedCount: 1,
  failedCount: 0,
  heldCount: 0,
  errorCode: null,
  errorMessage: null,
  lookbackDays: 7,
};
const item = {
  kind: "leave",
  startsAt: "2026-11-09T00:30:00Z",
  endsAt: "2026-11-09T10:00:00Z",
  errorCode: null,
  errorMessage: null,
};
const items = [
  {
    ...item,
    eventId: "22222222-2222-2222-2222-222222222221",
    outcome: "updated",
    desNo: "L-1",
    applicantName: "王大明",
    cause: "content",
    changes: [{ field: "flowStatus", from: "Send", to: "Decide" }],
  },
  {
    ...item,
    eventId: "22222222-2222-2222-2222-222222222222",
    outcome: "deleted",
    desNo: "L-2",
    applicantName: "林小芳",
    cause: "form_status",
    changes: [{ field: "flowStatus", from: "Decide", to: "Finish" }],
  },
];
const page$ = <T>(rows: T[]) => ({
  items: rows,
  total: rows.length,
  offset: 0,
  complete: true,
});

async function openPage(page: import("@playwright/test").Page) {
  const api = new ApiFixture();
  api.extraFeatures.push(featureOf("calendar-sync"));
  await api.attach(page);
  const eventQueries: string[] = [];
  await page.route("**/api/v1/calendar-sync/**", (route: Route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace("/api/v1/calendar-sync", "");
    const body =
      path === "/status"
        ? {
            enabled: true,
            calendarId,
            intervalSeconds: 900,
            lookbackDays: 7,
            serviceAccountConfigured: true,
            serviceAccount: null,
            running: false,
            lastCheck: null,
          }
        : path === "/calendars"
          ? [
              {
                calendarId,
                configured: true,
                total: 6314,
                pending: 0,
                failed: 0,
              },
            ]
          : path === "/runs"
            ? page$([run])
            : path.endsWith("/items")
              ? page$(items)
              : (eventQueries.push(url.search), page$([]));
    return route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });
  await page.goto("/office/calendar-sync");
  return eventQueries;
}

test("數字欄的標題貼齊下方的數字，不會看成隔壁欄", async ({ page }) => {
  await openPage(page);
  await page.getByRole("button", { name: "執行紀錄" }).click();
  const table = page.locator(".calendar-sync-runs");
  await expect(table.getByRole("cell", { name: "63" })).toBeVisible();

  for (const [label, value] of [
    ["更新", "63"],
    ["刪除", "1"],
    ["失敗", "0"],
  ] as const) {
    const header = table.getByRole("columnheader", { name: label });
    const heading = await header.locator(".ui-table-sort").boundingBox();
    const cell = await table
      .locator(`td[data-label="${label}"]`)
      .first()
      .boundingBox();
    expect(heading).not.toBeNull();
    expect(cell).not.toBeNull();
    // 標題的右緣落在自己這一欄裡，而且貼近儲存格的右緣（數字靠右）。
    expect(
      Math.abs(heading!.x + heading!.width - (cell!.x + cell!.width)),
    ).toBeLessThan(24);
    await expect(table.locator(`td[data-label="${label}"]`).first()).toHaveText(
      value,
    );
  }
});

test("點一輪看逐筆明細：結果、變更與移除原因", async ({ page }) => {
  await openPage(page);
  await page.getByRole("button", { name: "執行紀錄" }).click();
  await page.getByRole("button", { name: /檢視同步明細/ }).click();

  const drawer = page.getByRole("dialog", { name: "同步明細" });
  await expect(drawer.getByText("回看 7 天")).toBeVisible();
  const rows = drawer.locator(".run-items tbody tr");
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toContainText("簽核：簽核中 → 已核准");
  await expect(rows.nth(1)).toContainText("刪除");
  await expect(rows.nth(1)).toContainText("單據不再生效");
  await expect(rows.nth(1)).toContainText("簽核：已核准 → 退回結案");
});

test("事件預設只看同步範圍內，切到全部歷史才帶 history", async ({ page }) => {
  const queries = await openPage(page);
  await expect.poll(() => queries.length).toBeGreaterThan(0);
  expect(queries.at(-1)).not.toContain("history");

  await page.getByRole("combobox", { name: "事件範圍" }).click();
  await page.getByRole("option", { name: /全部歷史/ }).click();
  await expect.poll(() => queries.at(-1)).toContain("history=true");
});
