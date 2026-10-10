import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  settleEntrance,
  expectCompactWorkspace,
} from "./fixtures";

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
  await expect(page.locator(".review-report nx-notice")).toContainText(
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
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.reload();
  await expect(page.locator(".review-results")).toContainText(
    "請測試未授權帳號",
  );
  await expectViewportContained(page);
  await expect(page.locator(".review-results > header")).toBeInViewport();
  await expectCompactWorkspace(page);
});
