import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  settleEntrance,
  expectViewportContained,
  expectCompactWorkspace,
  dashboardFixture,
} from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

test("chat history stays primary with grouped tools collapsed and settings only in the account menu", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = [
    { id: "projects", name: "專案", route: "/projects" },
    { id: "tasks", name: "背景任務", route: "/tasks" },
  ];
  await fixture.attach(page);
  await page.goto("/chat");
  const tools = page.getByRole("navigation", {
    name: "工作區功能",
    exact: true,
  });
  await expect(
    tools.getByRole("button", { name: "工作區", exact: true }),
  ).toHaveAttribute("aria-expanded", "false");
  await expect(tools.getByRole("link")).toHaveCount(0);
  await tools.getByRole("button", { name: "工作區", exact: true }).click();
  await expect(
    tools.getByRole("region", { name: "工作", exact: true }),
  ).toBeVisible();
  await expect(
    tools.getByRole("region", { name: "協作與品質", exact: true }),
  ).toBeVisible();
  await expect(
    tools.getByRole("region", { name: "系統", exact: true }),
  ).toBeVisible();
  await expect(
    tools.getByRole("link", { name: "設定", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "登入者選單", exact: true }).click();
  await expect(
    page.getByRole("menuitem", { name: "設定", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("menuitem", { name: "切換登入方式", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("menuitem")).toHaveCount(2);
  await page.keyboard.press("Escape");
  await settleEntrance(page);
});

test("chat and feature sidebars share brand, account alignment and compact navigation sizing", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.extraFeatures = [{ id: "tasks", name: "背景任務", route: "/tasks" }];
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("button", { name: "工作區", exact: true }).click();
  await expect(page.locator(".workspace-icons a").first()).toBeVisible();
  await settleEntrance(page);
  const measure = () =>
    page.locator(".workspace-sidebar").evaluate((el) => {
      const brand = el.querySelector(".brand")!.getBoundingClientRect();
      const account = el
        .querySelector("nx-account-menu")!
        .getBoundingClientRect();
      const link = el
        .querySelector(".workspace-icons a")!
        .getBoundingClientRect();
      return {
        brand: { x: brand.x, y: brand.y },
        account: { x: account.x, bottom: account.bottom },
        link: { width: link.width, height: link.height },
      };
    });
  const chat = await measure();
  await expect(page.locator(".brand")).toHaveAttribute("href", "/dashboard");
  await expectViewportContained(page);
  await page.goto("/tasks");
  await expect(page.locator(".workspace-icons a").first()).toBeVisible();
  expect(await measure()).toEqual(chat);
  await expectViewportContained(page);
  await page.locator(".brand").click();
  await expect(page).toHaveURL(/\/dashboard$/);
});

test("the shared wordmark has one N and the compact new-chat icon stays centered", async ({
  page,
}) => {
  const core = new ApiFixture();
  await core.attach(page);
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  await expect(sidebar.locator(".brand-wordmark")).toHaveText("AIexus");
  await expect(sidebar.locator(".brand-symbol svg")).toHaveCount(1);
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("名稱顯示測試");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.locator(".message-model")).toHaveText("本機測試模型");
  await expect(page.locator("body")).not.toContainText("fixture:8b");
  for (const width of [1280, 375]) {
    await page.setViewportSize({ width, height: 812 });
    if (width === 375) {
      await expect(sidebar).not.toBeVisible();
      await expect(page.locator(".workspace-menu-button")).toBeVisible();
      await expectViewportContained(page);
      continue;
    }
    const collapse = page.getByRole("button", {
      name: "收合側欄",
      exact: true,
    });
    if (await collapse.isVisible()) await collapse.click();
    const button = sidebar.getByRole("link", { name: "新對話", exact: true });
    const [rail, bounds, icon] = await Promise.all([
      sidebar.boundingBox(),
      button.boundingBox(),
      button.locator("nx-icon").boundingBox(),
    ]);
    expect(bounds!.width).toBeGreaterThanOrEqual(44);
    expect(bounds!.height).toBeGreaterThanOrEqual(44);
    expect(
      Math.abs(bounds!.x + bounds!.width / 2 - (icon!.x + icon!.width / 2)),
    ).toBeLessThan(1);
    expect(
      Math.abs(bounds!.y + bounds!.height / 2 - (icon!.y + icon!.height / 2)),
    ).toBeLessThan(1);
    expect(
      Math.abs(bounds!.x + bounds!.width / 2 - (rail!.x + rail!.width / 2)),
    ).toBeLessThan(1);
  }
});

test("one header notification icon exposes the full count and workspace expansion uses the chat sidebar", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push(
    { id: "knowledge", name: "知識庫", route: "/knowledge" },
    { id: "tasks", name: "背景任務", route: "/tasks" },
    { id: "repositories", name: "程式庫", route: "/repositories" },
  );
  await core.attach(page);
  let unread = 125;
  await page.route("**/api/v1/notifications**", (route) =>
    route.fulfill({ json: { items: [], unread, hasMore: false } }),
  );
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  const bell = sidebar.getByRole("button", { name: "通知", exact: true });
  await expect(bell).toHaveCount(1);
  await expect(bell.locator("nx-count-badge")).toHaveText("99+");
  const [iconBounds, badgeBounds] = await Promise.all([
    bell.locator("nx-icon").boundingBox(),
    bell.locator("nx-count-badge").boundingBox(),
  ]);
  expect(badgeBounds!.x).toBeLessThan(iconBounds!.x + iconBounds!.width);
  expect(badgeBounds!.x).toBeGreaterThan(iconBounds!.x + iconBounds!.width / 3);
  expect(badgeBounds!.y + badgeBounds!.height).toBeGreaterThan(iconBounds!.y);
  const description = await bell.getAttribute("aria-describedby");
  await expect(page.locator("#" + description)).toHaveText("125 則未讀通知");
  const [notification, toggle] = await Promise.all([
    bell.boundingBox(),
    sidebar.locator(".sidebar-toggle").boundingBox(),
  ]);
  expect(notification!.x + notification!.width).toBeLessThanOrEqual(toggle!.x);
  await expect(sidebar).toHaveCSS("width", "240px");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveCSS("font-size", "15px");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveCSS("line-height", "18px");
  const workspace = sidebar.getByRole("button", {
    name: "工作區",
    exact: true,
  });
  await workspace.click();
  await expect(sidebar.locator(".new-chat")).not.toBeVisible();
  await expect(sidebar.locator(".history-search")).not.toBeVisible();
  await expect(workspace).toHaveAttribute("aria-expanded", "true");
  await expect(sidebar.locator(".workspace-groups")).toHaveCSS(
    "max-height",
    "none",
  );
  const [heading, navigation] = await Promise.all([
    sidebar.locator(".workspace-sidebar-heading").boundingBox(),
    workspace.boundingBox(),
  ]);
  expect(navigation!.y - (heading!.y + heading!.height)).toBeLessThanOrEqual(
    16,
  );
  await settleEntrance(page);
  await workspace.press("Enter");
  await expect(sidebar.locator(".new-chat")).toBeVisible();
  await expect(sidebar.locator(".history-search")).toBeVisible();
  unread = 0;
  await bell.click();
  const notificationDialog = page.getByRole("dialog", {
    name: "通知",
    exact: true,
  });
  await expect(notificationDialog).toBeVisible();
  await expect(bell.locator("nx-count-badge")).not.toBeVisible();
  await page.keyboard.press("Escape");
  await expect(notificationDialog).not.toBeVisible();
  await expect(bell).toBeFocused();
  await page.setViewportSize({ width: 375, height: 667 });
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await workspace.click();
  await expect(sidebar.locator(".new-chat")).not.toBeVisible();
  await expect(
    sidebar.getByRole("link", { name: "背景任務", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(sidebar).not.toBeVisible();
  const mobileToggle = page.getByRole("button", {
    name: "展開側欄",
    exact: true,
  });
  await expect(mobileToggle).toBeFocused();
  await mobileToggle.click();
  await bell.focus();
  await bell.press("Tab");
  await expect(sidebar.locator(".sidebar-toggle")).toBeFocused();
  await page.keyboard.press("Escape");
  await expectViewportContained(page);
});

test("icon rail, notification filtering and typed task navigation work across pages and mobile", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  const job = fixture.jobs[0];
  const items = [
    {
      id: randomUUID(),
      version: 1,
      type: "task.completed",
      severity: "success",
      title: "文件索引完成",
      body: job.label,
      target: { kind: "task", id: job.id },
      createdAt: new Date().toISOString(),
      readAt: null as string | null,
    },
    {
      id: randomUUID(),
      version: 99,
      type: "future.event",
      severity: "info",
      title: "未來版本通知",
      body: "保留文字內容",
      target: { kind: "task", id: job.id },
      createdAt: new Date().toISOString(),
      readAt: null as string | null,
    },
  ];
  await page.route("**/api/v1/notifications**", (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === "POST") {
      const row = items.find((x) => url.pathname.includes(x.id));
      if (row) row.readAt = new Date().toISOString();
      return route.fulfill({ status: 204 });
    }
    if (route.request().method() === "DELETE") {
      items.splice(
        items.findIndex((x) => url.pathname.endsWith(x.id)),
        1,
      );
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({
      json: {
        items: items.filter(
          (x) => url.searchParams.get("unread") !== "true" || !x.readAt,
        ),
        unread: items.filter((x) => !x.readAt).length,
        hasMore: false,
      },
    });
  });
  await page.goto("/files");
  await page.getByRole("button", { name: "收合側欄", exact: true }).click();
  const sidebar = page.locator(".workspace-sidebar");
  expect((await sidebar.boundingBox())!.width).toBe(52);
  await expect(
    sidebar.getByRole("link", { name: "知識庫", exact: true }),
  ).toBeVisible();
  await sidebar.getByRole("link", { name: "知識庫", exact: true }).click();
  await expect(sidebar).toHaveClass(/is-compact/);
  await page.getByRole("button", { name: "通知", exact: true }).click();
  let dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await expect(dialog.locator(".notification-row")).toHaveCount(2);
  await expect(dialog).toHaveCSS("font-size", "13px");
  await expect(dialog.locator(".notification-title strong").first()).toHaveCSS(
    "font-size",
    "13px",
  );
  await expect(dialog.locator(".notification-row time").first()).toHaveCSS(
    "font-size",
    "12px",
  );
  expect(
    (await dialog.locator(".notification-row").first().boundingBox())!.height,
  ).toBeLessThanOrEqual(120);
  await settleEntrance(page);
  await page.evaluate(() =>
    document.documentElement.setAttribute("data-theme", "dark"),
  );
  await page.evaluate(() =>
    document.documentElement.setAttribute("data-theme", "light"),
  );
  await expect(
    dialog
      .locator(".notification-row")
      .filter({ hasText: "未來版本通知" })
      .getByRole("button", { name: "查看內容" }),
  ).toHaveCount(0);
  await dialog.getByRole("button", { name: "只看未讀", exact: true }).click();
  await dialog
    .locator(".notification-row")
    .filter({ hasText: "未來版本通知" })
    .getByRole("button", { name: "標為已讀", exact: true })
    .click();
  await expect(dialog.locator(".notification-row")).toHaveCount(1);
  await dialog.getByRole("button", { name: "查看內容" }).click();
  await expect(page).toHaveURL(new RegExp(`/tasks\\?job=${job.id}$`));
  await expect(page.locator(".job-card.current")).toContainText(job.label);
  await expect(page.locator(".job-card.current")).toBeFocused();
  await expect(page.locator(".job-card.current")).toHaveCSS("padding", "12px");
  await expect(page.locator(".job-card.current .job-heading h2")).toHaveCSS(
    "font-size",
    "16px",
  );
  await expect(page.locator(".job-card.current nx-status-badge")).toHaveText(
    "已完成",
  );
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await expectCompactWorkspace(page);
  await expect(sidebar).not.toBeVisible();
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await expect(page.locator(".feature-main")).toHaveAttribute("inert", "");
  await page.getByRole("button", { name: "通知", exact: true }).click();
  dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await dialog.getByRole("button", { name: "只看未讀", exact: true }).click();
  await dialog
    .locator(".notification-row")
    .filter({ hasText: "文件索引完成" })
    .getByRole("button", { name: "查看內容" })
    .click();
  await expect(sidebar).not.toBeVisible();
  await expect(page.locator(".feature-main")).not.toHaveAttribute("inert", "");
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await page.getByRole("button", { name: "通知", exact: true }).click();
  for (let i = 0; i < 20; i++)
    items.push({
      ...items[0],
      id: randomUUID(),
      title: "較早的通知 " + i,
      readAt: null,
    });
  await dialog
    .getByRole("button", { name: "重新整理通知", exact: true })
    .click();
  await expect(dialog.locator(".notification-row")).toHaveCount(22);
  await page.setViewportSize({ width: 375, height: 667 });
  const bounds = await dialog.boundingBox();
  expect(bounds!.y).toBeGreaterThanOrEqual(0);
  expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(667);
  expect(
    await dialog
      .locator(".notification-list")
      .evaluate((el) => el.scrollHeight > el.clientHeight),
  ).toBe(true);
});

test("mobile navigation uses the full viewport, traps focus and preserves desktop density", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  await sidebar.locator(".sidebar-toggle").click();
  expect((await sidebar.boundingBox())!.width).toBe(52);
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(sidebar).not.toBeVisible();
  expect((await page.locator("main").boundingBox())!.width).toBe(375);
  expect((await page.locator("main").boundingBox())!.y).toBe(0);
  expect((await page.locator("main").boundingBox())!.height).toBe(812);
  const opener = page.getByRole("button", { name: "展開側欄", exact: true });
  await opener.click();
  await expect(sidebar).toHaveAttribute("aria-modal", "true");
  await expect(page.locator("main")).toHaveAttribute("inert", "");
  await expect(sidebar.locator(".sidebar-toggle")).toBeFocused();
  expect((await page.locator("main").boundingBox())!.width).toBe(375);
  await sidebar
    .getByRole("button", { name: "登入者選單", exact: true })
    .focus();
  await page.keyboard.press("Tab");
  await expect(sidebar.locator(".workspace-sidebar-heading a")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(opener).toBeFocused();
  await expect(page.locator("main")).not.toHaveAttribute("inert", "");
  await opener.click();
  await sidebar.locator(".workspace-backdrop").click();
  await expect(opener).toBeFocused();
  await expectViewportContained(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await expect(sidebar).toHaveClass(/is-compact/);
  expect((await sidebar.boundingBox())!.width).toBe(52);
});

test("feature navigation opens from the shared header and dismisses after selecting a destination", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.extraFeatures.push({
    id: "knowledge",
    name: "知識庫",
    route: "/knowledge",
  });
  await fixture.attach(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto("/files");
  await expect(page.locator(".feature-header h1")).toBeVisible();
  expect((await page.locator(".feature-main").boundingBox())!.width).toBe(375);
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  const sidebar = page.locator(".workspace-sidebar");
  await sidebar.getByRole("link", { name: "知識庫", exact: true }).click();
  await expect(page).toHaveURL(/\/knowledge$/);
  await expect(sidebar).not.toBeVisible();
  await expect(page.locator(".feature-main")).not.toHaveAttribute("inert", "");
  await expectViewportContained(page);
});

test.describe("touch interaction", () => {
  test.use({
    viewport: { width: 375, height: 812 },
    isMobile: true,
    hasTouch: true,
  });

  test("touch navigation and compact notices keep accessible targets", async ({
    page,
  }) => {
    const fixture = new ApiFixture();
    await fixture.attach(page);
    await page.route("**/api/client-issues", (route) =>
      route.fulfill({ status: 503, body: "{}" }),
    );
    await page.goto("/chat");
    const opener = page.locator(".workspace-menu-button");
    await opener.tap();
    const sidebar = page.locator(".workspace-sidebar");
    await expect(sidebar).toBeVisible();
    expect((await sidebar.boundingBox())!.width).toBeLessThan(375);
    await sidebar.locator(".workspace-backdrop").tap();
    await expect(sidebar).not.toBeVisible();
    expect((await page.locator("main").boundingBox())!.width).toBe(375);
    await page.evaluate(() => {
      setTimeout(() => {
        throw new Error("touch diagnostic");
      });
    });
    const notice = page.locator("nx-unhandled-issue nx-notice");
    await expect(notice).toBeVisible();
    for (const button of await notice.getByRole("button").all()) {
      const bounds = (await button.boundingBox())!;
      expect(bounds.width).toBeGreaterThanOrEqual(44);
      expect(bounds.height).toBeGreaterThanOrEqual(44);
    }
    await notice.getByRole("button", { name: "關閉提示", exact: true }).tap();
    await expect(notice).toHaveCount(0);
    await expectViewportContained(page);
  });
});

test("custom feature names stay consistent across navigation, overview links and destination headings", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.extraFeatures = [
    { id: "dashboard", name: "總覽", route: "/dashboard" },
    { id: "knowledge", name: "部門知識庫", route: "/knowledge" },
  ];
  await fixture.attach(page);
  await page.route("**/api/v1/dashboard?**", (route) =>
    route.fulfill({ json: dashboardFixture(fixture) }),
  );
  await page.route("**/api/v1/knowledge/collections", (route) =>
    route.fulfill({ json: [] }),
  );
  await page.goto("/dashboard");
  await expect(
    page
      .getByRole("navigation", { name: "工作區功能" })
      .getByRole("link", { name: "部門知識庫", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".flow-inspector").getByRole("heading")).toHaveText(
    "部門知識庫",
  );
  await page.getByRole("link", { name: "前往部門知識庫", exact: true }).click();
  await expect(page).toHaveURL(/\/knowledge$/);
  await expect(
    page.getByRole("heading", { name: "部門知識庫", exact: true }),
  ).toBeVisible();
  await page.goto("/dashboard");
  await page.locator(".flow-node").filter({ hasText: "背景任務" }).click();
  await expect(page.locator(".flow-inspector").getByRole("link")).toHaveCount(
    0,
  );
});

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
  await page.keyboard.press("Escape");
  await expect(sidebar).toHaveAttribute("aria-hidden", "true");
  await expect(
    page.getByRole("button", { name: "展開側欄", exact: true }),
  ).toBeFocused();
  await expectViewportContained(page);
});
