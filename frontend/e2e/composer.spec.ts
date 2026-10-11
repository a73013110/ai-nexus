import { test, expect } from "@playwright/test";
import { ApiFixture, chooseSelect, settleEntrance } from "./fixtures";

test("focused composer synchronizes edits and uses the same IME safe submission behavior", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/chat");
  const compact = page.getByRole("textbox", { name: "傳送訊息", exact: true });
  await compact.fill("準備長篇提問");
  await page.getByRole("button", { name: "展開訊息輸入", exact: true }).click();
  const dialog = page.getByRole("dialog", {
    name: "讓長篇提問也容易整理",
    exact: true,
  });
  const expanded = dialog.getByRole("textbox", {
    name: "放大的訊息輸入",
    exact: true,
  });
  await expect(expanded).toHaveValue("準備長篇提問");
  await expanded.fill("修改後的問題\n第二行");
  await expanded.dispatchEvent("compositionstart");
  await expanded.press("Enter");
  expect(fixture.posts).toBe(0);
  await expanded.dispatchEvent("compositionend");
  await settleEntrance(page);
  await dialog.getByRole("button", { name: "收合輸入區", exact: true }).click();
  await expect(compact).toHaveValue(/修改後的問題/);
  await page.getByRole("button", { name: "展開訊息輸入", exact: true }).click();
  await dialog.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.getByRole("table")).toBeVisible();
  expect(fixture.posts).toBe(1);
});

test("model lists remain single line, searchable and bounded with many long names", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  const models = Array.from({ length: 42 }, (_, i) => ({
    id: i === 0 ? "fixture:8b" : `model-${i}`,
    displayName: `企業模型 ${String(i).padStart(2, "0")} · 這是一個非常長且具有完整供應商說明的模型名稱`,
    contextTokens: 8192,
    maxOutputTokens: 2048,
    supportsImages: true,
    reasoningEfforts: [],
    defaultReasoningEffort: "auto",
  }));
  await page.route("**/api/v1/models", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        models,
        providerAvailable: true,
        notice: null,
        policy: fixture.modelPolicy,
      }),
    }),
  );
  await page.goto("/chat");
  await page.getByRole("combobox", { name: "選擇模型", exact: true }).click();
  const list = page.getByRole("listbox", { name: "選擇模型", exact: true });
  await expect(list.getByRole("option")).toHaveCount(42);
  await expect(list.locator("strong").first()).toHaveCSS(
    "white-space",
    "nowrap",
  );
  const box = await page.locator(".select-panel:popover-open").boundingBox();
  expect(box!.height).toBeLessThan(450);
  const search = page.getByRole("combobox", {
    name: "搜尋選擇模型",
    exact: true,
  });
  await search.fill("企業模型 41");
  await expect(list.getByRole("option")).toHaveCount(1);
  await search.press("Enter");
  await expect(
    page.getByRole("combobox", { name: "選擇模型", exact: true }),
  ).toContainText("企業模型 41");
});

test("model selector reports loading before enabling installed models", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  let release!: () => void;
  const loading = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/v1/models", async (route) => {
    await loading;
    await route.fallback();
  });
  await page.goto("/chat");
  const model = page.getByRole("combobox", { name: "選擇模型" });
  try {
    await expect(model).toContainText("正在載入模型…");
    await expect(model).toBeDisabled();
  } finally {
    release();
  }
  await expect(model).toContainText("本機測試模型");
  await expect(model).toBeEnabled();
});

test("group restrictions explain an empty model list and prevent submission", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  const notice =
    "你的群組目前沒有可用模型，請由管理員確認群組允許的模型與目前服務設定。";
  await page.route("**/api/v1/models", (route) =>
    route.fulfill({
      json: {
        models: [],
        providerAvailable: true,
        notice,
        policy: { ...fixture.modelPolicy, defaultModelId: null },
      },
    }),
  );
  await page.goto("/chat");
  const model = page.getByRole("combobox", { name: "選擇模型" });
  await expect(model).toContainText("沒有可用模型");
  await expect(model).toBeDisabled();
  await expect(page.getByText(notice, { exact: true })).toBeVisible();
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("保留授權限制");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  expect(fixture.posts).toBe(0);
});

test("model list failures show a retry path and recover without losing the draft", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  let attempts = 0;
  await page.route("**/api/v1/models", (route) =>
    ++attempts === 1
      ? route.fulfill({
          status: 503,
          json: {
            title: "Password=fixture-private; internal SQL endpoint",
            code: "migrations_pending",
            issueCode: "NX-" + "D".repeat(32),
          },
        })
      : route.fallback(),
  );
  await page.goto("/chat");
  const model = page.getByRole("combobox", { name: "選擇模型" });
  await expect(model).toContainText("模型清單載入失敗");
  await expect(model).toBeDisabled();
  await expect(page.getByRole("alert")).toContainText(
    "操作未完成，請聯絡管理員。查證代碼：NX-",
  );
  await expect(page.getByRole("alert")).not.toContainText("fixture-private");
  const draft = page.getByRole("textbox", { name: "傳送訊息" });
  await draft.fill("恢復後保留的草稿");
  await page.getByRole("button", { name: "重新連線" }).click();
  await expect(model).toContainText("本機測試模型");
  await expect(model).toBeEnabled();
  await expect(draft).toHaveValue("恢復後保留的草稿");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeEnabled();
});

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
  await expect(
    page.getByRole("group", { name: "上下文用量", exact: true }),
  ).toContainText("預留 2,048 tokens");
  await page.keyboard.press("Escape");
  await expect(
    page.getByRole("group", { name: "上下文用量", exact: true }),
  ).not.toHaveAttribute("open");
  await expect(context).toBeFocused();
  await page.getByRole("button", { name: "送出訊息" }).click();
  await expect.poll(() => fixture.lastRequest?.reasoningEffort).toBe("high");
  await expect(page.getByText("這是已保存的部分回答。")).toBeVisible();
  await expect(page.locator(".signal-flow").first()).toHaveCSS(
    "animation-name",
    "signal-transit",
  );
  await settleEntrance(page);
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
  await expect(page.getByRole("alert")).toContainText("沒有對話功能");
  await page.getByRole("textbox", { name: "傳送訊息" }).fill("權限測試");
  await expect(page.getByRole("button", { name: "送出訊息" })).toBeDisabled();
  expect(fixture.posts).toBe(0);
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
});
