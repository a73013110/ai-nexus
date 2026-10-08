import { test, expect, type Page } from "@playwright/test";
import { KnowledgeFixture } from "./knowledge-fixture";
import {
  chooseSelect,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";

export async function previewImage(page: Page) {
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

test("attachment modals preserve the live conversation, draft, scroll and focus without starting image OCR", async ({
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
  await page.locator(".composer input[type=file][multiple]").setInputFiles({
    name: fileName,
    mimeType: "image/png",
    buffer: await previewImage(page),
  });
  await input.fill("請分析這張工作流程圖");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toHaveCount(2);
  const origin = page.url();
  await input.fill("關閉預覽後繼續的問題");
  const preview = page.getByRole("link", { name: "閱讀附件：" + fileName });
  await preview.scrollIntoViewIfNeeded();
  const scrollTop = await page
    .locator(".conversation-viewport")
    .evaluate((element) => element.scrollTop);
  const id = fixture.core.attachments[0].id;
  await expect(preview).toHaveAttribute("href", /returnTo=%2Fchat%2F/);
  await preview.click();
  const dialog = page.getByRole("dialog", { name: "檔案預覽", exact: true });
  await expect(dialog.getByRole("heading", { name: fileName })).toBeVisible();
  await expect(dialog.locator(".reader-original img")).toBeVisible();
  await expect(dialog.locator(".viewer-stage")).toHaveAttribute(
    "aria-busy",
    "false",
  );
  expect(fixture.attachmentDocuments.has(id)).toBe(false);
  await expect(page).toHaveURL(origin);
  const area = await dialog.locator(".viewer-stage").boundingBox();
  expect(area!.height).toBeGreaterThan(740);
  await chooseSelect(page, "圖片縮放", "200%");
  await expect(dialog.locator(".reader-original img")).toHaveCSS(
    "width",
    "1920px",
  );
  expect(
    await dialog
      .locator(".viewer-stage")
      .evaluate((element) => element.scrollWidth > element.clientWidth),
  ).toBe(true);
  await chooseSelect(page, "圖片縮放", "符合視窗");
  await dialog.getByRole("button", { name: "放大預覽視窗" }).click();
  await expect(dialog).toHaveClass(/reader-expanded/);
  await dialog.getByRole("button", { name: "還原預覽視窗" }).click();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/reader-modal-desktop.png",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  expect(
    (await dialog.getByRole("button", { name: "關閉檔案預覽" }).boundingBox())!
      .height,
  ).toBeGreaterThanOrEqual(44);
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/reader-modal-mobile.png",
  });
  await page.evaluate(() =>
    document.documentElement.setAttribute("data-theme", "dark"),
  );
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.screenshot({
    path: "artifacts/screenshots/reader-modal-dark.png",
  });
  await dialog.getByRole("button", { name: "辨識文字", exact: true }).click();
  await expect(dialog.locator(".reader-text")).toContainText(
    "文件、索引與答案",
  );
  expect(fixture.attachmentDocuments.has(id)).toBe(true);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(page).toHaveURL(origin);
  await expect(input).toHaveValue("關閉預覽後繼續的問題");
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
  await expect(dialog.locator(".reader-original img")).toBeVisible();
  await dialog.getByRole("button", { name: "關閉檔案預覽" }).click();
  await expect(input).toHaveValue("關閉預覽後繼續的問題");
  expect(errors).toEqual([]);
});

test("knowledge modals and direct reader links share PDF rendering and reject external return destinations", async ({
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
  const dialog = page.getByRole("dialog", { name: "檔案預覽", exact: true });
  await expect(dialog.locator("canvas")).toHaveAttribute("width", /[1-9]\d+/);
  await expect(page).toHaveURL(/\/knowledge$/);
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
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
    tab.getByRole("link", { name: "返回檔案庫", exact: true }),
  ).toHaveAttribute("href", "/files");
  await tab.close();
});
