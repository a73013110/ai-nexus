import { test, expect, type Page } from "@playwright/test";
import { FEATURE_NAMES } from "../src/app/core/auth/feature-names";
import {
  expectCompactWorkspace,
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  settleEntrance,
  dashboardFixture,
} from "./fixtures";

const FEATURES = [
  "dashboard",
  "knowledge",
  "tasks",
  "projects",
  "artifacts",
  "shared",
  "quality",
  "repositories",
];

async function openDashboard(page: Page, admin = true) {
  const fixture = new ApiFixture();
  fixture.adminAccess = admin;
  fixture.extraFeatures = FEATURES.map((id) => ({
    id,
    name: FEATURE_NAMES[id],
    route: "/" + id,
  }));
  await fixture.attach(page);
  const reads: URL[] = [];
  await page.route("**/api/v1/dashboard?**", (route) => {
    const url = new URL(route.request().url());
    reads.push(url);
    return route.fulfill({
      json: dashboardFixture(
        fixture,
        url.searchParams.get("scope") ?? "personal",
      ),
    });
  });
  return { fixture, reads };
}
const tile = (page: Page, name: string) =>
  page.getByRole("region", { name, exact: true });
const tileOrder = (page: Page) =>
  page
    .locator("nx-dashboard-tile")
    .evaluateAll((tiles) => tiles.map((x) => (x as HTMLElement).dataset.id));

test("dashboard shows every granted feature, keeps currencies apart and narrows the platform view to one user", async ({
  page,
}) => {
  const { fixture, reads } = await openDashboard(page);
  await page.goto("/");
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole("heading", { name: "總覽", exact: true }),
  ).toBeVisible();
  const overview = tile(page, "關鍵指標");
  await expect(overview).toContainText("USD");
  await expect(overview).not.toContainText("15.1208");
  await expect(page.locator("body")).not.toContainText("fixture:8b");
  // Every granted feature has its own tile, titled with the feature name.
  for (const name of [
    "對話",
    "知識庫",
    "背景任務",
    "專案",
    "成果文件",
    "分享",
    "品質評測",
    "程式庫",
    "通知",
  ])
    await expect(tile(page, name)).toBeVisible();
  const knowledge = tile(page, "知識庫");
  await expect(knowledge).toContainText("214");
  await expect(knowledge).toContainText("語意向量");
  await expect(
    knowledge.getByRole("link", { name: "前往知識庫", exact: true }),
  ).toHaveAttribute("href", "/knowledge");
  await expect(
    tile(page, "繼續工作").getByRole("link", { name: /整理期末評量規準/ }),
  ).toHaveAttribute("href", /^\/chat\/[\da-f-]{36}$/);
  await expect(
    tile(page, "通知").getByRole("link", { name: /前往/ }),
  ).toHaveCount(0);

  const tokens = page.locator("nx-token-usage-chart");
  await expect(tokens).toContainText("有完整用量回報");
  await chooseSelect(page, "Token 統計模型", "雲端助理");
  await chooseSelect(page, "Token 統計類型", "輸出 Token");
  await expect(tokens.getByRole("slider")).toHaveAccessibleName(
    "逐日檢視每日 輸出 Tokens",
  );
  await chooseSelect(page, "Token 統計模型", "所有模型");

  const chart = tile(page, "費用趨勢").locator("nx-trend-chart");
  await chart.getByRole("slider").focus();
  await page.keyboard.press("End");
  await expect(chart.locator(".trend-tooltip")).toBeVisible();
  await chooseSelect(page, "顯示的費用幣別與類型", "TWD · 內部成本估算");
  await expect(overview).toContainText("TWD");
  await chart.getByRole("slider").focus();
  await page.keyboard.press("End");
  await expect(chart.locator(".trend-tooltip")).toContainText("TWD");

  await chooseSelect(page, "總覽範圍", "整個平台");
  // Administrators see the same tiles as everyone, plus the per-user spend; no monitoring tile.
  await expect(
    page.locator('nx-dashboard-tile[data-id="users"]'),
  ).toBeVisible();
  await expect(tile(page, "平台")).toHaveCount(0);
  await expect(tile(page, "即時監控")).toHaveCount(0);
  await expect(tile(page, "工作流程")).toContainText("目前帳號有權限的工作區");
  await chooseSelect(page, "顯示的費用幣別與類型", "USD · API 計費估算");
  const users = tile(page, "使用者區間費用");
  await users
    .getByRole("row", { name: /測試使用者/ })
    .getByRole("button", { name: "檢視", exact: true })
    .click();
  await expect(overview).toContainText("使用者：測試使用者");
  await page
    .getByRole("group", { name: "費用查詢期間", exact: true })
    .getByRole("button", { name: "自訂", exact: true })
    .click();
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
  // Export follows the applied period, never an unfinished date.
  await page.getByLabel("費用開始日期", { exact: true }).fill("2025/13/01");
  await page.getByRole("button", { name: "匯出費用", exact: true }).click();
  await expect
    .poll(() => exportedFrom)
    .toBe(
      new Date(appliedFrom.replaceAll("/", "-") + "T00:00:00").toISOString(),
    );
  expect(exportedOwner).toBe(fixture.userId);
  // A valid date applies itself once typing pauses; there is no query button.
  await expect(
    page.getByRole("button", { name: "查詢", exact: true }),
  ).toHaveCount(0);
  await page.getByLabel("費用開始日期", { exact: true }).fill("2025/01/01");
  await expect
    .poll(() => reads.at(-1)?.searchParams.get("from"))
    .toBe(new Date("2025-01-01T00:00:00").toISOString());
  await users.getByRole("button", { name: "查看所有人", exact: true }).click();
  await expect(overview).toContainText("整個平台");
});

test("the platform scope follows platform operations, not access management", async ({
  page,
}) => {
  const { fixture, reads } = await openDashboard(page);
  fixture.operationsAccess = false;
  await page.goto("/dashboard?scope=platform");
  await expect(
    page.getByRole("heading", { name: "總覽", exact: true }),
  ).toBeVisible();
  await expect.poll(() => reads.length).toBeGreaterThan(0);
  await expect(
    page.getByRole("combobox", { name: "總覽範圍", exact: true }),
  ).toHaveCount(0);
  expect(
    reads.every((url) => url.searchParams.get("scope") !== "platform"),
  ).toBe(true);
});

test("one overview style, and tiles can be moved, resized, hidden and reset", async ({
  page,
}) => {
  await openDashboard(page, false);
  await page.goto("/dashboard");
  await expect(page.getByRole("group", { name: "總覽風格" })).toHaveCount(0);
  await expect(page.locator("nx-dashboard-page")).not.toHaveAttribute(
    "data-theme",
    /.+/,
  );

  const defaults = await tileOrder(page);
  await page.getByRole("button", { name: "自訂版面", exact: true }).click();
  const bar = page.getByRole("region", { name: "自訂總覽版面", exact: true });
  await expect(bar).toBeVisible();
  // Keyboard: Home moves the heat map to the front and announces it.
  await page.getByRole("button", { name: /^移動 呼叫熱度/ }).press("Home");
  await expect
    .poll(() => tileOrder(page))
    .toEqual(["activity", ...defaults.filter((x) => x !== "activity")]);
  await expect(page.locator("nx-dashboard-board [aria-live]")).toHaveText(
    /呼叫熱度：第 1 個/,
  );
  await expect(
    page.getByRole("button", { name: /^移動 呼叫熱度/ }),
  ).toBeFocused();
  // Pointer: drop the knowledge tile on the chat tile beside it.
  // Both tiles in view, away from the edges that scroll the page while dragging.
  await page.setViewportSize({ width: 1440, height: 1400 });
  const handle = page.getByRole("button", { name: /^移動 知識庫/ });
  await tile(page, "對話").evaluate((el) =>
    el.scrollIntoView({ block: "center" }),
  );
  const start = (await handle.boundingBox())!;
  const target = (await tile(page, "對話").boundingBox())!;
  await page.mouse.move(start.x + start.width / 2, start.y + start.height / 2);
  await page.mouse.down();
  await page.mouse.move(
    target.x + target.width / 2,
    target.y + target.height / 2,
    { steps: 16 },
  );
  await page.mouse.up();
  await expect
    .poll(async () => {
      const order = await tileOrder(page);
      return order.indexOf("section:knowledge") < order.indexOf("section:chat");
    })
    .toBe(true);
  // Size, height and hiding. A tile made small can grow again (its sizes used to vanish).
  const activity = page.locator('nx-dashboard-tile[data-id="activity"]');
  const sizes = activity.getByRole("group", { name: "呼叫熱度 大小" });
  await sizes.getByRole("button", { name: "小", exact: true }).click();
  await expect(activity).toHaveAttribute("data-size", "s");
  await sizes.getByRole("button", { name: "滿版", exact: true }).click();
  await expect(activity).toHaveAttribute("data-size", "xl");
  await page
    .getByRole("button", { name: "加高：呼叫熱度", exact: true })
    .click();
  await expect(activity).toHaveClass(/is-tall/);
  await page
    .getByRole("button", { name: "隱藏 服務狀態", exact: true })
    .click();
  await expect(tile(page, "服務狀態")).toHaveCount(0);
  await page.reload();
  await expect(
    page.locator('nx-dashboard-tile[data-id="activity"]'),
  ).toHaveAttribute("data-size", "xl");
  expect((await tileOrder(page))[0]).toBe("activity");
  await expect(tile(page, "服務狀態")).toHaveCount(0);
  await page.getByRole("button", { name: "自訂版面", exact: true }).click();
  await bar.getByRole("button", { name: "服務狀態" }).click();
  await expect(tile(page, "服務狀態")).toBeVisible();
  await bar.getByRole("button", { name: "恢復預設", exact: true }).click();
  await expect.poll(() => tileOrder(page)).toEqual(defaults);
  await bar.getByRole("button", { name: "完成", exact: true }).click();
  await expect(bar).toHaveCount(0);
  await expect(page.getByRole("button", { name: /^移動 / })).toHaveCount(0);
});

test("dashboard fits a phone in the dark with reduced motion", async ({
  page,
}) => {
  await openDashboard(page);
  await page.goto("/dashboard");
  await expect(tile(page, "關鍵指標")).toBeVisible();
  for (const width of [1920, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await expectViewportContained(page);
  }
  await settleEntrance(page);
  await expectCompactWorkspace(page);
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
  // One column: every tile spans the board.
  const widths = await page
    .locator("nx-dashboard-tile")
    .evaluateAll((tiles) =>
      tiles.map((x) => Math.round(x.getBoundingClientRect().width)),
    );
  expect(new Set(widths).size).toBe(1);
  await expect(page.locator("nx-dashboard-tile").first()).toHaveCSS(
    "animation-duration",
    "0s",
  );
  // Headline figures fit their panels at phone width.
  for (const value of await page.locator(".kpi-value").all())
    expect(
      await value.evaluate((x) => x.scrollWidth <= x.clientWidth + 1),
    ).toBe(true);
  await page
    .getByRole("group", { name: "費用查詢期間", exact: true })
    .getByRole("button", { name: "自訂", exact: true })
    .click();
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
  const flow = await tile(page, "工作流程")
    .locator(".flow-node")
    .evaluateAll((nodes) =>
      nodes.map((node) => {
        const { left, right, top, bottom } = node.getBoundingClientRect();
        return { left, right, top, bottom };
      }),
    );
  for (let i = 0; i < flow.length; i++)
    for (const other of flow.slice(i + 1))
      expect(
        flow[i].right <= other.left ||
          other.right <= flow[i].left ||
          flow[i].bottom <= other.top ||
          other.bottom <= flow[i].top,
      ).toBe(true);
});

for (const width of [1440, 375]) {
  test(`dashboard tour walks the visible controls and tiles at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const { fixture } = await openDashboard(page);
    // Without platform operations the scope switch is absent, and so is its step.
    if (width === 375) fixture.operationsAccess = false;
    const height = width === 375 ? 812 : 1000;
    await page.setViewportSize({ width, height });
    await page.goto("/dashboard");
    await expect(tile(page, "關鍵指標")).toBeVisible();
    const launcher = page
      .locator(".feature-header")
      .getByRole("button", { name: "操作導覽", exact: true });
    await launcher.click();
    const tour = page.locator(".driver-popover.nx-tour");
    const title = tour.locator(".driver-popover-title");
    const progress = tour.locator(".driver-popover-progress-text");
    await expect(title).toHaveText("一句話掌握現況");
    const total = Number((await progress.innerText()).split("/")[1]);
    const titles = [await title.innerText()];
    for (let step = 2; step <= total; step++) {
      await tour.getByRole("button", { name: "下一步", exact: true }).click();
      await expect(progress).toHaveText(`${step} / ${total}`);
      titles.push(await title.innerText());
      await settleEntrance(page);
      const bounds = (await tour.boundingBox())!;
      expect(bounds.x).toBeGreaterThanOrEqual(0);
      expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
      expect(bounds.y + bounds.height).toBeLessThanOrEqual(height);
    }
    expect(titles).toEqual([
      "一句話掌握現況",
      ...(width === 375 ? [] : ["切換總覽範圍"]),
      "選擇查詢期間",
      "幣別與費用類型分開看",
      "先看關鍵指標",
      "每個功能一張卡片",
      "從上次停下的地方繼續",
      "排出自己的總覽",
      "需要最新數字時",
    ]);
    await tour.getByRole("button", { name: "開始使用", exact: true }).click();
    await expect(tour).toHaveCount(0);
    await expect(launcher).toBeFocused();
    await expect(page.locator(".driver-overlay")).toHaveCount(0);
    expect(errors).toEqual([]);
  });
}
