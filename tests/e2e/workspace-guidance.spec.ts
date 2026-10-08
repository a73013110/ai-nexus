import { test, expect, type Route } from "@playwright/test";
import { KnowledgeFixture } from "./knowledge-fixture";
import {
  ApiFixture,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";

test("mobile navigation uses the full viewport, traps focus and preserves desktop density", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  await sidebar.locator(".sidebar-toggle").click();
  expect((await sidebar.boundingBox())!.width).toBe(52);
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(sidebar).not.toBeVisible();
  expect((await page.locator("main").boundingBox())!.width).toBe(375);
  expect((await page.locator("main").boundingBox())!.y).toBe(0);
  expect((await page.locator("main").boundingBox())!.height).toBe(812);
  const opener = page.getByRole("button", { name: "展開側欄", exact: true });
  await opener.click();
  await expect(sidebar).toHaveAttribute("aria-modal", "true");
  await expect(page.locator("main")).toHaveAttribute("inert", "");
  await expect(sidebar.locator(".sidebar-toggle")).toBeFocused();
  expect((await page.locator("main").boundingBox())!.width).toBe(375);
  await sidebar
    .getByRole("button", { name: "登入者選單", exact: true })
    .focus();
  await page.keyboard.press("Tab");
  await expect(sidebar.locator(".workspace-sidebar-heading a")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(opener).toBeFocused();
  await expect(page.locator("main")).not.toHaveAttribute("inert", "");
  await opener.click();
  await sidebar.locator(".workspace-backdrop").click();
  await expect(opener).toBeFocused();
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/chat-full-width-mobile.png",
    animations: "disabled",
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await expect(sidebar).toHaveClass(/is-compact/);
  expect((await sidebar.boundingBox())!.width).toBe(52);
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
  await sidebar.getByRole("link", { name: "知識庫", exact: true }).click();
  await expect(page).toHaveURL(/\/knowledge$/);
  await expect(sidebar).not.toBeVisible();
  await expect(page.locator(".feature-main")).not.toHaveAttribute("inert", "");
  await expectViewportContained(page);
});

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
    await page.screenshot({
      path: `artifacts/screenshots/chat-tour-${width}.png`,
      animations: "disabled",
    });
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
  await page.screenshot({
    path: "artifacts/screenshots/chat-tour-dark.png",
    animations: "disabled",
  });
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
});

test("global problem notices are compact, preserve safe copy and release viewport height", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.route("**/api/client-issues", (route) =>
    route.fulfill({ status: 503, body: "{}" }),
  );
  await page.goto("/chat");
  await page.evaluate(() => {
    setTimeout(() => {
      throw new Error("private diagnostic details must stay hidden");
    });
  });
  const notice = page.locator("nx-unhandled-issue nx-notice");
  await expect(notice).toBeVisible();
  await expect(notice).toHaveAttribute("role", "alert");
  await expect(notice).not.toContainText("private diagnostic");
  expect((await notice.boundingBox())!.height).toBeLessThanOrEqual(40);
  await notice
    .getByRole("button", { name: "複製問題查證代碼", exact: true })
    .click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as { __copied: string }).__copied),
    )
    .toMatch(/^LOCAL-[A-F0-9]{16}$/);
  await page.screenshot({
    path: "artifacts/screenshots/notice-compact-desktop.png",
    animations: "disabled",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  expect((await notice.boundingBox())!.height).toBeLessThan(150);
  await page.screenshot({
    path: "artifacts/screenshots/notice-compact-mobile.png",
    animations: "disabled",
  });
  await notice.getByRole("button", { name: "關閉提示", exact: true }).click();
  await expect(notice).toHaveCount(0);
  await expect
    .poll(() =>
      page.evaluate(() =>
        document.documentElement.style.getPropertyValue(
          "--global-issue-height",
        ),
      ),
    )
    .toBe("");
  expect((await page.locator(".workbench").boundingBox())!.height).toBe(812);
});
