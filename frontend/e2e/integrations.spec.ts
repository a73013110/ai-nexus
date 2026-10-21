import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import type {
  SourceDetailDto,
  ConversationDto,
} from "../src/app/core/api/schema";
import { expectCompactWorkspace, ApiFixture, settleEntrance } from "./fixtures";

test("controlled source search opens authorized status and history, then hands a draft to chat without sending", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "integrations",
    name: "資料來源",
    route: "/admin/integrations",
  });
  await core.attach(page);
  const detail: SourceDetailDto = {
    sourceId: "gd",
    record: {
      id: "DOC-001",
      kind: "document",
      title: "例行作業通知",
      status: "核准",
      revision: "v3",
      modifiedAt: new Date().toISOString(),
    },
    body: "## 作業通知\n\n請在本週五前整理待確認事項。",
    history: [
      {
        at: new Date().toISOString(),
        kind: "approved",
        actor: "承辦人",
        description: "核對並完成本次簽核",
        revision: "v3",
      },
    ],
    historyLimited: false,
  };
  await page.route("**/api/v1/integrations**", (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const json = (body: unknown) =>
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (path.endsWith("/integrations"))
      return json([
        {
          id: "gd",
          name: "公文系統",
          description: "授權公文與簽核歷程",
          status: "configured",
          notice: "已設定，查詢時確認連線與資料授權。",
          canQuery: true,
          kinds: ["document"],
        },
        {
          id: "meiho",
          name: "校務系統",
          description: "核准的政策與單位資料",
          status: "disabled",
          notice: "來源尚未啟用。",
          canQuery: false,
          kinds: ["reference", "organization"],
        },
      ]);
    if (path.endsWith("/records")) {
      expect(url.searchParams.get("query")).toBe("通知");
      return json([detail.record]);
    }
    if (path.endsWith("/record")) return json(detail);
    if (path.endsWith("/chat")) {
      const request = route.request().postDataJSON();
      expect(request).toEqual({
        recordId: detail.record.id,
        expectedRevision: "v3",
      });
      const conversation: ConversationDto = {
        id: randomUUID(),
        title: detail.record.title,
        activeLeafId: null,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        isFavorite: false,
        isArchived: false,
        systemInstruction: "",
        labels: [],
        projectId: null,
      };
      core.conversations.push(conversation);
      return json({
        conversation,
        prompt: "請分析下方來源資料：\n\n" + detail.body,
      });
    }
    return route.fulfill({ status: 404 });
  });
  await page.goto("/admin/integrations");
  await page.getByPlaceholder("搜尋公文系統的標題或識別碼…").fill("通知");
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "例行作業通知", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("核對並完成本次簽核")).toBeVisible();
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await expectCompactWorkspace(page);
  await page.getByRole("button", { name: "帶入對話", exact: true }).click();
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue("請分析下方來源資料：\n\n" + detail.body);
  expect(core.posts).toBe(0);
  expect(
    await page.evaluate(() => JSON.stringify(history.state)),
  ).not.toContain("作業通知");
});

test("unconfigured sources disclose setup status and do not offer queries", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "integrations",
    name: "資料來源",
    route: "/admin/integrations",
  });
  await core.attach(page);
  await page.route("**/api/v1/integrations", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: "gd",
          name: "公文系統",
          description: "唯讀文件",
          status: "acl-unconfirmed",
          notice: "等待確認來源端授權。",
          canQuery: false,
          kinds: ["document"],
        },
      ]),
    }),
  );
  await page.goto("/admin/integrations");
  await expect(
    page.getByRole("heading", { name: "公文系統尚未可用" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "搜尋", exact: true }),
  ).toHaveCount(0);
});

test("Gitea token is cleared after connecting, picked files become a draft with an attachment without sending", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.extraFeatures = [
    { id: "repositories", name: "程式庫", route: "/work/repositories" },
    { id: "knowledge", name: "知識庫", route: "/work/knowledge" },
  ];
  await fixture.attach(page);
  let connected = false;
  let tokenReceived = "";
  const commit = "a".repeat(40);
  const repository = {
    fullName: "hanglong/nexus",
    description: "唯讀文件",
    private: true,
    archived: false,
    empty: false,
    defaultBranch: "main",
    language: "",
    stars: 0,
    forks: 0,
    openIssues: 0,
    openPulls: 0,
    updatedAt: null,
    url: "https://gitea.fixture/hanglong/nexus",
    pinned: false,
  };
  await page.route("**/api/v1/repositories**", (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace("/api/v1/repositories", "");
    if (path === "/connection") {
      if (route.request().method() === "POST") {
        tokenReceived = route.request().postDataJSON().token;
        connected = true;
      }
      return route.fulfill({
        json: {
          available: true,
          connected,
          baseUrl: "https://gitea.fixture/",
          login: connected ? "fixture-user" : null,
          connectedAt: connected ? new Date().toISOString() : null,
        },
      });
    }
    if (path === "/hanglong/nexus")
      return route.fulfill({
        json: {
          repository,
          commit,
          branches: [{ name: "main", commit }],
          tags: [],
        },
      });
    if (path === "/hanglong/nexus/tree")
      return route.fulfill({
        json: {
          commit,
          path: "",
          entries: [
            { name: "README.md", path: "README.md", kind: "file", size: 20 },
          ],
          readme: null,
        },
      });
    if (path === "/hanglong/nexus/chat") {
      expect(route.request().postDataJSON()).toEqual({
        commit,
        paths: ["README.md"],
        issue: null,
        pull: null,
      });
      const now = new Date().toISOString();
      const conversation = {
        id: randomUUID(),
        title: "hanglong/nexus",
        activeLeafId: null,
        createdAt: now,
        updatedAt: now,
        isFavorite: false,
        isArchived: false,
        systemInstruction: "",
        labels: [],
      };
      const attachment = {
        id: randomUUID(),
        fileName: "hanglong-nexus.md",
        contentType: "text/markdown",
        size: 40,
        isImage: false,
        analysisMode: "extracted-text",
      };
      fixture.conversations.unshift(conversation);
      fixture.attachments.push(attachment);
      return route.fulfill({
        json: {
          conversation,
          attachment,
          prompt: "請閱讀附件中的內部技術文件。",
          skipped: [],
        },
      });
    }
    return route.fulfill({
      json: { items: [repository], pinned: [], page: 1, hasMore: false },
    });
  });
  await page.goto("/work/repositories");
  await page
    .getByLabel("貼上權杖", { exact: true })
    .fill("fixtureReadOnlyToken00000000");
  await page.getByRole("button", { name: "連線 Gitea", exact: true }).click();
  await expect(page.locator(".account")).toContainText("fixture-user");
  expect(tokenReceived).toBe("fixtureReadOnlyToken00000000");
  await expect(page.locator("#gitea-token")).toHaveCount(0);
  await page.getByRole("link", { name: /hanglong\/nexus/ }).click();
  await page.getByRole("checkbox", { name: "選取 README.md" }).check();
  await page
    .getByRole("region", { name: "已選取的檔案" })
    .getByRole("button", { name: "帶入對話" })
    .click();
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue("請閱讀附件中的內部技術文件。");
  await expect(page.getByText("hanglong-nexus.md")).toBeVisible();
  expect(fixture.posts).toBe(0);
  expect(fixture.conversations).toHaveLength(1);
});
