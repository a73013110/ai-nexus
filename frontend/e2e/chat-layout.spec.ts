import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
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
});

test("compact chat keeps more history and controls visible while touch menus remain accessible", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.ldap = true;
  core.authenticated = true;
  for (let index = 0; index < 30; index++) {
    core.conversations.push({
      id: randomUUID(),
      title: `工作紀錄 ${index + 1}：需要保留完整標題的長對話`,
      activeLeafId: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      isFavorite: index === 0,
      isArchived: false,
      systemInstruction: "",
      labels: [],
      projectId: null,
    });
  }
  await core.attach(page);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  const history = sidebar.getByRole("navigation", { name: "對話歷史" });
  await expect(history.locator(".history-row")).toHaveCount(30);
  await expect(history.locator(".history-row > a").first()).toHaveCSS(
    "font-size",
    "14px",
  );
  await expect(sidebar.locator(".history-group h2").first()).toHaveCSS(
    "font-size",
    "13px",
  );
  expect(
    (await sidebar.locator(".history-search").boundingBox())!.height,
  ).toBeGreaterThanOrEqual(40);
  const visibleRows = await history.evaluate((element) => {
    const bounds = element.getBoundingClientRect();
    return [...element.querySelectorAll(".history-row")].filter((row) => {
      const rectangle = row.getBoundingClientRect();
      return rectangle.top >= bounds.top && rectangle.bottom <= bounds.bottom;
    }).length;
  });
  expect(visibleRows).toBeGreaterThanOrEqual(14);
  const composer = page.locator("form.composer");
  expect((await composer.boundingBox())!.height).toBeLessThanOrEqual(92);
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveCSS("font-size", "15px");
  const account = sidebar.getByRole("button", {
    name: "登入者選單",
    exact: true,
  });
  const workspace = sidebar.getByRole("button", {
    name: "工作區",
    exact: true,
  });
  await expect(account).toBeInViewport();
  await expect(workspace).toBeInViewport();
  const [workspaceBounds, accountBounds] = await Promise.all([
    workspace.boundingBox(),
    account.boundingBox(),
  ]);
  expect(
    accountBounds!.y - (workspaceBounds!.y + workspaceBounds!.height),
  ).toBeLessThanOrEqual(9);
  await account.press("ArrowDown");
  const menu = page.getByRole("menu", { name: "登入者選單", exact: true });
  await expect(
    menu.getByRole("menuitem", { name: "設定", exact: true }),
  ).toBeFocused();
  await settleEntrance(page);
  const desktopRow = await menu.getByRole("menuitem").first().boundingBox();
  expect(desktopRow!.height).toBeGreaterThanOrEqual(34);
  expect(desktopRow!.height).toBeLessThanOrEqual(36);
  await menu.getByRole("menuitem").first().press("End");
  await expect(menu.getByRole("menuitem").last()).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(account).toBeFocused();
  await settleEntrance(page);

  const row = history.locator(".history-row").first();
  await row.getByRole("link").click();
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("確認緊湊聊天操作");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page
      .getByRole("article", { name: "AI 回覆", exact: true })
      .getByRole("button", { name: "複製訊息", exact: true }),
  ).toBeVisible();
  await expectViewportContained(page);
  await settleEntrance(page);
  for (const width of [859, 375]) {
    await page.setViewportSize({ width, height: 812 });
    await page.getByRole("button", { name: "展開側欄", exact: true }).click();
    for (const control of [
      sidebar.getByRole("link", { name: "新對話", exact: true }),
      workspace,
      account,
    ]) {
      await expect(control).toBeInViewport();
      expect((await control.boundingBox())!.height).toBeGreaterThanOrEqual(44);
    }
    await account.click();
    await settleEntrance(page);
    expect(
      (await menu.getByRole("menuitem").first().boundingBox())!.height,
    ).toBeGreaterThanOrEqual(44);
    await expect(menu).toBeInViewport();
    await page.keyboard.press("Escape");
    await expect(account).toBeFocused();
    await sidebar.locator(".sidebar-toggle").click();
    await expect(
      page.getByRole("button", { name: "送出訊息", exact: true }),
    ).toBeInViewport();
    await expectViewportContained(page);
    await settleEntrance(page);
  }
});

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
      return Math.abs(question!.y - viewport!.y - 16);
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

test("a running conversation keeps its sidebar signal when another conversation is selected", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.hold = true;
  core.partialAnswer = "";
  await core.attach(page);
  const noticeId = randomUUID();
  let read = false;
  await page.route("**/api/v1/notifications**", (route) => {
    if (route.request().method() === "POST") {
      read = true;
      return route.fulfill({ status: 204 });
    }
    const run = core.runs[0],
      done = run?.status === "completed";
    return route.fulfill({
      json: {
        items: done
          ? [
              {
                id: noticeId,
                version: 1,
                type: "conversation.completed",
                severity: "success",
                title: "回答已完成",
                body: "先處理這份分析",
                target: { kind: "conversation", id: run.conversationId },
                createdAt: new Date().toISOString(),
                readAt: read ? new Date().toISOString() : null,
              },
            ]
          : [],
        unread: done && !read ? 1 : 0,
        hasMore: false,
      },
    });
  });
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("先處理這份分析");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.locator(".history-state.is-running")).toBeVisible();
  await page.locator("a.new-chat").click();
  await expect(page).toHaveURL(/\/chat$/);
  await expect(page.locator(".history-state.is-running")).toBeVisible();
  await page
    .locator(".history-row")
    .getByRole("button", { name: /對話操作/ })
    .click();
  await expect(
    page.getByRole("menuitem", { name: "封存對話", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("menuitem", { name: "重新命名", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("menuitem", { name: "收藏", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  const run = core.runs[0];
  run.status = "completed";
  run.finishedAt = new Date().toISOString();
  core.messages.find((x) => x.id === run.assistantMessageId)!.status =
    "completed";
  core.events.get(run.id)!.push({
    version: 1,
    sequence: ++run.lastSequence,
    runId: run.id,
    type: "status",
    status: "completed",
    delta: null,
    errorCode: null,
  });
  await expect(page.locator(".history-state.is-running")).toHaveCount(0);
  await expect(page.locator(".history-complete")).toBeVisible();
  await page.locator(".history-row").getByRole("link").click();
  await expect.poll(() => read).toBe(true);
  await expect(page.locator(".history-complete")).toHaveCount(0);
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
  await expect(page.locator(".markdown")).toHaveCSS("font-size", "15px");
  await expect(page.locator(".topbar")).toHaveCSS("height", "48px");
  await settleEntrance(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(
    page.getByRole("button", { name: "展開側欄", exact: true }),
  ).toBeVisible();
  await page.getByLabel(/上下文用量：/).click();
  const panel = await page
    .getByRole("group", { name: "上下文用量", exact: true })
    .locator(".context-panel")
    .boundingBox();
  expect(panel!.x).toBeGreaterThanOrEqual(0);
  expect(panel!.x + panel!.width).toBeLessThanOrEqual(375);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await expect(page.locator(".markdown")).toHaveCSS("font-size", "15px");
  await settleEntrance(page);
});
