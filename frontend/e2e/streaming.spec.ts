import { test, expect } from "@playwright/test";
import { ApiFixture } from "./fixtures";

test("live Markdown formats partial emphasis, retains committed DOM and safely completes rich content", async ({
  page,
}) => {
  const fixture = new ApiFixture();
  fixture.hold = true;
  fixture.partialAnswer = "## 核心問題\n\n重點是 **時間";
  await fixture.attach(page);
  await page.goto("/chat");
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page
    .getByRole("textbox", { name: "傳送訊息", exact: true })
    .fill("逐步格式化回答");
  await page.getByRole("button", { name: "送出訊息", exact: true }).click();
  const stream = page.locator("nx-streaming-answer");
  await expect(stream.getByRole("heading", { name: "核心問題" })).toBeVisible();
  await expect(stream.locator("strong")).toHaveText("時間");
  await expect(
    page.getByRole("button", { name: "停止生成", exact: true }),
  ).toBeVisible();
  await stream
    .locator("h2")
    .evaluate(
      (element) =>
        ((window as unknown as { __streamHeading: Element }).__streamHeading =
          element),
    );
  const run = fixture.runs[0],
    assistant = fixture.messages.find(
      (value) => value.id === run.assistantMessageId,
    )!;
  function append(delta: string, complete = false) {
    run.content += delta;
    assistant.content = run.content;
    fixture.events.get(run.id)!.push({
      version: 1,
      sequence: ++run.lastSequence,
      runId: run.id,
      type: "delta",
      status: "running",
      delta,
      errorCode: null,
    });
    if (complete) {
      run.status = assistant.status = "completed";
      run.finishedAt = new Date().toISOString();
      fixture.events.get(run.id)!.push({
        version: 1,
        sequence: ++run.lastSequence,
        runId: run.id,
        type: "status",
        status: "completed",
        delta: null,
        errorCode: null,
      });
    }
  }
  append(
    "不同步**。\n\n1. 比對日誌。\n   - 確認伺服器時區。\n2. 保留原始紀錄。\n\n| 檢查 | 結果 |\n| --- | --- |\n| 日誌 | 待核對 |",
  );
  await expect(stream.locator("strong")).toHaveText("時間不同步");
  await expect(stream.locator("ol > li")).toHaveCount(2);
  await expect(stream.getByRole("table")).toContainText("待核對");
  expect(
    await stream
      .locator("h2")
      .evaluate(
        (element) =>
          element ===
          (window as unknown as { __streamHeading: Element }).__streamHeading,
      ),
  ).toBe(true);
  await page.emulateMedia({ reducedMotion: "reduce" });
  append(
    '\n\n```typescript\nconst result = "待驗證";\n```\n\n[來源](javascript:alert(1))\n<img src=x onerror=alert(1)>\n\n接著驗證。',
  );
  await expect(stream.locator("pre code")).toHaveText(
    'const result = "待驗證";\n',
  );
  await expect(stream.locator('img, a[href^="javascript:"]')).toHaveCount(0);
  await page.getByRole("button", { name: "複製程式碼", exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(() => (window as unknown as { __copied: string }).__copied),
    )
    .toBe('const result = "待驗證";\n');
  append("", true);
  await expect(stream).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "重新生成", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".message:not(.user) .markdown")).toContainText(
    "接著驗證。",
  );
  expect(errors).toEqual([]);
});
