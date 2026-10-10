import { test, expect } from "@playwright/test";
import { FEATURE_NAMES } from "../src/app/core/auth/feature-names";
import {
  expectCompactWorkspace,
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  settleEntrance,
  dashboardFixture,
} from "./fixtures";

test("dashboard keeps currencies separate, supports node inspection, and fits mobile with reduced motion", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = ["dashboard", "knowledge", "tasks", "projects"].map(
    (id) => ({
      id,
      name: FEATURE_NAMES[id],
      route: "/" + id,
    }),
  );
  await fixture.attach(page);
  await page.route("**/api/v1/dashboard?**", (route) => {
    const data = dashboardFixture(fixture);
    data.scope =
      new URL(route.request().url()).searchParams.get("scope") ?? "personal";
    return route.fulfill({ json: data });
  });
  await page.goto("/");
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole("heading", { name: "總覽", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".metric-card")).toHaveCount(4);
  const tokens = page.locator("nx-token-usage-chart");
  await expect(tokens).toContainText("2 / 3 次模型呼叫有完整用量回報");
  await expect(page.locator("body")).not.toContainText("fixture:8b");
  await chooseSelect(page, "Token 統計模型", "雲端助理");
  await expect(tokens.locator(".token-totals")).toContainText("300");
  await chooseSelect(page, "Token 統計類型", "輸出 Token");
  await expect(tokens.getByRole("slider")).toHaveAccessibleName(
    "逐日檢視每日 輸出 Tokens",
  );
  await chooseSelect(page, "Token 統計模型", "所有模型");
  for (const width of [1920, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await expectViewportContained(page);
    expect(
      await page
        .locator(".feature-main")
        .evaluate((el) => el.scrollHeight > el.clientHeight),
    ).toBe(true);
  }
  await expect(page.locator(".metric-card").first()).toContainText("USD");
  await expect(page.locator(".metric-card").first()).not.toContainText(
    "15.1208",
  );
  await page.locator(".flow-node").filter({ hasText: "知識" }).click();
  const inspector = page.locator(".flow-inspector");
  await expect(inspector).toContainText(/2\s*個知識庫/);
  await expect(inspector).toContainText("214 段索引");
  for (const [label, count, unit, destination, route] of [
    ["檔案庫", "5", "個檔案", "檔案庫", "/files"],
    ["知識庫", "2", "個知識庫", "知識庫", "/knowledge"],
    ["AI 回覆生成", "0", "件進行中", "對話", "/chat"],
    ["背景任務", "1", "件進行中", "背景任務", "/tasks"],
    ["專案", "3", "個專案", "專案", "/projects"],
  ]) {
    const node = page.locator(".flow-node").filter({ hasText: label });
    await node.click();
    await expect(node).toHaveAttribute("aria-pressed", "true");
    await expect(inspector.getByRole("heading")).toHaveText(label);
    await expect(inspector.locator("strong")).toHaveText(count);
    await expect(inspector).toContainText(unit);
    await expect(
      inspector.getByRole("link", { name: "前往" + destination, exact: true }),
    ).toHaveAttribute("href", route);
  }
  await page.locator(".flow-node").filter({ hasText: "檔案庫" }).click();
  await inspector
    .getByRole("link", { name: "前往檔案庫", exact: true })
    .click();
  await expect(page).toHaveURL(/\/files$/);
  await expect(
    page.getByRole("heading", { name: "檔案庫", exact: true }),
  ).toBeVisible();
  await page.goto("/dashboard");
  const chart = page.locator("nx-trend-chart").first();
  await chart.getByRole("slider").focus();
  await page.keyboard.press("End");
  await expect(chart.locator(".trend-tooltip")).toBeVisible();
  await chooseSelect(page, "顯示的費用幣別與類型", "TWD · 內部成本估算");
  await expect(page.locator(".metric-card").first()).toContainText("TWD");
  await chart.getByRole("slider").focus();
  await page.keyboard.press("End");
  await expect(chart.locator(".trend-tooltip")).toContainText("TWD");
  await chooseSelect(page, "總覽範圍", "整個平台");
  await expect(page.locator(".flow-scope-note")).toContainText(
    "目前帳號有權限的工作區",
  );
  await expect(
    page.getByRole("heading", { name: "使用者區間費用" }),
  ).toBeVisible();
  await chooseSelect(page, "顯示的費用幣別與類型", "USD · API 計費估算");
  await page
    .locator(".dashboard-users")
    .getByRole("button", { name: "檢視", exact: true })
    .click();
  await expect(page.locator(".dashboard-content > .form-note")).toContainText(
    "使用者：測試使用者",
  );
  await expect(inspector.locator(".panel-eyebrow")).toHaveText(
    "使用者：測試使用者",
  );
  const appliedFrom = await page
    .getByLabel("費用開始日期", { exact: true })
    .inputValue();
  let exportedFrom = "";
  let exportedOwner = "";
  await page.route("**/api/v1/admin/billing/export?**", (route) => {
    exportedFrom = new URL(route.request().url()).searchParams.get("from")!;
    exportedOwner = new URL(route.request().url()).searchParams.get("ownerId")!;
    return route.fulfill({
      contentType: "text/csv",
      body: "使用者,費用\n測試,1",
    });
  });
  await page.getByLabel("費用開始日期", { exact: true }).fill("2025/01/01");
  await page.getByRole("button", { name: "匯出費用", exact: true }).click();
  await expect
    .poll(() => exportedFrom)
    .toBe(
      new Date(appliedFrom.replaceAll("/", "-") + "T00:00:00").toISOString(),
    );
  expect(exportedOwner).toBe(fixture.userId);
  await page.getByLabel("費用開始日期", { exact: true }).fill(appliedFrom);
  await page.getByRole("button", { name: "查看所有人", exact: true }).click();
  await expect(page.locator(".dashboard-content > .form-note")).toContainText(
    "整個平台",
  );
  await settleEntrance(page);
  await page
    .locator(".feature-main")
    .evaluate((el) => el.scrollTo({ top: 0, behavior: "instant" }));
  await expectCompactWorkspace(page);
  await page
    .locator(".feature-main")
    .evaluate((el) =>
      el.scrollTo({ top: el.scrollHeight, behavior: "instant" }),
    );
  await page.setViewportSize({ width: 375, height: 812 });
  await page.evaluate(() => {
    document.documentElement.dataset.theme = "dark";
    document.documentElement.dataset.reducedMotion = "true";
  });
  await expect
    .poll(() =>
      page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    )
    .toBe(true);
  await expectViewportContained(page);
  for (const label of ["費用開始日期", "費用結束日期"]) {
    await expect
      .poll(() =>
        page.getByLabel(label, { exact: true }).evaluate((element) => {
          const input = element as HTMLInputElement;
          const style = getComputedStyle(input);
          const context = document.createElement("canvas").getContext("2d")!;
          context.font = style.font;
          return (
            context.measureText(input.value).width <=
            input.clientWidth -
              parseFloat(style.paddingLeft) -
              parseFloat(style.paddingRight)
          );
        }),
      )
      .toBe(true);
  }
  await expect(page.locator(".flow-pulse")).toHaveCSS("animation-name", "none");
  const nodeBounds = await page.locator(".flow-node").evaluateAll((nodes) =>
    nodes.map((node) => {
      const { left, right, top, bottom } = node.getBoundingClientRect();
      return { left, right, top, bottom };
    }),
  );
  for (let i = 0; i < nodeBounds.length; i++) {
    for (const other of nodeBounds.slice(i + 1)) {
      const node = nodeBounds[i];
      expect(
        node.right <= other.left ||
          other.right <= node.left ||
          node.bottom <= other.top ||
          other.bottom <= node.top,
      ).toBe(true);
    }
  }
  await page
    .locator(".feature-main")
    .evaluate((el) => el.scrollTo({ top: 0, behavior: "instant" }));
  await expectCompactWorkspace(page);
  await page
    .locator(".feature-main")
    .evaluate((el) =>
      el.scrollTo({ top: el.scrollHeight, behavior: "instant" }),
    );
});
