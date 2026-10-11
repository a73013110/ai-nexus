import { test, expect, type Route } from "@playwright/test";
import {
  ApiFixture,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

for (const width of [1440, 375]) {
  test(`chat tour explains visible capabilities, replays and restores focus at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    const styles: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("request", (request) => {
      if (request.url().includes("/vendor/driver/")) styles.push(request.url());
    });
    const knowledge = new KnowledgeFixture();
    const fixture = width === 1440 ? knowledge.core : new ApiFixture();
    if (width === 1440) {
      knowledge.seed();
      await knowledge.attach(page);
    } else await fixture.attach(page);
    await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 });
    await page.goto("/chat");
    const launcher = page
      .locator(".topbar")
      .getByRole("button", { name: "操作導覽", exact: true });
    await expect(launcher).toBeEnabled();
    expect(styles).toHaveLength(0);
    await launcher.click();
    const tour = page.locator(".driver-popover.nx-tour");
    await expect(tour).toBeVisible();
    await expect(tour.locator(".driver-popover-title")).toHaveText(
      "從這裡開始新的思路",
    );
    await expect(tour.locator(".driver-popover-progress-text")).toHaveText(
      /1 \/ [56]/,
    );
    await expect(tour).toHaveAttribute("role", "dialog");
    await tour.getByRole("button", { name: "下一步", exact: true }).click();
    await expect(tour.locator(".driver-popover-title")).toHaveText(
      "把問題交給 AI",
    );
    await settleEntrance(page);
    const bounds = (await tour.boundingBox())!;
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
    expect(bounds.y + bounds.height).toBeLessThanOrEqual(
      width === 375 ? 812 : 1000,
    );
    await page.keyboard.press("Control+k");
    await expect(page.locator("dialog[open]")).toHaveCount(0);
    await page.keyboard.press("Escape");
    await expect(tour).toHaveCount(0);
    await expect(launcher).toBeFocused();
    await launcher.click();
    await expect(tour.locator(".driver-popover-progress-text")).toHaveText(
      /1 \/ [56]/,
    );
    const total = Number(
      (await tour.locator(".driver-popover-progress-text").innerText()).split(
        "/",
      )[1],
    );
    for (let step = 2; step <= total; step++) {
      await tour.getByRole("button", { name: "下一步", exact: true }).click();
      await expect(tour.locator(".driver-popover-progress-text")).toHaveText(
        `${step} / ${total}`,
      );
    }
    await expect(tour.locator(".driver-popover-title")).toHaveText(
      "送出，然後一起完善",
    );
    await tour.getByRole("button", { name: "開始使用", exact: true }).click();
    await expect(tour).toHaveCount(0);
    await expect(launcher).toBeFocused();
    expect(styles).toHaveLength(1);
    expect(fixture.messages).toHaveLength(0);
    expect(errors).toEqual([]);
    await expectViewportContained(page);
  });
}

test("tour respects reduced motion and cleans up overlays when its route closes", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.emulateMedia({ reducedMotion: "reduce", colorScheme: "dark" });
  await page.goto("/chat");
  await page.locator(".topbar .tour-trigger").click();
  const tour = page.locator(".driver-popover.nx-tour");
  await expect(tour).toBeVisible();
  await expect(tour).toHaveCSS("animation-name", "none");
  await expect(tour).toHaveClass(/tour-reduced-motion/);
  await page.evaluate(() => {
    const link = document.querySelector<HTMLAnchorElement>(
      ".workspace-sidebar .brand",
    );
    link?.click();
  });
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.locator(".driver-popover, .driver-overlay")).toHaveCount(0);
  await expect(page.locator("body")).not.toHaveClass(/driver-active/);
});

test("a pending tour never opens after navigation and can be started again", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  let pending: Route | undefined;
  await page.route("**/vendor/driver/driver.css", (route) => {
    pending = route;
  });
  await page.goto("/chat");
  const launcher = page.locator(".topbar .tour-trigger");
  await launcher.click();
  await expect(launcher).toBeDisabled();
  await expect.poll(() => Boolean(pending)).toBe(true);
  await page.locator(".workspace-sidebar .brand").click();
  await expect(page).toHaveURL(/\/dashboard$/);
  await pending!.continue();
  await expect
    .poll(() =>
      page
        .locator('link[href*="vendor/driver"]')
        .evaluate((link: HTMLLinkElement) => Boolean(link.sheet)),
    )
    .toBe(true);
  await expect(page.locator(".driver-overlay")).toHaveCount(0);
  await page
    .locator(".workspace-sidebar")
    .getByRole("link", { name: "對話", exact: true })
    .click();
  await expect(launcher).toBeEnabled();
  await launcher.click();
  await expect(page.locator(".driver-popover.nx-tour")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator(".driver-overlay")).toHaveCount(0);
});
