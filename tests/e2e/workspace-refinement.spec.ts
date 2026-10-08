import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

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
  await page.screenshot({
    path: "artifacts/screenshots/chat-density-desktop.png",
  });

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
  await page.screenshot({
    path: "artifacts/screenshots/chat-density-conversation.png",
  });
  for (const width of [859, 375]) {
    await page.setViewportSize({ width, height: 812 });
    await sidebar
      .getByRole("button", { name: "展開側欄", exact: true })
      .click();
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
    await page.screenshot({
      path: `artifacts/screenshots/chat-density-${width}.png`,
    });
  }
});

test("the shared wordmark has one N and the compact new-chat icon stays centered", async ({
  page,
}) => {
  const core = new ApiFixture();
  await core.attach(page);
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  await expect(sidebar.locator(".brand-wordmark")).toHaveText("AIexus");
  await expect(sidebar.locator(".brand-symbol svg")).toHaveCount(1);
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("名稱顯示測試");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.locator(".message-model")).toHaveText("本機測試模型");
  await expect(page.locator("body")).not.toContainText("fixture:8b");
  await page.screenshot({
    path: "artifacts/screenshots/sidebar-wordmark-expanded.png",
  });
  for (const width of [1280, 375]) {
    await page.setViewportSize({ width, height: 812 });
    const collapse = page.getByRole("button", {
      name: "收合側欄",
      exact: true,
    });
    if (await collapse.isVisible()) await collapse.click();
    const button = sidebar.getByRole("link", { name: "新對話", exact: true });
    const [rail, bounds, icon] = await Promise.all([
      sidebar.boundingBox(),
      button.boundingBox(),
      button.locator("nx-icon").boundingBox(),
    ]);
    expect(bounds!.width).toBeGreaterThanOrEqual(44);
    expect(bounds!.height).toBeGreaterThanOrEqual(44);
    expect(
      Math.abs(bounds!.x + bounds!.width / 2 - (icon!.x + icon!.width / 2)),
    ).toBeLessThan(1);
    expect(
      Math.abs(bounds!.y + bounds!.height / 2 - (icon!.y + icon!.height / 2)),
    ).toBeLessThan(1);
    expect(
      Math.abs(bounds!.x + bounds!.width / 2 - (rail!.x + rail!.width / 2)),
    ).toBeLessThan(1);
    await page.screenshot({
      path: `artifacts/screenshots/sidebar-wordmark-${width}.png`,
    });
  }
});

test("one header notification icon exposes the full count and workspace expansion uses the chat sidebar", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push(
    { id: "knowledge", name: "知識庫", route: "/knowledge" },
    { id: "tasks", name: "背景任務", route: "/tasks" },
    { id: "repositories", name: "程式庫", route: "/repositories" },
  );
  await core.attach(page);
  let unread = 125;
  await page.route("**/api/v1/notifications**", (route) =>
    route.fulfill({ json: { items: [], unread, hasMore: false } }),
  );
  await page.goto("/chat");
  const sidebar = page.locator(".workspace-sidebar");
  const bell = sidebar.getByRole("button", { name: "通知", exact: true });
  await expect(bell).toHaveCount(1);
  await expect(bell.locator("nx-count-badge")).toHaveText("99+");
  const [iconBounds, badgeBounds] = await Promise.all([
    bell.locator("nx-icon").boundingBox(),
    bell.locator("nx-count-badge").boundingBox(),
  ]);
  expect(badgeBounds!.x).toBeLessThan(iconBounds!.x + iconBounds!.width);
  expect(badgeBounds!.x).toBeGreaterThan(iconBounds!.x + iconBounds!.width / 3);
  expect(badgeBounds!.y + badgeBounds!.height).toBeGreaterThan(iconBounds!.y);
  const description = await bell.getAttribute("aria-describedby");
  await expect(page.locator("#" + description)).toHaveText("125 則未讀通知");
  const [notification, toggle] = await Promise.all([
    bell.boundingBox(),
    sidebar.locator(".sidebar-toggle").boundingBox(),
  ]);
  expect(notification!.x + notification!.width).toBeLessThanOrEqual(toggle!.x);
  await expect(sidebar).toHaveCSS("width", "240px");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveCSS("font-size", "15px");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveCSS("line-height", "18px");
  const workspace = sidebar.getByRole("button", {
    name: "工作區",
    exact: true,
  });
  await workspace.click();
  await expect(sidebar.locator(".new-chat")).not.toBeVisible();
  await expect(sidebar.locator(".history-search")).not.toBeVisible();
  await expect(workspace).toHaveAttribute("aria-expanded", "true");
  await expect(sidebar.locator(".workspace-groups")).toHaveCSS(
    "max-height",
    "none",
  );
  const [heading, navigation] = await Promise.all([
    sidebar.locator(".workspace-sidebar-heading").boundingBox(),
    workspace.boundingBox(),
  ]);
  expect(navigation!.y - (heading!.y + heading!.height)).toBeLessThanOrEqual(
    16,
  );
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/chat-workspace-expanded.png",
  });
  await workspace.press("Enter");
  await expect(sidebar.locator(".new-chat")).toBeVisible();
  await expect(sidebar.locator(".history-search")).toBeVisible();
  unread = 0;
  await bell.click();
  await expect(bell.locator("nx-count-badge")).not.toBeVisible();
  await page.keyboard.press("Escape");
  await expect(bell).toBeFocused();
  await page.setViewportSize({ width: 375, height: 667 });
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await workspace.click();
  await expect(sidebar.locator(".new-chat")).not.toBeVisible();
  await expect(
    sidebar.getByRole("link", { name: "背景任務", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  const compactBell = await bell.boundingBox();
  const compactToggle = sidebar.getByRole("button", {
    name: "展開側欄",
    exact: true,
  });
  const compactToggleBounds = await compactToggle.boundingBox();
  expect(compactBell!.y + compactBell!.height).toBeLessThanOrEqual(
    compactToggleBounds!.y,
  );
  await bell.focus();
  await bell.press("Tab");
  await expect(compactToggle).toBeFocused();
  await expectViewportContained(page);
});

test("vector retrieval cards contain long filenames, URLs and multiline excerpts in both themes and small viewports", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  await page.route("**/api/v1/knowledge/search", (route) =>
    route.fulfill({
      json: {
        mode: "vector",
        hits: [
          {
            documentId: fixture.documents[0].id,
            title: "LOG-".repeat(80) + ".pdf",
            pageNumber: 1,
            text:
              "誤植原因與處理紀錄\n" +
              "https://example.test/" +
              "long-url-".repeat(160) +
              "\n後續處理\n".repeat(40),
            score: 0.9,
          },
        ],
      },
    }),
  );
  await page.goto("/knowledge");
  await page.getByRole("textbox", { name: "知識庫查詢內容" }).fill("誤植原因");
  await page.getByRole("button", { name: "檢索來源" }).click();
  const hit = page.locator(".knowledge-hit");
  await expect(hit).toBeVisible();
  for (const theme of ["light", "dark"]) {
    for (const width of [1440, 1101, 375]) {
      await page.evaluate(
        (value) => (document.documentElement.dataset["theme"] = value),
        theme,
      );
      await page.setViewportSize({ width, height: 900 });
      await hit.scrollIntoViewIfNeeded();
      const contained = await hit.evaluate((element) => {
        const card = element.getBoundingClientRect();
        const panel = element.closest(".knowledge-test")!;
        const bounds = panel.getBoundingClientRect();
        const style = getComputedStyle(panel);
        const excerpt = element.querySelector("p")!;
        return (
          card.left >= bounds.left + parseFloat(style.paddingLeft) - 1 &&
          card.right <= bounds.right - parseFloat(style.paddingRight) + 1 &&
          excerpt.scrollWidth <= excerpt.clientWidth
        );
      });
      expect(contained, theme + " " + width).toBe(true);
      await expectViewportContained(page);
      if (width !== 1101) {
        await settleEntrance(page);
        await page.screenshot({
          path:
            "artifacts/screenshots/vector-results-" +
            theme +
            "-" +
            width +
            ".png",
        });
      }
    }
  }
});

async function reviewFixture(page: Page, existing = false) {
  const core = new ApiFixture();
  core.extraFeatures.push(
    { id: "repositories", name: "程式庫", route: "/repositories" },
    { id: "tasks", name: "背景任務", route: "/tasks" },
  );
  await core.attach(page);
  const id = randomUUID(),
    commit = "a".repeat(40),
    basis = "b".repeat(40);
  let release!: () => void;
  const ready = new Promise<void>((resolve) => (release = resolve));
  const job = {
    id: randomUUID(),
    subjectId: id,
    kind: "repository-review",
    label: "team/repo",
    status: "running",
    stage: "檢閱區段 1 / 2",
    attempt: 1,
    completedUnits: 0,
    totalUnits: 3,
    cancelRequested: false,
    errorCode: null,
    errorMessage: null,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  };
  const review = {
    id,
    repository: "team/repo",
    commit,
    baseCommit: basis,
    purpose: "summary",
    note: "",
    modelId: "fixture:8b",
    createdAt: job.createdAt,
    job,
  };
  let created = false;
  let report: object | null = null;
  const calls = { commits: 0, detail: 0 };
  await page.route("**/api/v1/repositories**", async (route) => {
    const url = new URL(route.request().url()),
      path = url.pathname;
    if (path.endsWith("/connection"))
      return route.fulfill({
        json: {
          available: true,
          connected: true,
          baseUrl: "https://gitea.example/",
          login: "fixture",
          notice: "",
        },
      });
    if (path.endsWith("/tree"))
      return route.fulfill({
        json: { repository: "team/repo", commit, path: "", entries: [] },
      });
    if (path.endsWith("/commits")) {
      calls.commits++;
      await ready;
      return route.fulfill({
        json: [
          { sha: commit, message: "最後版本" },
          { sha: basis, message: "起始版本" },
        ],
      });
    }
    if (path.endsWith("/reviews")) {
      if (route.request().method() === "POST") {
        expect(route.request().postDataJSON()).toMatchObject({
          purpose: "summary",
          commit,
          baseCommit: basis,
        });
        created = true;
        return route.fulfill({ json: review });
      }
      return route.fulfill({ json: created || existing ? [review] : [] });
    }
    if (path.endsWith("/" + id)) {
      calls.detail++;
      return route.fulfill({
        json: {
          review,
          version: 2,
          report,
          sections: [
            {
              ordinal: 0,
              label: "src/auth.ts",
              diff: "+authorize(user)",
              output: "區段筆記，不應預設展開",
              binary: false,
              truncated: false,
            },
          ],
        },
      });
    }
    return route.fulfill({
      json: {
        items: [
          {
            fullName: "team/repo",
            description: "",
            private: true,
            defaultBranch: "main",
            url: "https://gitea.example/team/repo",
          },
        ],
        page: 1,
        hasMore: false,
      },
    });
  });
  return {
    release,
    calls,
    id,
    job,
    commit,
    basis,
    setReport: (value: object) => (report = value),
  };
}

test("review stays usable while commits load and presents one expandable overall report", async ({
  page,
}) => {
  const fixture = await reviewFixture(page);
  await page.goto("/repositories");
  await page.locator(".repository-row").click();
  await page.getByRole("button", { name: "AI Review", exact: true }).click();
  await expect(
    page.getByRole("status").filter({ hasText: "正在取得近期 commit" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "建立背景 review", exact: true }),
  ).toBeEnabled();
  await chooseSelect(page, "檢閱範圍", "Commit 區間");
  fixture.release();
  const start = page.getByRole("combobox", { name: "起點 SHA", exact: true });
  await start.click();
  await page.getByRole("combobox", { name: "搜尋起點 SHA" }).fill("起始");
  await page.getByRole("option", { name: /起始版本/ }).click();
  await expect(start).toContainText("bbbbbbbbbb");
  const endpoint = page.getByRole("combobox", {
    name: "終點 SHA",
    exact: true,
  });
  await endpoint.click();
  await expect(page.getByRole("option", { name: /起始版本/ })).toBeDisabled();
  await page.keyboard.press("Escape");
  const picker = page
    .locator("nx-repository-commit-picker")
    .filter({ has: start });
  await picker.getByText("貼上完整 SHA", { exact: true }).click();
  const sha = page.getByRole("textbox", { name: "起點 SHA（完整 SHA）" });
  await sha.fill("c".repeat(40));
  await expect(start).toContainText("自訂版本");
  await sha.fill(fixture.basis);
  await page.getByRole("combobox", { name: "檢閱目的", exact: true }).click();
  await page.getByRole("option", { name: /^變更摘要/ }).click();
  await page
    .getByRole("button", { name: "建立背景 review", exact: true })
    .click();
  await expect(page.locator(".review-report")).toContainText(
    "正在分析變更並彙整整體報告",
  );
  const stage = page.locator(".review-results .job-stage-label");
  const [signal, label] = await Promise.all([
    stage.locator("svg").boundingBox(),
    stage.locator("> span").boundingBox(),
  ]);
  expect(signal!.x + signal!.width + 8).toBeLessThanOrEqual(label!.x);
  await expect(page.locator(".review-evidence")).not.toHaveAttribute(
    "open",
    "",
  );
  await expect(page.locator(".review-section pre")).toHaveCount(0);
  fixture.job.status = "completed";
  fixture.job.stage = "整體報告已完成";
  fixture.setReport({
    output:
      "結論：已彙整跨檔案影響。\n\n" +
      "變更說明與主要影響。\n\n".repeat(100) +
      "最後一項跨檔結論",
    truncated: true,
    inputTokens: 200,
    outputTokens: 100,
    elapsedMs: 1000,
  });
  await expect(
    page.getByRole("region", { name: "整體報告內容" }),
  ).toContainText("已彙整跨檔案影響");
  const body = page.locator(".review-report-body");
  expect(await body.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(
    true,
  );
  await page.getByRole("button", { name: "展開完整報告", exact: true }).click();
  await expect(body).toHaveCSS("max-height", "none");
  await expect(page.locator(".review-report .error-banner")).toContainText(
    "結果不完整",
  );
  await page.locator(".review-evidence > summary").click();
  await page.locator(".review-section > summary").click();
  await expect(page.locator(".review-section")).toContainText(
    "authorize(user)",
  );
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});

test("historical reports are read on selection and a deep link reuses the authorized detail while commits wait", async ({
  page,
}) => {
  const fixture = await reviewFixture(page, true);
  fixture.job.status = "completed";
  fixture.setReport({
    output: "已彙整變更，未發現明確缺陷。",
    truncated: false,
    elapsedMs: 100,
  });
  await page.goto("/repositories");
  await page.locator(".repository-row").click();
  await page.getByRole("button", { name: "AI Review", exact: true }).click();
  await expect(
    page.getByRole("combobox", { name: "歷史 review", exact: true }),
  ).toBeVisible();
  expect(fixture.calls.detail).toBe(0);
  await page.goto(`/repositories?review=${fixture.id}`);
  await expect(page.locator(".review-report")).toContainText("未發現明確缺陷");
  await page.locator(".review-create > summary").click();
  await expect(
    page.getByRole("status").filter({ hasText: "正在取得近期 commit" }),
  ).toBeVisible();
  expect(fixture.calls.detail).toBe(1);
  fixture.release();
  await expect(
    page.getByRole("status").filter({ hasText: "正在取得近期 commit" }),
  ).toHaveCount(0);
  expect(fixture.calls.detail).toBe(1);
  await page.locator(".review-create > summary").click();
  await page.screenshot({
    path: "artifacts/screenshots/repository-review-brief.png",
  });
  await page.getByRole("button", { name: "檔案", exact: true }).click();
  await page.getByRole("button", { name: "AI Review", exact: true }).click();
  await expect(page.locator(".review-report")).toContainText("未發現明確缺陷");
  expect(fixture.calls.detail).toBe(2);
});

test("a failed commit list and history keep manual SHA entry and the review form available", async ({
  page,
}) => {
  const fixture = await reviewFixture(page);
  await page.route("**/api/v1/repositories/commits?**", (route) =>
    route.fulfill({
      status: 503,
      json: { message: "近期 commit 暫時無法取得" },
    }),
  );
  await page.route("**/api/v1/repositories/reviews?**", (route) =>
    route.fulfill({ status: 503, json: { message: "歷史紀錄暫時無法取得" } }),
  );
  await page.goto("/repositories");
  await page.locator(".repository-row").click();
  await page.getByRole("button", { name: "AI Review", exact: true }).click();
  await expect(page.locator(".review-create")).toContainText(
    "可直接貼上完整 SHA",
  );
  await page.getByText("貼上完整 SHA", { exact: true }).click();
  await page
    .getByRole("textbox", { name: "Commit SHA（完整 SHA）" })
    .fill("c".repeat(40));
  await expect(
    page.getByRole("button", { name: "建立背景 review", exact: true }),
  ).toBeEnabled();
  fixture.release();
});
