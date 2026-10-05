import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  openSettings,
  chooseSelect,
  settleEntrance,
  expectViewportContained,
} from "./fixtures";

test("login copy follows the completed mark and fits standard desktop and mobile viewports", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  await expect(page.locator(".login-brand")).toHaveAttribute(
    "aria-hidden",
    "true",
  );
  await expect(page.locator(".login-brand .login-reveal")).toHaveCSS(
    "opacity",
    "0",
  );
  await page.getByRole("button", { name: "跳過標誌動畫", exact: true }).click();
  await expect(page.locator(".login-intro")).toHaveClass(/intro-ready/);
  await expect(page.locator(".login-brand .login-reveal")).toHaveCSS(
    "opacity",
    "1",
  );
  for (const viewport of [
    { width: 1366, height: 768 },
    { width: 1024, height: 640 },
    { width: 375, height: 667 },
    { width: 320, height: 568 },
  ]) {
    await page.setViewportSize(viewport);
    expect(
      await page.evaluate(
        () =>
          document.documentElement.scrollHeight <= innerHeight &&
          document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    const submit = await page
      .getByRole("button", { name: "登入工作區", exact: true })
      .boundingBox();
    expect(submit!.y + submit!.height).toBeLessThan(viewport.height);
  }
});

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
  await expect(page.getByRole("menuitem")).toHaveCount(1);
  await page.keyboard.press("Escape");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/refined-navigation.png",
    fullPage: true,
  });
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

test("settings stay over the current conversation, preserve scroll and draft, and support 12px with line height one", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.answer = "這是一段用來確認閱讀位置的長篇回覆。\n\n".repeat(60);
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await input.fill("開始閱讀");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.locator(".message:not(.user) .markdown")).toContainText(
    "閱讀位置",
  );
  await expect(
    page.getByRole("button", { name: "送出訊息", exact: true }),
  ).toBeDisabled();
  await input.fill("保留這份下一個問題的草稿");
  const viewport = page.locator(".conversation-viewport");
  await viewport.evaluate((el) => (el.scrollTop = 240));
  const position = await viewport.evaluate((el) => el.scrollTop),
    url = page.url();
  const dialog = await openSettings(page);
  expect(page.url()).toBe(url);
  await chooseSelect(page, "對話字級", "12 px");
  await chooseSelect(page, "正文行距", "1 倍");
  await dialog.getByRole("button", { name: "儲存變更", exact: true }).click();
  await expect(dialog.getByRole("status")).toContainText("已儲存");
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  await expect(input).toHaveValue("保留這份下一個問題的草稿");
  expect(page.url()).toBe(url);
  expect(fixture.posts).toBe(1);
  await expect(page.locator(".message:not(.user)")).toHaveCSS(
    "font-size",
    "12px",
  );
  // Font changes naturally alter document height; a plain open/close must preserve the resulting position exactly.
  const afterFont = await viewport.evaluate((el) => el.scrollTop);
  await openSettings(page);
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  expect(await viewport.evaluate((el) => el.scrollTop)).toBe(afterFont);
  expect(position).toBeGreaterThan(0);
});

test("unsaved settings can be discarded without losing the question draft", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("尚未送出的問題");
  const dialog = await openSettings(page);
  await chooseSelect(page, "主題", "深色");
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  const confirm = page.getByRole("dialog", {
    name: "放棄尚未儲存的設定？",
    exact: true,
  });
  await expect(confirm).toBeVisible();
  await confirm.getByRole("button", { name: "取消", exact: true }).click();
  await expect(dialog).toBeVisible();
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  await confirm.getByRole("button", { name: "放棄變更", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue("尚未送出的問題");
});

test("focused composer synchronizes edits and uses the same IME safe submission behavior", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const compact = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await compact.fill("準備長篇提問");
  await page.getByRole("button", { name: "展開訊息輸入", exact: true }).click();
  const dialog = page.getByRole("dialog", {
    name: "讓長篇提問也容易整理",
    exact: true,
  });
  const expanded = dialog.getByRole("textbox", {
    name: "放大的訊息輸入",
    exact: true,
  });
  await expect(expanded).toHaveValue("準備長篇提問");
  await expanded.fill("修改後的問題\n第二行");
  await expanded.dispatchEvent("compositionstart");
  await expanded.press("Enter");
  expect(fixture.posts).toBe(0);
  await expanded.dispatchEvent("compositionend");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/focused-composer.png",
    fullPage: true,
  });
  await dialog.getByRole("button", { name: "收合輸入區", exact: true }).click();
  await expect(compact).toHaveValue(/修改後的問題/);
  await page.getByRole("button", { name: "展開訊息輸入", exact: true }).click();
  await dialog.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.getByRole("table")).toBeVisible();
  expect(fixture.posts).toBe(1);
});

test("model lists remain single line, searchable and bounded with many long names", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  const models = Array.from({ length: 42 }, (_, i) => ({
    id: i === 0 ? "fixture:8b" : `model-${i}`,
    displayName: `企業模型 ${String(i).padStart(2, "0")} · 這是一個非常長且具有完整供應商說明的模型名稱`,
    contextTokens: 8192,
    maxOutputTokens: 2048,
    supportsImages: true,
    reasoningEfforts: [],
    defaultReasoningEffort: "auto",
  }));
  await page.route("**/api/v1/models", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        models,
        providerAvailable: true,
        notice: null,
        policy: fixture.modelPolicy,
      }),
    }),
  );
  await page.goto("/chat");
  await page.getByRole("combobox", { name: "選擇模型", exact: true }).click();
  const list = page.getByRole("listbox", { name: "選擇模型", exact: true });
  await expect(list.getByRole("option")).toHaveCount(42);
  await expect(list.locator("strong").first()).toHaveCSS(
    "white-space",
    "nowrap",
  );
  const box = await page.locator(".select-panel:popover-open").boundingBox();
  expect(box!.height).toBeLessThan(450);
  const search = page.getByRole("combobox", {
    name: "搜尋選擇模型",
    exact: true,
  });
  await search.fill("企業模型 41");
  await expect(list.getByRole("option")).toHaveCount(1);
  await search.press("Enter");
  await expect(
    page.getByRole("combobox", { name: "選擇模型", exact: true }),
  ).toContainText("企業模型 41");
});

test("pending AI replies have motion feedback that honors reduced motion", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.hold = true;
  await fixture.attach(page);
  await page.route("**/api/v1/runs/*/events*", (route) =>
    route.fulfill({
      contentType: "text/event-stream",
      body: "retry: 10000\n\n",
    }),
  );
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("等待回覆");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.locator(".thinking-indicator")).toBeVisible();
  await expect(page.locator(".thinking-spectrum i").first()).toHaveCSS(
    "animation-name",
    "spectrum-wave",
  );
  await page.emulateMedia({ reducedMotion: "reduce" });
  const duration = await page
    .locator(".thinking-spectrum i")
    .first()
    .evaluate((el) => getComputedStyle(el).animationDuration);
  expect(parseFloat(duration)).toBeLessThan(0.1);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/thinking-feedback.png",
    fullPage: true,
  });
});
