import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  expectCompactSurfaces,
  expectViewportContained,
  chooseSelect,
  settleEntrance,
  openSettings,
} from "./fixtures";

test("personal settings preview, cancel, save and reload account preferences", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/settings");
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole("heading", { name: "外觀與閱讀", exact: true }),
  ).toBeVisible();
  await expectCompactSurfaces(page.locator(".settings-layout"));
  await expect(page.locator(".settings-card h2").first()).toHaveCSS(
    "font-size",
    "16px",
  );
  await page.locator(".reading-preview").scrollIntoViewIfNeeded();
  await settleEntrance(page);
  await chooseSelect(page, "主題", "深色");
  await chooseSelect(page, "對話字級", "20 px");
  await expect(page.locator(".reading-preview p")).toHaveCSS(
    "font-size",
    "20px",
  );
  expect(fixture.settings.readingFontSize).toBe(15);
  await page.getByRole("button", { name: "取消", exact: true }).click();
  await expect(page.locator(".reading-preview p")).toHaveCSS(
    "font-size",
    "15px",
  );
  await chooseSelect(page, "主題", "深色");
  await chooseSelect(page, "對話字級", "20 px");
  await page.getByRole("button", { name: "儲存變更" }).click();
  await expect(
    page
      .getByRole("dialog", { name: "個人設定", exact: true })
      .getByRole("status"),
  ).toContainText("已儲存");
  expect(fixture.settings.readingFontSize).toBe(20);
  const settings = page.getByRole("dialog", { name: "個人設定", exact: true });
  await settings.locator(".settings-main").evaluate((element) => {
    element.scrollTop = element.scrollHeight;
  });
  await expect(
    settings.getByRole("button", { name: "關閉設定", exact: true }),
  ).toBeInViewport();
  await expect(
    settings.getByRole("heading", { name: "外觀與閱讀", exact: true }),
  ).toBeInViewport();
  await settleEntrance(page);
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  await expect(
    page.getByRole("dialog", { name: "個人設定", exact: true }),
  ).not.toBeVisible();
  await page.reload();
  await openSettings(page);
  await expect(page.locator(".reading-preview p")).toHaveCSS(
    "font-size",
    "20px",
  );
  await page.getByRole("button", { name: "對話", exact: true }).click();
  await page.getByRole("switch", { name: "按 Enter 送出" }).click();
  await page.getByRole("button", { name: "儲存變更" }).click();
  await expect(
    page
      .getByRole("dialog", { name: "個人設定", exact: true })
      .getByRole("status"),
  ).toContainText("已儲存");
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  await expect(
    page.getByRole("dialog", { name: "個人設定", exact: true }),
  ).not.toBeVisible();
  const composer = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await page.getByRole("link", { name: "對話", exact: true }).click();
  await composer.click();
  await composer.fill("換行模式");
  await expect(
    page.getByRole("button", { name: "送出訊息", exact: true }),
  ).toBeEnabled();
  await composer.press("Enter");
  await expect(composer).toHaveValue("換行模式\n");
  expect(fixture.posts).toBe(0);
  await composer.press("Control+Enter");
  await expect(page.getByRole("table")).toBeVisible();
});

test("custom dropdown supports keyboard, light dismissal and mobile layout", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/settings");
  const theme = page.getByRole("combobox", { name: "主題", exact: true });
  await theme.focus();
  await theme.press("ArrowDown");
  await expect(theme).toHaveAttribute("aria-expanded", "true");
  await theme.press("End");
  await theme.press("Enter");
  await expect(theme).toContainText("深色");
  await expect(theme).toBeFocused();
  await theme.click();
  await theme.press("Escape");
  await expect(theme).toHaveAttribute("aria-expanded", "false");
  await theme.click();
  await page.getByRole("heading", { name: "外觀與閱讀", exact: true }).click();
  await expect(theme).toHaveAttribute("aria-expanded", "false");
  await page.setViewportSize({ width: 375, height: 812 });
  await expectCompactSurfaces(page.locator(".settings-layout"));
  await chooseSelect(page, "對話字級", "18 px");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await page.getByRole("searchbox", { name: "搜尋設定" }).fill("用量");
  await page.getByRole("button", { name: "使用狀況", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "使用狀況", exact: true }),
  ).toBeVisible();
});

test("每個設定分類共用緊湊卡片，閱讀偏好不放大操作介面", async ({ page }) => {
  const fixture = new ApiFixture();
  fixture.settings.readingFontSize = 20;
  await fixture.attach(page);
  await page.goto("/settings");
  const dialog = page.getByRole("dialog", { name: "個人設定", exact: true });
  const sections = [
    "外觀與閱讀",
    "對話",
    "通知",
    "資料與草稿",
    "帳號與權限",
    "使用狀況",
    "鍵盤快捷鍵",
  ];
  for (const width of [1440, 375]) {
    await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 });
    for (const section of sections) {
      await dialog.getByRole("button", { name: section, exact: true }).click();
      await expect(
        dialog.getByRole("heading", { name: section, exact: true }),
      ).toBeVisible();
      await expectCompactSurfaces(dialog.locator(".settings-layout"));
      await expect(dialog.locator(".settings-page-header h1")).toHaveCSS(
        "font-size",
        "24px",
      );
      await expectViewportContained(page);
      await settleEntrance(page);
    }
  }
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
  // While streaming, the long answer is split into several Markdown blocks.
  await expect(
    page.locator(".message:not(.user) .markdown").first(),
  ).toContainText("閱讀位置");
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
