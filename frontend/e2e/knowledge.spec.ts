import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  chooseSelect,
  settleEntrance,
  expectViewportContained,
} from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

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
  await dialog.getByRole("checkbox", { name: "基本工作區" }).check();
  await dialog.getByRole("button", { name: "儲存權限" }).click();
  await expect(dialog).not.toBeVisible();
  expect(fixture.acl.members[0].role).toBe("editor");
  expect(fixture.acl.groupIds).toEqual(["workspace"]);
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await settleEntrance(page);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
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
  const original = page.getByRole("button", { name: "原始頁面", exact: true });
  const extracted = page.getByRole("button", { name: "擷取文字", exact: true });
  await original.focus();
  await original.press("ArrowRight");
  await expect(extracted).toBeFocused();
  await expect(extracted).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator(".reader-text")).toContainText("AI 文字辨識");
  await expect(page.locator(".reader-text img")).toHaveCount(0);
  await expect(page.locator(".reader-text")).toContainText(
    "<img src=x onerror=alert(1)>",
  );
  await page.getByRole("searchbox", { name: "搜尋文件文字" }).fill("差旅");
  await expect(page.locator(".reader-text mark")).toHaveCount(1);
  await page.getByRole("button", { name: "第 1 頁", exact: true }).click();
  await expect(page.locator(".reader-text")).toContainText("第一頁");
  await expect(page.locator(".reader-text pre")).toHaveCSS("font-size", "15px");
  await expect(page.getByRole("searchbox", { name: "搜尋文件文字" })).toHaveCSS(
    "font-size",
    "13px",
  );
  await page.setViewportSize({ width: 375, height: 812 });
  await expect(page.getByRole("searchbox", { name: "搜尋文件文字" })).toHaveCSS(
    "font-size",
    "16px",
  );
  await expect(page.locator(".reader-text pre")).toHaveCSS("font-size", "15px");
  await settleEntrance(page);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(errors).toEqual([]);
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
  await expect(page.getByRole("main").getByRole("status")).toContainText(
    "已提出停止要求",
  );
  job.status = "failed";
  job.errorCode = "fixture_error";
  job.errorMessage = "Password=fixture-private; source service stack trace";
  job.issueCode = "NX-" + "D".repeat(32);
  await page.getByRole("button", { name: "重新整理", exact: true }).click();
  await page.getByRole("button", { name: "需要處理", exact: true }).click();
  const failure = page.locator("nx-job-progress").getByRole("alert");
  await expect(failure).toContainText(
    "操作未完成，請聯絡管理員。查證代碼：NX-",
  );
  await expect(failure).not.toContainText("fixture-private");
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
  const sourceError = page
    .getByRole("alert")
    .filter({ hasText: "知識來源尚未載入" });
  await expect(sourceError).toBeVisible();
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("要有來源的下一題");
  await expect(
    page.getByRole("button", { name: "送出訊息", exact: true }),
  ).toBeDisabled();
  failing = false;
  await page.getByRole("button", { name: "重新載入來源", exact: true }).click();
  await expect(sourceError).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "選取對話知識來源" }),
  ).toContainText("來源 1");
  await expect(
    page.getByRole("button", { name: "送出訊息", exact: true }),
  ).toBeEnabled();
  expect(fixture.core.posts).toBe(1);
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
      }
    }
  }
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
        json: {
          title: "Password=fixture-private",
          code: "text_version_changed",
          issueCode: "NX-" + "D".repeat(32),
        },
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
  await expect(dialog.getByRole("alert")).toContainText("來源已被其他人修改");
  await expect(dialog.getByRole("alert")).not.toContainText("fixture-private");
  await expect(dialog.getByLabel("純文字內容", { exact: true })).toHaveValue(
    "修訂內容",
  );
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await dialog.getByRole("button", { name: "取消", exact: true }).click();
  await page
    .getByRole("dialog", { name: "放棄未儲存的內容" })
    .getByRole("button", { name: "放棄修改", exact: true })
    .click();
  await expect(dialog).not.toBeVisible();
});
