import { test, expect, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  ApiFixture,
  settleEntrance,
  chooseSelect,
  openSettings,
  expectViewportContained,
} from "./fixtures";
import type {
  AdminCatalog,
  AuditEntry,
  AdminUser,
} from "../../frontend/src/app/core/api/types";

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
  await page.getByRole("button", { name: "知識檢索", exact: true }).click();
  const activate = page.getByRole("button", { name: "啟用索引", exact: true });
  await expect(activate).toBeDisabled();
  await page
    .locator(".retrieval-profile")
    .filter({ hasText: "bge-m3-v2" })
    .getByRole("button", { name: "開始重建" })
    .click();
  await expect(activate).toBeEnabled();
  await activate.click();
  await expect(page.getByText("使用中索引已切換。")).toBeVisible();
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
  await expect(page.getByText("退役向量已清除。")).toBeVisible();
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
    await page.screenshot({
      path: `artifacts/screenshots/retrieval-admin-${theme}.png`,
      fullPage: true,
      animations: "disabled",
    });
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
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
  const table = page.locator("nx-admin-audit nx-data-table"),
    scroll = table.getByRole("region", { name: "異動稽核列表" });
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
  await page.screenshot({
    path: "artifacts/screenshots/admin-audit-table-drawer.png",
    animations: "disabled",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(drawer).toHaveAttribute("aria-modal", "true");
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-audit-drawer-mobile.png",
    animations: "disabled",
  });
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
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
  await expect(
    table.getByRole("columnheader", { name: "操作者", exact: true }),
  ).toHaveCount(0);
});

test("管理工具集中、分類分組，使用者分頁及功能短表單保持緊湊", async ({
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
  await page.reload();
  const users = page.locator(".admin-users-table");
  await expect(users.locator("tbody tr")).toHaveCount(100);
  await users.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(users.locator("tbody tr")).toHaveCount(6);
  await expect(users).toContainText("101–106 / 106");
  await users.getByRole("button", { name: "上一頁", exact: true }).click();
  await expect(users.locator("tbody tr")).toHaveCount(100);
  await expect(
    page.getByRole("navigation", { name: "管理分類" }).getByRole("group"),
  ).toHaveCount(2);
  await expect(
    page.locator(".feature-header .page-actions button:visible"),
  ).toHaveCount(1);
  await page.screenshot({
    path: "artifacts/screenshots/admin-users-table.png",
    animations: "disabled",
  });
  await page.route("**/api/v1/admin/billing/prices", (route) =>
    route.fulfill({ json: [] }),
  );
  await page.route("**/api/v1/admin/billing/targets", (route) =>
    route.fulfill({ json: [] }),
  );
  const tools = page.getByRole("button", { name: "管理工具", exact: true });
  await tools.click();
  const menu = page.getByRole("menu", { name: "管理工具", exact: true });
  for (const name of ["用量與費用總覽", "模型與工具價格", "介面元件"])
    await expect(
      menu.getByRole("menuitem", { name, exact: true }),
    ).toBeVisible();
  await menu
    .getByRole("menuitem", { name: "模型與工具價格", exact: true })
    .click();
  const prices = page.getByRole("dialog", { name: "模型與工具價格版本" });
  await expect(prices).toBeVisible();
  await prices
    .getByRole("button", { name: "關閉價格設定", exact: true })
    .click();
  await expect(tools).toBeFocused();
  await page.getByRole("button", { name: "功能", exact: true }).click();
  const features = page.locator(".admin-features-table");
  await expect(
    features.getByRole("region", { name: "功能配置列表" }),
  ).toBeVisible();
  await features
    .getByRole("button", { name: "編輯功能：對話", exact: true })
    .click();
  const editor = page.locator('dialog[aria-labelledby="admin-editor-title"]');
  expect((await editor.boundingBox())!.width).toBeLessThanOrEqual(450);
  expect((await editor.boundingBox())!.height).toBeLessThan(440);
  await page.route("**/api/v1/admin/features/chat", async (route) => {
    const body = route.request().postDataJSON();
    expect(body).toMatchObject({
      name: "AI 對話",
      sortOrder: 7,
      enabled: true,
    });
    Object.assign(
      state.catalog.features.find((feature) => feature.id === "chat")!,
      body,
    );
    await route.fulfill({ status: 204 });
  });
  await editor.getByLabel("名稱", { exact: true }).fill("AI 對話");
  await editor.getByLabel("顯示順序", { exact: true }).fill("7");
  await page.screenshot({
    path: "artifacts/screenshots/admin-feature-compact-form.png",
    animations: "disabled",
  });
  await editor.getByRole("button", { name: "儲存功能", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await expect(features).toContainText("AI 對話");
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-features-table-mobile.png",
    animations: "disabled",
  });
});

async function administration(page: Page) {
  const fixture = new ApiFixture();
  fixture.adminAccess = true;
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
  const managedUsers: AdminUser[] = [actor, bob];
  const catalog: AdminCatalog = {
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
        featureIds: ["admin"],
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
        name: "平台管理",
        route: "/admin",
        sortOrder: 90,
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
  const audit: AuditEntry[] = [];
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
      recordRead("admin.user_usage_read", bob.id);
      return json({
        user: bob,
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
    page.getByRole("heading", { name: "平台管理", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("王小明", { exact: true })).toBeVisible();
  return { fixture, catalog, bob, actor, managedUsers, audit, conversationId };
}
test("administrators edit roles with effective access preview and an audit trail", async ({
  page,
}) => {
  const state = await administration(page);
  await page.getByRole("button", { name: "設定角色：王小明" }).click();
  const dialog = page.getByRole("dialog");
  await dialog
    .getByRole("checkbox", { name: "平台管理員", exact: true })
    .check();
  await expect(dialog.locator("nx-feature-summary")).toContainText("管理");
  await dialog.getByRole("button", { name: "儲存授權" }).click();
  await expect(dialog).not.toBeVisible();
  expect(state.bob.roleIds).toContain("administrator");
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
  await expect(page.getByText("調整使用者角色", { exact: true })).toBeVisible();
  await page
    .getByRole("button", { name: /^檢視稽核：#/ })
    .first()
    .click();
  await expect(
    page.getByRole("dialog", { name: "稽核詳情" }).locator("pre"),
  ).toContainText("administrator");
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-audit.png",
    fullPage: true,
  });
});

test("manual user creation, dual login editing and removal share the administrator dialog", async ({
  page,
}) => {
  const state = await administration(page);
  await page.getByRole("button", { name: "新增使用者", exact: true }).click();
  const dialog = page.locator('dialog[aria-labelledby="admin-editor-title"]');
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
  await page.screenshot({
    path: "artifacts/screenshots/admin-local-user-mobile.png",
  });
  expect(
    await dialog.evaluate(
      (element) => element.scrollWidth <= element.clientWidth,
    ),
  ).toBe(true);
  await dialog.getByRole("button", { name: "儲存使用者", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  const created = state.managedUsers.find(
    (user) => user.displayName === "本地測試者",
  )!;
  expect(created.authentication.localEnabled).toBe(true);
  expect(created.authentication.adEnabled).toBe(false);
  expect(JSON.stringify(created)).not.toContain("test long password 123!");
  await page
    .getByRole("button", { name: "使用者操作：本地測試者", exact: true })
    .click();
  await page
    .getByRole("menuitem", { name: "編輯使用者與登入方式", exact: true })
    .click();
  const edit = page.locator('dialog[aria-labelledby="admin-editor-title"]');
  await expect(edit.getByLabel("設定本地密碼", { exact: true })).toHaveValue(
    "",
  );
  await edit
    .getByRole("checkbox", { name: "允許 AD 驗證", exact: true })
    .check();
  await edit.getByLabel("使用者 AD 帳號", { exact: true }).fill("test-ad");
  await edit.getByRole("button", { name: "儲存使用者", exact: true }).click();
  await expect(edit).not.toBeVisible();
  expect(created.authentication.adEnabled).toBe(true);
  await page
    .getByRole("button", { name: "使用者操作：本地測試者", exact: true })
    .click();
  await page.getByRole("menuitem", { name: "刪除使用者", exact: true }).click();
  const confirm = page.getByRole("dialog", {
    name: "刪除使用者：本地測試者",
    exact: true,
  });
  await expect(confirm).toContainText("仍會保留");
  await confirm
    .getByRole("button", { name: "刪除使用者", exact: true })
    .click();
  await expect(page.getByText("本地測試者", { exact: true })).toHaveCount(0);
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
              ? [{ id: "admin", name: "平台管理", route: "/admin" }]
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
    await page.screenshot({
      path: `artifacts/screenshots/test-identity-${width}.png`,
    });
  }
  await banner.getByRole("button", { name: "返回管理者", exact: true }).click();
  await expect(page).toHaveURL(/\/admin$/);
  await expect(banner).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "平台管理", exact: true }),
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
    for (const tab of ["功能", "異動稽核", "平台用量"]) {
      const tabButton = page.getByRole("button", { name: tab, exact: true });
      await tabButton.click();
      await expect(tabButton).toHaveAttribute("aria-pressed", "true");
      const target =
        tab === "異動稽核"
          ? "nx-admin-audit > nx-filter-panel"
          : ".feature-content > .form-note";
      await expect(page.locator(target).first()).toBeVisible();
      if (tab === "平台用量")
        await expect(page.locator(".stat-card")).toHaveCount(4);
      await expectCompactWorkspace(page);
      const header = await page.locator(".feature-header").boundingBox();
      const content = await page.locator(target).first().boundingBox();
      expect(Math.abs(header!.x - content!.x)).toBeLessThan(1);
      expect(Math.abs(header!.width - content!.width)).toBeLessThan(1);
      await expectViewportContained(page);
      if (width === 1920 || width === 375) {
        await settleEntrance(page);
        await page.screenshot({
          path: `artifacts/screenshots/admin-layout-${width}-${tab}.png`,
        });
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
  await page.getByRole("button", { name: "新增群組" }).click();
  const dialog = page.getByRole("dialog");
  await dialog
    .getByRole("textbox", { name: "識別碼", exact: true })
    .fill("research");
  await dialog
    .getByRole("textbox", { name: "名稱", exact: true })
    .fill("研發工作區");
  await dialog.getByRole("button", { name: "功能授權", exact: true }).click();
  await dialog.getByRole("checkbox", { name: "對話", exact: true }).check();
  await dialog.getByRole("button", { name: "AI 模型", exact: true }).click();
  await dialog.getByRole("checkbox", { name: "限制可用模型" }).check();
  await dialog.getByRole("checkbox", { name: "測試模型", exact: true }).check();
  await dialog
    .getByRole("spinbutton", { name: "每日 token 上限：測試模型" })
    .fill("50000");
  await dialog.getByRole("button", { name: "儲存授權" }).click();
  await expect(dialog).not.toBeVisible();
  expect(
    state.catalog.groups?.find((x) => x.id === "research")?.policy
      ?.dailyTokenLimits?.["fixture:8b"],
  ).toBe(50000);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-groups.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "使用者", exact: true }).click();
  await page.getByRole("button", { name: "設定角色：測試使用者" }).click();
  await dialog
    .getByRole("checkbox", { name: "平台管理員", exact: true })
    .uncheck();
  await dialog.getByRole("button", { name: "儲存授權" }).click();
  await expect(dialog.getByRole("alert")).toContainText("撤銷你的管理權限");
  await dialog.getByRole("button", { name: "取消", exact: true }).click();
  await page.setViewportSize({ width: 375, height: 812 });
  await page.getByRole("searchbox", { name: "搜尋使用者" }).fill("王小明");
  await expect(
    page.getByRole("button", { name: "設定角色：王小明" }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-mobile.png",
    fullPage: true,
  });
});
test("members have no management navigation and direct routes show an access explanation", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  await fixture.attach(page);
  await page.goto("/admin");
  await expect(page.getByRole("alert")).toContainText("沒有平台管理權限");
  await expect(
    page
      .getByRole("navigation", { name: "工作區功能" })
      .getByRole("link", { name: "平台管理", exact: true }),
  ).toHaveCount(0);
});

test("administrators inspect user usage and deleted conversations through an audited read only view", async ({
  page,
}) => {
  const state = await administration(page);
  await page
    .getByRole("button", { name: "使用者活動：王小明", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator(".inspector-stats")).toContainText("1,800");
  await dialog.getByRole("button", { name: "附件容量", exact: true }).click();
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
  await dialog.getByRole("button", { name: "對話", exact: true }).click();
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
  await page.screenshot({
    path: "artifacts/screenshots/admin-user-insights.png",
    fullPage: true,
  });
  await dialog
    .getByRole("checkbox", { name: "包含已刪除對話", exact: true })
    .uncheck();
  await expect(
    dialog.getByRole("button", { name: "檢視對話：公文內容討論", exact: true }),
  ).toHaveCount(0);
  await page.setViewportSize({ width: 375, height: 812 });
  await dialog.getByRole("button", { name: "附件容量", exact: true }).click();
  await dialog.getByLabel("個人容量上限（GB）", { exact: true }).fill("10");
  await dialog
    .getByRole("button", { name: "儲存容量上限", exact: true })
    .click();
  await expect(dialog.locator("nx-storage-usage")).toContainText("10.00 GB");
  expect(
    await dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1),
  ).toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/admin-user-insights-mobile.png",
    fullPage: true,
  });
  await dialog
    .getByRole("button", { name: "關閉使用者活動", exact: true })
    .click();
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
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
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
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
  await page.getByRole("button", { name: "異動稽核", exact: true }).click();
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
    .getByRole("button", { name: "使用者活動：王小明", exact: true })
    .click();
  await received;
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
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

test("personal model budgets share the group editor and keep conversations spacious", async ({
  page,
}) => {
  await administration(page);
  await page
    .getByRole("button", { name: "使用者活動：王小明", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "王小明", exact: true });
  await expect(dialog.getByLabel("個人容量上限（GB）")).toHaveCount(0);
  const content = await dialog.locator(".inspector-body").boundingBox();
  expect(content!.height).toBeGreaterThan(400);
  await dialog.getByRole("button", { name: "AI 模型", exact: true }).click();
  await expect(dialog).toContainText("100,000 tokens / 日");
  await expect(
    dialog.getByRole("region", { name: "模型授權與額度" }),
  ).toBeVisible();
  expect(
    (await dialog
      .locator(".model-policy-table tbody tr")
      .first()
      .boundingBox())!.height,
  ).toBeLessThan(110);
  expect((await dialog.boundingBox())!.height).toBeLessThan(700);
  await dialog
    .getByLabel("每日 token 上限：測試模型", { exact: true })
    .fill("150000");
  await dialog
    .getByRole("button", { name: "儲存模型政策", exact: true })
    .click();
  await expect(dialog).toContainText("150,000 tokens / 日");
  await expect(dialog).toContainText("個人設定");
  await expectCompactWorkspace(page);
  await page.screenshot({
    path: "artifacts/screenshots/admin-user-model-policy.png",
    animations: "disabled",
  });
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
    dialog.getByRole("group", { name: "使用者活動分類" }),
  ).toBeInViewport();
  await expect(dialog.locator(".inspector-stats")).toBeInViewport();
  await expect(
    dialog.getByRole("button", { name: "儲存模型政策", exact: true }),
  ).toBeInViewport({ ratio: 1 });
  await page.screenshot({
    path: "artifacts/screenshots/admin-user-model-policy-mobile.png",
    animations: "disabled",
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
    .getByRole("button", { name: "編輯群組：基本工作區", exact: true })
    .click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("button", { name: "AI 模型", exact: true }).click();
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
      dialog.getByRole("group", { name: "群組設定分類" }),
    ).toBeInViewport({ ratio: 1 });
    await expect(
      dialog.getByRole("button", { name: "儲存授權", exact: true }),
    ).toBeInViewport({ ratio: 1 });
    expect(
      await dialog.evaluate((el) => el.scrollWidth <= el.clientWidth + 1),
    ).toBe(true);
    await page.screenshot({
      path: `artifacts/screenshots/admin-group-models-${viewport.width}.png`,
      animations: "disabled",
    });
    await page.emulateMedia({ colorScheme: "light" });
    await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
    await expect(
      dialog.getByRole("button", { name: "儲存授權", exact: true }),
    ).toBeInViewport({ ratio: 1 });
  }
  await dialog.getByRole("button", { name: "儲存授權", exact: true }).click();
  expect(
    state.catalog.groups!.find((group) => group.id === "workspace")!.policy!
      .dailyTokenLimits!["fixture:11"],
  ).toBe(50000);
});
