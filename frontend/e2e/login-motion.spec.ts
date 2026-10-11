import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance } from "./fixtures";

test("the drawing lands on the wordmark N after a wide-screen and font-size change", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.clock.install({ time: new Date("2026-10-06T00:00:00Z") });
  await page.goto("/login");
  const stage = page.locator("nx-signal-mark .signal-stage");
  await expect(stage).toBeAttached();
  const now = await page.evaluate(() => Date.now());
  await page.clock.pauseAt(new Date(now + 1000));
  // Reset the drawing after startup, then sample it with the clock paused between steps.
  const skip = page.getByRole("button", { name: "跳過標誌動畫", exact: true });
  if (await skip.count()) await skip.dispatchEvent("click");
  await page.clock.runFor(100);
  await page
    .getByRole("button", { name: "重播標誌動畫", exact: true })
    .dispatchEvent("click");
  await page.clock.runFor(1000);
  // Mid-drawing it covers the outline left in the space above the copy, away from the wordmark.
  const [drawing, slot] = await Promise.all([
    stage.boundingBox(),
    page.locator(".login-signal-slot svg").boundingBox(),
  ]);
  expect(Math.abs(drawing!.width - slot!.width)).toBeLessThan(1);
  expect(Math.abs(drawing!.y - slot!.y)).toBeLessThan(1);
  await page.clock.runFor(1300);
  await page.setViewportSize({ width: 2560, height: 1080 });
  await page
    .locator(".login-brand")
    .evaluate((element) => (element.style.fontSize = "32px"));
  await page.clock.runFor(590);
  const [landing, mark] = await Promise.all([
    stage.boundingBox(),
    page.locator(".login-brand .brand-symbol").boundingBox(),
  ]);
  expect(Math.abs(landing!.x - mark!.x)).toBeLessThan(1);
  expect(Math.abs(landing!.y - mark!.y)).toBeLessThan(1);
  expect(Math.abs(landing!.width - mark!.width)).toBeLessThan(1);
  await page.clock.runFor(1000);
  await expect(
    page.getByRole("button", { name: "重播標誌動畫", exact: true }),
  ).toBeEnabled();
  await expect(stage).toBeHidden();
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
});

test("the sign-in mark keeps sign-in usable, can settle immediately and respects reduced motion", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.ldap = true;
  await fixture.attach(page);
  await page.goto("/login");
  const account = page.getByRole("textbox", { name: "AD 帳號", exact: true });
  await expect(account).toBeVisible();
  await account.fill("fixture");
  await page.locator("#login-password").fill("fixture-password");
  await expect(
    page.getByRole("button", { name: "登入工作區", exact: true }),
  ).toBeEnabled();
  await expect(page.locator("nx-signal-mark .signal-stage")).toBeVisible();
  await page.getByRole("button", { name: "跳過標誌動畫", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重播標誌動畫", exact: true }),
  ).toBeEnabled();
  await settleEntrance(page);
  await page.evaluate(() => (document.documentElement.dataset.theme = "dark"));
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
  await page.getByRole("button", { name: "登入工作區", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
});
