import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance } from "./fixtures";

test("Fourier identity keeps sign-in usable, can settle immediately and respects reduced motion", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  const account = page.getByRole("textbox", { name: "AD 帳號", exact: true });
  await expect(account).toBeVisible();
  await account.fill("fixture");
  await page.locator("#ad-password").fill("fixture-password");
  await expect(
    page.getByRole("button", { name: "登入工作台", exact: true }),
  ).toBeEnabled();
  const canvas = page.locator("nx-fourier-mark canvas");
  await expect(canvas).toHaveAttribute("width", /[1-9]\d+/);
  await page.getByRole("button", { name: "跳過標誌動畫", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重播標誌動畫", exact: true }),
  ).toBeEnabled();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/login-fourier-light.png",
    fullPage: true,
  });
  await page.evaluate(() => (document.documentElement.dataset.theme = "dark"));
  await page.screenshot({
    path: "artifacts/screenshots/login-fourier-dark.png",
    fullPage: true,
  });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await expect(
    page.getByRole("button", { name: "重播標誌動畫", exact: true }),
  ).toBeDisabled();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(account).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/login-fourier-mobile.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "登入工作台", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});
