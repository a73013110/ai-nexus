import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";
import { KnowledgeFixture, twoPagePdf } from "./knowledge-fixture";

test("sent shares preserve their list and reuse the authorized PDF reader, citations and timing", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({ id: "shared", name: "分享", route: "/shared" });
  await core.attach(page);
  const id = randomUUID(),
    fileId = randomUUID();
  const file = {
    id: fileId,
    fileName: "分享報告.pdf",
    contentType: "application/pdf",
    size: twoPagePdf().length,
    isImage: false,
    analysisMode: "shared-file",
  };
  const share = {
    id,
    kind: "conversation",
    title: "分享報告",
    owner: "測試使用者",
    isOwner: true,
    isRevoked: false,
    expiresAt: new Date(Date.now() + 86400000).toISOString(),
    createdAt: new Date().toISOString(),
    recipients: ["林同事"],
    includeAttachments: true,
  };
  await page.route("**/api/v1/shares**", (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/shares"))
      return route.fulfill({
        json: url.searchParams.get("sent") === "true" ? [share] : [],
      });
    if (url.pathname.endsWith("/preview"))
      return route.fulfill({
        json: {
          file,
          pages: [
            {
              pageNumber: 1,
              text: "第一頁摘要",
              extraction: "native",
              needsReview: false,
            },
            {
              pageNumber: 2,
              text: "第二頁摘要",
              extraction: "native",
              needsReview: false,
            },
          ],
        },
      });
    if (url.pathname.includes("/files/"))
      return route.fulfill({
        contentType: "application/pdf",
        body: twoPagePdf(),
      });
    return route.fulfill({
      json: {
        share,
        snapshot: {
          content: "",
          artifactVersion: null,
          messages: [
            {
              role: "user",
              content: "檢視附件",
              status: "completed",
              createdAt: share.createdAt,
              attachments: [file],
            },
            {
              role: "assistant",
              content: "## 回答\n\n依據來源整理。",
              status: "completed",
              createdAt: share.createdAt,
              attachments: [],
              modelId: "fixture:8b",
              sources: [
                {
                  number: 1,
                  documentId: randomUUID(),
                  title: "受控來源",
                  pageNumber: 1,
                  excerpt: "核准的摘要文字",
                },
              ],
              webSources: null,
              timing: {
                totalMilliseconds: 1500,
                queueMilliseconds: 200,
                generationMilliseconds: 1300,
                inputTokens: 100,
                outputTokens: 30,
              },
            },
          ],
        },
      },
    });
  });
  await page.goto("/shared");
  await page.getByRole("button", { name: "我分享的", exact: true }).click();
  await page
    .getByRole("navigation", { name: "分享清單" })
    .getByRole("link")
    .click();
  await expect(page).toHaveURL(new RegExp(`/shared/${id}\\?sent=true$`));
  await expect(
    page.getByRole("navigation", { name: "分享清單" }).getByRole("link"),
  ).toHaveCount(1);
  await page.reload();
  await expect(
    page.getByRole("button", { name: "我分享的", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.getByRole("navigation", { name: "分享清單" }).getByRole("link"),
  ).toHaveCount(1);
  await page.locator("nx-run-timing summary").click();
  await expect(page.locator("nx-run-timing")).toContainText("100");
  await page.locator(".shared-citation summary").click();
  await expect(page.locator(".shared-citation")).toContainText(
    "核准的摘要文字",
  );
  await page.getByRole("link", { name: "閱讀附件：分享報告.pdf" }).click();
  const reader = page.getByRole("dialog", { name: "檔案預覽" });
  await expect(reader.locator("canvas")).toHaveAttribute("width", /[1-9]\d+/);
  await reader.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(
    reader.getByRole("combobox", { name: "文件頁碼", exact: true }),
  ).toContainText("第 2 頁");
  await reader.getByRole("button", { name: "擷取文字", exact: true }).click();
  await expect(reader).toContainText("第二頁摘要");
  await page.keyboard.press("Escape");
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/shared-rich-content.png",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
});

test("icon rail, notification filtering and typed task navigation work across pages and mobile", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  const job = fixture.jobs[0];
  const items = [
    {
      id: randomUUID(),
      version: 1,
      type: "task.completed",
      severity: "success",
      title: "文件索引完成",
      body: job.label,
      target: { kind: "task", id: job.id },
      createdAt: new Date().toISOString(),
      readAt: null as string | null,
    },
    {
      id: randomUUID(),
      version: 99,
      type: "future.event",
      severity: "info",
      title: "未來版本通知",
      body: "保留文字內容",
      target: { kind: "task", id: job.id },
      createdAt: new Date().toISOString(),
      readAt: null as string | null,
    },
  ];
  await page.route("**/api/v1/notifications**", (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === "POST") {
      const row = items.find((x) => url.pathname.includes(x.id));
      if (row) row.readAt = new Date().toISOString();
      return route.fulfill({ status: 204 });
    }
    if (route.request().method() === "DELETE") {
      items.splice(
        items.findIndex((x) => url.pathname.endsWith(x.id)),
        1,
      );
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({
      json: {
        items: items.filter(
          (x) => url.searchParams.get("unread") !== "true" || !x.readAt,
        ),
        unread: items.filter((x) => !x.readAt).length,
        hasMore: false,
      },
    });
  });
  await page.goto("/files");
  await page.getByRole("button", { name: "收合側欄", exact: true }).click();
  const sidebar = page.locator(".workspace-sidebar");
  expect((await sidebar.boundingBox())!.width).toBe(76);
  await expect(
    sidebar.getByRole("link", { name: "知識庫", exact: true }),
  ).toBeVisible();
  await sidebar.getByRole("link", { name: "知識庫", exact: true }).click();
  await expect(sidebar).toHaveClass(/is-compact/);
  await page.getByRole("button", { name: "通知", exact: true }).click();
  let dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await expect(dialog.locator(".notification-row")).toHaveCount(2);
  await expect(
    dialog
      .locator(".notification-row")
      .filter({ hasText: "未來版本通知" })
      .getByRole("button", { name: "查看內容" }),
  ).toHaveCount(0);
  await dialog.getByRole("button", { name: "只看未讀", exact: true }).click();
  await dialog
    .locator(".notification-row")
    .filter({ hasText: "未來版本通知" })
    .getByRole("button", { name: "標為已讀", exact: true })
    .click();
  await expect(dialog.locator(".notification-row")).toHaveCount(1);
  await dialog.getByRole("button", { name: "查看內容" }).click();
  await expect(page).toHaveURL(new RegExp(`/tasks\\?job=${job.id}$`));
  await expect(page.locator(".job-card.current")).toContainText(job.label);
  await expect(page.locator(".job-card.current")).toBeFocused();
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  expect((await sidebar.boundingBox())!.width).toBe(64);
  await page.getByRole("button", { name: "展開側欄", exact: true }).click();
  await expect(page.locator(".feature-main")).toHaveAttribute("inert", "");
  await page.getByRole("button", { name: "通知", exact: true }).click();
  dialog = page.getByRole("dialog", { name: "通知", exact: true });
  await dialog.getByRole("button", { name: "只看未讀", exact: true }).click();
  await dialog
    .locator(".notification-row")
    .filter({ hasText: "文件索引完成" })
    .getByRole("button", { name: "查看內容" })
    .click();
  await expect(sidebar).toHaveClass(/is-compact/);
  await expect(page.locator(".feature-main")).not.toHaveAttribute("inert", "");
  await page.getByRole("button", { name: "通知", exact: true }).click();
  for (let i = 0; i < 20; i++)
    items.push({
      ...items[0],
      id: randomUUID(),
      title: "較早的通知 " + i,
      readAt: null,
    });
  await dialog
    .getByRole("button", { name: "重新整理通知", exact: true })
    .click();
  await expect(dialog.locator(".notification-row")).toHaveCount(22);
  await page.setViewportSize({ width: 375, height: 667 });
  const bounds = await dialog.boundingBox();
  expect(bounds!.y).toBeGreaterThanOrEqual(0);
  expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(667);
  expect(
    await dialog
      .locator(".notification-list")
      .evaluate((el) => el.scrollHeight > el.clientHeight),
  ).toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/notifications-mobile.png",
  });
});

test("file names retain their extension and a rejected edit remains recoverable", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  const file = fixture.core.attachments[0];
  let reject = true;
  await page.route(`**/api/v1/files/${file.id}/name`, (route) => {
    const body = route.request().postDataJSON();
    expect(body.expectedFileName).toBe("差旅費用申請.pdf");
    if (reject) {
      reject = false;
      return route.fulfill({
        status: 409,
        json: { title: "檔案名稱已被其他操作更新。" },
      });
    }
    file.fileName = body.fileName;
    return route.fulfill({ json: file });
  });
  await page.goto("/files");
  await page
    .getByRole("button", { name: `重新命名 ${file.fileName}`, exact: true })
    .click();
  const dialog = page.getByRole("dialog", {
    name: "重新命名檔案",
    exact: true,
  });
  await dialog.getByLabel("名稱", { exact: true }).fill("新版報告.pdf");
  await dialog.getByRole("button", { name: "儲存名稱", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText(
    "檔案名稱已被其他操作更新",
  );
  await expect(dialog.getByLabel("名稱", { exact: true })).toHaveValue(
    "新版報告.pdf",
  );
  await dialog.getByRole("button", { name: "儲存名稱", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.locator(".file-card")).toContainText("新版報告.pdf");
});

test("pasted knowledge text is editable and a version conflict preserves the draft", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.collection("文字知識庫");
  await fixture.attach(page);
  let source = { title: "", text: "", version: 1 };
  let conflict = true;
  await page.route("**/api/v1/**/text", (route) => {
    const method = route.request().method();
    if (method === "GET") return route.fulfill({ json: source });
    const body = route.request().postDataJSON();
    if (method === "PUT" && conflict) {
      conflict = false;
      return route.fulfill({
        status: 409,
        json: { title: "文字來源已有較新版本，請重新讀取後合併修改。" },
      });
    }
    source = {
      title: body.title,
      text: body.text,
      version: method === "POST" ? 1 : body.expectedVersion + 1,
    };
    const doc = fixture.documents[0] ?? {
      id: randomUUID(),
      collectionId: fixture.collections[0].resource.id,
      fileName: "",
      contentType: "text/plain",
      status: "ready",
      pageCount: 1,
      chunkCount: 1,
      warning: null,
      jobId: null,
      canEdit: true,
      hasOriginal: true,
      textVersion: 1,
    };
    doc.fileName = source.title + ".txt";
    doc.textVersion = source.version;
    if (!fixture.documents.length) fixture.documents.push(doc);
    return route.fulfill({ json: doc });
  });
  await page.goto("/knowledge");
  await page.getByRole("button", { name: "貼上純文字", exact: true }).click();
  const dialog = page.getByRole("dialog", {
    name: "純文字來源編輯器",
    exact: true,
  });
  await dialog.getByLabel("來源名稱", { exact: true }).fill("工作筆記");
  await dialog.getByLabel("純文字內容", { exact: true }).fill("第一版工作內容");
  await dialog
    .getByRole("button", { name: "儲存並建立索引", exact: true })
    .click();
  await expect(page.locator(".document-row")).toContainText("工作筆記.txt");
  await page
    .locator(".document-row")
    .getByRole("button", { name: /文件操作/ })
    .click();
  await page.getByRole("menuitem", { name: "編輯純文字", exact: true }).click();
  await expect(dialog.getByLabel("純文字內容", { exact: true })).toHaveValue(
    "第一版工作內容",
  );
  await dialog.getByLabel("純文字內容", { exact: true }).fill("修訂內容");
  await dialog
    .getByRole("button", { name: "儲存並建立索引", exact: true })
    .click();
  await expect(dialog.getByRole("alert")).toContainText("較新版本");
  await expect(dialog.getByLabel("純文字內容", { exact: true })).toHaveValue(
    "修訂內容",
  );
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/knowledge-text-mobile.png",
  });
  await dialog.getByRole("button", { name: "取消", exact: true }).click();
  await page
    .getByRole("dialog", { name: "放棄未儲存的內容" })
    .getByRole("button", { name: "放棄修改", exact: true })
    .click();
  await expect(dialog).not.toBeVisible();
});

test("repository range review creates a fixed background task and opens the same result after navigation", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push(
    { id: "repositories", name: "程式庫", route: "/repositories" },
    { id: "tasks", name: "背景任務", route: "/tasks" },
  );
  await core.attach(page);
  const commit = "a".repeat(40),
    basis = "b".repeat(40),
    id = randomUUID(),
    jobId = randomUUID();
  let created = false;
  const job = {
    id: jobId,
    subjectId: id,
    kind: "repository-review",
    label: "team/repo",
    status: "completed",
    stage: "處理完成",
    attempt: 1,
    completedUnits: 1,
    totalUnits: 1,
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
    modelId: "fixture:8b",
    note: "確認授權",
    purpose: "review",
    createdAt: job.createdAt,
    job,
  };
  await page.route("**/api/v1/repositories**", (route) => {
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
    if (path.endsWith("/commits"))
      return route.fulfill({
        json: [
          { sha: commit, message: "修正授權" },
          { sha: basis, message: "起始版本" },
        ],
      });
    if (path.endsWith("/tree"))
      return route.fulfill({
        json: { repository: "team/repo", commit, path: "", entries: [] },
      });
    if (path.endsWith("/reviews")) {
      if (route.request().method() === "POST") {
        const body = route.request().postDataJSON();
        expect(body).toMatchObject({
          repository: "team/repo",
          commit,
          baseCommit: basis,
          note: "確認授權",
          purpose: "review",
        });
        expect(body.idempotencyKey).toMatch(/^[\da-f-]{36}$/);
        created = true;
        return route.fulfill({ json: review });
      }
      return route.fulfill({ json: created ? [review] : [] });
    }
    if (path.endsWith("/" + id))
      return route.fulfill({
        json: {
          review,
          version: 2,
          report: {
            output: "## [P2] 檢查角色範圍\n\n請測試未授權帳號的請求。",
            truncated: false,
            inputTokens: 800,
            outputTokens: 120,
            elapsedMs: 1000,
          },
          sections: [
            {
              ordinal: 0,
              label: "src/auth.ts",
              diff: "diff --git a/src/auth.ts b/src/auth.ts\n+authorize(user);",
              binary: false,
              output: "## [P2] 檢查角色範圍\n\n請測試未授權帳號的請求。",
              truncated: false,
              inputTokens: 800,
              outputTokens: 120,
              elapsedMs: 1000,
            },
          ],
        },
      });
    return route.fulfill({
      json: {
        items: [
          {
            fullName: "team/repo",
            description: "受控程式庫",
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
  await page.goto("/repositories");
  await page
    .locator(".repository-row")
    .filter({ hasText: "team/repo" })
    .click();
  await page.getByRole("button", { name: "AI Review", exact: true }).click();
  await chooseSelect(page, "檢閱範圍", "Commit 區間");
  await page.getByRole("combobox", { name: "起點 SHA", exact: true }).click();
  await page.getByRole("option", { name: /bbbbbbbbbb · 起始版本/ }).click();
  await page
    .getByLabel("特別關注的內容（選填）", { exact: true })
    .fill("確認授權");
  await page
    .getByRole("button", { name: "建立背景 review", exact: true })
    .click();
  await expect(page).toHaveURL(new RegExp(`review=${id}`));
  await expect(page.locator(".review-results")).toContainText(
    "[P2] 檢查角色範圍",
  );
  await expect(page.locator(".review-create")).not.toHaveAttribute("open", "");
  await page.reload();
  await expect(page.locator(".review-results")).toContainText(
    "請測試未授權帳號",
  );
  await settleEntrance(page);
  await expect(page.locator(".review-results > header")).toBeInViewport();
  await expect(page.locator(".review-results > header")).toBeFocused();
  await expect
    .poll(() =>
      page
        .locator(".review-report-body")
        .evaluate((element) => element.scrollHeight - element.clientHeight),
    )
    .toBeLessThanOrEqual(1);
  await page.screenshot({
    path: "artifacts/screenshots/repository-review.png",
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await page.reload();
  await expect(page.locator(".review-results")).toContainText(
    "請測試未授權帳號",
  );
  await expectViewportContained(page);
  await expect(page.locator(".review-results > header")).toBeInViewport();
  await page.screenshot({
    path: "artifacts/screenshots/repository-review-mobile.png",
  });
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
