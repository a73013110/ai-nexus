import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  richAnswer,
  settleEntrance,
  chooseSelect,
} from "./fixtures";

test("blank desktop workspace, real forms, keyboard and Markdown copy", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(
    page.getByRole("heading", { name: "今天，從哪件事開始？" }),
  ).toBeVisible();
  await expect(page.locator(".workbench")).toHaveCSS("display", "grid");
  await expect(page.locator(".sidebar")).toHaveCSS("width", "264px");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/desktop-empty.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: /整理思緒/ }).click();
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toHaveValue(
    /幫我整理/,
  );
  await page
    .getByRole("textbox", { name: "傳送訊息" })
    .fill("請整理第一版工作");
  await page.getByRole("textbox", { name: "傳送訊息" }).press("Shift+Enter");
  expect(fixture.posts).toBe(0);
  await page.getByRole("textbox", { name: "傳送訊息" }).press("Enter");
  await expect(page.getByRole("table")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeEnabled();
  expect(fixture.posts).toBe(1);
  await page.getByRole("button", { name: "複製程式碼", exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as { __copied: string }).__copied),
    )
    .toBe('const nextStep = "開始實作";\nconsole.log(nextStep);\n');
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/desktop-chat.png",
    fullPage: true,
  });
});

test("Chinese composition Enter never submits, then normal Enter submits", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const input = page.getByRole("textbox", { name: "傳送訊息" });
  await input.fill("中文輸入法測試");
  await input.dispatchEvent("compositionstart");
  await input.dispatchEvent("keydown", {
    key: "Enter",
    code: "Enter",
    isComposing: true,
    keyCode: 229,
  });
  await page.waitForTimeout(100);
  expect(fixture.posts).toBe(0);
  await input.dispatchEvent("compositionend");
  await input.press("Enter");
  await expect(page.getByRole("table")).toBeVisible();
  expect(fixture.posts).toBe(1);
});

test("editing and regenerating preserve versions; history can be renamed and deleted", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("原始提問");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  await page.getByRole("button", { name: "重新生成", exact: true }).click();
  await expect(page.getByLabel("目前版本")).toHaveText("2 / 2");
  await page.getByRole("button", { name: "上一個版本" }).click();
  await expect(page.getByLabel("目前版本")).toHaveText("1 / 2");
  await page.getByRole("button", { name: "編輯提問", exact: true }).click();
  await page
    .getByRole("textbox", { name: "修改你的提問" })
    .fill("修改後的提問");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByText("修改後的提問", { exact: true })).toBeVisible();
  expect(fixture.messages.some((x) => x.content === "原始提問")).toBe(true);
  await page.getByLabel("對話操作", { exact: true }).click();
  await page.getByRole("button", { name: "重新命名目前對話" }).click();
  await page.getByRole("textbox", { name: "對話標題" }).fill("第一版開發紀錄");
  await page.getByRole("button", { name: "儲存標題" }).click();
  await expect(
    page.getByRole("heading", { name: "第一版開發紀錄" }),
  ).toBeVisible();
  await page
    .getByRole("searchbox", { name: "搜尋對話標題與內容" })
    .fill("不存在");
  await expect(page.getByText("沒有符合的對話。")).toBeVisible();
  await page.getByRole("searchbox", { name: "搜尋對話標題與內容" }).fill("");
  await page.getByLabel("對話操作", { exact: true }).click();
  await page.getByRole("button", { name: "刪除目前對話" }).click();
  await page.getByRole("button", { name: "刪除對話", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "今天，從哪件事開始？" }),
  ).toBeVisible();
  expect(fixture.conversations).toHaveLength(0);
});

test("stop gives immediate feedback and preserves a partial answer", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.hold = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("停止測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByText("這是已保存的部分回答。")).toBeVisible();
  await page.getByRole("button", { name: "停止生成" }).click();
  await expect(page.getByText("已停止 · 保留部分回答")).toBeVisible();
  await expect(page.getByText("這是已保存的部分回答。")).toBeVisible();
  await expect(page.getByRole("button", { name: "停止生成" })).toHaveCount(0);
});

test("SSE disconnect resumes from a GET snapshot without a duplicate POST", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.disconnectOnce = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("重連測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  expect(fixture.posts).toBe(1);
  expect(fixture.stateReads).toBeGreaterThan(0);
  expect(fixture.messages.filter((x) => x.role === "assistant")).toHaveLength(
    1,
  );
});

test("lost POST response retries the original idempotency key and retains only one run", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.losePostOnce = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("回應遺失測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await page.getByRole("button", { name: "重試提交" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  expect(fixture.posts).toBe(2);
  expect(fixture.generated).toBe(1);
  expect(fixture.runs).toHaveLength(1);
});

test("unconfigured server shows an honest error and cannot submit", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.unavailable = true;
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByRole("alert")).toContainText(
    "伺服器尚未完成資料庫設定。",
  );
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("不能假裝聊天");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  expect(fixture.posts).toBe(0);
});

test("dark theme, reduced motion and narrow viewport remain usable", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await page.locator(".profile-menu summary").click();
  await chooseSelect(page, "外觀", "深色");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("checkbox", { name: "減少動態效果" }).check();
  await expect(page.locator("html")).toHaveAttribute(
    "data-reduced-motion",
    "true",
  );
  await page.locator(".profile-menu summary").click();
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/desktop-dark.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await page.getByRole("button", { name: "關閉對話導覽" }).click();
  await expect(page.locator("main")).not.toHaveAttribute("inert", "");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("窄畫面測試");
  await expect(page.getByRole("textbox", { name: "傳送訊息" })).toHaveValue(
    "窄畫面測試",
  );
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/mobile-dark-chat.png",
    fullPage: true,
  });
  await page.getByLabel("對話操作", { exact: true }).click();
  await page.getByRole("button", { name: "刪除目前對話", exact: true }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByRole("button", { name: "取消", exact: true }).click();
});

test("mobile drawer supports Escape and restores visible keyboard focus", async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const toggle = page.getByRole("button", { name: "開啟側欄" });
  await toggle.click();
  await expect(page.locator("main")).toHaveAttribute("inert", "");
  await page.keyboard.press("Escape");
  await expect(toggle).toBeFocused();
  await expect(page.locator("main")).not.toHaveAttribute("inert", "");
  await page.getByRole("textbox", { name: "傳送訊息" }).focus();
  await expect(page.locator(".composer")).toHaveCSS("outline-width", "2px");
});

test("failed preference save restores the last confirmed theme and can be retried", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByText("測試使用者", { exact: true })).toBeVisible();
  await page.locator(".profile-menu summary").click();
  await chooseSelect(page, "外觀", "深色");
  await expect.poll(() => fixture.preferences.theme).toBe("dark");
  fixture.failPreferencesOnce = true;
  await chooseSelect(page, "外觀", "淺色");
  await expect(page.getByRole("alert")).toContainText("偏好設定保存失敗。");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.getByRole("combobox", { name: "外觀", exact: true })).toContainText("深色");
  await chooseSelect(page, "外觀", "淺色");
  await expect.poll(() => fixture.preferences.theme).toBe("light");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
});

test("unsafe Markdown cannot execute script or request tracking images", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.answer =
    '<img src="https://tracking.invalid/pixel" onerror="window.__xss=1">\n<script>window.__xss=1</script>\n![追蹤](https://tracking.invalid/pixel)\n[危險](javascript:alert(1))';
  const tracking: string[] = [];
  page.on("request", (request) => {
    if (request.url().includes("tracking.invalid"))
      tracking.push(request.url());
  });
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("安全測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.locator(".markdown")).toBeVisible();
  expect(
    await page.evaluate(() => (window as unknown as { __xss?: number }).__xss),
  ).toBeUndefined();
  expect(tracking).toEqual([]);
  await expect(page.locator(".markdown img")).toHaveCount(0);
});

test("long answers on a throttled browser keep reading position and offer return to latest", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.answer = (richAnswer + "\n\n").repeat(24);
  fixture.hold = true;
  const cdp = await page.context().newCDPSession(page);
  await cdp.send("Emulation.setCPUThrottlingRate", { rate: 4 });
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("長答案測試");
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect(page.getByText("這是已保存的部分回答。")).toBeVisible();
  await page.getByRole("button", { name: "停止生成" }).click();
  // Load a long historical answer to inspect the stable scroller independently of stream timing.
  fixture.messages.find((x) => x.role === "assistant")!.content =
    fixture.answer;
  await page.reload();
  await expect(page.getByRole("table")).toHaveCount(24);
  const viewport = page.locator(".conversation-viewport");
  await viewport.evaluate((element) => (element.scrollTop = 120));
  await expect(page.getByRole("button", { name: "回到最新" })).toBeVisible();
  const before = await viewport.evaluate((element) => element.scrollTop);
  await page.waitForTimeout(250);
  expect(await viewport.evaluate((element) => element.scrollTop)).toBe(before);
  await page.getByRole("button", { name: "回到最新" }).click();
  await expect
    .poll(() =>
      viewport.evaluate(
        (element) =>
          element.scrollHeight - element.scrollTop - element.clientHeight,
      ),
    )
    .toBeLessThan(100);
});
