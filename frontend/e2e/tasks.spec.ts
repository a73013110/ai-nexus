import { test, expect } from "@playwright/test";
import { toasts } from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

test("a source added here announces once when it becomes searchable, not in other tabs or after reload", async ({
  page,
  context,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  fixture.queueUploads = true;
  await fixture.attach(page);
  await page.goto("/knowledge");
  await page.locator("nx-knowledge-page input[type=file]").setInputFiles({
    name: "差旅補充.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("測試補充資料"),
  });
  await expect(toasts(page).filter({ hasText: "文件已上傳" })).toBeVisible();
  const document = fixture.documents.at(-1)!,
    job = fixture.jobs.find((x) => x.id === document.jobId)!;

  const other = await context.newPage();
  await fixture.attach(other);
  await other.goto("/tasks");
  await other.getByRole("button", { name: "全部", exact: true }).click();
  await expect(
    other.locator(".job-card", { hasText: "差旅補充.txt" }),
  ).toContainText("排隊中");

  job.status = "completed";
  job.stage = "處理完成";
  job.updatedAt = new Date().toISOString();
  document.status = "ready";
  const done = toasts(page).filter({ hasText: "背景任務已完成" });
  await expect(done).toHaveCount(1);
  await expect(done).toContainText("文件辨識與索引：差旅補充.txt");
  // The other tab sees the result on its own page but did not start the job.
  await expect(
    other.locator(".job-card", { hasText: "差旅補充.txt" }),
  ).toContainText("已完成");
  await expect(toasts(other)).toHaveCount(0);
  await other.close();
  await page.bringToFront();

  await done.getByRole("button", { name: "查看", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/reader/${document.id}$`));
  await page.reload();
  await expect(page.locator("nx-document-reader")).toBeVisible();
  // A reload starts a new session of this tab; finished work stays in the inbox and task list.
  await page.waitForTimeout(3000);
  await expect(toasts(page)).toHaveCount(0);
});

test("a retried task that fails again announces it and links to the task", async ({
  page,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  const job = fixture.jobs[0];
  job.status = "failed";
  job.stage = "需要重試";
  job.errorCode = "ocr_failed";
  job.errorMessage = "文件辨識沒有完成。";
  await fixture.attach(page);
  await page.goto("/tasks");
  await page.getByRole("button", { name: "全部", exact: true }).click();
  const card = page.locator(".job-card", { hasText: "差旅費用申請.pdf" });
  await card.getByRole("button", { name: "重試" }).click();
  await expect(card).toContainText("排隊中");
  await expect(toasts(page)).toHaveCount(0);

  job.status = "failed";
  job.stage = "需要重試";
  job.updatedAt = new Date().toISOString();
  const failed = toasts(page).filter({ hasText: "背景任務需要重試" });
  await expect(failed).toHaveCount(1);
  await expect(failed).toContainText("文件辨識與索引：差旅費用申請.pdf");
  await failed.getByRole("button", { name: "查看", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/tasks\\?job=${job.id}$`));
  await expect(page.locator(`#job-${job.id}`)).toContainText("處理失敗");
});
