import { test, expect } from "@playwright/test";
import {
  ApiFixture,
  richAnswer,
  settleEntrance,
  chooseSelect,
  openSettings,
  expectViewportContained,
} from "./fixtures";

test("對話開場沿用共用卡片與操作密度，手機及深色保留閱讀設定", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.settings.readingFontSize = 20;
  fixture.preferences.theme = "system";
  await fixture.attach(page);
  await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });
  await page.goto("/chat");
  const start = page.locator(".chat-start");
  const suggestions = page.getByRole("region", {
    name: "對話起點",
    exact: true,
  });
  await expect(start).toHaveCSS("font-size", "13px");
  await expect(suggestions).toHaveCSS("font-size", "13px");
  await expect(page.locator("#composer")).toHaveCSS("font-size", "20px");
  for (const viewport of [
    { width: 1280, height: 768 },
    { width: 375, height: 812 },
  ]) {
    await page.setViewportSize(viewport);
    await expect(start.getByRole("heading")).toHaveCSS("font-size", "24px");
    expect((await start.boundingBox())!.height).toBeLessThan(
      viewport.width === 375 ? 400 : 220,
    );
    const starters = await suggestions.boundingBox();
    const composer = await page.locator(".composer").boundingBox();
    expect(starters!.y).toBeGreaterThan(composer!.y + composer!.height);
    const surface = (await page.locator(".chat-surface").boundingBox())!;
    if (viewport.width > 640) {
      expect(
        Math.abs(
          composer!.y + composer!.height / 2 - (surface.y + surface.height / 2),
        ),
      ).toBeLessThan(surface.height * 0.1);
    } else {
      expect(composer!.y + composer!.height / 2).toBeGreaterThan(
        viewport.height * 0.35,
      );
    }
    for (const button of await suggestions.locator("button.ui-card").all())
      expect((await button.boundingBox())!.height).toBeGreaterThanOrEqual(
        viewport.width === 375 ? 44 : 34,
      );
  }
  await suggestions.getByRole("button", { name: /整理思緒/ }).click();
  await expect(page.locator("#composer")).toBeFocused();
  await expect(page.locator("#composer")).toHaveValue(/幫我整理/);
  await page.emulateMedia({ colorScheme: "light" });
  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
});

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
  await expect(page.locator(".sidebar")).toHaveCSS("width", "240px");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  const start = page.locator(".chat-start");
  await expect(start).toHaveCSS("font-size", "13px");
  await expect(start.getByRole("heading")).toHaveCSS("font-size", "24px");
  const suggestions = page.getByRole("region", {
    name: "對話起點",
    exact: true,
  });
  await expect(suggestions.locator("button.ui-card")).toHaveCount(3);
  const layout = await suggestions.evaluate(
    (el) => getComputedStyle(el).gridTemplateColumns.split(" ").length,
  );
  expect(layout).toBe(3);
  expect((await start.boundingBox())!.height).toBeLessThan(220);
  const composer = (await page.locator(".composer").boundingBox())!,
    surface = (await page.locator(".chat-surface").boundingBox())!;
  expect(
    Math.abs(
      composer.y + composer.height / 2 - (surface.y + surface.height / 2),
    ),
  ).toBeLessThan(surface.height * 0.1);
  await settleEntrance(page);
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
  await expect(page.locator("nx-run-timing")).toContainText("1.5 秒");
  await page.locator("nx-run-timing summary").click();
  await expect(page.locator("nx-run-timing")).toContainText("輸入 123");
  await expect(page.locator("nx-run-timing")).toContainText("輸出 12");
  await page.getByRole("button", { name: "複製程式碼", exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as { __copied: string }).__copied),
    )
    .toBe('const nextStep = "開始實作";\nconsole.log(nextStep);\n');
  await settleEntrance(page);
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
  await page.getByRole("button", { name: "對話操作", exact: true }).click();
  await page.getByRole("menuitem", { name: "重新命名目前對話" }).click();
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
  await page.getByRole("button", { name: "對話操作", exact: true }).click();
  await page.getByRole("menuitem", { name: "刪除目前對話" }).click();
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
    "操作未完成，請聯絡管理員。查證代碼：NX-",
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
  await openSettings(page);
  await chooseSelect(page, "主題", "深色");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("switch", { name: "減少動態效果" }).click();
  await expect(page.locator("html")).toHaveAttribute(
    "data-reduced-motion",
    "true",
  );
  await page.getByRole("button", { name: "儲存變更" }).click();
  await page.getByRole("button", { name: "關閉設定", exact: true }).click();
  await settleEntrance(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(
    page.getByRole("button", { name: "展開側欄", exact: true }),
  ).toBeVisible();
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
  await page.getByRole("button", { name: "對話操作", exact: true }).click();
  await page
    .getByRole("menuitem", { name: "刪除目前對話", exact: true })
    .click();
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
  const toggle = page.getByRole("button", { name: "展開側欄" });
  await toggle.click();
  await expect(page.locator("main")).toHaveAttribute("inert", "");
  await page.keyboard.press("Escape");
  await expect(toggle).toBeFocused();
  await expect(page.locator("main")).not.toHaveAttribute("inert", "");
  await page.getByRole("textbox", { name: "傳送訊息" }).focus();
  await expect(page.locator(".composer")).toHaveCSS("outline-width", "2px");
});

test("failed preference save retains editable changes and can be retried", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  await expect(page.getByText("測試使用者", { exact: true })).toBeVisible();
  await openSettings(page);
  await chooseSelect(page, "主題", "深色");
  await page.getByRole("button", { name: "儲存變更" }).click();
  await expect.poll(() => fixture.preferences.theme).toBe("dark");
  fixture.failPreferencesOnce = true;
  await chooseSelect(page, "主題", "淺色");
  await page.getByRole("button", { name: "儲存變更" }).click();
  await expect(page.getByRole("alert")).toContainText(
    "操作未完成，請聯絡管理員。查證代碼：NX-",
  );
  await expect(page.getByRole("alert")).not.toContainText("fixture-private");
  expect(fixture.preferences.theme).toBe("dark");
  await expect(
    page.getByRole("combobox", { name: "主題", exact: true }),
  ).toContainText("淺色");
  await page.getByRole("button", { name: "儲存變更" }).click();
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

test("chat search is opt-in and cost/source panels stay accessible without clipping", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.route("**/api/v1/tools/web-search", (route) =>
    route.fulfill({ json: { available: true, notice: "只送出本次提問。" } }),
  );
  await page.route("**/api/v1/conversations/*/spend", (route) =>
    route.fulfill({
      json: {
        requests: 2,
        pendingCalls: 0,
        legacyCalls: 0,
        totals: [
          {
            currency: "USD",
            kind: "api",
            amount: 0.0022,
            knownCalls: 2,
            unknownCalls: 0,
          },
        ],
        models: [
          {
            label: "本機測試模型",
            currency: "USD",
            kind: "api",
            amount: 0.0022,
            requests: 2,
            unknownCalls: 0,
            inputTokens: 100,
            outputTokens: 30,
          },
        ],
      },
    }),
  );
  await page.goto("/chat");
  const search = page.getByRole("button", { name: "搜尋網路", exact: true });
  await expect(search).toHaveAttribute("aria-pressed", "false");
  await search.click();
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("公開資料的最新消息");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect.poll(() => fixture.lastRequest?.webSearch).toBe(true);
  await expect(
    page.getByRole("article", { name: "AI 回覆", exact: true }),
  ).toBeVisible();
  const assistant = fixture.messages.find((x) => x.role === "assistant")!;
  assistant.charge = {
    state: "metered",
    kind: "api",
    currency: "USD",
    amount: 0.0022,
    inputTokens: 100,
    cachedInputTokens: 40,
    outputTokens: 30,
    reasoningTokens: 5,
  };
  assistant.webSources = [
    {
      number: 1,
      title: "可核對的網路來源",
      url: "https://example.org/research",
      excerpt: "摘要",
      retrievedAt: new Date().toISOString(),
    },
  ];
  await page.reload();
  await expect(
    page.getByRole("link", { name: /可核對的網路來源/ }),
  ).toHaveAttribute("href", "https://example.org/research");
  await page
    .getByRole("button", { name: "本次模型呼叫的費用與用量", exact: true })
    .click();
  const panel = page.getByRole("dialog", {
    name: "本次模型呼叫的費用與用量",
    exact: true,
  });
  await expect(panel).toContainText("40 個快取");
  await expect(panel).toContainText("5 個思考");
  const box = await panel.boundingBox();
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(box!.y + box!.height).toBeLessThanOrEqual(1000);
  await page.keyboard.press("Escape");
  await expect(panel).not.toBeVisible();
  await page
    .getByRole("button", { name: "檢視全對話費用", exact: true })
    .click();
  await expect(
    page.getByRole("dialog", { name: "檢視全對話費用", exact: true }),
  ).toContainText("全對話 · 2 次呼叫");
  await page.setViewportSize({ width: 375, height: 812 });
  await expect
    .poll(() =>
      page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    )
    .toBe(true);
  await expect(
    page.getByRole("button", { name: "搜尋網路", exact: true }),
  ).toBeVisible();
  const searchBox = await page
    .getByRole("button", { name: "搜尋網路", exact: true })
    .boundingBox();
  const contextBox = await page.locator(".context-trigger").boundingBox();
  expect(Math.abs(searchBox!.y - contextBox!.y)).toBeLessThanOrEqual(1);
  await page.keyboard.press("Escape");
  await page
    .getByRole("button", { name: "本次模型呼叫的費用與用量", exact: true })
    .click();
  const mobileBox = await panel.boundingBox();
  expect(mobileBox!.x).toBeGreaterThanOrEqual(0);
  expect(mobileBox!.y + mobileBox!.height).toBeLessThanOrEqual(812);
});

test("local deployments explain why web search is not enabled", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.route("**/api/v1/tools/web-search", (route) =>
    route.fulfill({
      json: {
        available: false,
        notice: "管理員尚未啟用網路搜尋。可設定自架 SearXNG。",
      },
    }),
  );
  await page.goto("/chat");
  await page.getByText("搜尋網路", { exact: true }).click();
  await expect(
    page.getByText("網路搜尋尚未啟用", { exact: true }),
  ).toBeVisible();
  await expect(page.locator(".search-setup")).toContainText(
    "地端模型本身不會連網",
  );
  expect(fixture.lastRequest?.webSearch).not.toBe(true);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
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
  await page.getByRole("button", { name: "對話操作", exact: true }).click();
  await expect(
    page.getByRole("menuitem", { name: "匯出 Markdown" }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "停止生成" }).click();
  await page.getByRole("button", { name: "對話操作", exact: true }).click();
  const download = page.waitForEvent("download");
  await page.getByRole("menuitem", { name: "匯出 Markdown" }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/\.md$/);
  const stream = await file.createReadStream();
  let contents = "";
  for await (const chunk of stream!) contents += chunk.toString();
  expect(contents).toContain("匯出測試");
  expect(contents).toContain("這是已保存的部分回答。");
  expect(contents).not.toContain("fixture:8b");
});
