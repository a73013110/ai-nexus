import {
  test,
  expect,
  type Locator,
  type Page,
  type Route,
} from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  ApiFixture,
  settleEntrance,
  chooseSelect,
  openSettings,
  expectViewportContained,
  toasts,
} from "./fixtures";
import type {
  AdminCatalogDto,
  AuditDto,
  AdminUserDto,
} from "../src/app/core/api/schema";

test("知識檢索管理顯示覆蓋率、重建啟用與授權檢索測試", async ({ page }) => {
  await administration(page);
  const collection = randomUUID(),
    document = randomUUID();
  const coverage = {
    completedChunks: 0,
    totalChunks: 1,
    pendingDocuments: 0,
    complete: false,
    ratio: 0,
  };
  const profiles = [
    {
      id: 1,
      key: "ollama:bge-m3:1024:old",
      provider: "ollama",
      model: "bge-m3",
      dimensions: 1024,
      status: "active",
      createdAt: "2026-10-06T00:00:00Z",
      activatedAt: "2026-10-06T00:00:00Z",
      retiredAt: null,
      coverage: { ...coverage, completedChunks: 1, complete: true, ratio: 1 },
      job: null,
    },
    {
      id: 2,
      key: "ollama:bge-m3-v2:1024:new",
      provider: "ollama",
      model: "bge-m3-v2",
      dimensions: 1024,
      status: "building",
      createdAt: "2026-10-06T01:00:00Z",
      activatedAt: null,
      retiredAt: null,
      coverage,
      job: null,
    },
  ];
  let rebuilding = false;
  await page.route("**/api/v1/knowledge/collections", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify([
        {
          resource: {
            id: collection,
            name: "採購規範",
            kind: "knowledge",
            canEdit: true,
            isOwner: true,
            updatedAt: "2026-10-06T00:00:00Z",
          },
          description: "",
          documents: 1,
          readyDocuments: 1,
        },
      ]),
    }),
  );
  await page.route("**/api/v1/admin/knowledge/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const json = (value: unknown) =>
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(value),
      });
    if (path.endsWith("/profiles")) {
      if (rebuilding)
        Object.assign(coverage, {
          completedChunks: 1,
          complete: true,
          ratio: 1,
        });
      return json(profiles);
    }
    if (path.includes("/capabilities"))
      return json({
        testStore: false,
        sql: {
          version: "17.0",
          edition: "Developer",
          majorVersion: 17,
          nativeVector: true,
          exactDistance: true,
          fullTextInstalled: true,
          traditionalChineseWordBreaker: true,
          fullTextIndex: true,
        },
        embedding: {
          provider: "ollama",
          model: "bge-m3",
          endpoint: "http://localhost:11434",
          available: true,
          notice: "批次向量化通過，維度 1024。",
        },
        rerank: {
          provider: "none",
          model: "bge-reranker-v2-m3",
          endpoint: "",
          available: true,
          notice: "未啟用重排。",
        },
      });
    if (path.endsWith("/rebuild")) {
      rebuilding = true;
      return json({
        id: randomUUID(),
        kind: "embedding-reindex",
        subjectId: randomUUID(),
        status: "queued",
        label: "重建索引",
        stage: "等待處理",
        attempt: 0,
        completedUnits: 0,
        totalUnits: 1,
      });
    }
    if (path.endsWith("/activate")) {
      profiles[0].status = "retired";
      profiles[1].status = "active";
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/vectors")) {
      profiles[0].coverage.completedChunks = 0;
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith("/search")) {
      const body = route.request().postDataJSON();
      expect(body.collectionIds).toEqual([collection]);
      expect(body.mode).toBe("hybrid");
      return json({
        mode: "hybrid",
        rewriteMs: 0,
        embedMs: 8,
        searchMs: 2,
        rerankMs: 0,
        hits: [
          {
            documentId: document,
            chunkId: randomUUID(),
            title: "採購規範",
            pageNumber: 1,
            endPage: 2,
            ordinal: 0,
            text: "主管核准後才可付款。",
            headingPath: "第三章",
            score: 0.03,
            vectorRank: 1,
            ftsRank: 2,
            rrfScore: 0.0325,
            rerankScore: null,
          },
        ],
      });
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto("/admin/operations");
  await expect(
    page.getByRole("heading", { name: "平台營運", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "知識檢索", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/operations\?tab=retrieval$/);
  const activate = page.getByRole("button", { name: "啟用索引", exact: true });
  await expect(activate).toBeDisabled();
  await page
    .locator(".retrieval-profile")
    .filter({ hasText: "bge-m3-v2" })
    .getByRole("button", { name: "開始重建" })
    .click();
  await expect(activate).toBeEnabled();
  await activate.click();
  await expect(
    toasts(page).filter({ hasText: "使用中索引已切換" }),
  ).toBeVisible();
  await page.getByRole("checkbox", { name: "採購規範", exact: true }).check();
  await page
    .getByRole("textbox", { name: "管理端查詢內容", exact: true })
    .fill("採購如何核准？");
  await page.getByRole("button", { name: "測試檢索", exact: true }).click();
  await expect(page.getByText("模式：hybrid · 1 個結果")).toBeVisible();
  await expect(page.getByText("第 1–2 頁", { exact: false })).toBeVisible();
  await expect(
    page.getByText("向量排名 1 · 全文排名 2", { exact: false }),
  ).toBeVisible();
  await page.getByRole("button", { name: "清除退役向量", exact: true }).click();
  await page
    .getByRole("dialog", { name: "清除退役向量" })
    .getByRole("button", { name: "清除向量", exact: true })
    .click();
  await expect(
    toasts(page).filter({ hasText: "退役向量已清除" }),
  ).toBeVisible();
  for (const theme of ["light", "dark"] as const) {
    const dialog = await openSettings(page);
    await chooseSelect(page, "主題", theme === "light" ? "淺色" : "深色");
    const save = dialog.getByRole("button", { name: "儲存變更", exact: true });
    if (await save.count()) {
      await save.click();
      await expect(dialog.getByRole("status")).toContainText("已儲存");
    }
    await dialog.getByRole("button", { name: "關閉設定", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await page.setViewportSize({
      width: theme === "light" ? 1440 : 375,
      height: 900,
    });
    await page.emulateMedia({ colorScheme: theme, reducedMotion: "reduce" });
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await expectCompactWorkspace(page);
    await expect(page.locator(".usage-stat-card strong").first()).toHaveCSS(
      "font-size",
      "16px",
    );
  }
});

test("稽核表格保留捲動與欄位偏好，抽屜支援逐筆檢視和手機焦點返回", async ({
  page,
}) => {
  const { audit } = await administration(page);
  for (let id = 1; id <= 205; id++)
    audit.push({
      id,
      actor: "AD\\admin",
      action: "admin.feature",
      resourceId: "chat",
      result: "saved",
      at: "2026-10-04T00:00:00Z",
      detailsJson: JSON.stringify({
        resourceKey: "chat",
        before: { name: "原名稱 " + id },
        after: { name: "新名稱 " + id },
        note: '<img src=x onerror="window.__auditInjected=true">',
      }),
    });
  const cursors: string[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname.endsWith("/admin/audit") && url.searchParams.has("before"))
      cursors.push(url.searchParams.get("before")!);
  });
  await page.goto("/admin/audit");
  const table = page.locator("nx-activity-audit-page nx-data-table"),
    scroll = table.getByRole("region", { name: "活動稽核列表" });
  await expect(table.locator(".audit-row")).toHaveCount(100);
  await expect(table.locator(".audit-row").first()).toHaveCSS(
    "font-size",
    "13px",
  );
  expect(
    (await table.locator(".audit-row").first().boundingBox())!.height,
  ).toBeLessThanOrEqual(64);
  await table.getByRole("button", { name: "顯示欄位", exact: true }).click();
  const columns = page.getByRole("dialog", { name: "顯示欄位", exact: true });
  await columns
    .getByRole("checkbox", { name: "操作者", exact: true })
    .uncheck();
  await columns.press("Escape");
  await expect(
    table.getByRole("columnheader", { name: "操作者", exact: true }),
  ).toHaveCount(0);
  await scroll.evaluate((el) => {
    el.scrollTop = 250;
  });
  const first = table.getByRole("button", {
    name: "檢視稽核：#197",
    exact: true,
  });
  await first.focus();
  const before = await scroll.evaluate((el) => el.scrollTop);
  await first.press("Enter");
  const drawer = page.getByRole("dialog", { name: "稽核詳情", exact: true });
  await expect(drawer).toBeVisible();
  await expect(drawer).not.toHaveAttribute("aria-modal", "true");
  await expect(drawer).toContainText("新名稱 197");
  expect(await scroll.evaluate((el) => el.scrollTop)).toBe(before);
  await drawer.getByRole("button", { name: "下一筆稽核", exact: true }).click();
  await expect(drawer).toContainText("新名稱 196");
  await table.locator(".audit-row").nth(9).locator("td").first().click();
  await expect(drawer).toContainText("新名稱 196");
  await expect(drawer.locator("img")).toHaveCount(0);
  expect(
    await page.evaluate(() => Boolean((window as any).__auditInjected)),
  ).toBe(false);
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(drawer).toHaveAttribute("aria-modal", "true");
  await expectViewportContained(page);
  await drawer.press("Escape");
  await expect(drawer).not.toBeVisible();
  await expect(first).toBeFocused();
  await page.setViewportSize({ width: 1440, height: 1000 });
  const nextPage = table.getByRole("button", { name: "下一頁", exact: true }),
    previousPage = table.getByRole("button", { name: "上一頁", exact: true });
  await nextPage.click();
  await expect(table.locator(".audit-row")).toHaveCount(100);
  await expect(table.locator(".audit-row").first()).toContainText("#105");
  await previousPage.click();
  await expect(table.locator(".audit-row").first()).toContainText("#205");
  await nextPage.click();
  await expect(table.locator(".audit-row").first()).toContainText("#105");
  expect(cursors).toEqual(["106"]);
  await nextPage.click();
  await expect(table.locator(".audit-row")).toHaveCount(5);
  await expect(table.locator(".audit-row").first()).toContainText("#5");
  expect(cursors).toEqual(["106", "6"]);
  await expect(nextPage).toBeDisabled();
  const download = page.waitForEvent("download");
  await table
    .getByRole("button", { name: "匯出已載入 205 筆", exact: true })
    .click();
  expect((await download).suggestedFilename()).toMatch(/-205筆\.csv$/);
  await previousPage.click();
  await previousPage.click();
  await expect(table.locator(".audit-row").first()).toContainText("#205");
  await page.getByRole("combobox", { name: "稽核動作", exact: true }).click();
  await page.getByRole("option", { name: "AI 生成", exact: true }).click();
  await expect(table.locator(".audit-row")).toHaveCount(0);
  await expect(nextPage).toBeDisabled();
  await page.reload();
  await page.goto("/admin/audit");
  await expect(
    table.getByRole("columnheader", { name: "操作者", exact: true }),
  ).toHaveCount(0);
});

test("存取管理只留四個分頁與「⋯」選單，使用者分頁及功能短表單保持緊湊", async ({
  page,
}) => {
  const state = await administration(page);
  for (let index = 0; index < 104; index++)
    state.managedUsers.push({
      ...state.bob,
      id: randomUUID(),
      displayName: "同事 " + index,
      account: "AD\\colleague" + index,
    });
  state.catalog.groups![1].enabled = false;
  state.catalog.features.push({
    id: "logs.export",
    name: "日誌匯出",
    route: "",
    sortOrder: 95,
    enabled: true,
  });
  await page.reload();
  const users = page.locator(".admin-users-table");
  await expect(users.locator("tbody tr")).toHaveCount(100);
  await users.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(users.locator("tbody tr")).toHaveCount(6);
  await expect(users).toContainText("101–106 / 106");
  await users.getByRole("button", { name: "上一頁", exact: true }).click();
  await expect(users.locator("tbody tr")).toHaveCount(100);
  const tabs = page.getByRole("group", { name: "存取管理分類", exact: true });
  await expect(tabs.getByRole("button")).toHaveText([
    "使用者",
    "角色",
    "功能群組與模型",
    "功能",
  ]);
  await expect(
    page.locator(".feature-header .page-actions button:visible"),
  ).toHaveCount(1);
  const tools = page.getByRole("button", { name: "存取管理工具", exact: true });
  await tools.click();
  const menu = page.getByRole("menu", { name: "存取管理工具", exact: true });
  await expect(menu.getByRole("menuitem")).toHaveText(["介面元件"]);
  await page.keyboard.press("Escape");
  await expect(tools).toBeFocused();
  await page.getByRole("button", { name: "功能", exact: true }).click();
  const features = page.locator(".admin-features-table");
  await expect(
    features.getByRole("region", { name: "功能配置列表" }),
  ).toBeVisible();
  // Like roles and groups: the name over its id, then the status; the route takes the rest.
  const chat = features.locator('tr[data-feature="chat"]');
  await expect(chat.locator("td").nth(1)).toHaveText(/對話\s*chat/);
  await expect(chat.locator("td").nth(2).locator("nx-status-badge")).toHaveText(
    "啟用",
  );
  await expect(chat.locator("td").nth(3)).toHaveText("/chat");
  const permission = features.locator('tr[data-feature="logs.export"]');
  await expect(permission.locator("td").nth(3)).toHaveText("無頁面，僅權限");
  await permission
    .getByRole("button", { name: "編輯功能：日誌匯出", exact: true })
    .click();
  await expect(
    page.locator("dialog.admin-editor nx-dialog-header small"),
  ).toHaveText("logs.export");
  await page
    .locator("dialog.admin-editor")
    .getByRole("button", { name: "取消", exact: true })
    .click();
  await features
    .getByRole("button", { name: "編輯功能：對話", exact: true })
    .click();
  const editor = page.locator("dialog.admin-editor");
  await expect(
    page.getByRole("dialog", { name: "編輯功能：對話", exact: true }),
  ).toContainText("chat · /chat");
  await expect(editor.locator(".panel-eyebrow")).toHaveCount(0);
  expect((await editor.boundingBox())!.width).toBeLessThanOrEqual(450);
  expect((await editor.boundingBox())!.height).toBeLessThan(440);
  await page.route("**/api/v1/admin/features/chat", async (route) => {
    const body = route.request().postDataJSON();
    // The order is moved on the list; the dialog keeps it as it is.
    expect(body).toEqual({
      name: "AI 對話",
      sortOrder: 10,
      enabled: true,
    });
    Object.assign(
      state.catalog.features.find((feature) => feature.id === "chat")!,
      body,
    );
    await route.fulfill({ status: 204 });
  });
  await expect(editor.getByLabel("顯示順序", { exact: true })).toHaveCount(0);
  await editor.getByLabel("名稱", { exact: true }).fill("AI 對話");
  await editor.getByRole("button", { name: "儲存功能", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await expect(features).toContainText("AI 對話");
  // Roles and groups share the feature table's shape: name and id, status, row activation.
  await page.getByRole("button", { name: "角色", exact: true }).click();
  const roles = page.locator(".admin-roles-table");
  await expect(roles.getByRole("region", { name: "角色列表" })).toBeVisible();
  await expect(roles.locator(".ui-table-toolbar")).toContainText("2 個");
  const member = roles.locator("tbody tr").first();
  await expect(member).toContainText("member");
  await expect(member).toContainText("2 位");
  await expect(member.locator(".resource-badges")).toHaveText("基本工作區");
  await member.locator("td").nth(2).click();
  await expect(
    page.getByRole("dialog", { name: "編輯角色：一般使用者", exact: true }),
  ).toContainText("member");
  await expect(editor.locator(".panel-eyebrow")).toHaveCount(0);
  // A section is one light frame; a checked row is not filled.
  const section = editor.locator("fieldset").first();
  await expect(section).toHaveCSS("border-left-style", "solid");
  await expect(
    editor.getByRole("checkbox", { name: "基本工作區", exact: true }),
  ).toBeChecked();
  await expect(
    editor.locator(".selection-control", {
      has: page.getByRole("checkbox", { name: "基本工作區", exact: true }),
    }),
  ).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
  await editor.getByRole("button", { name: "取消", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await page
    .getByRole("button", { name: "功能群組與模型", exact: true })
    .click();
  const groups = page.locator(".admin-groups-table");
  await expect(groups.locator("tbody tr")).toHaveCount(2);
  await expect(groups.locator("tbody tr").first()).toContainText("全部模型");
  await expect(
    groups.locator("tbody tr").nth(1).locator("nx-status-badge"),
  ).toHaveAttribute("data-tone", "warning");
  await expect(groups.locator("tbody tr").nth(1)).toContainText("停用");
  // Each feature category is its own framed card with a count; no outer fieldset wraps them.
  await groups.locator("tbody tr").first().locator("td").first().click();
  await editor.getByRole("tab", { name: "功能授權", exact: true }).click();
  await expect(editor.getByRole("tabpanel")).toHaveAttribute(
    "aria-labelledby",
    (await editor
      .getByRole("tab", { name: "功能授權", exact: true })
      .getAttribute("id"))!,
  );
  await expect(editor.locator("fieldset")).toHaveCount(0);
  const work = editor.getByRole("group", { name: "工作", exact: true });
  await expect(work).toHaveCSS("border-left-style", "solid");
  await expect(work.locator(".choice-group-count")).toHaveText(/^\d+\/\d+$/);
  await editor.getByRole("button", { name: "取消", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});

test("手機版存取管理四個分頁改為卡片列，不橫向捲動，點卡片開啟詳情或編輯", async ({
  page,
}) => {
  await administration(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await settleEntrance(page);
  const contained = async (table: Locator) => {
    await expect(table.locator(".ui-table-row").first()).toHaveCSS(
      "display",
      "grid",
    );
    expect(
      await table
        .locator(".ui-table-scroll")
        .evaluate((el) => el.scrollWidth <= el.clientWidth),
    ).toBe(true);
    await expectViewportContained(page);
  };
  // Users: name, account, status and roles; usage stays in the detail, the menu sits top-right.
  const users = page.locator(".admin-users-table");
  await contained(users);
  const card = users.locator(".ui-table-row", { hasText: "王小明" });
  await expect(card.locator('[data-stack="hide"]').first()).toBeHidden();
  await expect(card.locator(".resource-badges")).toHaveText("一般使用者");
  const box = (await card.boundingBox())!;
  const menu = (await card
    .getByRole("button", { name: "使用者操作：王小明", exact: true })
    .boundingBox())!;
  expect(menu.height).toBeGreaterThanOrEqual(44);
  expect(box.x + box.width - (menu.x + menu.width)).toBeLessThan(20);
  expect(menu.y - box.y).toBeLessThan(20);
  // Search and the add button share one row; the button keeps its text as its name.
  const search = (await page
    .getByRole("searchbox", { name: "搜尋使用者" })
    .boundingBox())!;
  const add = (await page
    .getByRole("button", { name: "新增使用者", exact: true })
    .boundingBox())!;
  expect(
    Math.abs(search.y + search.height / 2 - (add.y + add.height / 2)),
  ).toBeLessThan(2);
  expect(add.width).toBeLessThanOrEqual(46);
  await card.locator("td").nth(2).click();
  const inspector = page.getByRole("dialog", { name: "王小明", exact: true });
  await expect(inspector).toBeVisible();
  await inspector
    .getByRole("button", { name: "關閉使用者管理", exact: true })
    .click();
  await expect(inspector).not.toBeVisible();
  const editor = page.locator("dialog.admin-editor");
  for (const [tab, table, row, title] of [
    ["角色", ".admin-roles-table", "一般使用者", "編輯角色：一般使用者"],
    [
      "功能群組與模型",
      ".admin-groups-table",
      "基本工作區",
      "編輯功能群組：基本工作區",
    ],
    ["功能", ".admin-features-table", "檔案庫", "編輯功能：檔案庫"],
  ] as const) {
    await page.getByRole("button", { name: tab, exact: true }).click();
    await settleEntrance(page);
    const list = page.locator(table);
    await contained(list);
    await list
      .locator(".ui-table-row", { hasText: row })
      .locator("td")
      .nth(2)
      .click();
    await expect(
      page.getByRole("dialog", { name: title, exact: true }),
    ).toBeVisible();
    if (tab === "功能群組與模型") {
      // The daily limit takes the card's width instead of a squeezed column.
      await editor.getByRole("tab", { name: "AI 模型", exact: true }).click();
      const limit = editor.getByRole("spinbutton", {
        name: "每日 token 上限：測試模型",
      });
      expect((await limit.boundingBox())!.width).toBeGreaterThan(250);
      expect(
        await editor.evaluate((el) => el.scrollWidth <= el.clientWidth + 1),
      ).toBe(true);
    }
    await editor.getByRole("button", { name: "取消", exact: true }).click();
    await expect(editor).not.toBeVisible();
  }
  // Moving a feature on a phone goes through the card's menu; the drag handle is left out.
  await expect(
    page.getByRole("button", { name: "移動「對話」，用上下鍵調整順序" }),
  ).toBeHidden();
  await expect(
    page.getByRole("button", { name: "調整順序：對話", exact: true }),
  ).toBeVisible();
});

test("功能在分群內以鍵盤、選單或拖曳重新排序，只送出變動的順序並在失敗時還原", async ({
  page,
}) => {
  const state = await administration(page);
  state.catalog.features.push(
    {
      id: "dashboard",
      name: "總覽",
      route: "/dashboard",
      sortOrder: 5,
      enabled: true,
    },
    {
      id: "knowledge",
      name: "知識庫",
      route: "/knowledge",
      sortOrder: 30,
      enabled: true,
    },
  );
  const puts: { id: string; body: unknown }[] = [];
  let me = 0;
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname === "/api/v1/me") me++;
    if (
      request.method() === "PUT" &&
      url.pathname.startsWith("/api/v1/admin/features/")
    )
      puts.push({
        id: url.pathname.split("/").at(-1)!,
        body: request.postDataJSON(),
      });
  });
  await page.reload();
  await page.getByRole("button", { name: "功能", exact: true }).click();
  const table = page.locator(".admin-features-table");
  const work = table.locator('tbody[data-section="work"]');
  const names = work.locator(".ui-table-row .admin-table-name");
  const live = page.locator("nx-admin-features-tab p[aria-live]");
  // The list follows the sidebar's order; no bare order numbers are shown or edited.
  await expect(names).toHaveText(["總覽", "對話", "檔案庫", "知識庫"]);
  await expect(
    table.locator("td").filter({ hasText: /^\s*\d+\s*$/ }),
  ).toHaveCount(0);
  const files = page.getByRole("button", {
    name: "移動「檔案庫」，用上下鍵調整順序",
    exact: true,
  });
  await files.focus();
  const reads = me;
  await files.press("ArrowUp");
  await expect(names).toHaveText(["總覽", "檔案庫", "對話", "知識庫"]);
  await expect(files).toBeFocused();
  await expect(live).toHaveText("檔案庫：工作第 2 個，共 4 個");
  // One write: the moved feature takes the middle of its new gap (5 … 10).
  await expect
    .poll(() => puts)
    .toEqual([
      { id: "files", body: { name: "檔案庫", enabled: true, sortOrder: 7 } },
    ]);
  // The sidebar reads this account's features, and so their order, again.
  await expect.poll(() => me).toBeGreaterThan(reads);
  await expect(table).toHaveAttribute("aria-busy", "false");
  await expect(names).toHaveText(["總覽", "檔案庫", "對話", "知識庫"]);
  // A refused write puts the order back and says why.
  await page.route("**/api/v1/admin/features/files", (route) =>
    route.fulfill({
      status: 500,
      contentType: "application/problem+json",
      body: JSON.stringify({ title: "暫時無法儲存", code: "unexpected" }),
    }),
  );
  await files.press("End");
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(live).toHaveText("無法儲存順序，已還原");
  await expect(names).toHaveText(["總覽", "檔案庫", "對話", "知識庫"]);
  expect(puts.at(-1)).toEqual({
    id: "files",
    body: { name: "檔案庫", enabled: true, sortOrder: 40 },
  });
  expect(state.catalog.features.find((x) => x.id === "files")!.sortOrder).toBe(
    7,
  );
  await page.unroute("**/api/v1/admin/features/files");
  // The row menu moves without dragging, for touch and pointer users alike.
  await page
    .getByRole("button", { name: "調整順序：總覽", exact: true })
    .click();
  const menu = page.getByRole("menu", { name: "調整順序：總覽", exact: true });
  await expect(
    menu.getByRole("menuitem", { name: "上移", exact: true }),
  ).toBeDisabled();
  await menu
    .getByRole("menuitem", { name: "移到分群最後", exact: true })
    .click();
  await expect(names).toHaveText(["檔案庫", "對話", "知識庫", "總覽"]);
  await expect
    .poll(() => puts.at(-1))
    .toEqual({
      id: "dashboard",
      body: { name: "總覽", enabled: true, sortOrder: 40 },
    });
  await expect(table).toHaveAttribute("aria-busy", "false");
  // Dragging the handle swaps rows of the same group as the pointer passes them.
  const handle = (await page
    .getByRole("button", {
      name: "移動「知識庫」，用上下鍵調整順序",
      exact: true,
    })
    .boundingBox())!;
  const top = (await work.locator('tr[data-feature="files"]').boundingBox())!;
  await page.mouse.move(
    handle.x + handle.width / 2,
    handle.y + handle.height / 2,
  );
  await page.mouse.down();
  await page.mouse.move(handle.x + handle.width / 2, top.y + 4, {
    steps: 16,
  });
  await page.mouse.up();
  await expect(names).toHaveText(["知識庫", "檔案庫", "對話", "總覽"]);
  await expect
    .poll(() => puts.at(-1))
    .toEqual({
      id: "knowledge",
      body: { name: "知識庫", enabled: true, sortOrder: 0 },
    });
  expect(puts).toHaveLength(4);
  // Other groups keep their order.
  await expect(
    table.locator(
      'tbody[data-section="system"] .ui-table-row .admin-table-name',
    ),
  ).toHaveText(["存取管理", "活動稽核"]);
});

async function administration(page: Page) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
  fixture.auditAccess = true;
  await fixture.attach(page);
  const bob = {
    id: randomUUID(),
    account: "AD\\bob",
    displayName: "王小明",
    lastSeenAt: "2026-10-04T00:00:00Z",
    roleIds: ["member"],
    storage: {
      usedBytes: 2048,
      limitBytes: 5_000_000_000,
      remainingBytes: 5_000_000_000 - 2048,
      personalLimitBytes: null as number | null,
      groupLimitBytes: null,
      defaultLimitBytes: 5_000_000_000,
      limitSource: "default",
    },
  };
  const actor = {
    id: fixture.userId,
    account: "AD\\admin",
    displayName: "測試使用者",
    lastSeenAt: "2026-10-04T00:00:00Z",
    roleIds: ["member", "administrator"],
  };
  const managedUsers: AdminUserDto[] = [actor, bob];
  const catalog: AdminCatalogDto = {
    roles: [
      {
        id: "member",
        name: "一般使用者",
        enabled: true,
        groupIds: ["workspace"],
        userCount: 2,
      },
      {
        id: "administrator",
        name: "平台管理員",
        enabled: true,
        groupIds: ["administrators"],
        userCount: 1,
      },
    ],
    groups: [
      {
        id: "workspace",
        name: "基本工作區",
        enabled: true,
        featureIds: ["chat", "files"],
        policy: null,
      },
      {
        id: "administrators",
        name: "平台管理",
        enabled: true,
        featureIds: ["admin", "audit"],
        policy: null,
      },
    ],
    features: [
      {
        id: "files",
        name: "檔案庫",
        route: "/files",
        sortOrder: 15,
        enabled: true,
      },
      {
        id: "chat",
        name: "對話",
        route: "/chat",
        sortOrder: 10,
        enabled: true,
      },
      {
        id: "admin",
        name: "存取管理",
        route: "/admin",
        sortOrder: 90,
        enabled: true,
      },
      {
        id: "audit",
        name: "活動稽核",
        route: "/admin/audit",
        sortOrder: 92,
        enabled: true,
      },
    ],
    models: [
      {
        id: "fixture:8b",
        displayName: "測試模型",
        contextTokens: 8192,
        maxOutputTokens: 2048,
        reasoningEfforts: [],
        defaultReasoningEffort: "auto",
        supportsImages: true,
        supportsStreaming: true,
        supportsUsage: true,
      },
    ],
  };
  let personalPolicy = {
    allowedModelIds: null as string[] | null,
    dailyTokenLimits: {} as Record<string, number>,
  };
  const audit: AuditDto[] = [];
  const conversationId = randomUUID();
  const recordRead = (action: string, resourceId: string) =>
    audit.unshift({
      id: audit.length + 1,
      actor: actor.account,
      action,
      resourceId,
      result: "read",
      at: actor.lastSeenAt,
      detailsJson: JSON.stringify({ userId: bob.id }),
    });
  await page.route("**/api/v1/admin/**", async (route: Route) => {
    const url = new URL(route.request().url()),
      path = url.pathname.replace("/api/v1/admin", ""),
      method = route.request().method();
    const json = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    if (path === "/catalog") return json(catalog);
    if (path === "/audit/catalog")
      return json({ features: catalog.features, models: catalog.models });
    if (path.endsWith("/model-policy")) {
      if (method === "PUT") {
        personalPolicy = route.request().postDataJSON();
        return route.fulfill({ status: 204 });
      }
      return json({
        personal: personalPolicy,
        effective: {
          allowedModelIds: personalPolicy.allowedModelIds,
          storedAttachmentLimitBytes: null,
          models: catalog.models.map((model) => ({
            modelId: model.id,
            dailyTokenLimit:
              personalPolicy.dailyTokenLimits[model.id] ?? 100000,
            usedTokens: 1800,
            reservedTokens: 300,
            remainingTokens: Math.max(
              0,
              (personalPolicy.dailyTokenLimits[model.id] ?? 100000) - 2100,
            ),
            source:
              personalPolicy.dailyTokenLimits[model.id] == null
                ? "group"
                : "personal",
          })),
          resetsAt: "2026-10-07T00:00:00Z",
        },
      });
    }
    if (path === "/users") {
      if (route.request().method() === "POST") {
        const body = route.request().postDataJSON();
        const user = {
          id: randomUUID(),
          account: body.localAccount || body.adAccount,
          displayName: body.displayName,
          enabled: body.enabled,
          lastSeenAt: actor.lastSeenAt,
          roleIds: body.roleIds,
          authentication: {
            adEnabled: body.adEnabled,
            localEnabled: body.localEnabled,
            adAccount: body.adAccount,
            localAccount: body.localAccount,
            hasLocalPassword: !!body.password,
          },
        };
        managedUsers.push(user);
        return json({ id: user.id }, 201);
      }
      const search = url.searchParams.get("search") || "";
      const users = managedUsers.filter((x) =>
        (x.displayName + x.account).includes(search),
      );
      const offset = Number(url.searchParams.get("offset") || 0);
      return json({
        users: users.slice(offset, offset + 100),
        total: users.length,
        offset,
      });
    }
    if (/^\/users\/[^/]+$/.test(path)) {
      const index = managedUsers.findIndex(
        (user) => user.id === path.split("/").at(-1),
      );
      if (route.request().method() === "DELETE") {
        managedUsers.splice(index, 1);
        return route.fulfill({ status: 204 });
      }
      if (route.request().method() === "PUT") {
        const body = route.request().postDataJSON(),
          user = managedUsers[index];
        Object.assign(user, {
          displayName: body.displayName,
          enabled: body.enabled,
          roleIds: body.roleIds,
          authentication: {
            ...user.authentication,
            adEnabled: body.adEnabled,
            localEnabled: body.localEnabled,
            adAccount: body.adAccount,
            localAccount: body.localAccount,
          },
        });
        return route.fulfill({ status: 204 });
      }
    }
    if (path.endsWith("/access"))
      return json({
        roles: [],
        groups: [],
        features: [{ id: "chat", name: "對話", route: "/chat" }],
      });
    if (path.endsWith("/insights")) {
      const user =
        managedUsers.find((x) => x.id === path.split("/").at(-2)) ?? bob;
      recordRead("admin.user_usage_read", user.id);
      return json({
        user: { ...user, activity: null },
        conversations: 1,
        kinds: [
          { kind: "chat", requests: 4, inputTokens: 1200, outputTokens: 600 },
        ],
        usage: {
          days: 30,
          requests: 4,
          completed: 4,
          failed: 0,
          cancelled: 0,
          inputTokens: 1200,
          outputTokens: 600,
          requestsWithUsage: 4,
          daily: [],
          storage: bob.storage,
          totalDurationMilliseconds: 6000,
          timedRequests: 4,
        },
      });
    }
    if (path.endsWith("/conversations")) {
      recordRead("admin.conversations_list", bob.id);
      const items = [
        {
          id: conversationId,
          title: "公文內容討論",
          createdAt: actor.lastSeenAt,
          updatedAt: actor.lastSeenAt,
          isArchived: true,
          isDeleted: true,
          messages: 2,
        },
      ].filter(
        (x) =>
          url.searchParams.get("includeDeleted") === "true" &&
          x.title.includes(url.searchParams.get("search") || ""),
      );
      return json({ items, total: items.length, offset: 0 });
    }
    if (path === `/conversations/${conversationId}`) {
      recordRead("admin.conversation_read", conversationId);
      return json({
        conversation: {
          id: conversationId,
          title: "公文內容討論",
          createdAt: actor.lastSeenAt,
          updatedAt: actor.lastSeenAt,
          isArchived: true,
          isDeleted: true,
          messages: 2,
        },
        ownerAccount: bob.account,
        ownerName: bob.displayName,
        systemInstruction: "請附上來源",
        offset: 0,
        total: 2,
        messages: [
          {
            id: randomUUID(),
            parentId: null,
            role: "user",
            content: "請協助摘要公文",
            status: "completed",
            createdAt: actor.lastSeenAt,
            modelId: null,
            attachments: [],
          },
          {
            id: randomUUID(),
            parentId: null,
            role: "assistant",
            content: "## 摘要\n\n這是唯讀的 AI 回覆。",
            status: "completed",
            createdAt: actor.lastSeenAt,
            modelId: "fixture:8b",
            timing: {
              totalMilliseconds: 1500,
              queueMilliseconds: 200,
              generationMilliseconds: 1300,
              inputTokens: 300,
              outputTokens: 150,
            },
            attachments: [],
          },
        ],
      });
    }
    if (path === "/audit")
      return json(
        audit
          .filter(
            (x) =>
              (!url.searchParams.get("action") ||
                x.action.startsWith(url.searchParams.get("action")!)) &&
              (!url.searchParams.get("result") ||
                x.result === url.searchParams.get("result")) &&
              (!url.searchParams.get("category") ||
                x.category === url.searchParams.get("category")) &&
              (!url.searchParams.get("traceId") ||
                x.traceId === url.searchParams.get("traceId")) &&
              (!url.searchParams.get("before") ||
                x.id < Number(url.searchParams.get("before"))),
          )
          .sort((a, b) => b.id - a.id)
          .slice(0, 100),
      );
    if (path === "/usage")
      return json({
        users: 2,
        activeUsers: 1,
        failed: 0,
        cancelled: 0,
        storedBytes: 2048,
        storedFiles: 1,
        since: "2026-09-07T00:00:00Z",
        until: "2026-10-06T01:00:00Z",
        models: [
          {
            modelId: "fixture:8b",
            requests: 4,
            failed: 0,
            requestsWithUsage: 4,
            inputTokens: 1200,
            outputTokens: 600,
            durationMilliseconds: 6000,
          },
        ],
        kinds: [
          { kind: "chat", requests: 4, inputTokens: 1200, outputTokens: 600 },
        ],
        providers: [{ id: "ollama", available: true, notice: null }],
        webSearch: { available: false, notice: "尚未部署地端搜尋服務。" },
        requests: 4,
        completed: 4,
        inputTokens: 1200,
        outputTokens: 600,
        requestsWithUsage: 4,
        totalDurationMilliseconds: 6000,
        timedRequests: 4,
      });
    if (method === "PUT") {
      const body = route.request().postDataJSON();
      if (path === `/users/${bob.id}/storage`) {
        bob.storage.personalLimitBytes = body.limitBytes;
        bob.storage.limitBytes = body.limitBytes ?? 5_000_000_000;
        bob.storage.remainingBytes = Math.max(
          0,
          bob.storage.limitBytes - bob.storage.usedBytes,
        );
        bob.storage.limitSource =
          body.limitBytes == null ? "default" : "personal";
        return route.fulfill({ status: 204 });
      }
      if (
        path === `/users/${actor.id}/roles` &&
        !body.roleIds.includes("administrator")
      )
        return json(
          {
            title: "此變更會撤銷你的管理權限。請先由另一位管理員處理。",
            code: "admin_lockout",
          },
          409,
        );
      if (path === `/users/${bob.id}/roles`) bob.roleIds = body.roleIds;
      if (path.startsWith("/groups/")) {
        const id = path.split("/").at(-1)!;
        const old = catalog.groups.find((x) => x.id === id);
        if (old) Object.assign(old, body);
        else catalog.groups.push({ id, ...body });
      }
      if (path.startsWith("/features/"))
        Object.assign(
          catalog.features.find((x) => x.id === path.split("/").at(-1))!,
          body,
        );
      if (path.startsWith("/roles/")) {
        const id = path.split("/").at(-1)!;
        const old = catalog.roles.find((x) => x.id === id);
        if (old) Object.assign(old, body);
        else catalog.roles.push({ id, ...body, userCount: 0 });
      }
      audit.unshift({
        id: audit.length + 1,
        actor: "AD\\admin",
        action: path.includes("/groups/") ? "admin.group" : "admin.user_roles",
        resourceId: bob.id,
        result: "saved",
        at: "2026-10-04T00:00:00Z",
        detailsJson: JSON.stringify(body),
      });
      return route.fulfill({ status: 204 });
    }
    return json({}, 404);
  });
  await page.goto("/admin");
  await expect(
    page.getByRole("heading", { name: "存取管理", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("王小明", { exact: true })).toBeVisible();
  return { fixture, catalog, bob, actor, managedUsers, audit, conversationId };
}
test("administrators edit roles with effective access preview and an audit trail", async ({
  page,
}) => {
  const state = await administration(page);
  // The inspector is the one place to edit a user; the row keeps no second edit button.
  await expect(page.getByRole("button", { name: /^設定角色：/ })).toHaveCount(
    0,
  );
  await page
    .getByRole("button", { name: "管理使用者：王小明", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
  await dialog.getByRole("tab", { name: "角色與權限", exact: true }).click();
  const save = dialog.getByRole("button", { name: "儲存角色", exact: true });
  await expect(save).toBeDisabled();
  await dialog
    .getByRole("checkbox", { name: "平台管理員", exact: true })
    .check();
  await expect(dialog.locator("nx-feature-summary")).toContainText("管理");
  await expect(dialog).toContainText("有未儲存的變更");
  await save.click();
  await expect(save).toBeDisabled();
  await expect(dialog).not.toContainText("有未儲存的變更");
  expect(state.bob.roleIds).toContain("administrator");
  await expect(dialog.locator(".summary-roles")).toContainText("平台管理員");
  await page.goto("/admin/audit");
  await expect(page.getByText("調整使用者角色", { exact: true })).toBeVisible();
  // Reading the user again after the save is audited too; open the save itself.
  await page
    .locator(".audit-row", { hasText: "調整使用者角色" })
    .getByRole("button", { name: /^檢視稽核：#/ })
    .click();
  await expect(
    page.getByRole("dialog", { name: "稽核詳情" }).locator("pre"),
  ).toContainText("administrator");
  await settleEntrance(page);
  await expectCompactWorkspace(page);
});

test("manual user creation, dual login editing and removal keep one place for each task", async ({
  page,
}) => {
  const state = await administration(page);
  await page.getByRole("button", { name: "新增使用者", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "新增使用者", exact: true });
  await dialog.getByLabel("使用者姓名", { exact: true }).fill("本地測試者");
  await dialog.getByLabel("本地登入帳號", { exact: true }).fill("local-tester");
  await dialog
    .getByLabel("設定本地密碼", { exact: true })
    .fill("test long password 123!");
  await expect(dialog.locator(".feature-summary-group")).toContainText([
    "工作",
  ]);
  await page.setViewportSize({ width: 375, height: 812 });
  await settleEntrance(page);
  expect(
    await dialog.evaluate(
      (element) => element.scrollWidth <= element.clientWidth,
    ),
  ).toBe(true);
  await dialog.getByRole("button", { name: "建立使用者", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  const created = state.managedUsers.find(
    (user) => user.displayName === "本地測試者",
  )!;
  expect(created.authentication.localEnabled).toBe(true);
  expect(created.authentication.adEnabled).toBe(false);
  expect(JSON.stringify(created)).not.toContain("test long password 123!");
  await page.setViewportSize({ width: 1440, height: 1000 });
  const manage = page.getByRole("button", {
    name: "管理使用者：本地測試者",
    exact: true,
  });
  await manage.click();
  // The dialog is named by the user, so it is followed through the rename below.
  const edit = page.locator("dialog.admin-inspector");
  await expect(edit).toHaveAccessibleName("本地測試者");
  await expect(edit).toContainText("登入：本地密碼");
  await expect(edit.getByLabel("設定本地密碼", { exact: true })).toHaveValue(
    "",
  );
  // Leaving a tab with unsaved changes asks first; cancelling keeps the draft.
  await edit.getByLabel("使用者姓名", { exact: true }).fill("本地測試員");
  await edit.getByRole("tab", { name: "對話", exact: true }).click();
  const discard = page.getByRole("dialog", {
    name: "放棄未儲存的變更？",
    exact: true,
  });
  await expect(discard).toContainText("帳號");
  await discard.getByRole("button", { name: "取消", exact: true }).click();
  await expect(edit.getByLabel("使用者姓名", { exact: true })).toHaveValue(
    "本地測試員",
  );
  await expect(
    edit.getByRole("tab", { name: "帳號", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await edit
    .getByRole("checkbox", { name: "允許 AD 驗證", exact: true })
    .check();
  await edit.getByLabel("使用者 AD 帳號", { exact: true }).fill("test-ad");
  await edit.getByRole("button", { name: "儲存帳號", exact: true }).click();
  await expect(edit).toContainText("登入：AD · 本地密碼");
  await expect(edit).toHaveAccessibleName("本地測試員");
  await expect(edit).not.toContainText("有未儲存的變更");
  expect(created.authentication.adEnabled).toBe(true);
  expect(created.displayName).toBe("本地測試員");
  // Saved: Escape closes without asking and focus returns to the row.
  await page.keyboard.press("Escape");
  await expect(edit).not.toBeVisible();
  await expect(
    page.getByRole("button", { name: "管理使用者：本地測試員", exact: true }),
  ).toBeFocused();
  await page
    .getByRole("button", { name: "使用者操作：本地測試員", exact: true })
    .click();
  await expect(page.getByRole("menuitem")).toHaveText([
    "以此身分測試",
    "刪除使用者",
  ]);
  await page.getByRole("menuitem", { name: "刪除使用者", exact: true }).click();
  const confirm = page.getByRole("dialog", {
    name: "刪除使用者：本地測試員",
    exact: true,
  });
  await expect(confirm).toContainText("仍會保留");
  await confirm
    .getByRole("button", { name: "刪除使用者", exact: true })
    .click();
  await expect(page.getByText("本地測試員", { exact: true })).toHaveCount(0);
});

test("testing an identity clears the prior draft, shows a responsive banner and returns to management", async ({
  page,
}) => {
  const { fixture, actor, bob } = await administration(page);
  let testing = false;
  const session = () => ({
    mode: "Windows",
    authenticated: true,
    configured: true,
    methods: ["windows", "local"],
    method: testing ? "test" : "windows",
    csrfToken: "browser-test-csrf",
    account: testing ? bob.account : actor.account,
    displayName: testing ? bob.displayName : actor.displayName,
    userId: testing ? bob.id : actor.id,
    testing: testing
      ? {
          administratorId: actor.id,
          administratorName: actor.displayName,
          userId: bob.id,
          expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        }
      : null,
  });
  await page.route("**/api/v1/auth/**", async (route) => {
    if (route.request().url().endsWith("/test-identity")) {
      expect(route.request().postDataJSON().userId).toBe(bob.id);
      testing = true;
    } else if (route.request().url().endsWith("/test-identity/end"))
      testing = false;
    await route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(session()),
    });
  });
  await page.route("**/api/v1/me", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        id: testing ? bob.id : actor.id,
        account: session().account,
        displayName: session().displayName,
        preferences: fixture.preferences,
        csrfToken: "browser-test-csrf",
        activeRunId: null,
        access: {
          roles: [],
          groups: [],
          features: [
            { id: "chat", name: "對話", route: "/chat" },
            ...(!testing
              ? [{ id: "admin", name: "存取管理", route: "/admin" }]
              : []),
          ],
        },
      }),
    }),
  );
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("管理者私人草稿");
  await page.goto("/admin");
  await page
    .getByRole("button", { name: "使用者操作：王小明", exact: true })
    .click();
  await page
    .getByRole("menuitem", { name: "以此身分測試", exact: true })
    .click();
  await page.getByLabel("測試目的", { exact: true }).fill("核對一般使用者權限");
  await page.getByRole("button", { name: "開始身分測試", exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  const banner = page.getByRole("region", {
    name: "管理者測試身分",
    exact: true,
  });
  await expect(banner).toContainText("王小明");
  await expect(banner).toContainText("15 分鐘");
  await page.goto("/chat");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue("");
  for (const width of [1920, 375]) {
    await page.setViewportSize({ width, height: 900 });
    await expectViewportContained(page);
    await settleEntrance(page);
  }
  await banner.getByRole("button", { name: "返回管理者", exact: true }).click();
  await expect(page).toHaveURL(/\/admin$/);
  await expect(banner).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "存取管理", exact: true }),
  ).toBeVisible();
});
test("feature notes, audit and platform usage stay aligned on wide and narrow screens", async ({
  page,
}) => {
  const { audit, catalog } = await administration(page);
  catalog.features[0].name = "對話功能與模型管理";
  audit.push({
    id: 1,
    actor: "AD\\admin",
    action: "admin.feature",
    resourceId: "chat",
    result: "saved",
    at: "2026-10-04T00:00:00Z",
    detailsJson: JSON.stringify({
      featureId: "chat",
      before: { name: "對話" },
      after: { name: "對話功能與模型管理" },
    }),
  });
  for (const width of [1920, 1440, 860, 375]) {
    await page.setViewportSize({ width, height: 900 });
    for (const tab of ["功能", "平台用量", "活動稽核"]) {
      if (tab === "活動稽核") await page.goto("/admin/audit");
      else if (tab === "平台用量") await page.goto("/admin/operations");
      else await page.goto("/admin");
      const tabButton = page.getByRole("button", { name: tab, exact: true });
      if (tab !== "活動稽核") {
        await tabButton.click();
        await expect(tabButton).toHaveAttribute("aria-pressed", "true");
        // The tab content arrives with a slight scale; measure where it settles.
        await settleEntrance(page);
      }
      const target =
        tab === "活動稽核"
          ? "nx-filter-panel"
          : tab === "平台用量"
            ? ".feature-content > nx-platform-usage > .page-toolbar"
            : ".feature-content nx-admin-features-tab > .page-toolbar";
      await expect(page.locator(target).first()).toBeVisible();
      if (tab === "平台用量") {
        await expect(page.locator(".stat-card")).toHaveCount(4);
        // The tabs are the way to prices; the usage tab only refreshes itself.
        await expect(page.locator("nx-platform-usage a")).toHaveCount(0);
        await expect(
          page.locator("nx-platform-usage .page-actions button"),
        ).toHaveCount(1);
      }
      await expectCompactWorkspace(page);
      const header = await page.locator(".feature-header").boundingBox();
      const content = await page.locator(target).first().boundingBox();
      expect(Math.abs(header!.x - content!.x)).toBeLessThan(1);
      expect(Math.abs(header!.width - content!.width)).toBeLessThan(1);
      await expectViewportContained(page);
      if (width === 1920 || width === 375) {
        await settleEntrance(page);
      }
    }
  }
});
test("group model limits and self-lockout errors work on desktop and mobile", async ({
  page,
}) => {
  const state = await administration(page);
  await page
    .getByRole("button", { name: "功能群組與模型", exact: true })
    .click();
  await page.getByRole("button", { name: "新增功能群組", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(
    page.getByRole("dialog", { name: "新增功能群組", exact: true }),
  ).toBeVisible();
  await dialog
    .getByRole("textbox", { name: "識別碼", exact: true })
    .fill("research");
  await dialog
    .getByRole("textbox", { name: "名稱", exact: true })
    .fill("研發工作區");
  await dialog.getByRole("tab", { name: "功能授權", exact: true }).click();
  await dialog.getByRole("checkbox", { name: "對話", exact: true }).check();
  await dialog.getByRole("tab", { name: "AI 模型", exact: true }).click();
  await dialog.getByRole("checkbox", { name: "限制可用模型" }).check();
  await dialog.getByRole("checkbox", { name: "測試模型", exact: true }).check();
  await dialog
    .getByRole("spinbutton", { name: "每日 token 上限：測試模型" })
    .fill("50000");
  await dialog.getByRole("button", { name: "建立功能群組" }).click();
  await expect(dialog).not.toBeVisible();
  expect(
    state.catalog.groups?.find((x) => x.id === "research")?.policy
      ?.dailyTokenLimits?.["fixture:8b"],
  ).toBe(50000);
  await settleEntrance(page);
  await page.getByRole("button", { name: "使用者", exact: true }).click();
  await page
    .getByRole("button", { name: "管理使用者：測試使用者", exact: true })
    .click();
  const self = page.getByRole("dialog", { name: "測試使用者", exact: true });
  await self.getByRole("tab", { name: "角色與權限", exact: true }).click();
  await self
    .getByRole("checkbox", { name: "平台管理員", exact: true })
    .uncheck();
  await self.getByRole("button", { name: "儲存角色", exact: true }).click();
  await expect(self.getByRole("alert")).toContainText("撤銷你的管理權限");
  // The refused draft is still unsaved, so closing asks before discarding it.
  await self
    .getByRole("button", { name: "關閉使用者管理", exact: true })
    .click();
  await page
    .getByRole("dialog", { name: "放棄未儲存的變更？", exact: true })
    .getByRole("button", { name: "放棄變更", exact: true })
    .click();
  await expect(self).not.toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  await page.getByRole("searchbox", { name: "搜尋使用者" }).fill("王小明");
  await expect(
    page.getByRole("button", { name: "管理使用者：王小明", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await expectCompactWorkspace(page);
});
test("members have no management navigation and direct routes show an access explanation", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/admin");
  await expect(page.getByRole("alert")).toContainText("需要「存取管理」權限");
  await page.goto("/admin/operations");
  await expect(page.getByRole("alert")).toContainText("需要「平台營運」權限");
  for (const name of ["存取管理", "平台營運"])
    await expect(
      page
        .getByRole("navigation", { name: "工作區功能" })
        .getByRole("link", { name, exact: true }),
    ).toHaveCount(0);
});

test("administrators inspect user usage and deleted conversations through an audited read only view", async ({
  page,
}) => {
  const state = await administration(page);
  await page
    .getByRole("button", { name: "管理使用者：王小明", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator(".inspector-stats")).toContainText("1,800");
  // Conversations are listed, and audited, only once their tab is opened.
  expect(state.audit.some((x) => x.action === "admin.conversations_list")).toBe(
    false,
  );
  await dialog.getByRole("tab", { name: "附件容量", exact: true }).click();
  await expect(dialog.locator("nx-storage-usage")).toContainText("5.00 GB");
  await dialog.getByLabel("個人容量上限（GB）", { exact: true }).fill("10");
  await dialog
    .getByRole("button", { name: "儲存容量上限", exact: true })
    .click();
  await expect(dialog.locator("nx-storage-usage")).toContainText("10.00 GB");
  expect(state.bob.storage.personalLimitBytes).toBe(10_000_000_000);
  await dialog.getByLabel("個人容量上限（GB）", { exact: true }).fill("");
  await dialog
    .getByRole("button", { name: "儲存容量上限", exact: true })
    .click();
  await expect(dialog.locator("nx-storage-usage")).toContainText("5.00 GB");
  expect(state.bob.storage.personalLimitBytes).toBeNull();
  await dialog.getByRole("tab", { name: "對話", exact: true }).click();
  await expect(dialog).toContainText("開啟清單與內容都會寫入稽核");
  await dialog
    .getByRole("button", { name: "檢視對話：公文內容討論", exact: true })
    .click();
  await expect(
    dialog.getByRole("heading", { name: "摘要", exact: true }),
  ).toBeVisible();
  await expect(dialog.locator(".inspector-user-message")).toContainText(
    "請協助摘要公文",
  );
  await dialog.locator("nx-run-timing summary").click();
  await expect(dialog.locator("nx-run-timing details")).toContainText(
    "輸入 300",
  );
  await expect(dialog.locator("nx-run-timing details")).toContainText("1.5 秒");
  await expect(
    dialog.getByRole("button", { name: "送出訊息", exact: true }),
  ).toHaveCount(0);
  expect(
    state.audit.some(
      (x) =>
        x.action === "admin.conversation_read" &&
        x.resourceId === state.conversationId,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await dialog
    .getByRole("checkbox", { name: "包含已刪除對話", exact: true })
    .uncheck();
  await expect(
    dialog.getByRole("button", { name: "檢視對話：公文內容討論", exact: true }),
  ).toHaveCount(0);
  await page.setViewportSize({ width: 375, height: 812 });
  await dialog.getByRole("tab", { name: "附件容量", exact: true }).click();
  await dialog.getByLabel("個人容量上限（GB）", { exact: true }).fill("10");
  await dialog
    .getByRole("button", { name: "儲存容量上限", exact: true })
    .click();
  await expect(dialog.locator("nx-storage-usage")).toContainText("10.00 GB");
  expect(
    await dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1),
  ).toBe(true);
  await dialog
    .getByRole("button", { name: "關閉使用者管理", exact: true })
    .click();
  await page.goto("/admin/audit");
  await chooseSelect(page, "稽核動作", "對話內容檢視");
  await expect(page.locator(".audit-row")).toHaveCount(1);
  const download = page.waitForEvent("download");
  await page
    .getByRole("button", { name: "匯出已載入 1 筆", exact: true })
    .click();
  expect((await download).suggestedFilename()).toContain("1筆.csv");
});

test("audit shows readable before after differences and server filters", async ({
  page,
}) => {
  const state = await administration(page);
  state.audit.push({
    id: 1,
    actor: "AD\\admin",
    action: "admin.role",
    resourceId: null,
    result: "saved",
    at: "2026-10-04T00:00:00Z",
    detailsJson: JSON.stringify({
      resourceKey: "analyst",
      before: {
        name: "分析人員",
        enabled: false,
        policy: { dailyTokenLimits: { "fixture:8b": 100000 } },
      },
      after: {
        name: "資深分析人員",
        enabled: true,
        policy: { dailyTokenLimits: { "fixture:8b": 150000 } },
      },
    }),
  });
  await page.goto("/admin/audit");
  await page.getByRole("button", { name: "檢視稽核：#1", exact: true }).click();
  await expect(page.locator(".audit-changes")).toContainText("資深分析人員");
  await expect(page.locator(".audit-before").first()).toContainText("分析人員");
  await expect(page.locator(".audit-changes")).toContainText(
    "每日 token 上限 · 測試模型",
  );
  await expect(page.locator(".audit-changes")).toContainText("100,000 tokens");
  await expect(page.locator(".audit-changes")).toContainText("150,000 tokens");
  await chooseSelect(page, "稽核結果", "已檢視");
  await expect(page.locator(".audit-row")).toHaveCount(0);
  await expect(
    page.getByRole("dialog", { name: "稽核詳情" }),
  ).not.toBeVisible();
});

test("audit dates validate input, keep Taipei boundaries and allow clearing optional filters", async ({
  page,
}) => {
  await administration(page);
  const queries: URL[] = [];
  await page.route("**/api/v1/admin/audit*", async (route) => {
    queries.push(new URL(route.request().url()));
    await route.fallback();
  });
  await page.goto("/admin/audit");
  const start = page.getByRole("textbox", {
    name: "稽核開始日期",
    exact: true,
  });
  const end = page.getByRole("textbox", { name: "稽核結束日期", exact: true });
  await start.fill("2026/10/04");
  await expect
    .poll(() => queries.at(-1)?.searchParams.get("from"))
    .toBe("2026-10-04T00:00:00+08:00");
  await end.fill("2026/10/07");
  await expect
    .poll(() => queries.at(-1)?.searchParams.get("until"))
    .toBe("2026-10-08T00:00:00+08:00");
  const requests = queries.length;
  await start.fill("2026/02/30");
  await start.press("Tab");
  await expect(start).toHaveAttribute("aria-invalid", "true");
  await expect(
    page.getByRole("button", { name: "重新整理稽核", exact: true }),
  ).toBeDisabled();
  await chooseSelect(page, "稽核結果", "已檢視");
  expect(queries.length).toBe(requests);
  await start.fill("");
  await start.press("Tab");
  await expect.poll(() => queries.length).toBeGreaterThan(requests);
  expect(queries.at(-1)?.searchParams.has("from")).toBe(false);
  const beforeClear = queries.length;
  await end.fill("");
  await end.press("Tab");
  await expect.poll(() => queries.length).toBeGreaterThan(beforeClear);
  expect(queries.at(-1)?.searchParams.has("until")).toBe(false);
  await expect(page.locator(".ui-date-error")).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "重新整理稽核", exact: true }),
  ).toBeEnabled();
});

test("a delayed initial conversation list cannot overwrite a newer filter", async ({
  page,
}) => {
  const state = await administration(page);
  let release!: () => void;
  let arrived!: () => void;
  const pending = new Promise<void>((resolve) => (release = resolve));
  const received = new Promise<void>((resolve) => (arrived = resolve));
  let initial = true;
  await page.route("**/api/v1/admin/users/*/conversations*", async (route) => {
    if (!initial) return route.fallback();
    initial = false;
    arrived();
    await pending;
    await route.fulfill({
      json: {
        items: [
          {
            id: state.conversationId,
            title: "公文內容討論",
            createdAt: state.bob.lastSeenAt,
            updatedAt: state.bob.lastSeenAt,
            isArchived: true,
            isDeleted: true,
            messages: 2,
          },
        ],
        total: 1,
        offset: 0,
      },
    });
  });
  await page
    .getByRole("button", { name: "管理使用者：王小明", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
  await dialog.getByRole("tab", { name: "對話", exact: true }).click();
  await received;
  await dialog
    .getByRole("checkbox", { name: "包含已刪除對話", exact: true })
    .uncheck();
  await expect(
    dialog.getByText("沒有符合的對話。", { exact: true }),
  ).toBeVisible();
  release();
  await expect(dialog.locator(".inspector-stats")).toBeVisible();
  await expect(
    dialog.getByRole("button", { name: "檢視對話：公文內容討論", exact: true }),
  ).toHaveCount(0);
});

test("personal model budgets share the group editor; settings stay form sized and conversations get room", async ({
  page,
}) => {
  await administration(page);
  await page
    .getByRole("button", { name: "管理使用者：王小明", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
  await expect(dialog.getByLabel("個人容量上限（GB）")).toHaveCount(0);
  await expect(dialog.getByLabel("使用者姓名", { exact: true })).toHaveValue(
    "王小明",
  );
  await expect(dialog.locator(".inspector-stats")).toBeVisible();
  await settleEntrance(page);
  const account = (await dialog.boundingBox())!;
  // Who the user is reads first; the three figures run underneath on one row.
  const identity = (await dialog.locator(".summary-identity").boundingBox())!;
  const stats = dialog.locator(".summary-stat");
  await expect(stats).toHaveCount(3);
  const first = (await stats.first().boundingBox())!;
  const last = (await stats.last().boundingBox())!;
  expect(first.y).toBeGreaterThanOrEqual(identity.y + identity.height - 1);
  expect(Math.abs(first.y - last.y)).toBeLessThan(1);
  expect(Math.abs(first.x - identity.x)).toBeLessThan(1);
  // Sections are divided by hairlines, never framed; on a wide dialog the title sits beside the
  // fields and a checkbox's box lines up with their edge. With nothing to save both actions rest.
  await expect(dialog.locator("fieldset")).toHaveCount(0);
  const login = dialog.getByRole("group", { name: "登入方式", exact: true });
  const loginTitle = (await login.getByRole("heading").boundingBox())!;
  const nameField = (await dialog
    .getByLabel("使用者姓名", { exact: true })
    .boundingBox())!;
  expect(nameField.x).toBeGreaterThan(loginTitle.x + loginTitle.width);
  const box = (await dialog.locator(".selection-box").first().boundingBox())!;
  expect(Math.abs(box.x - nameField.x)).toBeLessThan(1);
  await expect(dialog.locator(".dialog-foot")).toContainText("尚未變更");
  await expect(
    dialog.getByRole("button", { name: "還原", exact: true }),
  ).toBeDisabled();
  await dialog.getByRole("tab", { name: "AI 模型", exact: true }).click();
  await expect(dialog).toContainText("100,000 tokens / 日");
  // Secondary rules are one click away; Escape closes that panel and keeps the dialog open.
  const rules = dialog.getByRole("button", { name: "額度規則", exact: true });
  await rules.click();
  const panel = page.getByRole("dialog", { name: "模型授權與額度規則" });
  await expect(panel).toContainText("共用預算");
  await page.keyboard.press("Escape");
  await expect(panel).toBeHidden();
  await expect(dialog).toBeVisible();
  await expect(rules).toBeFocused();
  await expect(
    dialog.getByRole("region", { name: "模型授權與額度" }),
  ).toBeVisible();
  expect(
    (await dialog
      .locator(".model-policy-table tbody tr")
      .first()
      .boundingBox())!.height,
  ).toBeLessThan(110);
  await settleEntrance(page);
  expect(
    Math.abs((await dialog.boundingBox())!.width - account.width),
  ).toBeLessThan(1);
  await dialog.getByRole("tab", { name: "附件容量", exact: true }).click();
  await expect(dialog.getByLabel("個人容量上限（GB）")).toBeVisible();
  await settleEntrance(page);
  expect(
    Math.abs((await dialog.boundingBox())!.width - account.width),
  ).toBeLessThan(1);
  await dialog.getByRole("tab", { name: "對話", exact: true }).click();
  await expect(dialog.locator(".conversation-body")).toBeVisible();
  await settleEntrance(page);
  const reading = (await dialog.boundingBox())!;
  expect(reading.width).toBeGreaterThan(account.width + 100);
  expect(
    (await dialog.locator(".conversation-body").boundingBox())!.height,
  ).toBeGreaterThan(400);
  // The pager is not shown for a single page; the count sits beside the filter.
  await expect(dialog.locator(".conversation-pages")).toHaveCount(0);
  await expect(dialog.locator(".conversation-filter")).toContainText("1 段");
  await dialog.getByRole("tab", { name: "AI 模型", exact: true }).click();
  await dialog
    .getByLabel("每日 token 上限：測試模型", { exact: true })
    .fill("150000");
  await dialog
    .getByRole("button", { name: "儲存模型政策", exact: true })
    .click();
  await expect(dialog).toContainText("150,000 tokens / 日");
  await expect(dialog).toContainText("個人設定");
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await expect
    .poll(() => dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1))
    .toBe(true);
  await dialog
    .getByLabel("每日 token 上限：測試模型", { exact: true })
    .fill("");
  await dialog
    .getByRole("button", { name: "儲存模型政策", exact: true })
    .click();
  await expect(dialog).toContainText("群組設定");
  await expect(
    dialog.getByRole("tablist", { name: "使用者管理分類" }),
  ).toBeInViewport();
  await expect(dialog.locator(".inspector-stats")).toBeInViewport();
  await expect(
    dialog.getByRole("button", { name: "儲存模型政策", exact: true }),
  ).toBeInViewport({
    ratio: 1,
  });
});

test("many model policies keep group tabs and save controls reachable on small screens", async ({
  page,
}) => {
  const state = await administration(page);
  state.catalog.models = Array.from({ length: 12 }, (_, index) => ({
    ...state.catalog.models![0],
    id: `fixture:${index}`,
    displayName: `地端模型 ${index + 1}`,
  }));
  state.fixture.preferences.theme = "system";
  await page.reload();
  await page
    .getByRole("button", { name: "功能群組與模型", exact: true })
    .click();
  await page
    .getByRole("button", { name: "編輯功能群組：基本工作區", exact: true })
    .click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("tab", { name: "AI 模型", exact: true }).click();
  for (const viewport of [
    { width: 1440, height: 1000 },
    { width: 375, height: 812 },
    { width: 812, height: 375 },
  ]) {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });
    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
    await dialog
      .getByLabel("每日 token 上限：地端模型 12", { exact: true })
      .fill("50000");
    await expect(
      dialog.getByRole("tablist", { name: "功能群組設定分類" }),
    ).toBeInViewport({ ratio: 1 });
    await expect(
      dialog.getByRole("button", { name: "儲存功能群組", exact: true }),
    ).toBeInViewport({
      ratio: 1,
    });
    expect(
      await dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1),
    ).toBe(true);
    await page.emulateMedia({ colorScheme: "light" });
    await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
    await expect(
      dialog.getByRole("button", { name: "儲存功能群組", exact: true }),
    ).toBeInViewport({
      ratio: 1,
    });
  }
  await dialog
    .getByRole("button", { name: "儲存功能群組", exact: true })
    .click();
  expect(
    state.catalog.groups!.find((group) => group.id === "workspace")!.policy!
      .dailyTokenLimits!["fixture:11"],
  ).toBe(50000);
});

test("activity audit separates sign-ins and opens a historical trace in system logs", async ({
  page,
}) => {
  const state = await administration(page);
  state.fixture.extraFeatures.push({
    id: "logs.query",
    name: "系統日誌",
    route: "/admin/logs",
  });
  const trace = "1234567890abcdef1234567890abcdef",
    at = "2026-09-12T08:00:00Z";
  state.audit.push({
    id: 400,
    actor: "未驗證",
    actingAs: null,
    action: "identity.login",
    result: "failed",
    resourceId: null,
    at,
    category: "authentication",
    traceId: trace,
    operationId: randomUUID(),
    issueCode: "NX-" + "A".repeat(32),
    detailsJson: JSON.stringify({
      account: "unverified-account",
      authentication: "local",
      clientAddress: "203.0.113.8",
      failureCode: "invalid_credentials",
    }),
  });
  const logQueries: URL[] = [];
  await page.route("**/api/v1/admin/logs**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/health"))
      return route.fulfill({
        status: 503,
        json: { code: "service_unavailable" },
      });
    logQueries.push(url);
    return route.fulfill({
      json: {
        events: [],
        nextCursor: null,
        from: url.searchParams.get("from"),
        to: url.searchParams.get("to"),
      },
    });
  });
  await page.goto("/admin/audit?category=authentication");
  const audit = page.locator("nx-activity-audit-page");
  await expect(audit.locator(".audit-row")).toHaveCount(1);
  await audit
    .getByRole("button", { name: "檢視稽核：#400", exact: true })
    .click();
  const drawer = page.getByRole("dialog", { name: "稽核詳情", exact: true });
  await expect(drawer).toContainText("unverified-account");
  await expect(drawer).toContainText("本地帳號");
  await expect(drawer).toContainText("203.0.113.8");
  await expect(drawer).toContainText(trace);
  await drawer.getByRole("link", { name: "查證相關日誌", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/logs\?/);
  await expect.poll(() => logQueries.length).toBeGreaterThan(0);
  expect(logQueries[0].searchParams.get("traceId")).toBe(trace);
  expect(Date.parse(logQueries[0].searchParams.get("from")!)).toBe(
    Date.parse(at) - 300_000,
  );
  expect(Date.parse(logQueries[0].searchParams.get("to")!)).toBe(
    Date.parse(at) + 300_000,
  );
  await page
    .locator(".feature-header")
    .getByRole("link", { name: "活動稽核", exact: true })
    .click();
  await expect(audit.locator(".audit-row")).toHaveCount(state.audit.length);
  await audit.getByRole("button", { name: "登入與身分", exact: true }).click();
  await expect(audit.locator(".audit-row")).toHaveCount(1);
});

test("dialog tabs animate their content and necessary frame changes, preserve focus and respect reduced motion", async ({
  page,
}) => {
  await administration(page);
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await page.addStyleTag({ content: ":root { --motion-panel: 600ms; }" });
  await page
    .getByRole("button", { name: "管理使用者：王小明", exact: true })
    .click();
  const inspector = page.getByRole("dialog", { name: "王小明", exact: true });
  await settleEntrance(page);
  const frame = (await inspector.boundingBox())!;
  const models = inspector.getByRole("tab", {
    name: "AI 模型",
    exact: true,
  });
  await models.click();
  await expect
    .poll(() =>
      inspector
        .getByRole("tabpanel")
        .evaluate((el) =>
          el
            .getAnimations()
            .some(
              (a) =>
                a.effect instanceof KeyframeEffect &&
                a.effect
                  .getKeyframes()
                  .some((frame) => frame.transform?.includes("scale")),
            ),
        ),
    )
    .toBe(true);
  await expect(models).toBeFocused();
  await settleEntrance(page);
  // Settings tabs share the form width; only their height follows the content.
  const after = (await inspector.boundingBox())!;
  expect(Math.abs(after.width - frame.width)).toBeLessThan(1);
  // Reading conversations needs a wider frame, which moves rather than jumps.
  await inspector.getByRole("tab", { name: "對話", exact: true }).click();
  await expect
    .poll(() =>
      inspector.evaluate((el) =>
        el
          .getAnimations()
          .some(
            (a) =>
              a.effect instanceof KeyframeEffect &&
              a.effect.getKeyframes().some((frame) => !!frame.width),
          ),
      ),
    )
    .toBe(true);
  await settleEntrance(page);
  await page.emulateMedia({ reducedMotion: "reduce" });
  await inspector.getByRole("tab", { name: "附件容量", exact: true }).click();
  expect(
    await inspector
      .getByRole("tabpanel")
      .evaluate((el) => el.getAnimations().length),
  ).toBe(0);
  expect(await inspector.evaluate((el) => el.getAnimations().length)).toBe(0);
  await inspector
    .getByRole("button", { name: "關閉使用者管理", exact: true })
    .click();
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await page
    .getByRole("button", { name: "功能群組與模型", exact: true })
    .click();
  await page
    .getByRole("button", { name: "編輯功能群組：基本工作區", exact: true })
    .click();
  const group = page.locator("dialog.admin-editor");
  await settleEntrance(page);
  // A feature group keeps one frame for all four tabs: switching never resizes or moves it,
  // and the tabs stay above the scrolling body.
  const groupFrame = (await group.boundingBox())!;
  const tabs = group.getByRole("tablist", { name: "功能群組設定分類" });
  const tabsTop = (await tabs.boundingBox())!.y;
  for (const name of [
    "功能授權",
    "AI 模型",
    "附件容量",
    "基本資料",
    "功能授權",
  ]) {
    await group.getByRole("tab", { name, exact: true }).click();
    await settleEntrance(page);
    const box = (await group.boundingBox())!;
    expect(Math.abs(box.width - groupFrame.width)).toBeLessThan(1);
    expect(Math.abs(box.height - groupFrame.height)).toBeLessThan(1);
    await group
      .locator(".dialog-body")
      .evaluate((el) => (el.scrollTop = el.scrollHeight));
    expect(Math.abs((await tabs.boundingBox())!.y - tabsTop)).toBeLessThan(1);
  }
  await expect(
    group.getByRole("tab", { name: "功能授權", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  // The selected tab's underline sits on the tabs' bottom rule.
  const underline = await tabs.evaluate((el) => {
    const host = el.getBoundingClientRect();
    const line = getComputedStyle(el, "::before");
    return {
      shown: el.hasAttribute("data-indicator") && line.opacity === "1",
      bottom: parseFloat(line.bottom),
      height: host.height,
    };
  });
  expect(underline).toEqual({
    shown: true,
    bottom: 0,
    height: expect.any(Number),
  });
  expect(underline.height).toBeGreaterThanOrEqual(34);
  await expect(
    group.getByRole("button", { name: "儲存功能群組", exact: true }),
  ).toBeVisible();
  await expectViewportContained(page);
});
