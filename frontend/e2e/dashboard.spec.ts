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
  const { fixture } = await openDashboard(page);
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
  await expect(tile(page, "平台")).toContainText("128");
  await expect(tile(page, "平台")).toContainText("無法連線");
  await expect(tile(page, "即時監控")).toContainText("14");
  await expect(tile(page, "工作流程")).toContainText("目前帳號有權限的工作區");
  await chooseSelect(page, "顯示的費用幣別與類型", "USD · API 計費估算");
  const users = tile(page, "使用者區間費用");
  await users
    .getByRole("row", { name: /測試使用者/ })
    .getByRole("button", { name: "檢視", exact: true })
    .click();
  await expect(overview).toContainText("使用者：測試使用者");
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
  // Export follows the applied period, not an unsubmitted date.
  await page.getByLabel("費用開始日期", { exact: true }).fill("2025/01/01");
  await page.getByRole("button", { name: "匯出費用", exact: true }).click();
  await expect
    .poll(() => exportedFrom)
    .toBe(
      new Date(appliedFrom.replaceAll("/", "-") + "T00:00:00").toISOString(),
    );
  expect(exportedOwner).toBe(fixture.userId);
  await page.getByLabel("費用開始日期", { exact: true }).fill(appliedFrom);
  await users.getByRole("button", { name: "查看所有人", exact: true }).click();
  await expect(overview).toContainText("整個平台");
});

test("styles switch and persist, and tiles can be moved, resized, hidden and reset", async ({
  page,
}) => {
  await openDashboard(page, false);
  await page.goto("/dashboard");
  const host = page.locator("nx-dashboard-page");
  await expect(host).toHaveAttribute("data-dashboard-style", "aurora");
  await expect(page.locator("nx-aurora-hero")).toContainText("測試使用者");
  const styles = page.getByRole("group", { name: "總覽風格", exact: true });
  await styles.getByRole("button", { name: "星軌", exact: true }).click();
  await expect(host).toHaveAttribute("data-dashboard-style", "orbit");
  // The night sky is dark whatever the theme; its feature labels are real buttons.
  await expect(host).toHaveAttribute("data-theme", "dark");
  const map = page.getByRole("group", { name: "功能星圖", exact: true });
  await map.getByRole("button", { name: /知識庫/ }).click();
  await expect(page.locator("nx-orbit-panel")).toContainText("索引分段");
  await expect(
    page.locator("nx-orbit-panel").getByRole("link", { name: "前往知識庫" }),
  ).toHaveAttribute("href", "/knowledge");
  await styles.getByRole("button", { name: "脈衝", exact: true }).click();
  await expect(page.locator("nx-pulse-hero")).toContainText("次模型與工具呼叫");
  await page.reload();
  await expect(host).toHaveAttribute("data-dashboard-style", "pulse");

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
  const handle = page.getByRole("button", { name: /^移動 知識庫/ });
  await handle.evaluate((el) => el.scrollIntoView({ block: "center" }));
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
  // Size, height and hiding.
  const activity = page.locator('nx-dashboard-tile[data-id="activity"]');
  await activity
    .getByRole("group", { name: "呼叫熱度 大小" })
    .getByRole("button", { name: "滿版", exact: true })
    .click();
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
  await expect(page.locator("nx-aurora-hero .aurora i").first()).toHaveCSS(
    "animation-iteration-count",
    "1",
  );
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
