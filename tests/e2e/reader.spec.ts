import { test, expect, type Page } from "@playwright/test";
import { KnowledgeFixture } from "./knowledge-fixture";
import { expectViewportContained, settleEntrance } from "./fixtures";

async function previewImage(page: Page) {
  const data = await page.evaluate(() => {
    const canvas = document.createElement("canvas");
    canvas.width = 960;
    canvas.height = 600;
    const ctx = canvas.getContext("2d")!;
    ctx.fillStyle = "#eff4f5";
    ctx.fillRect(0, 0, 960, 600);
    ctx.fillStyle = "#243f4f";
    ctx.font = "36px sans-serif";
    ctx.fillText("From information to answers", 60, 95);
    ["DOCUMENT", "INDEX", "ANSWER"].forEach((label, index) => {
      const x = 60 + index * 300;
      ctx.fillStyle = "#ffffff";
      ctx.beginPath();
      ctx.roundRect(x, 230, 240, 160, 16);
      ctx.fill();
      ctx.strokeStyle = "#cad9dc";
      ctx.stroke();
      ctx.fillStyle = "#236f77";
      ctx.font = "20px sans-serif";
      ctx.fillText(label, x + 24, 290);
      ctx.fillStyle = "#243f4f";
      ctx.font = "40px sans-serif";
      ctx.fillText(String(index + 1).padStart(2, "0"), x + 24, 350);
    });
    return canvas.toDataURL("image/png").split(",")[1];
  });
  return Buffer.from(data, "base64");
}

test("attachment previews keep the exact conversation, draft, scroll and browser Back behavior", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.core.answer +=
    "\n\n" +
    Array.from(
      { length: 45 },
      (_, i) => `工作流程 ${i + 1}：保留來源並逐步核對答案。`,
    ).join("\n\n");
  await fixture.attach(page);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await input.fill("先閱讀這段較長的對話");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeVisible();
  const fileName = "工作流程與來源核對-文件索引與AI答案.png";
  const chooser = page.waitForEvent("filechooser");
  await page
    .getByRole("button", { name: "加入文件或圖片", exact: true })
    .click();
  await (
    await chooser
  ).setFiles({
    name: fileName,
    mimeType: "image/png",
    buffer: await previewImage(page),
  });
  await input.fill("請分析這張工作流程圖");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toHaveCount(2);
  await expect(page).toHaveURL(/\/chat\/[^/?]+$/);
  const origin = page.url();
  await input.fill("回來後繼續的問題");
  const preview = page.getByRole("link", { name: "閱讀附件：" + fileName });
  await preview.scrollIntoViewIfNeeded();
  await preview.hover();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/attachment-hover-refined.png",
  });
  const scrollTop = await page
    .locator(".conversation-viewport")
    .evaluate((element) => element.scrollTop);
  expect(scrollTop).toBeGreaterThan(200);
  await expect(preview).toHaveAttribute("href", /returnTo=%2Fchat%2F/);
  await preview.click();
  await expect(page.getByRole("heading", { name: fileName })).toBeVisible();
  await expect(page.locator(".reader-original img")).toBeVisible();
  await expect(page.locator(".reader-original")).toHaveAttribute(
    "aria-busy",
    "false",
  );
  await expect(
    page.getByRole("link", { name: "返回對話", exact: true }),
  ).toHaveAttribute("href", new URL(origin).pathname);
  await page.reload();
  await expect(page.locator(".reader-original img")).toBeVisible();
  await page.getByRole("button", { name: "擷取文字", exact: true }).click();
  await expect(page.locator(".reader-text")).toContainText("文件、索引與答案");
  await page.getByRole("button", { name: "原始頁面", exact: true }).click();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/reader-image-refined.png",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/reader-image-mobile-refined.png",
  });
  await page.evaluate(() =>
    document.documentElement.setAttribute("data-theme", "dark"),
  );
  await page.emulateMedia({ reducedMotion: "reduce" });
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/reader-image-dark-refined.png",
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.getByRole("link", { name: "返回對話", exact: true }).click();
  await expect(page).toHaveURL(origin);
  await expect(input).toHaveValue("回來後繼續的問題");
  await expect
    .poll(async () =>
      Math.abs(
        (await page
          .locator(".conversation-viewport")
          .evaluate((element) => element.scrollTop)) - scrollTop,
      ),
    )
    .toBeLessThan(4);
  await expect(preview).toBeFocused();
  await preview.click();
  await expect(page.locator(".reader-original img")).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(origin);
  await expect(input).toHaveValue("回來後繼續的問題");
  await expect
    .poll(async () =>
      Math.abs(
        (await page
          .locator(".conversation-viewport")
          .evaluate((element) => element.scrollTop)) - scrollTop,
      ),
    )
    .toBeLessThan(4);
  expect(errors).toEqual([]);
});

test("knowledge readers retain their origin in new tabs and reject external return destinations", async ({
  page,
  context,
}) => {
  const fixture = new KnowledgeFixture(),
    doc = fixture.seed();
  await fixture.attach(page);
  await page.goto("/knowledge");
  const link = page.getByRole("link", { name: /差旅費用申請.pdf/ }).first();
  const href = (await link.getAttribute("href"))!;
  await link.click();
  await expect(page.locator("canvas")).toHaveAttribute("width", /[1-9]\d+/);
  await page.getByRole("link", { name: "返回知識庫", exact: true }).click();
  await expect(page).toHaveURL(/\/knowledge$/);
  const tab = await context.newPage();
  await fixture.attach(tab);
  await tab.goto(href);
  await expect(tab.locator("canvas")).toHaveAttribute("width", /[1-9]\d+/);
  await tab.getByRole("link", { name: "返回知識庫", exact: true }).click();
  await expect(tab).toHaveURL(/\/knowledge$/);
  await tab.goto(
    `/reader/${doc.id}?returnTo=${encodeURIComponent("https://evil.invalid")}`,
  );
  await expect(
    tab.getByRole("link", { name: "返回知識庫", exact: true }),
  ).toHaveAttribute("href", "/knowledge");
  await tab.close();
});
