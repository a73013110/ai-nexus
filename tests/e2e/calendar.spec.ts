import { test, expect } from "@playwright/test";
import { ApiFixture, expectViewportContained } from "./fixtures";

test.use({ timezoneId: "America/New_York" });

test("日曆月份使用共用選單，巢狀浮層支援鍵盤、取消和手機觸控", async ({
  page,
}) => {
  const api = new ApiFixture();
  api.adminAccess = true;
  await api.attach(page);
  await page.goto("/design");
  const value = page.getByLabel("西元日期時間", { exact: true });
  const original = await value.inputValue();
  await page
    .getByRole("button", { name: "西元日期時間選擇日期", exact: true })
    .click();
  const calendar = page.getByRole("dialog", {
    name: "西元日期時間日期選擇",
    exact: true,
  });
  const month = calendar.getByRole("combobox", { name: "月份", exact: true });
  const options = calendar.getByRole("listbox", { name: "月份", exact: true });
  await expect(month).toContainText("10 月");
  await month.click();
  await expect(calendar).toBeVisible();
  await expect(options.getByRole("option")).toHaveCount(12);
  await options.getByRole("option", { name: "2 月", exact: true }).click();
  await expect(month).toContainText("2 月");
  await expect(value).toHaveValue(original);
  await month.press("ArrowDown");
  await month.press("End");
  await month.press("Enter");
  await expect(month).toContainText("12 月");
  await month.click();
  await month.press("Escape");
  await expect(options).not.toBeVisible();
  await expect(calendar).toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(month).toHaveCSS("font-size", "16px");
  expect((await month.boundingBox())!.height).toBeGreaterThanOrEqual(44);
  expect(
    (await calendar
      .getByRole("button", { name: "上個月", exact: true })
      .boundingBox())!.height,
  ).toBeGreaterThanOrEqual(44);
  const label = month.locator("span");
  await expect
    .poll(() => label.evaluate((el) => el.scrollWidth <= el.clientWidth))
    .toBe(true);
  await month.click();
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/calendar-month-shared-mobile.png",
    animations: "disabled",
  });
  await options.getByRole("option", { name: "10 月", exact: true }).click();
  await calendar.getByRole("button", { name: "取消", exact: true }).click();
  await expect(value).toHaveValue(original);
  await expect(
    page.getByRole("button", { name: "西元日期時間選擇日期", exact: true }),
  ).toBeFocused();
});

test("共用日曆的民國／西元值一致，閏日與取消有正確行為", async ({ page }) => {
  const api = new ApiFixture();
  api.adminAccess = true;
  await api.attach(page);
  await page.goto("/design");
  const gregorian = page.getByLabel("西元日期時間", { exact: true });
  const roc = page.getByLabel("民國日期時間", { exact: true });
  await expect(gregorian).toHaveValue("2026/10/08 14:30:45");
  await expect(roc).toHaveValue("民國 115/10/08 14:30:45");
  await roc.fill("113/02/29 23:59:59");
  await roc.blur();
  await expect(roc).toHaveValue("民國 113/02/29 23:59:59");
  await expect(gregorian).toHaveValue("2024/02/29 23:59:59");
  await page
    .getByRole("button", { name: "民國日期時間選擇日期", exact: true })
    .click();
  const calendar = page.getByRole("dialog", {
    name: "民國日期時間日期選擇",
    exact: true,
  });
  await expect(calendar.getByLabel("民國年", { exact: true })).toHaveValue(
    "113",
  );
  await calendar.getByLabel("民國年", { exact: true }).fill("113");
  await page.keyboard.press("Enter");
  await expect(calendar).toBeVisible();
  await page.keyboard.press("PageDown");
  await expect(
    calendar.getByRole("button", { name: "民國 113/03/29", exact: true }),
  ).toBeFocused();
  await calendar.getByRole("button", { name: "取消", exact: true }).click();
  await expect(roc).toHaveValue("民國 113/02/29 23:59:59");
  await roc.fill("114/02/29 23:59:59");
  await roc.blur();
  await expect(roc).toHaveAttribute("aria-invalid", "true");
  await roc.fill("115/10/08 14:30:45");
  await roc.blur();
  await page.setViewportSize({ width: 375, height: 900 });
  await page
    .getByRole("button", { name: "民國日期時間選擇日期", exact: true })
    .click();
  await expect(calendar).toBeVisible();
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/calendar-roc-mobile.png",
    animations: "disabled",
  });
  const bounds = await calendar.boundingBox();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(375);
  await calendar.getByLabel("秒", { exact: true }).fill("99");
  await expect(
    calendar.getByRole("button", { name: "套用", exact: true }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(calendar).not.toBeVisible();
});
