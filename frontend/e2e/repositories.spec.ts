import { test, expect, type Page, type Route } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";

const head = "c".repeat(40),
  middle = "b".repeat(40),
  first = "a".repeat(40),
  root = "9".repeat(40),
  dev = "d".repeat(40);

const repository = {
  fullName: "team/nexus",
  description: "AI 工作平台",
  private: true,
  archived: false,
  empty: false,
  defaultBranch: "main",
  language: "C#",
  stars: 3,
  forks: 1,
  openIssues: 1,
  openPulls: 1,
  updatedAt: new Date().toISOString(),
  url: "https://gitea.example/team/nexus",
  pinned: false,
};

const commit = (
  sha: string,
  title: string,
  parents: string[],
  day: string,
) => ({
  sha,
  title,
  message: `${title}\n\n詳細說明 ${title}`,
  author: "kai",
  at: `${day}T08:00:00Z`,
  parents,
});
const commits = [
  commit(head, "修正登入逾時", [middle], "2026-10-12"),
  commit(middle, "新增匯出 API", [first], "2026-10-12"),
  commit(first, "初始版本", [root], "2026-10-10"),
];

const diff = [
  "diff --git a/src/app.ts b/src/app.ts",
  "--- a/src/app.ts",
  "+++ b/src/app.ts",
  "@@ -1,2 +1,2 @@",
  " export const answer = 42;",
  "-console.log(answer);",
  "+console.info(answer);",
  "diff --git a/docs/new.md b/docs/new.md",
  "new file mode 100644",
  "--- /dev/null",
  "+++ b/docs/new.md",
  "@@ -0,0 +1 @@",
  "+# 新文件",
  "",
].join("\n");

const file = (path: string, text: string) => ({
  commit: head,
  path,
  text,
  size: text.length,
  url: `${repository.url}/src/commit/${head}/${path}`,
});

const label = { name: "bug", color: "#d73a4a" };
const pull = {
  number: 2,
  title: "登入逾時改為可設定",
  state: "open",
  merged: false,
  draft: false,
  author: "mei",
  headRef: "feature/timeout",
  baseRef: "main",
  labels: [label],
  comments: 1,
  createdAt: "2026-10-11T08:00:00Z",
  updatedAt: "2026-10-12T08:00:00Z",
  url: `${repository.url}/pulls/2`,
};
const issue = {
  number: 1,
  title: "登入後偶爾被登出",
  state: "open",
  author: "lin",
  labels: [label],
  assignees: [],
  comments: 1,
  createdAt: "2026-10-09T08:00:00Z",
  updatedAt: "2026-10-12T08:00:00Z",
  url: `${repository.url}/issues/1`,
};
const comment = {
  id: 7,
  author: "kai",
  body: "已在 **#2** 修正。",
  createdAt: "2026-10-12T09:00:00Z",
  url: `${repository.url}/issues/1#issuecomment-7`,
};

class RepositoryFixture {
  readonly core = new ApiFixture();
  readonly requests: {
    method: string;
    path: string;
    body: unknown;
    query: URLSearchParams;
  }[] = [];
  connection = {
    available: true,
    connected: true,
    baseUrl: "https://gitea.example/",
    login: "kai",
    connectedAt: new Date().toISOString(),
  };
  listFailure: { status: number; code: string } | null = null;
  pinned = false;
  reviews: Record<string, unknown>[] = [];
  /** Reads of a review before it reports completion. */
  pendingReads = 1;

  constructor() {
    this.core.extraFeatures.push(
      { id: "repositories", name: "程式庫", route: "/repositories" },
      { id: "knowledge", name: "知識庫", route: "/knowledge" },
      { id: "tasks", name: "背景任務", route: "/tasks" },
    );
  }

  async attach(page: Page) {
    await this.core.attach(page);
    await page.route("**/api/v1/repositories**", (route) => this.handle(route));
    await page.route("**/api/v1/knowledge/collections", (route) =>
      route.fulfill({
        json: [
          {
            resource: {
              id: randomUUID(),
              name: "工程知識",
              canEdit: true,
              isOwner: true,
            },
            description: "",
            documentCount: 0,
          },
        ],
      }),
    );
  }

  private job(status: string) {
    const now = new Date().toISOString();
    return {
      id: randomUUID(),
      subjectId: randomUUID(),
      kind: "repository-review",
      label: "team/nexus",
      status,
      stage: status === "completed" ? "完成" : "檢閱區段 1 / 2",
      attempt: 1,
      completedUnits: status === "completed" ? 2 : 1,
      totalUnits: 2,
      cancelRequested: false,
      errorCode: null,
      errorMessage: null,
      issueCode: null,
      createdAt: now,
      updatedAt: now,
    };
  }

  private async handle(route: Route) {
    const request = route.request(),
      url = new URL(request.url()),
      path = url.pathname.replace("/api/v1/repositories", ""),
      method = request.method(),
      body = request.postData() ? request.postDataJSON() : null;
    this.requests.push({ method, path, body, query: url.searchParams });
    const json = (value: unknown, status = 200) =>
      route.fulfill({ status, json: value });
    if (path === "/connection") return json(this.connection);
    if (path === "") {
      if (this.listFailure)
        return route.fulfill({
          status: this.listFailure.status,
          contentType: "application/problem+json",
          body: JSON.stringify({
            title: "Forbidden",
            code: this.listFailure.code,
          }),
        });
      const q = url.searchParams.get("q") ?? "";
      return json({
        items:
          q && !"nexus".includes(q)
            ? []
            : [{ ...repository, pinned: this.pinned }],
        pinned: this.pinned
          ? [
              {
                fullName: repository.fullName,
                description: repository.description,
                private: true,
                pinnedAt: new Date().toISOString(),
              },
            ]
          : [],
        page: 1,
        hasMore: false,
      });
    }
    const review = /^\/reviews\/([^/]+)(?:\/(comment|cancel|retry))?$/.exec(
      path,
    );
    if (review) {
      const row = this.reviews.find((x) => x["id"] === review[1]);
      if (!row)
        return json({ title: "Not found", code: "review_not_found" }, 404);
      if (review[2] === "comment") {
        row["commentUrl"] = `${repository.url}/pulls/2#issuecomment-99`;
        return json(row);
      }
      const done = this.pendingReads-- <= 0;
      if (done) row["job"] = this.job("completed");
      return json({
        review: row,
        sections: [
          {
            ordinal: 1,
            label: "src/app.ts",
            diff: "@@ -1 +1 @@\n-a\n+b",
            binary: false,
            output: "此段沒有問題。",
            truncated: false,
            inputTokens: 10,
            outputTokens: 5,
            elapsedMs: 900,
          },
        ],
        report: done
          ? {
              output: "## 結論\n\n變更把日誌層級改為 info。",
              truncated: false,
              inputTokens: 1200,
              outputTokens: 300,
              elapsedMs: 4200,
            }
          : null,
      });
    }
    const repo = path.replace("/team/nexus", "");
    if (repo === "")
      return json({
        repository: { ...repository, pinned: this.pinned },
        commit: head,
        branches: [
          { name: "main", commit: head },
          { name: "dev", commit: dev },
        ],
        tags: [{ name: "v1.0", commit: first }],
      });
    if (repo === "/pin") {
      this.pinned = method === "PUT";
      return route.fulfill({ status: 204 });
    }
    if (repo === "/tree") {
      const folder = url.searchParams.get("path") ?? "";
      return json(
        folder === "docs"
          ? {
              commit: head,
              path: "docs",
              entries: [
                {
                  name: "guide.md",
                  path: "docs/guide.md",
                  kind: "file",
                  size: 40,
                },
              ],
              readme: null,
            }
          : {
              commit: head,
              path: "",
              entries: [
                { name: "docs", path: "docs", kind: "dir", size: 0 },
                {
                  name: "README.md",
                  path: "README.md",
                  kind: "file",
                  size: 30,
                },
                { name: "app.ts", path: "app.ts", kind: "file", size: 48 },
              ],
              readme: file(
                "README.md",
                "# Nexus\n\n給團隊用的 **AI 工作平台**。",
              ),
            },
      );
    }
    if (repo === "/file") {
      const target = url.searchParams.get("path")!;
      return json(
        file(
          target,
          target.endsWith(".ts")
            ? "export const answer = 42;\nconsole.log(answer);\n"
            : "# 指南\n",
        ),
      );
    }
    if (repo === "/commits")
      return json({ items: commits, page: 1, hasMore: false });
    if (repo === "/diff") {
      const base = url.searchParams.get("base");
      const headCommit =
        commits.find((x) => x.sha === url.searchParams.get("head")) ??
        commits[0];
      return json({
        head: headCommit,
        base: base
          ? (commits.find((x) => x.sha === base) ??
            commit(base, "更早的版本", [], "2026-10-01"))
          : null,
        diff,
        tooLarge: false,
      });
    }
    if (repo === "/pulls")
      return json({
        items: url.searchParams.get("state") === "closed" ? [] : [pull],
        page: 1,
        hasMore: false,
      });
    if (repo === "/pulls/2")
      return json({
        pull,
        body: "把逾時改成設定值。",
        headCommit: head,
        baseCommit: first,
        comments: [comment],
      });
    if (repo === "/issues")
      return json({ items: [issue], page: 1, hasMore: false });
    if (repo === "/issues/1")
      return json({
        issue,
        body: "重現步驟：閒置 10 分鐘。",
        comments: [comment],
      });
    if (repo === "/chat") {
      const now = new Date().toISOString();
      const conversation = {
        id: randomUUID(),
        title: "team/nexus",
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
        fileName: "team-nexus.md",
        contentType: "text/markdown",
        size: 120,
        isImage: false,
        analysisMode: "extracted-text",
      };
      this.core.conversations.unshift(conversation);
      this.core.attachments.push(attachment);
      return json({
        conversation,
        attachment,
        prompt: "請閱讀附件中的程式碼。",
        skipped: [],
      });
    }
    if (repo === "/imports")
      return json({
        id: randomUUID(),
        files: 4,
        job: { ...this.job("queued"), kind: "repository-import" },
      });
    if (repo === "/reviews" && method === "POST") {
      const created = {
        id: randomUUID(),
        repository: "team/nexus",
        commit: (body as { commit: string | null }).commit ?? head,
        baseCommit: (body as { baseCommit: string | null }).baseCommit,
        pullRequest: (body as { pullRequest: number | null }).pullRequest,
        purpose: (body as { purpose: string }).purpose,
        modelId: "fixture:8b",
        modelDisplayName: "本機測試模型",
        note: "",
        createdAt: new Date().toISOString(),
        job: this.job("running"),
        commentUrl: null,
      };
      this.reviews.unshift(created);
      return json(created, 201);
    }
    if (repo === "/reviews") return json(this.reviews);
    return json({ title: "Not found", code: "gitea_not_found" }, 404);
  }
}

test("a rejected Gitea token says what to fix instead of a generic failure", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  fixture.listFailure = { status: 403, code: "gitea_unauthorized" };
  await fixture.attach(page);
  await page.goto("/repositories");
  await expect(
    page.getByText("Gitea 拒絕這個權杖，請確認權杖仍有效，或更新授權。"),
  ).toBeVisible();
  await expect(page.getByText("操作未完成")).toHaveCount(0);
});

test("a connection that is not set up shows the steps without an error", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  fixture.connection = { ...fixture.connection, connected: false, login: "" };
  await fixture.attach(page);
  await page.goto("/repositories");
  await expect(
    page.getByRole("heading", { name: "連接你的 Gitea" }),
  ).toBeVisible();
  await expect(page.locator("nx-notice")).toHaveCount(0);
});

test("browsing code pins a commit, reads folders and files, and hands picked files to a conversation", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  await fixture.attach(page);
  await page.goto("/repositories");
  await page.getByRole("link", { name: /team\/nexus/ }).click();
  await expect(page).toHaveURL(/\/repositories\/team\/nexus$/);
  await expect(page.getByRole("button", { name: /ccccccc/ })).toBeVisible();
  // The folder's README is rendered under its entries.
  await expect(
    page.getByRole("heading", { name: "Nexus", exact: true }),
  ).toBeVisible();

  await page.getByRole("button", { name: "釘選到列表上方" }).click();
  await expect(page.getByRole("button", { name: "取消釘選" })).toHaveAttribute(
    "aria-pressed",
    "true",
  );
  expect(
    fixture.requests.some((x) => x.method === "PUT" && x.path.endsWith("/pin")),
  ).toBe(true);

  await page.getByRole("link", { name: "app.ts" }).click();
  await expect(page).toHaveURL(/file=app\.ts/);
  await expect(page.locator(".gutter")).toHaveText("1\n2");
  await expect(page.locator(".code code")).toContainText(
    "console.log(answer);",
  );
  expect(
    fixture.requests.find((x) => x.path.endsWith("/file"))?.query.get("commit"),
  ).toBe(head);

  await page.getByRole("link", { name: "nexus", exact: true }).click();
  await page.getByRole("checkbox", { name: "選取 app.ts" }).check();
  await page.getByRole("link", { name: "docs" }).click();
  await page.getByRole("checkbox", { name: "選取 guide.md" }).check();
  const selection = page.getByRole("region", { name: "已選取的檔案" });
  await expect(selection).toContainText("2 / 20 個檔案");
  await selection.getByRole("button", { name: "帶入對話" }).click();
  await expect(page).toHaveURL(/\/chat\/[\da-f-]{36}$/);
  expect(fixture.requests.find((x) => x.path.endsWith("/chat"))?.body).toEqual({
    commit: head,
    paths: ["app.ts", "docs/guide.md"],
    issue: null,
    pull: null,
  });
});

test("a commit range opens its diff and a summary review that reports back", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  await fixture.attach(page);
  await page.goto("/repositories/team/nexus/commits");
  await expect(
    page.getByRole("heading", { name: /2026年10月12日/ }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "選為區間端點：修正登入逾時" })
    .click();
  await page.getByRole("button", { name: "選為區間端點：初始版本" }).click();
  const range = page.getByRole("region", { name: "提交區間" });
  await expect(range).toContainText("3 個提交");
  await range.getByRole("button", { name: "查看區間變更" }).click();
  // The older end is included, so the range starts after its parent.
  await expect(page).toHaveURL(new RegExp(`/compare/${root}\\.\\.\\.${head}$`));
  await expect(page.getByText("2 個檔案")).toBeVisible();
  await expect(page.locator(".line[data-kind='add']").first()).toContainText(
    "console.info(answer);",
  );

  await page.getByRole("button", { name: "AI 變更摘要" }).click();
  const dialog = page.getByRole("dialog", { name: "交給 AI 初檢" });
  await expect(
    dialog.getByRole("button", { name: "變更摘要" }),
  ).toHaveAttribute("aria-pressed", "true");
  await dialog.getByRole("button", { name: "開始" }).click();
  await expect(page).toHaveURL(
    /\/repositories\/team\/nexus\/reviews\/[\da-f-]{36}$/,
  );
  expect(
    fixture.requests.find(
      (x) => x.method === "POST" && x.path.endsWith("/reviews"),
    )?.body,
  ).toMatchObject({
    commit: head,
    baseCommit: root,
    pullRequest: null,
    purpose: "summary",
    modelId: "fixture:8b",
  });
  await expect(page.getByRole("article", { name: "整體報告" })).toContainText(
    "變更把日誌層級改為 info",
    {
      timeout: 10000,
    },
  );
  await expect(page.getByRole("button", { name: "貼到 PR 留言" })).toHaveCount(
    0,
  );
});

test("a pull request review can be posted back as a comment once", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  fixture.pendingReads = 0;
  await fixture.attach(page);
  await page.goto("/repositories/team/nexus/pulls");
  await page.getByRole("link", { name: /登入逾時改為可設定/ }).click();
  await expect(page.getByText("把逾時改成設定值。")).toBeVisible();
  await expect(page.getByRole("link", { name: "查看變更" })).toHaveAttribute(
    "href",
    `/repositories/team/nexus/compare/${first}...${head}`,
  );
  await page.locator(".head").getByRole("button", { name: "AI 檢閱" }).click();
  await page
    .getByRole("dialog", { name: "交給 AI 初檢" })
    .getByRole("button", { name: "開始" })
    .click();
  expect(
    fixture.requests.find(
      (x) => x.method === "POST" && x.path.endsWith("/reviews"),
    )?.body,
  ).toMatchObject({
    commit: null,
    pullRequest: 2,
    purpose: "review",
  });
  await page.getByRole("button", { name: "貼到 PR 留言" }).click();
  const dialog = page.getByRole("dialog", { name: /貼到 Pull Request #2/ });
  await expect(dialog.getByRole("textbox", { name: "留言內容" })).toHaveValue(
    /變更把日誌層級改為 info/,
  );
  await dialog.getByRole("button", { name: "留言" }).click();
  await expect(page.getByRole("link", { name: /已貼到 PR/ })).toBeVisible();
  await expect(page.getByRole("button", { name: "貼到 PR 留言" })).toHaveCount(
    0,
  );
});

test("issues filter by state and search, and open with their discussion", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  await fixture.attach(page);
  await page.goto("/repositories/team/nexus/issues");
  await page.getByRole("searchbox", { name: "搜尋議題" }).fill("登出");
  await expect(page).toHaveURL(/q=%E7%99%BB%E5%87%BA/);
  expect(
    fixture.requests
      .filter((x) => x.path.endsWith("/issues"))
      .at(-1)
      ?.query.get("q"),
  ).toBe("登出");
  await page.getByRole("button", { name: "已關閉", exact: true }).click();
  await expect(page).toHaveURL(/state=closed/);
  await page.getByRole("link", { name: /登入後偶爾被登出/ }).click();
  await expect(page.getByText("重現步驟：閒置 10 分鐘。")).toBeVisible();
  await expect(page.locator(".post").nth(1)).toContainText("已在 #2 修正。");
});

test("a repository fits a phone screen", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const fixture = new RepositoryFixture();
  await fixture.attach(page);
  await page.goto("/repositories/team/nexus/compare/" + first + "..." + head);
  await expect(page.getByText("2 個檔案")).toBeVisible();
  await expectViewportContained(page);
  await page.goto("/repositories/team/nexus?file=app.ts");
  await expect(page.locator(".code code")).toContainText("answer");
  await expectViewportContained(page);
});

test("notification links that only know the review id open it inside its repository", async ({
  page,
}) => {
  const fixture = new RepositoryFixture();
  fixture.pendingReads = 0;
  const id = randomUUID();
  fixture.reviews.push({
    id,
    repository: "team/nexus",
    commit: head,
    baseCommit: null,
    pullRequest: null,
    purpose: "review",
    modelId: "fixture:8b",
    modelDisplayName: "本機測試模型",
    note: "",
    createdAt: new Date().toISOString(),
    job: {},
    commentUrl: null,
  });
  await fixture.attach(page);
  await page.goto(`/repositories?review=${id}`);
  await expect(page).toHaveURL(
    new RegExp(`/repositories/team/nexus/reviews/${id}$`),
  );
  await expect(page.getByRole("article", { name: "整體報告" })).toContainText(
    "結論",
  );
});

for (const width of [1440, 375]) {
  test(`repositories tours walk the list and the open section at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const fixture = new RepositoryFixture();
    // A phone viewer without chat or knowledge reads a pull request; the desktop one browses code.
    const phone = width === 375;
    if (phone) {
      fixture.core.chatAccess = false;
      fixture.core.extraFeatures = fixture.core.extraFeatures.filter(
        (x) => x.id !== "knowledge",
      );
    } else fixture.pinned = true;
    await fixture.attach(page);
    const height = phone ? 812 : 1000;
    await page.setViewportSize({ width, height });
    const launcher = page
      .locator(".feature-header")
      .getByRole("button", { name: "操作導覽", exact: true });
    const tour = page.locator(".driver-popover.nx-tour");
    const title = tour.locator(".driver-popover-title");
    const description = tour.locator(".driver-popover-description");
    const progress = tour.locator(".driver-popover-progress-text");
    // Walks to the end and returns each step's title and description.
    const walk = async () => {
      await launcher.click();
      await expect(title).toHaveText(/./);
      const total = Number((await progress.innerText()).split("/")[1]);
      const titles = [await title.innerText()];
      const descriptions = [await description.innerText()];
      for (let step = 2; step <= total; step++) {
        await tour.getByRole("button", { name: "下一步", exact: true }).click();
        await expect(progress).toHaveText(`${step} / ${total}`);
        titles.push(await title.innerText());
        descriptions.push(await description.innerText());
        await settleEntrance(page);
        const bounds = (await tour.boundingBox())!;
        expect(bounds.x).toBeGreaterThanOrEqual(0);
        expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
        expect(bounds.y + bounds.height).toBeLessThanOrEqual(height);
      }
      await tour.getByRole("button", { name: "開始使用", exact: true }).click();
      await expect(tour).toHaveCount(0);
      await expect(launcher).toBeFocused();
      await expect(page.locator(".driver-overlay")).toHaveCount(0);
      return { titles, descriptions };
    };
    await page.goto("/repositories");
    await expect(page.locator(".resource-card")).toBeVisible();
    const list = await walk();
    expect(list.titles).toEqual([
      "用你的 Gitea 權限閱讀程式",
      ...(phone ? [] : ["釘選的程式庫"]),
      "搜尋程式庫",
      "打開程式庫",
      "你的 Gitea 連線",
    ]);
    expect(list.descriptions[0]).toContain(
      phone ? "可以交給 AI 檢閱" : "可以帶入對話、匯入知識庫或交給 AI 檢閱",
    );
    expect(list.descriptions.at(-1)).toContain("重播導覽");
    if (phone) {
      await page.goto("/repositories/team/nexus/pulls/2");
      await expect(page.getByText("把逾時改成設定值。")).toBeVisible();
    } else {
      await page.locator(".resource-card").click();
      await expect(
        page.getByRole("checkbox", { name: "選取 app.ts" }),
      ).toBeVisible();
    }
    const detail = await walk();
    expect(detail.titles).toEqual([
      "程式碼、紀錄與討論",
      "固定閱讀的版本",
      ...(phone
        ? ["檢閱這個 Pull Request"]
        : ["瀏覽資料夾與檔案", "勾選檔案帶入對話", "匯入知識庫"]),
      "釘選常用的程式庫",
      "你的 Gitea 連線",
    ]);
    if (phone) expect(detail.descriptions[2]).not.toContain("帶入對話");
    expect(detail.descriptions.at(-1)).toContain("重播導覽");
    expect(errors).toEqual([]);
  });
}
