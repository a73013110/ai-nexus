import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  expectViewportContained,
  settleEntrance,
  ApiFixture,
  walkTour,
} from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

/** Opens a library tile's menu and returns the named item. */
async function fileAction(page: Page, fileName: string, label: string) {
  await page
    .getByRole("button", { name: `檔案操作：${fileName}`, exact: true })
    .click();
  return page.getByRole("menuitem", { name: label, exact: true });
}

test("capacity preflight prevents an upload when the remaining space is exhausted", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  fixture.core.attachmentLimitBytes = 0;
  await fixture.attach(page);
  await page.goto("/work/files");
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
  await page.goto("/work/files");
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
  await (await fileAction(page, "分析計畫.txt", "加入知識庫")).click();
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
    await fileAction(page, "分析計畫.txt", "刪除檔案（仍被引用）"),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await page
    .getByRole("searchbox", { name: "搜尋檔案庫", exact: true })
    .fill("");
  await expect(page.locator(".file-card")).toHaveCount(2);
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await expectViewportContained(page);
  const sourceLabel = page
    .getByRole("combobox", { name: "檔案來源", exact: true })
    .locator("span");
  await expect(sourceLabel).toHaveText("全部來源");
  await expect
    .poll(() =>
      sourceLabel.evaluate(
        (element) => element.scrollWidth <= element.clientWidth,
      ),
    )
    .toBe(true);
  await expectCompactWorkspace(page);
  await page.evaluate(() =>
    document.documentElement.setAttribute("data-theme", "dark"),
  );
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/work/chat");
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
  await page.goto("/work/knowledge");
  await page.getByRole("link", { name: /新的知識庫/ }).click();
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
        json: {
          title: "Password=fixture-private",
          code: "file_name_changed",
          issueCode: "NX-" + "D".repeat(32),
        },
      });
    }
    file.fileName = body.fileName;
    return route.fulfill({ json: file });
  });
  await page.goto("/work/files");
  await (await fileAction(page, file.fileName, "重新命名")).click();
  const dialog = page.getByRole("dialog", {
    name: "重新命名檔案",
    exact: true,
  });
  await dialog.getByLabel("名稱", { exact: true }).fill("新版報告.pdf");
  await dialog.getByRole("button", { name: "儲存名稱", exact: true }).click();
  await expect(dialog.getByRole("alert")).toContainText(
    "檔名已被修改，請重新載入後再試。",
  );
  await expect(dialog.getByLabel("名稱", { exact: true })).toHaveValue(
    "新版報告.pdf",
  );
  await dialog.getByRole("button", { name: "儲存名稱", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.locator(".file-card")).toContainText("新版報告.pdf");
});

test("retained project originals are discoverable through the library and explicit deletion is confirmed", async ({
  page,
}) => {
  const core = new ApiFixture();
  core.extraFeatures.push({
    id: "projects",
    name: "專案",
    route: "/work/projects",
  });
  const id = randomUUID();
  core.attachments.push({
    id,
    fileName: "保留的參考文件.txt",
    contentType: "text/plain",
    size: 100,
    isImage: false,
    analysisMode: "extracted-text",
  });
  core.retainedFiles.add(id);
  await core.attach(page);
  await page.route("**/api/v1/projects", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" }),
  );
  await page.goto("/work/projects");
  await page.getByRole("link", { name: "檔案庫", exact: true }).last().click();
  await expect(page.locator(".file-card")).toContainText("保留的參考文件.txt");
  const search = page.getByRole("searchbox", {
    name: "搜尋檔案庫",
    exact: true,
  });
  await search.fill("不符合的文字");
  await expect(
    page.getByRole("heading", { name: "沒有符合的檔案" }),
  ).toBeVisible();
  await search.fill("參考");
  await expect(page.locator(".file-card")).toBeVisible();
  await (await fileAction(page, "保留的參考文件.txt", "刪除檔案")).click();
  const confirm = page.getByRole("dialog", { name: "刪除檔案", exact: true });
  await expect(confirm).toBeVisible();
  await page.keyboard.press("Escape");
  expect(core.retainedFiles.has(id)).toBe(true);
  await (await fileAction(page, "保留的參考文件.txt", "刪除檔案")).click();
  await confirm.getByRole("button", { name: "刪除檔案", exact: true }).click();
  await expect(page.locator(".file-card")).toHaveCount(0);
  expect(core.retainedFiles.has(id)).toBe(false);
});

test("the library and its chooser page with the shared offset pagination", async ({
  page,
}) => {
  const core = new ApiFixture();
  for (let index = 1; index <= 45; index++) {
    const id = randomUUID();
    core.attachments.push({
      id,
      fileName: `報告 ${String(index).padStart(2, "0")}.txt`,
      contentType: "text/plain",
      size: 100,
      isImage: false,
      analysisMode: "extracted-text",
    });
    core.retainedFiles.add(id);
  }
  await core.attach(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/work/files");
  const footer = page.locator(".file-footer");
  const pager = footer.locator("nx-table-pagination");
  await expect(page.locator(".file-card")).toHaveCount(40);
  await expect(pager.getByRole("status")).toHaveText("1–40 / 45 筆");
  // 筆數只在分頁出現一次，不另寫「45 份」。
  await expect(footer).not.toContainText("45 份");
  const previous = pager.getByRole("button", { name: "上一頁", exact: true }),
    next = pager.getByRole("button", { name: "下一頁", exact: true }),
    jump = pager.getByRole("spinbutton", { name: "跳到頁碼", exact: true });
  await expect(previous).toBeDisabled();
  await next.click();
  await expect(page.locator(".file-card")).toHaveCount(5);
  await expect(pager.getByRole("status")).toHaveText("41–45 / 45 筆");
  await expect(next).toBeDisabled();
  await expect(next).toBeFocused();
  await jump.fill("1");
  await jump.press("Enter");
  await expect(page.locator(".file-card")).toHaveCount(40);
  await expect(jump).toHaveValue("1");
  await page
    .getByRole("searchbox", { name: "搜尋檔案庫", exact: true })
    .fill("報告 01");
  await expect(page.locator(".file-card")).toHaveCount(1);
  await expect(pager).toHaveCount(0);
  await page.setViewportSize({ width: 375, height: 812 });
  await page
    .getByRole("searchbox", { name: "搜尋檔案庫", exact: true })
    .fill("");
  await expect(pager).toBeVisible();
  await expectViewportContained(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/work/chat");
  await page.getByRole("button", { name: "從檔案庫加入", exact: true }).click();
  const picker = page.getByRole("dialog", {
    name: "從檔案庫選取",
    exact: true,
  });
  const chooser = picker.locator("nx-table-pagination");
  await expect(picker.locator(".picker-list > li")).toHaveCount(40);
  await expect(chooser.getByRole("status")).toHaveText("1–40 / 45 筆");
  await expect(picker).not.toContainText("45 份檔案");
  await chooser.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(picker.locator(".picker-list > li")).toHaveCount(5);
  await picker
    .getByRole("button", { name: "選取 報告 01.txt", exact: true })
    .click();
  await expect(page.locator(".composer nx-attachment-list")).toContainText(
    "報告 01.txt",
  );
});

for (const width of [1440, 375]) {
  test(`the library tour walks upload, filters, a file and capacity at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    // Without the knowledge feature the menu step leaves out "加入知識庫".
    const knowledge = width === 1440;
    if (knowledge) {
      const fixture = new KnowledgeFixture();
      fixture.seed();
      await fixture.attach(page);
    } else {
      const core = new ApiFixture();
      const id = randomUUID();
      core.attachments.push({
        id,
        fileName: "保留的參考文件.txt",
        contentType: "text/plain",
        size: 100,
        isImage: false,
        analysisMode: "extracted-text",
      });
      core.retainedFiles.add(id);
      await core.attach(page);
    }
    const height = width === 375 ? 812 : 1000;
    await page.setViewportSize({ width, height });
    await page.goto("/work/files");
    await expect(page.locator(".file-card")).toBeVisible();
    await expect(page.locator("nx-files-page nx-storage-usage")).toBeVisible();
    const launcher = page
      .locator(".feature-header")
      .getByRole("button", { name: "操作導覽", exact: true });
    const { titles, descriptions, last } = await walkTour(page, launcher);
    expect(titles).toEqual([
      "你的原檔都在這裡",
      "先保存，再決定用途",
      "依類型篩選",
      "依來源篩選",
      "用名稱尋找檔案",
      "預覽與引用位置",
      "檔案操作",
      "留意原檔容量",
    ]);
    expect(
      descriptions[titles.indexOf("檔案操作")].includes("加入知識庫"),
    ).toBe(knowledge);
    expect(last).toContain("重播導覽");
    expect(errors).toEqual([]);
  });
}
