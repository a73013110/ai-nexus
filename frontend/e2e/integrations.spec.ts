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
    route: "/integrations",
  });
  await core.attach(page);
  const detail: SourceDetailDto = {
    sourceId: "gdweb",
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
          id: "gdweb",
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
  await page.goto("/integrations");
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
    route: "/integrations",
  });
  await core.attach(page);
  await page.route("**/api/v1/integrations", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: "gdweb",
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
  await page.goto("/integrations");
  await expect(
    page.getByRole("heading", { name: "公文系統尚未可用" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "搜尋", exact: true }),
  ).toHaveCount(0);
});

test("Gitea token is cleared after connecting, pinned files become drafts without sending automatically", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.extraFeatures = [
    { id: "repositories", name: "程式庫", route: "/repositories" },
    { id: "knowledge", name: "知識庫", route: "/knowledge" },
  ];
  await fixture.attach(page);
  let connected = false;
  let tokenReceived = "";
  const commit = "a".repeat(40);
  await page.route("**/api/v1/repositories**", (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (path.endsWith("/connection")) {
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
          notice: "使用你的唯讀權限。",
        },
      });
    }
    if (path.endsWith("/tree"))
      return route.fulfill({
        json: {
          repository: "hanglong/nexus",
          commit,
          path: "",
          entries: [
            { name: "README.md", path: "README.md", kind: "file", size: 20 },
          ],
        },
      });
    if (path.endsWith("/file")) {
      expect(url.searchParams.get("commit")).toBe(commit);
      return route.fulfill({
        json: {
          repository: "hanglong/nexus",
          commit,
          path: "README.md",
          text: "# 內部技術文件",
          url: `https://gitea.fixture/hanglong/nexus/src/commit/${commit}/README.md`,
        },
      });
    }
    return route.fulfill({
      json: {
        items: [
          {
            fullName: "hanglong/nexus",
            description: "唯讀文件",
            private: true,
            defaultBranch: "main",
            url: "https://gitea.fixture/hanglong/nexus",
          },
        ],
        page: 1,
        hasMore: false,
      },
    });
  });
  await page.route("**/api/v1/knowledge/collections", (route) =>
    route.fulfill({ json: [] }),
  );
  await page.goto("/repositories");
  await page
    .getByLabel("個人存取權杖", { exact: true })
    .fill("fixtureReadOnlyToken00000000");
  await page.getByRole("button", { name: "連線 Gitea", exact: true }).click();
  await expect(page.locator(".repository-login")).toContainText("fixture-user");
  expect(tokenReceived).toBe("fixtureReadOnlyToken00000000");
  await expect(page.locator("#gitea-token")).toHaveCount(0);
  await page.getByRole("button", { name: /hanglong\/nexus/ }).click();
  await page.getByRole("button", { name: /README.md/ }).click();
  await expect(page.locator(".repository-file-text")).toContainText(
    "內部技術文件",
  );
  await settleEntrance(page);
  await page.getByRole("button", { name: "帶入對話草稿", exact: true }).click();
  await expect(
    page.getByRole("textbox", { name: "傳送訊息", exact: true }),
  ).toHaveValue(/內部技術文件/);
  expect(fixture.posts).toBe(0);
  expect(fixture.conversations).toHaveLength(1);
});
