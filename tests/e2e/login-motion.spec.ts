import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance } from "./fixtures";

test("the drawing docks into the wordmark N after a wide-screen and font-size change", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.clock.install({ time: new Date("2026-10-06T00:00:00Z") });
  await page.goto("/login");
  await expect(page.locator("nx-fourier-mark canvas")).toHaveAttribute(
    "width",
    /[1-9]\d+/,
  );
  const now = await page.evaluate(() => Date.now());
  await page.clock.pauseAt(new Date(now + 1000));
  // Reset the drawing after startup, then sample it with the clock paused between steps.
  const skip = page.getByRole("button", { name: "跳過標誌動畫", exact: true });
  if (await skip.count()) await skip.dispatchEvent("click");
  await page.clock.runFor(100);
  await page
    .getByRole("button", { name: "重播標誌動畫", exact: true })
    .dispatchEvent("click");
  await page.clock.runFor(3000);
  await page.setViewportSize({ width: 2560, height: 1080 });
  await page
    .locator(".login-brand")
    .evaluate((element) => (element.style.fontSize = "32px"));
  await page.clock.runFor(1192);
  const drawing = await page
    .locator("nx-fourier-mark canvas")
    .evaluate((element: HTMLCanvasElement) => {
      const pixels = element
        .getContext("2d")!
        .getImageData(0, 0, element.width, element.height).data;
      let left = element.width,
        right = 0,
        top = element.height,
        bottom = 0,
        count = 0;
      for (let y = 0; y < element.height; y++)
        for (let x = 0; x < element.width; x++) {
          if (pixels[(y * element.width + x) * 4 + 3] > 200) {
            left = Math.min(left, x);
            right = Math.max(right, x);
            top = Math.min(top, y);
            bottom = Math.max(bottom, y);
            count++;
          }
        }
      const bounds = element.getBoundingClientRect();
      return {
        count,
        x: bounds.x + (((left + right) / 2) * bounds.width) / element.width,
        y: bounds.y + (((top + bottom) / 2) * bounds.height) / element.height,
        width: ((right - left) * bounds.width) / element.width,
      };
    });
  const mark = await page.locator(".login-brand .brand-symbol").boundingBox();
  expect(drawing.count).toBeGreaterThan(0);
  expect(Math.abs(drawing.x - (mark!.x + mark!.width / 2))).toBeLessThan(2);
  expect(Math.abs(drawing.y - (mark!.y + mark!.height / 2))).toBeLessThan(2);
  expect(Math.abs(drawing.width - mark!.width * 0.96)).toBeLessThan(2);
  await page.clock.runFor(1000);
  await expect(
    page.getByRole("button", { name: "重播標誌動畫", exact: true }),
  ).toBeEnabled();
  await expect(page.locator(".login-brand .brand-symbol")).toHaveCSS(
    "opacity",
    "1",
  );
  const [copy, form] = await Promise.all([
    page.locator(".login-intro-copy").boundingBox(),
    page.locator(".login-content").boundingBox(),
  ]);
  expect(form!.x - (copy!.x + copy!.width)).toBeLessThan(180);
  await page.clock.resume();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/login-wordmark-wide.png",
  });
});

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
    page.getByRole("button", { name: "登入工作區", exact: true }),
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
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});
