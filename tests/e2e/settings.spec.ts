import { test, expect } from "@playwright/test";
import { ApiFixture, chooseSelect, settleEntrance } from "./fixtures";

test("personal settings preview, cancel, save and reload account preferences", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/settings");
  await expect(
    page.getByRole("heading", { name: "外觀與閱讀", exact: true }),
  ).toBeVisible();
  await chooseSelect(page, "主題", "深色");
  await chooseSelect(page, "對話字級", "20 px");
  await expect(page.locator(".reading-preview p")).toHaveCSS(
    "font-size",
    "20px",
  );
  expect(fixture.settings.readingFontSize).toBe(17);
  await page.getByRole("button", { name: "取消", exact: true }).click();
  await expect(page.locator(".reading-preview p")).toHaveCSS(
    "font-size",
    "17px",
  );
  await chooseSelect(page, "主題", "深色");
  await chooseSelect(page, "對話字級", "20 px");
  await page.getByRole("button", { name: "儲存變更" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  expect(fixture.settings.readingFontSize).toBe(20);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/personal-settings-dark.png",
    fullPage: true,
  });
  await page.reload();
  await expect(page.locator(".reading-preview p")).toHaveCSS(
    "font-size",
    "20px",
  );
  await page.getByRole("button", { name: "對話", exact: true }).click();
  await page.getByRole("switch", { name: "按 Enter 送出" }).click();
  await page.getByRole("button", { name: "儲存變更" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  await page.getByRole("link", { name: "返回工作台" }).click();
  const composer = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await composer.fill("換行模式");
  await composer.press("Enter");
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
