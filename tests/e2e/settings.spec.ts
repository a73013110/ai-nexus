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
  await page.screenshot({
    path: "artifacts/screenshots/personal-settings-light.png",
  });
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
  await page.screenshot({
    path: "artifacts/screenshots/personal-settings-dark.png",
    fullPage: true,
  });
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
  await page.screenshot({
    path: "artifacts/screenshots/personal-settings-mobile.png",
    fullPage: true,
  });
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
    for (const [index, section] of sections.entries()) {
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
      await page.screenshot({
        path: `artifacts/screenshots/settings-section-${index}-${width}.png`,
      });
    }
  }
});
