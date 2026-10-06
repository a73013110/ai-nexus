import { test, expect } from "@playwright/test";
import { ApiFixture, settleEntrance } from "./fixtures";

async function conversation(page: import("@playwright/test").Page) {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("本週會議摘要");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeEnabled();
  return fixture;
}

test("header renames in place by double click and keyboard, preserving Escape cancellation", async ({
  page,
}) => {
  const fixture = await conversation(page);
  await page
    .getByRole("heading", { name: "本週會議摘要", exact: true })
    .dblclick();
  const field = page.getByRole("textbox", { name: "目前對話名稱" });
  await field.fill("專案週報");
  await field.press("Enter");
  await expect(
    page.getByRole("heading", { name: "專案週報", exact: true }),
  ).toBeVisible();
  expect(fixture.conversations[0].title).toBe("專案週報");
  await page.getByRole("heading", { name: "專案週報", exact: true }).focus();
  await page.keyboard.press("F2");
  await field.fill("不要保存");
  await field.press("Escape");
  await expect(
    page.getByRole("heading", { name: "專案週報", exact: true }),
  ).toBeVisible();
  expect(fixture.conversations[0].title).toBe("專案週報");
  await page.getByRole("button", { name: "修改目前對話名稱" }).click();
  await field.fill("點擊外面自動儲存");
  await page.getByRole("textbox", { name: "傳送訊息", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "點擊外面自動儲存", exact: true }),
  ).toBeVisible();
});

test("user questions and AI answers have distinct roles and spatial alignment", async ({
  page,
}) => {
  await conversation(page);
  const user = page.getByRole("article", { name: "你的提問" });
  const ai = page.getByRole("article", { name: "AI 回覆" });
  await expect(user).toContainText("你的提問");
  await expect(ai.locator(".assistant-label")).toHaveText("AI 回覆");
  const positions = await page.evaluate(() => {
    const user = document.querySelector<HTMLElement>(".message.user")!;
    const ai = document.querySelector<HTMLElement>(".message:not(.user)")!;
    return {
      user: user.getBoundingClientRect().left,
      ai: ai.getBoundingClientRect().left,
    };
  });
  expect(positions.user).toBeGreaterThan(positions.ai + 30);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/chat-roles.png",
    fullPage: true,
  });
});

test("conversation minimap previews a turn and jumps; mobile exposes a direct directory", async ({
  page,
}) => {
  await conversation(page);
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("接著整理明天的工作");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toHaveCount(2);
  const rail = page.getByRole("navigation", { name: "對話定位" });
  await expect(rail.locator(".outline-count")).toContainText("/2");
  const first = rail.getByRole("button", { name: /跳至第 1 輪/ });
  await first.hover();
  await expect(rail.locator(".outline-preview")).toContainText("本週會議摘要");
  const card = await rail.locator(".outline-preview").boundingBox();
  const body = await page.locator(".conversation-body").boundingBox();
  const tick = await first.boundingBox();
  expect(card!.y).toBeGreaterThanOrEqual(body!.y);
  expect(card!.y + card!.height).toBeLessThanOrEqual(body!.y + body!.height);
  expect(card!.x + card!.width).toBeLessThan(tick!.x + 1);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/conversation-minimap.png",
    fullPage: true,
  });
  await first.click();
  await expect
    .poll(async () => {
      const question = await page
        .getByRole("article", { name: "你的提問" })
        .first()
        .boundingBox();
      const viewport = await page
        .locator(".conversation-viewport")
        .boundingBox();
      return Math.abs(question!.y - viewport!.y - 30);
    })
    .toBeLessThan(8);
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(
    page.getByRole("button", { name: "展開側欄", exact: true }),
  ).toBeVisible();
  await rail.getByRole("button", { name: "開啟對話目錄" }).click();
  await expect(rail.locator(".outline-entry")).toHaveCount(2);
  await rail.locator(".outline-entry").last().click();
  await expect(rail.locator(".outline-directory")).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});
