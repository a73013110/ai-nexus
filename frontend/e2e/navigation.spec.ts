import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  settleEntrance,
  expectViewportContained,
  expectCompactWorkspace,
  dashboardFixture,
  touchSwipe,
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
  const launcher = tools.getByRole("button", { name: "全部功能", exact: true });
  const pinned = tools.getByRole("list", { name: "釘選功能", exact: true });
  await expect(launcher).toHaveAttribute("aria-expanded", "false");
  await expect(pinned.getByRole("link")).toHaveText([
    "對話",
    "專案",
    "檔案庫",
    "背景任務",
  ]);
  await expect(tools.getByRole("region", { name: "系統" })).toHaveCount(0);
  await launcher.click();
  // The launcher lists every feature in the shared groups; pinned ones keep their place with a
  // pin mark, and the current page stays marked once, on its pinned row.
  await expect(
    tools.getByRole("region", { name: "系統", exact: true }),
  ).toBeVisible();
  const work = tools.getByRole("region", { name: "工作", exact: true });
  await expect(
    work.getByRole("link", { name: "對話", exact: true }),
  ).not.toHaveClass(/current/);
  await expect(work.locator(".pin-mark")).toHaveCount(3);
  await expect(tools.locator('[aria-current="page"]')).toHaveCount(1);
  await tools.getByRole("button", { name: "自訂釘選", exact: true }).click();
  await expect(tools.getByRole("status")).toContainText("已釘選 4／5 項");
  await tools
    .getByRole("button", { name: "釘選背景任務", exact: true })
    .click();
  await expect(
    tools.getByRole("button", { name: "釘選背景任務", exact: true }),
  ).toHaveAttribute("aria-pressed", "false");
  await tools
    .getByRole("button", { name: "釘選平台管理", exact: true })
    .click();
  await tools.getByRole("button", { name: "完成", exact: true }).click();
  await expect(pinned.getByRole("link")).toHaveText([
    "對話",
    "專案",
    "檔案庫",
    "平台管理",
  ]);
  await expect(
    tools
      .getByRole("region", { name: "協作與品質", exact: true })
      .getByRole("link", { name: "背景任務", exact: true }),
  ).toBeVisible();
  await page.reload();
  await expect(pinned.getByRole("link")).toHaveText([
    "對話",
    "專案",
    "檔案庫",
    "平台管理",
  ]);
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
  // On the collapsed rail the menu opens beside it, wide enough for its labels and naming the
  // account the rail no longer shows.
  await page.getByRole("button", { name: "收合側欄", exact: true }).click();
  const account = page.getByRole("button", { name: "登入者選單", exact: true });
  await account.click();
  const menu = page.getByRole("menu", { name: "登入者選單", exact: true });
  await expect(menu).toContainText("測試使用者");
  const [rail, panel] = await Promise.all([
    page.locator(".workspace-sidebar").boundingBox(),
    menu.boundingBox(),
  ]);
  expect(panel!.x).toBeGreaterThanOrEqual(rail!.x + rail!.width);
  expect(panel!.width).toBeGreaterThanOrEqual(200);
  await expect(menu).toBeInViewport({ ratio: 1 });
  await page.keyboard.press("Escape");
  await expect(account).toBeFocused();
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
  await expect(page.locator(".pinned-features a").first()).toBeVisible();
  await settleEntrance(page);
  const measure = () =>
    page.locator(".workspace-sidebar").evaluate((el) => {
      const brand = el.querySelector(".brand")!.getBoundingClientRect();
      const account = el
        .querySelector("nx-account-menu")!
        .getBoundingClientRect();
      const link = el
        .querySelector(".pinned-features a")!
        .getBoundingClientRect();
      return {
        brand: { x: brand.x, y: brand.y },
        account: { x: account.x, bottom: account.bottom },
        // Pages with a sidebar panel add the launcher toggle to the pinned row, so only height matches.
        link: { height: link.height },
      };
    });
  const chat = await measure();
  await expect(page.locator(".brand")).toHaveAttribute("href", "/dashboard");
  await expectViewportContained(page);
  await page.goto("/tasks");
  await expect(page.locator(".pinned-features a").first()).toBeVisible();
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
    await expect(sidebar).toHaveClass(/is-compact/);
    const button = sidebar.getByRole("link", { name: "新對話", exact: true });
    const measure = () =>
      Promise.all([
        sidebar.boundingBox(),
        button.boundingBox(),
        button.locator("nx-icon").boundingBox(),
      ]);
    // Measure once the rail has settled; reduced motion still finishes layout on the next frame.
    await expect
      .poll(async () => JSON.stringify(await measure()))
      .toBe(JSON.stringify(await measure()));
    const [rail, bounds, icon] = await measure();
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
    name: "全部功能",
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
    sidebar.locator(".pinned-features a").first().boundingBox(),
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
  // Desktop: a non-modal popover hanging from the bell; the page stays usable.
  await expect(notificationDialog).not.toHaveAttribute("aria-modal", "true");
  await expect(page.locator(".workspace-main")).not.toHaveAttribute(
    "inert",
    "",
  );
  const bellBounds = (await bell.boundingBox())!;
  await expect
    .poll(async () => (await notificationDialog.boundingBox())!.y)
    .toBeGreaterThanOrEqual(bellBounds.y + bellBounds.height);
  await page.keyboard.press("Escape");
  await expect(notificationDialog).not.toBeVisible();
  await expect(bell).toBeFocused();
  // The bell toggles it, and a click elsewhere dismisses it.
  await bell.click();
  await expect(notificationDialog).toBeVisible();
  await bell.click();
  await expect(notificationDialog).toBeHidden();
  await bell.click();
  await expect(notificationDialog).toBeVisible();
  await page.mouse.click(900, 600);
  await expect(notificationDialog).toBeHidden();
  // Over the tablet drawer it is still a popover; Escape closes it before the drawer.
  await page.setViewportSize({ width: 800, height: 700 });
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await bell.click();
  await expect(notificationDialog).toBeVisible();
  await expect(notificationDialog).not.toHaveAttribute("aria-modal", "true");
  await page.keyboard.press("Escape");
  await expect(notificationDialog).toBeHidden();
  await expect(sidebar).toBeVisible();
  await expect(bell).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(sidebar).not.toBeVisible();
  await page.setViewportSize({ width: 375, height: 667 });
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await workspace.click();
  await expect(sidebar.locator(".new-chat")).not.toBeVisible();
  await expect(
    sidebar.getByRole("link", { name: "程式庫", exact: true }).first(),
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
  await expect(sidebar).toHaveCSS("width", "52px");
  await expect(
    sidebar.getByRole("link", { name: "知識庫", exact: true }),
  ).toBeVisible();
  await sidebar
    .getByRole("link", { name: "知識庫", exact: true })
    .first()
    .click();
  await expect(sidebar).toHaveClass(/is-compact/);
  await page.getByRole("button", { name: "通知", exact: true }).click();
  let dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await expect(dialog.locator(".notification-row")).toHaveCount(2);
  // On the icon rail the popover opens beside the rail.
  const rail = (await sidebar.boundingBox())!.width;
  await expect
    .poll(async () => (await dialog.boundingBox())!.x)
    .toBeGreaterThanOrEqual(rail);
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
  // The whole row opens its target, like 「查看內容」.
  await dialog.locator(".notification-row p").click();
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
  await expect(page.locator(".workspace-main")).toHaveAttribute("inert", "");
  await page.getByRole("button", { name: "通知", exact: true }).click();
  dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await dialog.getByRole("button", { name: "只看未讀", exact: true }).click();
  await dialog
    .locator(".notification-row")
    .filter({ hasText: "文件索引完成" })
    .getByRole("button", { name: "查看內容" })
    .click();
  await expect(sidebar).not.toBeVisible();
  await expect(page.locator(".workspace-main")).not.toHaveAttribute(
    "inert",
    "",
  );
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
  await expect(sidebar).toHaveCSS("width", "52px");
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(sidebar).not.toBeVisible();
  expect((await page.locator("main").boundingBox())!.width).toBe(375);
  expect((await page.locator("main").boundingBox())!.y).toBe(0);
  expect((await page.locator("main").boundingBox())!.height).toBe(812);
  const opener = page.getByRole("button", { name: "展開側欄", exact: true });
  await opener.click();
  await expect(sidebar).toHaveAttribute("aria-modal", "true");
  await expect(page.locator(".workspace-main")).toHaveAttribute("inert", "");
  await expect(sidebar.locator(".sidebar-toggle")).toBeFocused();
  expect((await page.locator("main").boundingBox())!.width).toBe(375);
  await sidebar
    .getByRole("button", { name: "登入者選單", exact: true })
    .focus();
  await page.keyboard.press("Tab");
  await expect(sidebar.locator(".workspace-sidebar-heading a")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(opener).toBeFocused();
  await expect(page.locator(".workspace-main")).not.toHaveAttribute(
    "inert",
    "",
  );
  await opener.click();
  await sidebar.locator(".workspace-backdrop").click();
  await expect(opener).toBeFocused();
  await expectViewportContained(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await expect(sidebar).toHaveClass(/is-compact/);
  await expect(sidebar).toHaveCSS("width", "52px");
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
  await sidebar
    .getByRole("link", { name: "知識庫", exact: true })
    .first()
    .click();
  await expect(page).toHaveURL(/\/knowledge$/);
  await expect(sidebar).not.toBeVisible();
  await expect(page.locator(".workspace-main")).not.toHaveAttribute(
    "inert",
    "",
  );
  await expectViewportContained(page);
});

test("the shell keeps one sidebar while pages and conversations change", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.extraFeatures.push({
    id: "knowledge",
    name: "知識庫",
    route: "/knowledge",
  });
  await fixture.attach(page);
  await page.goto("/files");
  const sidebar = page.locator(".workspace-sidebar");
  await sidebar.evaluate(
    (element) => ((element as HTMLElement).dataset["kept"] = "1"),
  );
  await sidebar
    .getByRole("link", { name: "知識庫", exact: true })
    .first()
    .click();
  await expect(page).toHaveURL(/\/knowledge$/);
  await sidebar
    .getByRole("link", { name: "對話", exact: true })
    .first()
    .click();
  await expect(page).toHaveURL(/\/chat$/);
  const workspace = page.locator(".main-workspace");
  await workspace.evaluate(
    (element) => ((element as HTMLElement).dataset["kept"] = "1"),
  );
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("第一句");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page).toHaveURL(/\/chat\/[^/]+$/);
  // The same elements survive: the shell did not rebuild the sidebar, nor the router the chat.
  await expect(sidebar).toHaveAttribute("data-kept", "1");
  await expect(workspace).toHaveAttribute("data-kept", "1");
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

  test("the drawer closes on a leftward swipe of its heading or scrim, and springs back from a short one", async ({
    page,
  }) => {
    await new ApiFixture().attach(page);
    await page.goto("/chat");
    const opener = page.locator(".workspace-menu-button");
    const sidebar = page.locator(".workspace-sidebar");
    await opener.tap();
    await expect(sidebar).toHaveClass(/is-open/);
    const heading = (await sidebar
      .locator(".workspace-sidebar-heading")
      .boundingBox())!;
    const from = { x: heading.x + 40, y: heading.y + heading.height / 2 };
    // Short and slow: the drawer follows, then springs back open.
    await touchSwipe(
      page,
      from,
      { x: from.x - 40, y: from.y },
      {
        hold: 200,
        held: async () =>
          expect((await sidebar.boundingBox())!.x).toBeCloseTo(-40, -1),
      },
    );
    await expect(sidebar).toHaveClass(/is-open/);
    await expect
      .poll(async () => (await sidebar.boundingBox())!.x)
      .toBeCloseTo(0, 0);
    // Past a third of its width closes it and returns focus to the opener.
    await touchSwipe(page, from, { x: from.x - 200, y: from.y }, { hold: 200 });
    await expect(sidebar).not.toBeVisible();
    await expect(opener).toBeFocused();
    // A quick flick on the scrim is enough.
    await opener.tap();
    await expect(sidebar).toHaveClass(/is-open/);
    const scrim = { x: 360, y: 400 };
    await touchSwipe(
      page,
      scrim,
      { x: scrim.x - 60, y: scrim.y },
      { steps: 3 },
    );
    await expect(sidebar).not.toBeVisible();
    await expectViewportContained(page);
  });

  test("bottom sheets close on a downward swipe of their header but not of their content", async ({
    page,
  }) => {
    await new ApiFixture().attach(page);
    await page.goto("/chat");
    const sidebar = page.locator(".workspace-sidebar");
    const openSheet = async () => {
      // Closing the sheet leaves the drawer it was opened from in place.
      if (!(await sidebar.evaluate((element) => element.matches(".is-open"))))
        await page.locator(".workspace-menu-button").tap();
      await page
        .locator(".workspace-sidebar")
        .getByRole("button", { name: "通知", exact: true })
        .tap();
      const sheet = page.getByRole("dialog", { name: "通知", exact: true });
      await expect(sheet).toBeVisible();
      // Measure once the rise has finished.
      await expect
        .poll(() => sheet.evaluate((element) => element.getAnimations().length))
        .toBe(0);
      return sheet;
    };
    let sheet = await openSheet();
    const rest = (await sheet.boundingBox())!.y;
    const bar = (await sheet.locator(".dialog-bar").boundingBox())!;
    const from = { x: bar.x + 40, y: bar.y + bar.height / 2 };
    // Short and slow: the sheet follows the finger, then springs back.
    await touchSwipe(
      page,
      from,
      { x: from.x, y: from.y + 50 },
      {
        hold: 200,
        held: async () =>
          expect((await sheet.boundingBox())!.y - rest).toBeCloseTo(50, -1),
      },
    );
    await expect(sheet).toBeVisible();
    await expect
      .poll(async () => Math.round((await sheet.boundingBox())!.y))
      .toBe(Math.round(rest));
    // Dragging the content scrolls it instead of moving the sheet.
    const body = (await sheet.locator(".notification-panel").boundingBox())!;
    await touchSwipe(
      page,
      { x: body.x + 40, y: body.y + 20 },
      { x: body.x + 40, y: body.y + 320 },
      { hold: 200 },
    );
    await expect(sheet).toBeVisible();
    // Past the distance closes it like Escape.
    await touchSwipe(page, from, { x: from.x, y: from.y + 300 }, { hold: 200 });
    await expect(sheet).toBeHidden();
    // A quick flick is enough.
    sheet = await openSheet();
    await touchSwipe(page, from, { x: from.x, y: from.y + 60 }, { steps: 3 });
    await expect(sheet).toBeHidden();
    await expectViewportContained(page);
  });

  test("with reduced motion a released sheet settles at once", async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await new ApiFixture().attach(page);
    await page.goto("/chat");
    await page.locator(".workspace-menu-button").tap();
    await page
      .locator(".workspace-sidebar")
      .getByRole("button", { name: "通知", exact: true })
      .tap();
    const sheet = page.getByRole("dialog", { name: "通知", exact: true });
    await expect(sheet).toBeVisible();
    const bar = (await sheet.locator(".dialog-bar").boundingBox())!;
    const from = { x: bar.x + 40, y: bar.y + bar.height / 2 };
    await touchSwipe(page, from, { x: from.x, y: from.y + 50 }, { hold: 200 });
    expect(
      await sheet.evaluate((element) => [
        getComputedStyle(element).translate,
        getComputedStyle(element).transitionDuration,
      ]),
    ).toEqual(["0px", "0s"]);
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
      .getByRole("link", { name: "部門知識庫", exact: true })
      .first(),
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

test("the icon rail keeps pins, opens every other feature beside it and still scrolls when short", async ({
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
  const down = sidebar.getByRole("button", {
    name: "向下捲動工作區功能",
    exact: true,
  });
  await expect(sidebar).toHaveCSS("width", "52px");
  await expect(viewport).toHaveCSS("scrollbar-width", "none");
  await expect(
    sidebar.getByRole("list", { name: "釘選功能" }).getByRole("link"),
  ).toHaveCount(4);
  await expect(down).not.toBeVisible();
  const center = (await sidebar
    .getByRole("link", { name: "總覽", exact: true })
    .boundingBox())!;
  expect(Math.abs(center.x + center.width / 2 - 26)).toBeLessThan(1);
  const trigger = sidebar.getByRole("button", {
    name: "全部功能",
    exact: true,
  });
  await trigger.click();
  const flyout = page.getByRole("dialog", { name: "全部功能", exact: true });
  await expect(flyout).toBeVisible();
  await expect(flyout.getByRole("link").first()).toBeFocused();
  // Every feature keeps its group; pinned ones carry a pin mark instead of disappearing.
  await expect(
    flyout
      .getByRole("link", { name: "總覽", exact: true })
      .locator(".pin-mark"),
  ).toHaveCount(1);
  await expect
    .poll(async () => {
      const box = (await flyout.boundingBox())!;
      return box.x >= 52 && box.y >= 0 && box.y + box.height <= 560;
    })
    .toBe(true);
  await page.keyboard.press("Escape");
  await expect(flyout).not.toBeVisible();
  await expect(trigger).toBeFocused();
  await trigger.click();
  await flyout.getByRole("link", { name: "活動稽核", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/audit$/);
  await expect(flyout).not.toBeVisible();
  await expect(trigger).toHaveClass(/current/);
  await page.setViewportSize({ width: 1280, height: 300 });
  // Focus is back on the launcher trigger, so the short rail keeps it in view and offers the way up.
  const up = sidebar.getByRole("button", {
    name: "向上捲動工作區功能",
    exact: true,
  });
  await expect(up).toBeVisible();
  await expect(trigger).toBeInViewport();
  await viewport.focus();
  await page.keyboard.press("Home");
  await expect(down).toBeVisible();
  await expect(up).not.toBeVisible();
  await expectViewportContained(page);
  await page.emulateMedia({ forcedColors: "active" });
  await expect(viewport).toHaveCSS("scrollbar-width", "auto");
});

test("Ctrl+K finds every feature, the pinned pill follows the route and the display switches to a list", async ({
  page,
}) => {
  await navigationWorkspace(page);
  await page.keyboard.press("Control+k");
  const search = page.getByRole("combobox", { name: "快捷指令" });
  await expect(search).toBeFocused();
  await search.fill("稽核");
  await expect(page.getByRole("option")).toHaveCount(1);
  await search.press("Enter");
  await expect(page).toHaveURL(/\/admin\/audit$/);
  const pinned = page.getByRole("list", { name: "釘選功能", exact: true });
  await expect(pinned).not.toHaveClass(/has-active/);
  await pinned.getByRole("link", { name: "知識庫", exact: true }).click();
  await expect(pinned).toHaveClass(/has-active/);
  const pillOnCurrent = () =>
    pinned.evaluate((list) => {
      const row = list.querySelector("a.current")!.getBoundingClientRect();
      const box = list.getBoundingClientRect();
      const pill = getComputedStyle(list, "::before");
      const move = new DOMMatrix(pill.transform);
      return (
        Math.abs(move.m41 - (row.left - box.left)) < 1 &&
        Math.abs(move.m42 - (row.top - box.top)) < 1 &&
        Math.abs(parseFloat(pill.width) - row.width) < 1
      );
    });
  // Icon display keeps the pins in one row; list display gives each its own line.
  await expect(pinned).toHaveClass(/is-strip/);
  expect((await pinned.boundingBox())!.height).toBeLessThanOrEqual(36);
  await expect.poll(pillOnCurrent).toBe(true);
  await page
    .getByRole("button", { name: "切換為條列顯示", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "切換為圖示顯示", exact: true }),
  ).toBeVisible();
  await expect(pinned).not.toHaveClass(/is-strip/);
  await expect(page.locator(".workspace-icons.is-list").first()).toBeVisible();
  await expect.poll(pillOnCurrent).toBe(true);
  await page.reload();
  await expect(pinned).not.toHaveClass(/is-strip/);
  await expect(page.locator(".workspace-icons.is-list").first()).toBeVisible();
  await expectViewportContained(page);
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
