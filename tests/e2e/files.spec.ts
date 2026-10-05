import { test, expect } from "@playwright/test";
import { KnowledgeFixture } from "./knowledge-fixture";
import {
  chooseSelect,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";

test("capacity preflight prevents an upload when the remaining space is exhausted", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  fixture.core.attachmentLimitBytes = 0;
  await fixture.attach(page);
  await page.goto("/files");
  await expect(page.locator("nx-files-page nx-storage-usage")).toContainText(
    "剩餘 0 bytes",
  );
  const count = fixture.core.attachments.length;
  await page.locator("nx-files-page input[type=file]").setInputFiles({
    name: "超額.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("over quota"),
  });
  await expect(page.getByRole("alert")).toContainText("附件容量不足");
  expect(fixture.core.attachments.length).toBe(count);
});

test("library files can be searched, previewed, reused in chat and added to knowledge without another upload", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  await fixture.attach(page);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/files");
  await expect(
    page.getByRole("heading", { name: "檔案庫", exact: true }),
  ).toBeVisible();
  await expect(page.locator("nx-files-page nx-storage-usage")).toContainText(
    "5.00 GB",
  );
  await page.locator("nx-files-page input[type=file]").setInputFiles({
    name: "分析計畫.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("Plan an accessible file library."),
  });
  await expect(
    page.locator(".file-card").filter({ hasText: "分析計畫.txt" }),
  ).toBeVisible();
  expect(fixture.core.attachments).toHaveLength(2);
  const storedOnce = fixture.core.storage().usedBytes;
  await page
    .getByRole("searchbox", { name: "搜尋檔案庫", exact: true })
    .fill("分析");
  await expect(page.locator(".file-card")).toHaveCount(1);
  await page
    .getByRole("button", { name: "將 分析計畫.txt 加入知識庫" })
    .click();
  const add = page.getByRole("dialog", { name: "加入知識庫", exact: true });
  await expect(add).toContainText("成員可以閱讀原檔");
  await add.getByRole("button", { name: "加入並建立索引" }).click();
  await expect(add).not.toBeVisible();
  await expect(
    page
      .locator(".file-card")
      .getByRole("link", { name: "公司作業規範", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "刪除 分析計畫.txt", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("searchbox", { name: "搜尋檔案庫", exact: true })
    .fill("");
  await expect(page.locator(".file-card")).toHaveCount(2);
  await settleEntrance(page);
  await page.screenshot({
    path: "artifacts/screenshots/file-library-desktop.png",
  });
  await page.getByRole("button", { name: "清單排列" }).click();
  await expect(page.locator(".file-browser")).toHaveClass(/file-browser-list/);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  await page.screenshot({
    path: "artifacts/screenshots/file-library-mobile.png",
  });
  await page.evaluate(() =>
    document.documentElement.setAttribute("data-theme", "dark"),
  );
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.screenshot({
    path: "artifacts/screenshots/file-library-dark.png",
  });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/chat");
  await page.getByRole("button", { name: "從檔案庫加入", exact: true }).click();
  const picker = page.getByRole("dialog", {
    name: "從檔案庫選取",
    exact: true,
  });
  await picker
    .getByRole("button", { name: "選取 分析計畫.txt", exact: true })
    .click();
  await expect(page.locator(".composer nx-attachment-list")).toContainText(
    "分析計畫.txt",
  );
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("使用這份計畫");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeVisible();
  expect(fixture.core.attachments).toHaveLength(2);
  expect(fixture.core.storage().usedBytes).toBe(storedOnce);
  expect(fixture.core.lastRequest!.attachmentIds).toEqual([
    fixture.core.attachments.find((value) => value.fileName === "分析計畫.txt")!
      .id,
  ]);
  expect(errors).toEqual([]);
});

test("knowledge can choose an existing library original and display its source in a modal", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  const doc = fixture.seed();
  const second = fixture.collection("新的知識庫");
  await fixture.attach(page);
  await page.goto("/knowledge");
  await chooseSelect(page, "目前知識庫", "新的知識庫 0 / 0 份文件可查詢");
  await page.getByRole("button", { name: "從檔案庫加入", exact: true }).click();
  const picker = page.getByRole("dialog", {
    name: "從檔案庫選取",
    exact: true,
  });
  await picker
    .getByRole("button", { name: "選取 差旅費用申請.pdf", exact: true })
    .click();
  await expect(page.locator(".document-row")).toContainText(doc.fileName);
  expect(fixture.core.attachments).toHaveLength(1);
  expect(
    fixture.documents.some(
      (value) =>
        value.collectionId === second.resource.id &&
        value.fileName === doc.fileName,
    ),
  ).toBe(true);
  await page.locator(".document-row").getByRole("link").click();
  await expect(
    page.getByRole("dialog", { name: "檔案預覽" }).locator("canvas"),
  ).toHaveAttribute("width", /[1-9]\d+/);
});
