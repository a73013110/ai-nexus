import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { settleEntrance, toasts } from "./fixtures";
import { KnowledgeFixture } from "./knowledge-fixture";

test("a source added here announces once when it becomes searchable, not in other tabs or after reload", async ({
  page,
  context,
}) => {
  const fixture = new KnowledgeFixture();
  fixture.seed();
  fixture.queueUploads = true;
  await fixture.attach(page);
  await page.goto(`/knowledge/${fixture.collections[0].resource.id}`);
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

for (const width of [1440, 375]) {
  test(`tasks tour follows the cards and actions the filter shows at ${width}px`, async ({
    page,
  }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    const fixture = new KnowledgeFixture();
    fixture.seed();
    // Desktop lists a running ingest that can be opened and stopped; mobile starts on an empty
    // 處理中 filter, then shows the completed ingest under 全部.
    const running = width === 1440;
    if (running)
      fixture.jobs.push({
        ...fixture.jobs[0],
        id: randomUUID(),
        subjectId: randomUUID(),
        label: "公文彙整.pdf",
        status: "running",
        stage: "辨識第 2 頁",
        completedUnits: 1,
        totalUnits: 3,
      });
    await fixture.attach(page);
    const height = width === 375 ? 812 : 1000;
    await page.setViewportSize({ width, height });
    const launcher = page
      .locator(".feature-header")
      .getByRole("button", { name: "操作導覽", exact: true });
    const tour = page.locator(".driver-popover.nx-tour");
    const title = tour.locator(".driver-popover-title");
    const progress = tour.locator(".driver-popover-progress-text");
    // Walks to the end and returns each step's title and the last description.
    const walk = async () => {
      await launcher.click();
      await expect(title).toHaveText(/./);
      const total = Number((await progress.innerText()).split("/")[1]);
      const titles = [await title.innerText()];
      for (let step = 2; step <= total; step++) {
        await tour.getByRole("button", { name: "下一步", exact: true }).click();
        await expect(progress).toHaveText(`${step} / ${total}`);
        titles.push(await title.innerText());
        await settleEntrance(page);
        const bounds = (await tour.boundingBox())!;
        expect(bounds.x).toBeGreaterThanOrEqual(0);
        expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
        expect(bounds.y + bounds.height).toBeLessThanOrEqual(height);
      }
      const last = await tour
        .locator(".driver-popover-description")
        .innerText();
      await tour.getByRole("button", { name: "開始使用", exact: true }).click();
      await expect(tour).toHaveCount(0);
      await expect(launcher).toBeFocused();
      await expect(page.locator(".driver-overlay")).toHaveCount(0);
      return { titles, last };
    };
    const toolbar = ["耗時的處理交給背景", "依狀態篩選", "用名稱尋找任務"];
    await page.goto("/tasks");
    if (running) {
      await expect(page.locator(".job-card")).toHaveCount(1);
      await expect(page.locator(".job-card")).toContainText("公文彙整.pdf");
      const active = await walk();
      expect(active.titles).toEqual([
        ...toolbar,
        "看真實進度",
        "查看處理結果",
        "停止或重試",
      ]);
      expect(active.last).toContain("重播導覽");
    } else {
      await expect(
        page.getByRole("heading", { name: "目前沒有處理中的任務" }),
      ).toBeVisible();
      // Nothing is listed, so the tour ends on the search.
      const empty = await walk();
      expect(empty.titles).toEqual(toolbar);
      expect(empty.last).toContain("重播導覽");
      await page.getByRole("button", { name: "全部", exact: true }).click();
      await expect(
        page.locator(".job-card", { hasText: "差旅費用申請.pdf" }),
      ).toContainText("已完成");
      // A completed task has nothing to stop or retry.
      const all = await walk();
      expect(all.titles).toEqual([...toolbar, "看真實進度", "查看處理結果"]);
      expect(all.last).toContain("重播導覽");
    }
    expect(errors).toEqual([]);
  });
}
