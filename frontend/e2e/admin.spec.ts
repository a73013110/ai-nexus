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
  chooseOptions,
  multiSelect,
  chooseSelect,
  openSettings,
  expectViewportContained,
  toasts,
  featureDto,
  navigationTree,
  SEEDED_NAVIGATION,
  routeOf,
  featureOf,
  catalogName,
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
  // 檢索管理是知識庫模組的頁面，不再是平台營運的分頁。
  await page.goto(routeOf("knowledge.retrieval"));
  await expect(
    page.getByRole("heading", { name: "檢索管理", exact: true }),
  ).toBeVisible();
  const indexes = page.getByRole("region", { name: "檢索索引", exact: true });
  await expect(
    indexes.locator(".retrieval-profile").filter({ hasText: "bge-m3-v2" }),
  ).toContainText("0 / 1 個片段");
  const activate = indexes.getByRole("button", {
    name: "啟用索引",
    exact: true,
  });
  await expect(activate).toBeDisabled();
  // Rebuilding and activating change what every search uses, so both ask first.
  await indexes
    .locator(".retrieval-profile")
    .filter({ hasText: "bge-m3-v2" })
    .getByRole("button", { name: "開始重建" })
    .click();
  await page
    .getByRole("dialog", { name: "重建檢索索引" })
    .getByRole("button", { name: "開始重建", exact: true })
    .click();
  await expect(activate).toBeEnabled();
  await activate.click();
  await page
    .getByRole("dialog", { name: "切換使用中索引" })
    .getByRole("button", { name: "啟用索引", exact: true })
    .click();
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
  await indexes
    .getByRole("button", { name: "清除退役向量", exact: true })
    .click();
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
    // The index table keeps the compact data size in both themes and widths.
    await expect(page.locator(".retrieval-profile strong").first()).toHaveCSS(
      "font-size",
      "13px",
    );
  }
});

test("稽核表格保留捲動與欄位偏好，抽屜支援逐筆檢視和手機焦點返回", async ({
  page,
}) => {
  const { audit, auditExports } = await administration(page);
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
  await expect(table.locator(".audit-row")).toHaveCount(50);
  await chooseSelect(page, "每頁筆數", "每頁 100 筆");
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
  const first = table.locator(".audit-row").nth(8).locator("[data-row-action]");
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
  const firstPage = table.getByRole("button", {
    name: "回到第一頁",
    exact: true,
  });
  await nextPage.click();
  await expect(table.locator(".audit-row")).toHaveCount(100);
  expect(cursors).toEqual(["106"]);
  await previousPage.click();
  await expect(table.locator(".audit-row")).toHaveCount(100);
  await expect(previousPage).toBeDisabled();
  await nextPage.click();
  await nextPage.click();
  await expect(table.locator(".audit-row")).toHaveCount(5);
  expect(cursors).toEqual(["106", "106", "6"]);
  await expect(nextPage).toBeDisabled();
  const download = page.waitForEvent("download");
  await table.getByRole("button", { name: "匯出 CSV", exact: true }).click();
  expect((await download).suggestedFilename()).toBe("ai-nexus-audit.csv");
  expect(auditExports.at(-1)!.searchParams.has("before")).toBe(false);
  await firstPage.click();
  await expect(table.locator(".audit-row")).toHaveCount(100);
  await expect(firstPage).toBeDisabled();
  await chooseSelect(page, "稽核動作", "AI 生成");
  await expect(table.locator(".audit-row")).toHaveCount(100);
  await expect(page.getByText("條件已變更，按「查詢」套用")).toBeVisible();
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect(table.locator(".audit-row")).toHaveCount(0);
  await expect(
    page.getByRole("list", { name: "已套用條件", exact: true }),
  ).toContainText("動作：AI 生成");
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
  state.catalog.features.push(
    featureDto({
      id: "diagnostics.logs.export",
      name: "日誌匯出",
      route: "",
      enabled: true,
    }),
  );
  await page.reload();
  const users = page.locator(".admin-users-table");
  await expect(users.locator("tbody tr")).toHaveCount(100);
  await users.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(users.locator("tbody tr")).toHaveCount(6);
  await expect(users).toContainText("101–106 / 106");
  await users.getByRole("button", { name: "上一頁", exact: true }).click();
  await expect(users.locator("tbody tr")).toHaveCount(100);
  // 每一頁都是側欄的一格，頁內不再放切換同模組其他頁的連結列。
  await expect(page.locator("nx-feature-page .ui-view-switch")).toHaveCount(0);
  // 介面元件是系統管理選單裡獨立的功能，存取管理頁首不再放工具選單。
  await expect(
    page.locator(".feature-header .page-actions button:visible"),
  ).toHaveCount(0);
  await page.goto(routeOf("access-control.features"));
  const features = page.locator(".admin-features-table");
  await expect(
    features.getByRole("region", { name: "功能列表" }),
  ).toBeVisible();
  // Like roles and groups: the name over its id, then the status; the route takes the rest.
  // 排序與分群在選單頁，這裡沒有把手或排序選單，依目錄順序列出。
  await expect(features.locator(".ui-table-row .admin-table-name")).toHaveText([
    "對話",
    "檔案庫",
    "使用者",
    "活動稽核",
    "日誌匯出",
  ]);
  const chat = features.locator('tr[data-feature="chat"]');
  await expect(chat.locator("td").nth(0)).toHaveText(/對話\s*chat/);
  await expect(chat.locator("td").nth(1).locator("nx-status-badge")).toHaveText(
    "啟用",
  );
  await expect(chat.locator("td").nth(2)).toHaveText("/work/chat");
  await expect(chat.getByRole("button", { name: /^移動/ })).toHaveCount(0);
  const permission = features.locator(
    'tr[data-feature="diagnostics.logs.export"]',
  );
  await expect(permission.locator("td").nth(2)).toHaveText("無頁面，僅權限");
  await permission
    .getByRole("button", { name: "編輯功能：日誌匯出", exact: true })
    .click();
  await expect(
    page.locator("dialog.admin-editor nx-dialog-header small"),
  ).toHaveText("diagnostics.logs.export");
  // Nothing changed: the save bar rests and closing asks nothing.
  await expect(page.locator("dialog.admin-editor .dialog-foot")).toContainText(
    "尚未變更",
  );
  await page
    .locator("dialog.admin-editor")
    .getByRole("button", { name: "關閉管理編輯", exact: true })
    .click();
  await features
    .getByRole("button", { name: "編輯功能：對話", exact: true })
    .click();
  const editor = page.locator("dialog.admin-editor");
  await expect(
    page.getByRole("dialog", { name: "編輯功能：對話", exact: true }),
  ).toContainText("chat · /work/chat");
  await expect(editor.locator(".panel-eyebrow")).toHaveCount(0);
  expect((await editor.boundingBox())!.width).toBeLessThanOrEqual(450);
  // Name and switch only: still a short dialog.
  expect((await editor.boundingBox())!.height).toBeLessThan(600);
  await page.route("**/api/v1/admin/features/chat", async (route) => {
    const body = route.request().postDataJSON();
    // 功能只剩名稱與開關；選單位置在選單頁。
    expect(body).toEqual({ name: "AI 對話", enabled: true });
    Object.assign(
      state.catalog.features.find((feature) => feature.id === "chat")!,
      body,
    );
    await route.fulfill({ status: 204 });
  });
  await expect(editor.getByLabel("顯示順序", { exact: true })).toHaveCount(0);
  await expect(editor.getByLabel("選單分群", { exact: true })).toHaveCount(0);
  await editor.getByLabel("名稱", { exact: true }).fill("AI 對話");
  await editor.getByRole("button", { name: "儲存功能", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await expect(features).toContainText("AI 對話");
  // Roles and groups share the feature table's shape: name and id, status, row activation.
  await tabs.getByRole("link", { name: "角色", exact: true }).click();
  await expect(page).toHaveURL(routeOf("access-control.roles"));
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
  // Like the user dialog: sections divided by hairlines, the title beside the fields on a wide
  // dialog; a checked row is not filled.
  await expect(editor.locator("fieldset")).toHaveCount(0);
  const groupsSection = editor.getByRole("group", {
    name: "功能群組",
    exact: true,
  });
  await expect(groupsSection).toHaveCSS("border-top-style", "solid");
  const groupsTitle = (await groupsSection
    .getByRole("heading")
    .first()
    .boundingBox())!;
  const roleName = (await editor
    .getByLabel("名稱", { exact: true })
    .boundingBox())!;
  expect(roleName.x).toBeGreaterThan(groupsTitle.x + groupsTitle.width);
  await expect(
    editor.getByRole("checkbox", { name: "基本工作區", exact: true }),
  ).toBeChecked();
  await expect(
    editor.locator(".selection-control", {
      has: page.getByRole("checkbox", { name: "基本工作區", exact: true }),
    }),
  ).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
  // The foot is note | revert | save; both actions rest until something changes, and revert
  // puts the draft back.
  const revert = editor.getByRole("button", { name: "還原", exact: true });
  const saveRole = editor.getByRole("button", {
    name: "儲存角色",
    exact: true,
  });
  await expect(editor.locator(".dialog-foot")).toContainText("尚未變更");
  await expect(revert).toBeDisabled();
  await expect(saveRole).toBeDisabled();
  await editor
    .getByRole("checkbox", { name: "基本工作區", exact: true })
    .uncheck();
  await expect(editor.locator(".dialog-foot")).toContainText("有未儲存的變更");
  await expect(saveRole).toBeEnabled();
  await revert.click();
  await expect(
    editor.getByRole("checkbox", { name: "基本工作區", exact: true }),
  ).toBeChecked();
  await expect(revert).toBeDisabled();
  // Closing with unsaved changes asks first; cancelling keeps the draft.
  await editor.getByLabel("名稱", { exact: true }).fill("成員");
  await editor
    .getByRole("button", { name: "關閉管理編輯", exact: true })
    .click();
  const discard = page.getByRole("dialog", {
    name: "放棄未儲存的變更？",
    exact: true,
  });
  await discard.getByRole("button", { name: "取消", exact: true }).click();
  await expect(editor.getByLabel("名稱", { exact: true })).toHaveValue("成員");
  await editor
    .getByRole("button", { name: "關閉管理編輯", exact: true })
    .click();
  await discard.getByRole("button", { name: "放棄變更", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await tabs.getByRole("link", { name: "功能群組與模型", exact: true }).click();
  await expect(page).toHaveURL(routeOf("access-control.groups"));
  const groups = page.locator(".admin-groups-table");
  await expect(groups.locator("tbody tr")).toHaveCount(2);
  await expect(groups.locator("tbody tr").first()).toContainText("全部模型");
  await expect(
    groups.locator("tbody tr").nth(1).locator("nx-status-badge"),
  ).toHaveAttribute("data-tone", "warning");
  await expect(groups.locator("tbody tr").nth(1)).toContainText("停用");
  // Like a role: one page of sections (basics, features, storage), with the model policy beside it.
  await groups.locator("tbody tr").first().locator("td").first().click();
  await expect(editor.getByRole("tab")).toHaveText(["群組設定", "AI 模型"]);
  await expect(editor.getByRole("tabpanel")).toHaveAttribute(
    "aria-labelledby",
    (await editor
      .getByRole("tab", { name: "群組設定", exact: true })
      .getAttribute("id"))!,
  );
  await expect(editor.locator(".form-section-head h3")).toHaveText([
    "基本資料",
    "功能授權",
    "附件容量",
  ]);
  // Each feature category is its own framed card with a count; no outer fieldset wraps them.
  await expect(editor.locator("fieldset")).toHaveCount(0);
  // 功能依目錄的區 › 模組分卡，與授權摘要相同（不依側欄分群）。
  const work = editor.getByRole("group", {
    name: "AI 工作台 › 對話",
    exact: true,
  });
  await expect(work).toHaveCSS("border-left-style", "solid");
  await expect(work.locator(".choice-group-count")).toHaveText(/^\d+\/\d+$/);
  await editor
    .getByRole("button", { name: "關閉管理編輯", exact: true })
    .click();
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
    // 各頁由網址進入；側欄的導覽另有測試。
    await page.goto(
      routeOf(
        tab === "角色"
          ? "access-control.roles"
          : tab === "功能"
            ? "access-control.features"
            : "access-control.groups",
      ),
    );
    await settleEntrance(page);
    const list = page.locator(table);
    await contained(list);
    // 功能表的第三欄（路由）在卡片中收起，點狀態欄。
    await list
      .locator(".ui-table-row", { hasText: row })
      .locator("td")
      .nth(table === ".admin-features-table" ? 1 : 2)
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
    await editor
      .getByRole("button", { name: "關閉管理編輯", exact: true })
      .click();
    await expect(editor).not.toBeVisible();
  }
  // 選單在手機上也是卡片；拖曳把手收起，移動走列的選單。
  await page.goto(routeOf("access-control.navigation"));
  await settleEntrance(page);
  await contained(page.locator(".navigation-table"));
  await expect(
    page.getByRole("button", { name: /^移動「對話」/ }),
  ).toBeHidden();
  await expect(
    page.getByRole("button", { name: "選單項目操作：對話", exact: true }),
  ).toBeVisible();
});

/** 選單表中某分群（或最上層，`null`）直接的子節點名稱，依畫面順序。 */
function menuLevel(page: Page, group: string | null) {
  return page.locator(".navigation-table").evaluate((table, group) => {
    const rows = [...table.querySelectorAll<HTMLElement>("tr[data-item]")];
    const start = group
      ? rows.findIndex((row) => row.dataset["item"] === group)
      : -1;
    const depth = group ? Number(rows[start].dataset["depth"]) : -1;
    const names: string[] = [];
    for (const row of rows.slice(start + 1)) {
      const current = Number(row.dataset["depth"]);
      if (current <= depth) break;
      if (current === depth + 1)
        names.push(row.querySelector(".admin-table-name")!.textContent!.trim());
    }
    return names;
  }, group);
}

test("選單以鍵盤、選單或拖曳在同層與跨層移動，一次送出並在失敗時還原", async ({
  page,
}) => {
  const state = await administration(page);
  let me = 0;
  page.on("request", (request) => {
    if (new URL(request.url()).pathname === "/api/v1/me") me++;
  });
  const orders = () =>
    state.navigationWrites
      .filter((write) => write.id === "order")
      .map((write) => write.body);
  await page.goto(routeOf("access-control.navigation"));
  const table = page.locator(".navigation-table");
  const live = page.locator("nx-navigation-editor p[aria-live]");
  // 預設樹：頁面在分群裡，子節點依層級內縮。
  await expect
    .poll(() => menuLevel(page, null))
    .toEqual(["總覽", "AI 工作台", "協作", "系統管理"]);
  await expect
    .poll(() => menuLevel(page, "work"))
    .toEqual(["對話", "專案", "檔案庫", "知識庫", "成果文件"]);
  await expect(table.locator('tr[data-item="system.access"]')).toHaveAttribute(
    "data-depth",
    "1",
  );
  const files = page.getByRole("button", { name: /^移動「檔案庫」/ });

  // ↑：同層上移，只送出移動的節點，取落點間隙的中間值（對話 10 … 專案 20）。
  await files.focus();
  const reads = me;
  await files.press("ArrowUp");
  await expect
    .poll(() => menuLevel(page, "work"))
    .toEqual(["對話", "檔案庫", "專案", "知識庫", "成果文件"]);
  await expect(files).toBeFocused();
  await expect(live).toHaveText("檔案庫：AI 工作台第 2 個，共 5 個");
  await expect
    .poll(orders)
    .toEqual([
      { items: [{ id: "work.files", parentId: "work", sortOrder: 15 }] },
    ]);
  // 側欄讀的是 `/me` 的選單樹，所以會重讀帳號。
  await expect.poll(() => me).toBeGreaterThan(reads);
  await expect(table).toHaveAttribute("aria-busy", "false");

  // ←：移出分群，排在原分群之後。
  await files.press("ArrowLeft");
  await expect
    .poll(() => menuLevel(page, null))
    .toEqual(["總覽", "AI 工作台", "檔案庫", "協作", "系統管理"]);
  await expect
    .poll(() => orders().at(-1))
    .toEqual({ items: [{ id: "work.files", parentId: null, sortOrder: 25 }] });
  await expect(table).toHaveAttribute("aria-busy", "false");

  // 伺服器拒絕時還原位置，並以錯誤代碼的提示說明原因。
  await page.route("**/api/v1/admin/navigation-items/order", (route) =>
    route.fulfill({
      status: 400,
      contentType: "application/problem+json",
      body: JSON.stringify({
        title: "Bad Request",
        code: "navigation_depth_exceeded",
      }),
    }),
  );
  await files.press("ArrowRight");
  await expect(page.getByRole("alert")).toContainText(
    "選單最多四層，請把項目放到較淺的分群。",
  );
  await expect(live).toHaveText("無法儲存位置，已還原");
  await expect
    .poll(() => menuLevel(page, null))
    .toEqual(["總覽", "AI 工作台", "檔案庫", "協作", "系統管理"]);
  await page.unroute("**/api/v1/admin/navigation-items/order");

  // 列的選單：不用拖曳也能跨層（觸控與指標都適用）。
  await page
    .getByRole("button", { name: "選單項目操作：檔案庫", exact: true })
    .click();
  const menu = page.getByRole("menu", { name: "選單項目操作", exact: true });
  await expect(
    menu.getByRole("menuitem", { name: "移出分群", exact: true }),
  ).toBeDisabled();
  await menu
    .getByRole("menuitem", { name: "移入上方分群「AI 工作台」", exact: true })
    .click();
  await expect
    .poll(() => menuLevel(page, "work"))
    .toEqual(["對話", "專案", "知識庫", "成果文件", "檔案庫"]);
  await expect
    .poll(() => orders().at(-1))
    .toEqual({
      items: [{ id: "work.files", parentId: "work", sortOrder: 60 }],
    });
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(table).toHaveAttribute("aria-busy", "false");

  // 拖曳到某列的上半：插到它之前。
  const drag = async (target: Locator, ratio: number) => {
    const handle = (await files.boundingBox())!;
    const box = (await target.boundingBox())!;
    await page.mouse.move(
      handle.x + handle.width / 2,
      handle.y + handle.height / 2,
    );
    await page.mouse.down();
    await page.mouse.move(
      handle.x + handle.width / 2,
      box.y + box.height * ratio,
      {
        steps: 16,
      },
    );
    await page.mouse.up();
  };
  await drag(table.locator('tr[data-item="work.chat"]'), 0.1);
  await expect
    .poll(() => menuLevel(page, "work"))
    .toEqual(["檔案庫", "對話", "專案", "知識庫", "成果文件"]);
  await expect
    .poll(() => orders().at(-1))
    .toEqual({ items: [{ id: "work.files", parentId: "work", sortOrder: 0 }] });
  await expect(table).toHaveAttribute("aria-busy", "false");

  // 拖到分群列的中段：移入該分群、排在最後。
  await drag(table.locator('tr[data-item="collaboration"]'), 0.5);
  await expect
    .poll(() => menuLevel(page, "collaboration"))
    .toEqual(["分享", "背景任務", "程式庫", "檔案庫"]);
  await expect
    .poll(() => orders().at(-1))
    .toEqual({
      items: [{ id: "work.files", parentId: "collaboration", sortOrder: 40 }],
    });
  expect(orders()).toHaveLength(5);
});

test("選單可新增分群、頁面與外部連結，錯誤代碼留在對話框，非空分群不能刪除", async ({
  page,
}) => {
  const state = await administration(page);
  // 外部連結只給平台管理群組看；這個帳號屬於它。
  state.fixture.groupIds = ["workspace", "administrators"];
  await page.goto(routeOf("access-control.navigation"));
  const table = page.locator(".navigation-table");
  const add = async (kind: string) => {
    await page
      .getByRole("button", { name: "新增選單項目", exact: true })
      .click();
    await page
      .getByRole("menu", { name: "新增選單項目", exact: true })
      .getByRole("menuitem", { name: kind, exact: true })
      .click();
    const dialog = page.getByRole("dialog", {
      name: "新增選單項目",
      exact: true,
    });
    await expect(dialog).toBeVisible();
    return dialog;
  };
  const posts = () =>
    state.navigationWrites
      .filter((write) => write.method === "POST")
      .map((write) => write.body);

  // 分群：名稱與位置；新節點排在同層最後。
  let dialog = await add("分群");
  await expect(
    dialog.getByRole("button", { name: "分群", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await dialog.getByRole("button", { name: "建立分群", exact: true }).click();
  await expect(dialog.getByRole("alert")).toHaveText(
    "請輸入 120 字元內的名稱。",
  );
  await expect(dialog.getByLabel("名稱", { exact: true })).toBeFocused();
  await dialog.getByLabel("名稱", { exact: true }).fill("報表");
  await chooseSelect(page, "位置", "系統管理");
  await dialog.getByRole("button", { name: "建立分群", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(posts().at(-1)).toEqual({
    kind: "group",
    parentId: "system",
    featureId: null,
    name: "報表",
    url: null,
    openInNewTab: true,
    icon: null,
    audienceGroupIds: null,
  });
  await expect
    .poll(async () => (await menuLevel(page, "system")).at(-1))
    .toBe("報表");

  // 頁面：同一層已有同一頁面時，伺服器的錯誤代碼顯示在對話框內並保留輸入。
  dialog = await add("頁面");
  await page.getByRole("combobox", { name: "頁面", exact: true }).click();
  await page
    .getByRole("listbox", { name: "頁面", exact: true })
    .getByRole("option", { name: /^活動稽核/ })
    .click();
  await chooseSelect(page, "位置", "系統管理");
  await dialog.getByRole("button", { name: "建立頁面", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText(
    "同一層已經有這個頁面。",
  );
  await expect(dialog).toBeVisible();
  await chooseSelect(page, "位置", "系統管理 › 報表");
  await dialog.getByRole("button", { name: "建立頁面", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  expect(posts().at(-1)).toMatchObject({
    kind: "page",
    featureId: "audit",
    name: null,
    icon: null,
  });
  const reports = state.fixture.navigationItems.find(
    (item) => item.name === "報表",
  )!;
  await expect.poll(() => menuLevel(page, reports.id)).toEqual(["活動稽核"]);

  // 有子節點的分群不能刪除。
  await page
    .getByRole("button", { name: "選單項目操作：報表", exact: true })
    .click();
  await expect(
    page
      .getByRole("menu", { name: "選單項目操作", exact: true })
      .getByRole("menuitem", { name: "刪除（請先清空分群）", exact: true }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");

  // 外部連結：網址先在前端檢查，主機不在允許清單時由伺服器拒絕。
  dialog = await add("外部連結");
  await dialog.getByLabel("名稱", { exact: true }).fill("維運手冊");
  await dialog
    .getByLabel("網址", { exact: true })
    .fill("http://wiki.example.com/ops");
  await dialog
    .getByRole("button", { name: "建立外部連結", exact: true })
    .click();
  await expect(dialog.getByRole("alert")).toHaveText(
    "請輸入 https:// 開頭的完整網址，不可包含帳號或密碼。",
  );
  await expect(dialog.getByLabel("網址", { exact: true })).toBeFocused();
  await dialog
    .getByLabel("網址", { exact: true })
    .fill("https://intranet.example.com/ops");
  await dialog
    .getByRole("button", { name: "建立外部連結", exact: true })
    .click();
  await expect(dialog.getByRole("alert")).toContainText(
    "外部連結的網站不在允許清單內，請聯絡系統管理員。",
  );
  await dialog
    .getByLabel("網址", { exact: true })
    .fill("https://wiki.example.com/ops");
  await dialog.getByRole("checkbox", { name: "在新分頁開啟" }).uncheck();
  await dialog.getByRole("checkbox", { name: /^平台管理/ }).check();
  await dialog
    .getByRole("button", { name: "建立外部連結", exact: true })
    .click();
  await expect(dialog).not.toBeVisible();
  expect(posts().at(-1)).toEqual({
    kind: "link",
    parentId: null,
    featureId: null,
    name: "維運手冊",
    url: "https://wiki.example.com/ops",
    openInNewTab: false,
    icon: null,
    audienceGroupIds: ["administrators"],
  });
  const link = table.locator(".ui-table-row", { hasText: "維運手冊" });
  await expect(link).toContainText("同分頁開啟 · 可見：平台管理");
  // 側欄：外部連結帶外部連結標記，一律 noopener noreferrer，這筆不開新分頁。
  const sidebarLink = page.locator(
    'nx-feature-launcher a[href="https://wiki.example.com/ops"]',
  );
  if (await sidebarLink.count()) {
    await expect(sidebarLink).toHaveAttribute("rel", "noopener noreferrer");
    await expect(sidebarLink).not.toHaveAttribute("target", /.*/);
  }

  // 修改內容：種類與位置不變。
  await link
    .getByRole("button", { name: "編輯外部連結：維運手冊", exact: true })
    .click();
  const editor = page.getByRole("dialog", {
    name: "編輯外部連結：維運手冊",
    exact: true,
  });
  await expect(
    editor.getByRole("button", { name: "分群", exact: true }),
  ).toHaveCount(0);
  await editor.getByLabel("名稱", { exact: true }).fill("維運手冊（新版）");
  await editor
    .getByRole("button", { name: "儲存外部連結", exact: true })
    .click();
  await expect(editor).not.toBeVisible();
  expect(state.navigationWrites.at(-1)).toMatchObject({
    method: "PUT",
    body: { kind: "link", name: "維運手冊（新版）", openInNewTab: false },
  });

  // 刪除先確認。
  await page
    .getByRole("button", {
      name: "選單項目操作：維運手冊（新版）",
      exact: true,
    })
    .click();
  await page
    .getByRole("menu", { name: "選單項目操作", exact: true })
    .getByRole("menuitem", { name: "刪除", exact: true })
    .click();
  const confirm = page.getByRole("dialog", {
    name: "刪除外部連結？",
    exact: true,
  });
  await confirm
    .getByRole("button", { name: "刪除外部連結", exact: true })
    .click();
  await expect(
    table.locator(".ui-table-row", { hasText: "維運手冊" }),
  ).toHaveCount(0);
  expect(state.navigationWrites.at(-1)).toMatchObject({ method: "DELETE" });
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
        scopes: {},
      },
      {
        id: "administrators",
        name: "平台管理",
        enabled: true,
        featureIds: ["identity.users", "audit"],
        policy: null,
        scopes: { audit: "all" },
      },
    ],
    // 伺服器依功能目錄的宣告順序列出。
    features: [
      { id: "chat", name: "對話", route: "/work/chat", enabled: true },
      { id: "files", name: "檔案庫", route: "/work/files", enabled: true },
      {
        id: "identity.users",
        name: "使用者",
        route: "/admin/identity/users",
        enabled: true,
      },
      { id: "audit", name: "活動稽核", route: "/admin/audit", enabled: true },
    ].map(featureDto),
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
  const auditExports: URL[] = [];
  const navigationWrites: { method: string; id: string; body: unknown }[] = [];
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
    if (path.startsWith("/navigation-items")) {
      // 與 `/me` 共用同一份節點，側欄跟著寫入改變；規則照伺服器：允許清單只有 wiki.example.com、
      // 同層不可重複同一頁面、非空分群不能刪。
      const navigation = fixture.navigationItems;
      const body =
        method === "GET" || method === "DELETE"
          ? null
          : route.request().postDataJSON();
      const id = decodeURIComponent(path.split("/")[2] ?? "");
      if (method !== "GET") navigationWrites.push({ method, id, body });
      const problem = (code: string, status = 400) =>
        route.fulfill({
          status,
          contentType: "application/problem+json",
          body: JSON.stringify({ title: "Bad Request", code }),
        });
      const duplicate = (items: typeof navigation) =>
        items.some((item) =>
          items.some(
            (other) =>
              other !== item &&
              !!other.featureId &&
              other.featureId === item.featureId &&
              (other.parentId ?? null) === (item.parentId ?? null),
          ),
        );
      if (method === "GET") return json(navigation);
      if (id === "order") {
        const next = navigation.map((item) => {
          const move = (
            body.items as {
              id: string;
              parentId: string | null;
              sortOrder: number;
            }[]
          ).find((x) => x.id === item.id);
          return move
            ? { ...item, parentId: move.parentId, sortOrder: move.sortOrder }
            : item;
        });
        if (duplicate(next)) return problem("navigation_page_duplicate", 409);
        navigation.splice(0, navigation.length, ...next);
        return route.fulfill({ status: 204 });
      }
      if (method === "DELETE") {
        if (navigation.some((item) => item.parentId === id))
          return problem("navigation_item_not_empty", 409);
        navigation.splice(
          navigation.findIndex((item) => item.id === id),
          1,
        );
        return route.fulfill({ status: 204 });
      }
      if (body.kind === "link" && new URL(body.url).host !== "wiki.example.com")
        return problem("navigation_link_host_not_allowed");
      const content = {
        kind: body.kind,
        featureId: body.featureId,
        name: body.name,
        url: body.url,
        openInNewTab: body.openInNewTab,
        icon: body.icon,
        audienceGroupIds: body.audienceGroupIds ?? [],
      };
      if (method === "POST") {
        const siblings = navigation.filter(
          (item) => (item.parentId ?? null) === body.parentId,
        );
        const created = {
          ...content,
          id: randomUUID().replaceAll("-", ""),
          parentId: body.parentId,
          sortOrder: Math.max(0, ...siblings.map((x) => x.sortOrder)) + 10,
        };
        if (duplicate([...navigation, created]))
          return problem("navigation_page_duplicate", 409);
        navigation.push(created);
        return json(created);
      }
      Object.assign(
        navigation.find((item) => item.id === id)!,
        content,
      );
      return route.fulfill({ status: 204 });
    }
    if (path === "/audit/catalog")
      return json({
        features: catalog.features,
        models: catalog.models,
        categories: [
          { value: "authentication", label: "登入與身分" },
          { value: "administration", label: "管理異動" },
          { value: "access", label: "查閱與匯出" },
          { value: "activity", label: "功能操作" },
        ],
        actions: [
          {
            value: "admin.",
            label: "平台管理",
            categories: ["administration", "access"],
          },
          {
            value: "identity.login",
            label: "登入",
            categories: ["authentication"],
          },
          {
            value: "admin.conversation_read",
            label: "對話內容檢視",
            categories: ["access"],
          },
          { value: "run.", label: "AI 生成", categories: ["activity"] },
        ],
      });
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
        features: [{ id: "chat", name: "對話", route: "/work/chat" }].map(
          featureDto,
        ),
        navigation: [],
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
    if (path === "/audit/export") {
      auditExports.push(url);
      return route.fulfill({
        contentType: "text/csv",
        body: "紀錄 ID,動作\n1,admin.role",
      });
    }
    if (path === "/audit")
      return json(
        audit
          .filter(
            (x) =>
              (!url.searchParams.get("action") ||
                x.action.startsWith(url.searchParams.get("action")!)) &&
              (!url.searchParams.getAll("result").length ||
                url.searchParams.getAll("result").includes(x.result ?? "")) &&
              (!url.searchParams.get("category") ||
                x.category === url.searchParams.get("category")) &&
              (!url.searchParams.get("traceId") ||
                x.traceId === url.searchParams.get("traceId")) &&
              (!url.searchParams.get("before") ||
                x.id < Number(url.searchParams.get("before"))),
          )
          .sort((a, b) => b.id - a.id)
          .slice(0, Number(url.searchParams.get("take") || 100)),
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
      if (path.startsWith("/features/")) {
        const id = path.split("/").at(-1)!;
        // Like the server: no name (null) means the catalog's default name.
        Object.assign(
          catalog.features.find((x) => x.id === id)!,
          {
            ...body,
            name: body.name ?? catalogName(id),
          },
        );
      }
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
  await page.goto(routeOf("identity.users"));
  await expect(
    page.getByRole("heading", { name: "使用者", level: 1, exact: true }),
  ).toBeVisible();
  await expect(page.getByText("王小明", { exact: true })).toBeVisible();
  return {
    fixture,
    catalog,
    bob,
    actor,
    managedUsers,
    audit,
    auditExports,
    conversationId,
    navigationWrites,
  };
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
    .getByRole("button", { name: /^檢視稽核：/ })
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
    "AI 工作台",
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
  // The tab holding the draft says so in its name until it is saved.
  await expect(
    edit.getByRole("tab", { name: "帳號（有未儲存的變更）", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
  await edit
    .getByRole("checkbox", { name: "允許 AD 驗證", exact: true })
    .check();
  await edit.getByLabel("使用者 AD 帳號", { exact: true }).fill("test-ad");
  await edit.getByRole("button", { name: "儲存帳號", exact: true }).click();
  await expect(edit).toContainText("登入：AD · 本地密碼");
  await expect(edit).toHaveAccessibleName("本地測試員");
  await expect(edit).not.toContainText("有未儲存的變更");
  await expect(
    edit.getByRole("tab", { name: "帳號", exact: true }),
  ).toHaveAttribute("aria-selected", "true");
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
            { id: "chat", name: "對話", route: "/work/chat" },
            ...(!testing ? [featureOf("identity.users")] : []),
          ].map(featureDto),
          navigation: navigationTree(SEEDED_NAVIGATION, [
            featureOf("chat"),
            ...(!testing ? [featureOf("identity.users")] : []),
          ]),
        },
      }),
    }),
  );
  await page.goto("/work/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("管理者私人草稿");
  await page.goto(routeOf("identity.users"));
  await page
    .getByRole("button", { name: "使用者操作：王小明", exact: true })
    .click();
  await page
    .getByRole("menuitem", { name: "以此身分測試", exact: true })
    .click();
  await page.getByLabel("測試目的", { exact: true }).fill("核對一般使用者權限");
  await page.getByRole("button", { name: "開始身分測試", exact: true }).click();
  await expect(page).toHaveURL(/\/work\/dashboard$/);
  const banner = page.getByRole("region", {
    name: "管理者測試身分",
    exact: true,
  });
  await expect(banner).toContainText("王小明");
  await expect(banner).toContainText("15 分鐘");
  await page.goto("/work/chat");
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue("");
  for (const width of [1920, 375]) {
    await page.setViewportSize({ width, height: 900 });
    await expectViewportContained(page);
    await settleEntrance(page);
  }
  await banner.getByRole("button", { name: "返回管理者", exact: true }).click();
  await expect(page).toHaveURL(routeOf("identity.users"));
  await expect(banner).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "使用者", level: 1, exact: true }),
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
      // 功能、平台用量與活動稽核各是自己的頁面。
      if (tab === "活動稽核") await page.goto(routeOf("audit"));
      else if (tab === "平台用量") await page.goto(routeOf("operations.usage"));
      else {
        await page.goto(routeOf("access-control.features"));
      }
      if (tab !== "活動稽核")
        // The content arrives with a slight scale; measure where it settles.
        await settleEntrance(page);
      const target =
        tab === "活動稽核"
          ? "nx-query-bar"
          : tab === "平台用量"
            ? ".feature-content nx-platform-usage > .page-toolbar"
            : ".feature-content nx-admin-features-tab > .page-toolbar";
      await expect(page.locator(target).first()).toBeVisible();
      if (tab === "平台用量") {
        await expect(
          page.locator("nx-platform-usage nx-stat-tile"),
        ).toHaveCount(4);
        // Prices are their own page; the usage page only refreshes itself.
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
  await page.goto(routeOf("access-control.groups"));
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
  await dialog.getByRole("checkbox", { name: "對話", exact: true }).check();
  // 有資料範圍的功能勾選後才出現範圍選擇，預設最窄，選項只有目錄宣告的範圍。
  await expect(dialog.getByText("資料範圍", { exact: true })).toHaveCount(0);
  await dialog.getByRole("checkbox", { name: "活動稽核", exact: true }).check();
  const scope = dialog.getByRole("combobox", {
    name: "活動稽核的資料範圍",
    exact: true,
  });
  await expect(scope).toHaveText("本人");
  await scope.click();
  const scopes = page.getByRole("listbox", {
    name: "活動稽核的資料範圍",
    exact: true,
  });
  await expect(scopes.getByRole("option")).toHaveText([
    "本人",
    "所屬單位",
    "所屬單位及下層",
    "全部",
  ]);
  await scopes.getByRole("option", { name: "所屬單位及下層" }).click();
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
  expect(
    state.catalog.groups?.find((x) => x.id === "research")?.scopes,
  ).toEqual({ audit: "unit-tree" });
  await settleEntrance(page);
  await page.goto(routeOf("identity.users"));
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
  await page.goto(routeOf("identity.users"));
  await expect(page.getByRole("alert")).toContainText("需要「使用者」權限");
  await page.goto(routeOf("operations.usage"));
  await expect(page.getByRole("alert")).toContainText("需要「平台用量」權限");
  for (const name of ["使用者", "平台用量"])
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
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect(page.locator(".audit-row")).toHaveCount(1);
  await expect(page).toHaveURL(/action=admin\.conversation_read/);
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "匯出 CSV", exact: true }).click();
  expect((await download).suggestedFilename()).toBe("ai-nexus-audit.csv");
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
  await page.getByRole("button", { name: /^檢視稽核：/ }).click();
  await expect(page.locator(".audit-changes")).toContainText("資深分析人員");
  await expect(page.locator(".audit-before").first()).toContainText("分析人員");
  await expect(page.locator(".audit-changes")).toContainText(
    "每日 token 上限 · 測試模型",
  );
  await expect(page.locator(".audit-changes")).toContainText("100,000 tokens");
  await expect(page.locator(".audit-changes")).toContainText("150,000 tokens");
  // The modeless drawer covers the right of the page; close it to reach the filters.
  await page.keyboard.press("Escape");
  await expect(
    page.getByRole("dialog", { name: "稽核詳情" }),
  ).not.toBeVisible();
  await chooseOptions(page, "稽核結果", ["已檢視", "已受理／完成"]);
  const results = multiSelect(page, "稽核結果");
  await expect(results).toHaveText("已選 2 項");
  // 報讀器讀得到完整的已選內容，不只是「已選 2 項」。
  await expect(results).toHaveAccessibleName("稽核結果 已受理／完成、已檢視");
  await page.getByRole("button", { name: "查詢", exact: true }).click();
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
  const range = page.getByRole("button", { name: /^時間範圍/ });
  const popover = page.getByRole("dialog", { name: "時間範圍", exact: true });
  await range.click();
  await expect(popover).toContainText("時間以 Asia/Taipei 顯示");
  const start = popover.getByRole("textbox", { name: "起始日期", exact: true });
  const end = popover.getByRole("textbox", { name: "結束日期", exact: true });
  await start.fill("2026/10/04");
  await end.fill("2026/10/07");
  await popover.getByRole("button", { name: "套用自訂", exact: true }).click();
  await expect(range).toContainText("2026/10/04 — 10/07");
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect
    .poll(() => queries.at(-1)?.searchParams.get("from"))
    .toBe("2026-10-03T16:00:00.000Z");
  expect(queries.at(-1)?.searchParams.get("until")).toBe(
    "2026-10-07T16:00:00.000Z",
  );
  await expect(page).toHaveURL(/from=2026-10-04&until=2026-10-07/);
  await range.click();
  await start.fill("2026/02/30");
  await start.press("Tab");
  await expect(start).toHaveAttribute("aria-invalid", "true");
  await expect(
    popover.getByRole("button", { name: "套用自訂", exact: true }),
  ).toBeDisabled();
  await start.fill("");
  await popover.getByRole("button", { name: "套用自訂", exact: true }).click();
  await expect(range).toContainText("至 2026/10/07");
  const requests = queries.length;
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect.poll(() => queries.length).toBeGreaterThan(requests);
  expect(queries.at(-1)?.searchParams.has("from")).toBe(false);
  await page
    .getByRole("button", { name: "移除條件：時間：至 2026/10/07", exact: true })
    .click();
  await expect
    .poll(() => queries.at(-1)?.searchParams.has("until"))
    .toBe(false);
});

test("activity audit separates sign-ins and opens a historical trace in system logs", async ({
  page,
}) => {
  const state = await administration(page);
  state.fixture.extraFeatures.push({
    id: "diagnostics.logs",
    name: "系統日誌",
    route: "/admin/diagnostics/logs",
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
  await audit.getByRole("button", { name: /^檢視稽核：登入工作區/ }).click();
  const drawer = page.getByRole("dialog", { name: "稽核詳情", exact: true });
  await expect(drawer).toContainText("unverified-account");
  await expect(drawer).toContainText("本地帳號");
  await expect(drawer).toContainText("203.0.113.8");
  await expect(drawer).toContainText(trace);
  await drawer.getByRole("link", { name: "查證相關日誌", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/diagnostics\/logs\?/);
  await expect.poll(() => logQueries.length).toBeGreaterThan(0);
  expect(logQueries[0].searchParams.get("traceId")).toBe(trace);
  expect(Date.parse(logQueries[0].searchParams.get("from")!)).toBe(
    Date.parse(at) - 300_000,
  );
  expect(Date.parse(logQueries[0].searchParams.get("to")!)).toBe(
    Date.parse(at) + 300_000,
  );
  await page
    .locator(".workspace-navigation")
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
  // 功能群組是自己的頁面；載入新頁面會丟掉上面加的樣式，所以再加一次。
  await page.goto(routeOf("access-control.groups"));
  await page.addStyleTag({ content: ":root { --motion-panel: 600ms; }" });
  await page
    .getByRole("button", { name: "編輯功能群組：基本工作區", exact: true })
    .click();
  const group = page.locator("dialog.admin-editor");
  await settleEntrance(page);
  // A feature group's frame follows its two tabs like the user's: same width, the height moves
  // instead of jumping, and the tabs stay above the scrolling body.
  const groupFrame = (await group.boundingBox())!;
  const tabs = group.getByRole("tablist", { name: "功能群組設定分類" });
  await group.getByRole("tab", { name: "AI 模型", exact: true }).click();
  await expect
    .poll(() =>
      group.evaluate((el) =>
        el
          .getAnimations()
          .some(
            (a) =>
              a.effect instanceof KeyframeEffect &&
              a.effect.getKeyframes().some((frame) => !!frame.height),
          ),
      ),
    )
    .toBe(true);
  await settleEntrance(page);
  const modelsFrame = (await group.boundingBox())!;
  expect(Math.abs(modelsFrame.width - groupFrame.width)).toBeLessThan(1);
  expect(Math.abs(modelsFrame.height - groupFrame.height)).toBeGreaterThan(1);
  for (const name of ["群組設定", "AI 模型", "群組設定"]) {
    await group.getByRole("tab", { name, exact: true }).click();
    await settleEntrance(page);
    // Each tab starts at the top of the body.
    expect(
      await group.locator(".dialog-body").evaluate((el) => el.scrollTop),
    ).toBe(0);
    const tabsTop = (await tabs.boundingBox())!.y;
    await group
      .locator(".dialog-body")
      .evaluate((el) => (el.scrollTop = el.scrollHeight));
    expect(Math.abs((await tabs.boundingBox())!.y - tabsTop)).toBeLessThan(1);
  }
  expect(
    Math.abs((await group.boundingBox())!.height - groupFrame.height),
  ).toBeLessThan(1);
  await expect(
    group.getByRole("tab", { name: "群組設定", exact: true }),
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
