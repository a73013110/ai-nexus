import { test, expect } from "@playwright/test";
import { KnowledgeFixture } from "./knowledge-fixture";
import { chooseSelect, settleEntrance } from "./fixtures";

test("knowledge upload, source query and named reader permissions are direct and themed", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  await page.goto("/knowledge");
  await expect(
    page.getByRole("link", { name: /差旅費用申請.pdf/ }),
  ).toBeVisible();
  await page.locator("nx-knowledge-page input[type=file]").setInputFiles({
    name: "差旅補充.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("測試補充資料"),
  });
  await expect(page.getByRole("link", { name: /差旅補充.txt/ })).toBeVisible();
  await page.getByRole("textbox", { name: "知識庫查詢內容" }).fill("差旅簽核");
  await page.getByRole("button", { name: "檢索來源", exact: true }).click();
  await expect(page.locator(".knowledge-hit")).toContainText("第 2 頁");
  expect(fixture.sourceQueries).toBe(1);
  await page.getByRole("button", { name: "知識庫操作", exact: true }).click();
  await page.getByRole("menuitem", { name: "存取權限" }).click();
  const dialog = page.getByRole("dialog");
  const directory = dialog.getByRole("searchbox", {
    name: "尋找要授權的使用者",
  });
  await directory.click();
  await directory.fill("同事");
  await dialog.getByRole("button", { name: /林同事/ }).click();
  await chooseSelect(page, "林同事的權限", "可編輯");
  await dialog.getByRole("checkbox", { name: "基本工作台" }).check();
  await dialog.getByRole("button", { name: "儲存權限" }).click();
  await expect(dialog).not.toBeVisible();
  expect(fixture.acl.members[0].role).toBe("editor");
  expect(fixture.acl.groupIds).toEqual(["workspace"]);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/knowledge-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 375, height: 812 });
  await settleEntrance(page);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "artifacts/screenshots/knowledge-mobile.png",
    fullPage: true,
  });
});
test("PDF reader renders real pages, searches extracted text and treats text as untrusted", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture(),
    doc = fixture.seed();
  await fixture.attach(page);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(`/reader/${doc.id}?page=2`);
  const canvas = page.locator("nx-document-reader canvas");
  await expect(canvas).toHaveAttribute("width", /[1-9]\d+/);
  await expect(canvas).toHaveAttribute("aria-label", /第 2 頁/);
  await chooseSelect(page, "文件縮放", "125%");
  await page.getByRole("button", { name: "擷取文字", exact: true }).click();
  await expect(page.locator(".reader-text")).toContainText("AI 文字辨識");
  await expect(page.locator(".reader-text img")).toHaveCount(0);
  await expect(page.locator(".reader-text")).toContainText(
    "<img src=x onerror=alert(1)>",
  );
  await page.getByRole("searchbox", { name: "搜尋文件文字" }).fill("差旅");
  await expect(page.locator(".reader-text mark")).toHaveCount(1);
  await page.getByRole("button", { name: "第 1 頁", exact: true }).click();
  await expect(page.locator(".reader-text")).toContainText("第一頁");
  await page.setViewportSize({ width: 375, height: 812 });
  await settleEntrance(page);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(errors).toEqual([]);
  await page.screenshot({
    path: "artifacts/screenshots/reader-mobile.png",
    fullPage: true,
  });
});
test("conversation knowledge selection saves before first send and is restored on reload", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  await page.goto("/chat");
  await page.getByRole("button", { name: "選取對話知識來源" }).click();
  const panel = page.getByRole("dialog", { name: "對話知識來源" });
  await panel.getByRole("checkbox", { name: /公司作業規範/ }).check();
  await page.keyboard.press("Escape");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("如何申請差旅？");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.getByRole("table")).toBeVisible();
  expect(fixture.selections.get(fixture.core.conversations[0].id)).toEqual([
    fixture.collections[0].resource.id,
  ]);
  await page.reload();
  await expect(
    page.getByRole("button", { name: "選取對話知識來源" }),
  ).toContainText("來源 1");
});
test("background tasks show real stages and distinguish cancellation requests from completion", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  const job = fixture.jobs[0];
  job.status = "running";
  job.stage = "正在索引第 1 / 2 段";
  job.completedUnits = 1;
  await fixture.attach(page);
  await page.goto("/tasks");
  await expect(page.locator("progress")).toHaveAttribute("value", "1");
  await page.getByRole("button", { name: "停止處理" }).click();
  await expect(page.getByRole("status")).toContainText("已提出停止要求");
  job.status = "failed";
  job.errorCode = "fixture_error";
  job.errorMessage = "來源服務暫時無法使用。";
  await page.getByRole("button", { name: "重新整理", exact: true }).click();
  await page.getByRole("button", { name: "需要處理", exact: true }).click();
  await expect(page.locator(".job-error")).toContainText("暫時無法使用");
  await page.getByRole("button", { name: "重試", exact: true }).click();
  expect(job.status).toBe("queued");
});

test("failed conversation source loading blocks submission until an explicit successful retry", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  await page.goto("/chat");
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("查詢公司規範");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(page.getByRole("table")).toBeVisible();
  const selected = fixture.core.conversations[0];
  fixture.selections.set(selected.id, [fixture.collections[0].resource.id]);
  let failing = true;
  await page.route("**/api/v1/conversations/*/knowledge", (route) =>
    failing && route.request().method() === "GET"
      ? route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({
            title: "來源暫時無法載入",
            code: "fixture_unavailable",
          }),
        })
      : route.fallback(),
  );
  await page.reload();
  await expect(page.locator(".source-load-error")).toBeVisible();
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("要有來源的下一題");
  await expect(
    page.getByRole("button", { name: "送出訊息", exact: true }),
  ).toBeDisabled();
  failing = false;
  await page.getByRole("button", { name: "重新載入來源", exact: true }).click();
  await expect(page.locator(".source-load-error")).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "選取對話知識來源" }),
  ).toContainText("來源 1");
  await expect(
    page.getByRole("button", { name: "送出訊息", exact: true }),
  ).toBeEnabled();
  expect(fixture.core.posts).toBe(1);
});
