import { test, expect, type Page, type Route } from "@playwright/test";
import {
  ApiFixture,
  expectViewportContained,
  productTour,
  settleEntrance,
} from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

// The tour picks its steps from what is visible when it starts, so start it on a loaded composer.
async function openChat(page: Page) {
  await page.goto("/work/chat");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toBeEditable();
}

for (const width of [1440, 375]) {
  test(`chat tour explains visible capabilities, replays and restores focus at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const knowledge = new KnowledgeFixture();
    const fixture = width === 1440 ? knowledge.core : new ApiFixture();
    if (width === 1440) {
      knowledge.seed();
      await knowledge.attach(page);
    } else await fixture.attach(page);
    await page.setViewportSize({ width, height: width === 375 ? 812 : 1000 });
    await openChat(page);
    if (width === 1440)
      await expect(page.locator(".knowledge-trigger")).toBeVisible();
    const launcher = page
      .locator(".topbar")
      .getByRole("button", { name: "操作導覽", exact: true });
    await expect(launcher).toBeEnabled();
    await launcher.click();
    const { popover: tour, title, progress, layer } = productTour(page);
    await expect(tour).toBeVisible();
    await expect(title).toHaveText("從這裡開始新的思路");
    await expect(progress).toHaveText(/1 \/ [56]/);
    await expect(tour).toHaveAttribute("role", "dialog");
    await expect(
      tour.getByRole("button", { name: "下一步", exact: true }),
    ).toBeFocused();
    // 導覽期間 Tab 留在說明框內。
    for (let press = 0; press < 3; press++) {
      await page.keyboard.press("Tab");
      await expect
        .poll(() =>
          tour.evaluate((card) => card.contains(document.activeElement)),
        )
        .toBe(true);
    }
    await page.keyboard.press("ArrowRight");
    await expect(title).toHaveText("把問題交給 AI");
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
    await expect(layer).toHaveCount(0);
    await expect(launcher).toBeFocused();
    await launcher.click();
    await expect(progress).toHaveText(/1 \/ [56]/);
    const total = Number((await progress.innerText()).split("/")[1]);
    for (let step = 2; step <= total; step++) {
      await tour.getByRole("button", { name: "下一步", exact: true }).click();
      await expect(progress).toHaveText(`${step} / ${total}`);
    }
    await expect(title).toHaveText("送出，然後一起完善");
    await tour.getByRole("button", { name: "開始使用", exact: true }).click();
    await expect(layer).toHaveCount(0);
    await expect(launcher).toBeFocused();
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
  await openChat(page);
  await page.locator(".topbar .tour-trigger").click();
  const { popover: tour, layer } = productTour(page);
  await expect(tour).toBeVisible();
  // 減少動態：說明框與聚光框直接到位，不播放進場與移動。
  await expect(tour).toHaveCSS("animation-duration", "0s");
  await expect(tour).toHaveCSS("transition-property", "none");
  await expect(page.locator(".tour-spot")).toHaveCSS("opacity", "1");
  await page.evaluate(() => {
    const link = document.querySelector<HTMLAnchorElement>(
      ".workspace-sidebar .brand",
    );
    link?.click();
  });
  await expect(page).toHaveURL(/\/work\/dashboard$/);
  await expect(layer).toHaveCount(0);
});

test("clicking the scrim ends the tour without touching the page", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await openChat(page);
  const launcher = page.locator(".topbar .tour-trigger");
  await launcher.click();
  const { popover: tour, layer } = productTour(page);
  await expect(tour).toBeVisible();
  await settleEntrance(page);
  // 點在遮罩上（頁面左下角），導覽結束、底下的元件不會收到點擊。
  const viewport = page.viewportSize()!;
  await page.mouse.click(4, viewport.height - 4);
  await expect(layer).toHaveCount(0);
  await expect(launcher).toBeFocused();
  expect(fixture.messages).toHaveLength(0);
});

test("a pending tour never opens after navigation and can be started again", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await openChat(page);
  // 導覽層是延遲載入的 chunk；攔住開啟導覽時的第一個 script 請求，模擬慢速網路。
  let pending: Route | undefined;
  await page.route("**/*.js", (route) => {
    if (pending) return route.continue();
    pending = route;
  });
  const launcher = page.locator(".topbar .tour-trigger");
  await launcher.click();
  await expect(launcher).toBeDisabled();
  await expect.poll(() => Boolean(pending)).toBe(true);
  await page.locator(".workspace-sidebar .brand").click();
  await expect(page).toHaveURL(/\/work\/dashboard$/);
  const loaded = page.waitForResponse(pending!.request().url());
  await pending!.continue();
  await loaded;
  await settleEntrance(page);
  const { popover: tour, layer } = productTour(page);
  await expect(layer).toHaveCount(0);
  await page
    .locator(".workspace-sidebar")
    .getByRole("link", { name: "對話", exact: true })
    .first()
    .click();
  await expect(launcher).toBeEnabled();
  await launcher.click();
  await expect(tour).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(layer).toHaveCount(0);
});
