import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance, chooseSelect } from "./fixtures";

test("composer exposes model, supported reasoning and keyboard-accessible context", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.hold = true;
  await fixture.attach(page);
  await page.goto("/chat");
  const model = page.getByRole("combobox", { name: "選擇模型" });
  await expect(
    page.locator(".composer").getByRole("combobox", { name: "選擇模型" }),
  ).toBeVisible();
  await expect(model).toContainText("本機測試模型");
  await expect(page.getByRole("combobox", { name: "思考強度" })).toContainText(
    "快速回應",
  );
  await chooseSelect(page, "思考強度", "深入思考");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("測試思考設定");
  const context = page.getByLabel(/上下文用量：/);
  await expect(context).toHaveAccessibleName(/預估/);
  await context.focus();
  await context.press("Enter");
  await expect(page.locator(".context-panel")).toContainText(
    "預留 2,048 tokens",
  );
  await page.keyboard.press("Escape");
  await expect(page.locator(".context-details")).not.toHaveAttribute("open");
  await expect(context).toBeFocused();
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect.poll(() => fixture.lastRequest?.reasoningEffort).toBe("high");
  await expect(page.getByText("這是已保存的部分回答。")).toBeVisible();
  await expect(page.locator(".signal-flow").first()).toHaveCSS(
    "animation-name",
    "signal-transit",
  );
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/active-inference.png",
    fullPage: true,
  });
});

test("locked hidden model has no selector or provider name in current or historical UI", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.modelPolicy = {
    allowModelSelection: false,
    showModelNames: false,
    defaultModelId: "model-1",
    maxInputCharacters: 12000,
  };
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByRole("combobox", { name: "選擇模型" })).toHaveCount(0);
  await expect(page.getByText("系統指定", { exact: true })).toBeVisible();
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("匿名模型測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  await expect(page.locator("body")).not.toContainText("fixture:8b");
  await expect(page.locator("body")).not.toContainText("本機測試模型");
  expect(fixture.lastRequest?.modelId).toBe("model-1");
});

test("chat access is resolved at login and a missing grant prevents submission", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.chatAccess = false;
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByRole("alert")).toContainText("沒有 AI 對話功能");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("權限測試");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  expect(fixture.posts).toBe(0);
});

test("compact conversation uses readable text and preserves over 70 percent of desktop height", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 768 });
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("版面檢查");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  const viewport = await page.locator(".conversation-viewport").boundingBox();
  expect(viewport!.height).toBeGreaterThanOrEqual(768 * 0.7);
  await expect(page.locator(".markdown")).toHaveCSS("font-size", "17px");
  await expect(page.locator(".topbar")).toHaveCSS("height", "52px");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/compact-desktop-chat.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 375, height: 812 });
  const close = page.getByRole("button", { name: "關閉對話導覽" });
  if (await close.isVisible()) await close.click();
  await page.getByLabel(/上下文用量：/).click();
  const panel = await page.locator(".context-panel").boundingBox();
  expect(panel!.x).toBeGreaterThanOrEqual(0);
  expect(panel!.x + panel!.width).toBeLessThanOrEqual(375);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await expect(page.locator(".markdown")).toHaveCSS("font-size", "17px");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/mobile-context.png",
    fullPage: true,
  });
});

test("signal motion respects reduced motion and Markdown export follows the visible branch", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.hold = true;
  fixture.preferences.reducedMotion = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("匯出測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByText("這是已保存的部分回答。")).toBeVisible();
  await expect(page.locator(".signal-flow").first()).toHaveCSS(
    "animation-name",
    "none",
  );
  await page.getByLabel("對話操作", { exact: true }).click();
  await expect(
    page.getByRole("button", { name: "匯出目前分支為 Markdown" }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "停止生成" }).click();
  await page.getByLabel("對話操作", { exact: true }).click();
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "匯出目前分支為 Markdown" }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/\.md$/);
  const stream = await file.createReadStream();
  let contents = "";
  for await (const chunk of stream!) contents += chunk.toString();
  expect(contents).toContain("匯出測試");
  expect(contents).toContain("這是已保存的部分回答。");
  expect(contents).not.toContain("fixture:8b");
});

test("context over budget blocks submission until the draft is shortened", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息" });
  await input.fill("中".repeat(2500));
  await expect(
    page.getByText("本次提問與附件超出 Context 預算，請縮短內容或減少附件。"),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  await input.fill("縮短後");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeEnabled();
  expect(fixture.posts).toBe(0);
});

test("200 percent text scaling keeps composer controls operable without page overflow", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page.evaluate(() => {
    document.documentElement.style.fontSize = "200%";
  });
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("放大文字測試");
  await expect(page.getByRole("combobox", { name: "思考強度" })).toBeVisible();
  const send = page.getByRole("button", { name: "送出訊息" });
  await expect(send).toBeEnabled();
  const box = await send.boundingBox();
  expect(box!.x + box!.width).toBeLessThanOrEqual(1280);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await send.click();
  await expect(page.getByRole("table")).toBeVisible();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/large-text-chat.png",
    fullPage: true,
  });
});
