import { test, expect, type Page } from "@playwright/test";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  openSettings,
  richAnswer,
  settleEntrance,
} from "./fixtures";

async function alignedChat(page: Page, expectedWidth?: number) {
  await expect(page.locator("form.composer")).toBeVisible();
  await expect(
    page.locator(".message-list, .chat-start").first(),
  ).toBeVisible();
  await expect
    .poll(() =>
      page.evaluate(() => {
        const form = document.querySelector("form.composer");
        const primary = document.querySelector(".message-list, .chat-start");
        const footer = document.querySelector(".composer-hint");
        if (!form || !primary || !footer) return Infinity;
        const composer = form.getBoundingClientRect();
        const content = primary.getBoundingClientRect();
        const hint = footer.getBoundingClientRect();
        const suggestions = document
          .querySelector(".chat-start-suggestions")
          ?.getBoundingClientRect();
        return Math.max(
          Math.abs(composer.left - content.left),
          Math.abs(composer.width - content.width),
          Math.abs(hint.left - composer.left - 4),
          Math.abs(hint.right - composer.right + 4),
          suggestions ? Math.abs(suggestions.left - composer.left) : 0,
          suggestions ? Math.abs(suggestions.width - composer.width) : 0,
        );
      }),
    )
    .toBeLessThan(1);
  if (expectedWidth !== undefined)
    await expect
      .poll(
        async () => (await page.locator("form.composer").boundingBox())!.width,
      )
      .toBeCloseTo(expectedWidth, 0);
  await expectViewportContained(page);
}

async function saveWidth(page: Page, label: string) {
  const dialog = await openSettings(page);
  await chooseSelect(page, "內容寬度", label);
  await dialog.getByRole("button", { name: "儲存變更", exact: true }).click();
  await expect(dialog.getByRole("status")).toContainText("已儲存");
  await dialog.getByRole("button", { name: "關閉設定", exact: true }).click();
  await expect(dialog).not.toBeVisible();
}

test("content width aligns messages, drafts, attachments and hints through preview, cancel, save and viewport changes", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.answer = `${richAnswer}\n\n`.repeat(8);
  await fixture.attach(page);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await input.fill("請整理完整的工作紀錄");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeEnabled();
  const draft = "這份草稿與附件必須保留，寬度設定只調整版面。";
  await input.fill(draft);
  await page.locator(".composer input[type=file]").setInputFiles({
    name: "寬度驗證.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("寬度設定的附件驗證", "utf8"),
  });
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
  await alignedChat(page, 832);

  const dialog = await openSettings(page);
  await chooseSelect(page, "內容寬度", "專注 · 窄");
  await alignedChat(page, 704);
  expect(fixture.settings.readingWidth).toBe("standard");
  await dialog.getByRole("button", { name: "取消", exact: true }).click();
  await alignedChat(page, 832);
  await dialog.getByRole("button", { name: "關閉設定", exact: true }).click();
  await expect(input).toHaveValue(draft);

  for (const [value, label, width] of [
    ["narrow", "專注 · 窄", 704],
    ["standard", "標準", 832],
    ["wide", "寬廣", 1024],
  ] as const) {
    await saveWidth(page, label);
    expect(fixture.settings.readingWidth).toBe(value);
    await alignedChat(page, width);
    await expect(input).toHaveValue(draft);
    const attachment = (await page
      .locator(".composer .attachment-card")
      .boundingBox())!;
    const form = (await page.locator("form.composer").boundingBox())!;
    expect(attachment.x).toBeGreaterThanOrEqual(form.x);
    expect(attachment.x + attachment.width).toBeLessThanOrEqual(
      form.x + form.width,
    );
    await settleEntrance(page);
    await page.screenshot({
      path: `artifacts/screenshots/chat-width-${value}.png`,
    });
  }
  await page.reload();
  await alignedChat(page, 1024);
  await expect(page.getByRole("table")).toHaveCount(8);
  await expect(input).toHaveValue(draft);
  await expect(page.locator(".composer .attachment-card")).toHaveCount(1);
  await page.getByRole("button", { name: "展開訊息輸入", exact: true }).click();
  const editor = page.getByRole("textbox", {
    name: "放大的訊息輸入",
    exact: true,
  });
  await expect(editor).toHaveValue(draft);
  await settleEntrance(page);
  expect((await editor.boundingBox())!.width).toBeCloseTo(1024, 0);
  await page.keyboard.press("Escape");
  await expect(input).toHaveValue(draft);

  for (const width of [960, 375]) {
    await page.setViewportSize({ width, height: 812 });
    await alignedChat(page);
    if (width === 375) {
      const card = page.locator(".composer .attachment-card");
      expect((await card.boundingBox())!.height).toBeLessThan(80);
      const cardBounds = (await card.boundingBox())!;
      const expandBounds = (await page
        .getByRole("button", { name: "展開訊息輸入", exact: true })
        .boundingBox())!;
      expect(expandBounds.y).toBeGreaterThanOrEqual(
        cardBounds.y + cardBounds.height,
      );
      expect(
        (await card.locator(".attachment-copy").boundingBox())!.width,
      ).toBeGreaterThanOrEqual(64);
      for (const control of [
        card.getByRole("link", { name: "下載附件：寬度驗證.txt", exact: true }),
        card.getByRole("button", {
          name: "移除附件：寬度驗證.txt",
          exact: true,
        }),
      ]) {
        await expect(control).toBeInViewport();
        expect((await control.boundingBox())!.width).toBeGreaterThanOrEqual(44);
      }
    }
    await page.locator(".conversation-viewport").evaluate((element) => {
      element.scrollTop = 120;
    });
    const latest = page.getByRole("button", { name: "回到最新", exact: true });
    await expect(latest).toBeInViewport();
    await latest.click();
    await expect(latest).not.toBeVisible();
    await expect(input).toHaveValue(draft);
    await settleEntrance(page);
    await page.screenshot({
      path: `artifacts/screenshots/chat-width-${width}.png`,
    });
  }
});

test("new conversations share the selected column width and narrow screens keep every width mode usable", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  for (const [label, width] of [
    ["專注 · 窄", 704],
    ["標準", 832],
    ["寬廣", 1024],
  ] as const) {
    await saveWidth(page, label);
    await alignedChat(page, width);
  }
  await page.locator(".workspace-sidebar .sidebar-toggle").click();
  await alignedChat(page, 1024);
  await page.setViewportSize({ width: 375, height: 812 });
  const widths: number[] = [];
  for (const label of ["專注 · 窄", "標準", "寬廣"]) {
    await saveWidth(page, label);
    await alignedChat(page);
    widths.push((await page.locator("form.composer").boundingBox())!.width);
    await expect(
      page.getByRole("button", { name: "送出訊息", exact: true }),
    ).toBeInViewport();
  }
  expect(Math.max(...widths) - Math.min(...widths)).toBeLessThan(1);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/chat-width-empty-mobile.png",
  });
});
