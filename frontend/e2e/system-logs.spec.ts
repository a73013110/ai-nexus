import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import {
  expectCompactWorkspace,
  ApiFixture,
  chooseSelect,
  expectViewportContained,
  settleEntrance,
} from "./fixtures";
import type {
  DiagnosticDetail,
  DiagnosticSummary,
  DiagnosticHealthDto,
} from "../src/app/core/api/schema";

class LogFixture {
  readonly api = new ApiFixture();
  readonly queries: URL[] = [];
  readonly exports: URL[] = [];
  detailDelay = 0;
  detailFailure = false;
  relatedFailure = false;
  health: DiagnosticHealthDto = {
    status: "healthy",
    queueDepth: 3,
    accepted: 8000,
    written: 7980,
    replayed: 1877,
    writeFailures: 0,
    lost: 0,
    sampled: 40,
    corrupt: 0,
    exportFailures: 0,
    diskBytes: 9_100_000,
    pendingBytes: 0,
    estimatedSqlRows: 7128,
    lastFileWrite: new Date().toISOString(),
    lastSqlWrite: new Date().toISOString(),
    failure: null,
  };
  readonly events: DiagnosticSummary[] = Array.from(
    { length: 65 },
    (_, index) => ({
      logId: randomUUID(),
      at: new Date(Date.now() - 60000 - index * 5000).toISOString(),
      level:
        index % 8 === 0 ? "Error" : index % 5 === 0 ? "Warning" : "Information",
      category:
        index % 8 === 0
          ? "AiNexus.Modules.Inference.ProviderRunner"
          : "AiNexus.BuildingBlocks.Diagnostics.DiagnosticRequestMiddleware",
      eventId: index,
      eventName: index % 8 === 0 ? "inference.failed" : "http.completed",
      messageTemplate:
        index % 8 === 0
          ? "Provider request failed after {Attempt} attempts."
          : "HTTP request completed with {StatusCode} in {DurationMs} ms.",
      message:
        index % 8 === 0
          ? "Provider request failed after 2 attempts."
          : "HTTP request completed with 200 in " + (48 + index * 12) + " ms.",
      issueCode:
        index % 8 === 0
          ? "NX-" + index.toString(16).padStart(32, "A").toUpperCase()
          : null,
      traceId: Math.floor(index / 4)
        .toString(16)
        .padStart(32, "b"),
      spanId: "a".repeat(16),
      requestId: "request-" + index,
      operationId: null,
      jobId: null,
      runId: null,
      attempt: index % 8 === 0 ? 2 : null,
      method: "POST",
      route: "/api/v1/runs",
      statusCode: index % 8 === 0 ? 502 : 200,
      durationMs: 48 + index * 12,
      externalService: null,
      errorCode: index % 8 === 0 ? "provider_failed" : null,
      instance: "nexus-api-01",
      untrustedClient: false,
    }),
  );
  async attach(page: Page, detail = true) {
    this.api.adminAccess = true;
    this.api.auditAccess = true;
    this.api.extraFeatures = [
      { id: "dashboard", name: "總覽", route: "/dashboard" },
      { id: "projects", name: "專案", route: "/projects" },
      { id: "knowledge", name: "知識庫", route: "/knowledge" },
      { id: "artifacts", name: "成果文件", route: "/artifacts" },
      { id: "shared", name: "分享", route: "/shared" },
      { id: "quality", name: "品質評測", route: "/quality" },
      { id: "repositories", name: "程式庫", route: "/repositories" },
      { id: "tasks", name: "背景任務", route: "/tasks" },
      { id: "integrations", name: "資料來源", route: "/integrations" },
      { id: "logs.query", name: "系統日誌", route: "/admin/logs" },
      ...(detail
        ? [
            { id: "logs.detail", name: "診斷詳情", route: "" },
            { id: "logs.export", name: "匯出日誌", route: "" },
          ]
        : []),
    ];
    await this.api.attach(page);
    await page.route("**/api/v1/admin/logs**", async (route) => {
      const url = new URL(route.request().url());
      const json = (body: unknown, status = 200) =>
        route.fulfill({
          status,
          contentType: "application/json",
          body: JSON.stringify(body),
        });
      const failure = () =>
        json(
          {
            code: "service_unavailable",
            title: "Password=fixture-private-do-not-display",
            issueCode: "NX-" + "D".repeat(32),
          },
          503,
        );
      if (url.pathname.endsWith("/health")) return json(this.health);
      if (url.pathname.endsWith("/export")) {
        this.exports.push(url);
        return route.fulfill({
          contentType: "text/csv",
          body: "at,level,eventName\n2026-10-07,Error,inference.failed",
        });
      }
      if (!url.pathname.endsWith("/logs")) {
        if (this.detailDelay)
          await new Promise((resolve) => setTimeout(resolve, this.detailDelay));
        if (this.detailFailure) return failure();
        const event = this.events.find((entry) =>
          url.pathname.endsWith("/" + entry.logId),
        )!;
        const detail: DiagnosticDetail = {
          event,
          service: "AiNexus.Host",
          environment: "Production",
          version: "1.2.0",
          userId: this.api.userId,
          propertiesJson: JSON.stringify({
            StatusCode: event.statusCode,
            DurationMs: event.durationMs,
            Masked: "[REDACTED]",
            ClientAddress: "203.0.113.8",
            UserAgent: "Mozilla/5.0 fixture-browser",
            RequestProtocol: "HTTP/2",
            RequestScheme: "https",
            RequestOutcome: "completed",
            RequestAborted: false,
          }),
          exceptionType:
            event.level === "Error" ? "HttpRequestException" : null,
          exceptionDetail:
            event.level === "Error"
              ? "HttpRequestException: [REDACTED]\n  at ProviderRunner.ExecuteAsync()\n".repeat(
                  50,
                )
              : null,
        };
        return json(detail);
      }
      this.queries.push(url);
      const query = url.searchParams;
      if (query.get("traceId") && this.relatedFailure) return failure();
      let events = this.events.filter(
        (entry) =>
          (!query.get("level") || query.get("level") === entry.level) &&
          (!query.get("issueCode") ||
            query.get("issueCode") === entry.issueCode) &&
          (!query.get("traceId") || query.get("traceId") === entry.traceId),
      );
      if (query.get("sortDirection") === "asc") events.reverse();
      const take = Number(query.get("take") || 50);
      const offset =
        query.get("cursor") === "page-two"
          ? 50
          : Number(query.get("cursor")?.replace("offset-", "") || 0);
      events = events.slice(offset);
      return json({
        events: events.slice(0, take),
        nextCursor:
          events.length > take
            ? offset + take === 50
              ? "page-two"
              : "offset-" + (offset + take)
            : null,
        from: query.get("from"),
        to: query.get("to"),
        health: this.health,
      });
    });
    await page.goto("/admin/logs?view=events");
    await expect(page.locator(".log-table tbody tr")).toHaveCount(50);
  }
}

test("日誌導覽唯一選取，桌面密度與固定表頭", async ({ page }) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  const current = page.locator(".workspace-navigation a.current");
  await expect(current).toHaveCount(1);
  await expect(current).toHaveAttribute("href", "/admin/logs");
  await expect(current).toHaveAttribute("aria-current", "page");
  await expect(
    page.locator('.workspace-navigation a[href="/admin"]'),
  ).not.toHaveClass(/current/);
  await expectViewportContained(page);
  const density = await page.locator(".ui-table-scroll").evaluate((scroll) => {
    const bounds = scroll.getBoundingClientRect();
    return {
      visible: [...scroll.querySelectorAll("tbody tr")].filter((row) => {
        const rect = row.getBoundingClientRect();
        return rect.top >= bounds.top && rect.bottom <= bounds.bottom;
      }).length,
      rows: [...scroll.querySelectorAll("tbody tr")].map(
        (row) => row.getBoundingClientRect().height,
      ),
    };
  });
  expect(density.visible).toBeGreaterThanOrEqual(9);
  expect(Math.max(...density.rows)).toBeLessThanOrEqual(68);
  await settleEntrance(page);
  await expectCompactWorkspace(page);
  await page.locator(".ui-table-scroll").evaluate((element) => {
    element.scrollTop = 800;
  });
  const scroller = await page.locator(".ui-table-scroll").boundingBox();
  const header = await page
    .locator(".log-table thead th")
    .first()
    .boundingBox();
  expect(Math.abs(header!.y - scroller!.y)).toBeLessThan(2);
});

test("詳情保持捲動位置，桌面列表可切換事件，窄螢幕改為原生 modal", async ({
  page,
}) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  const row = page.locator(".log-table tbody tr").nth(20);
  const opener = row.getByRole("button", { name: /檢視 .* 診斷詳情/ });
  await opener.scrollIntoViewIfNeeded();
  const before = await page
    .locator(".ui-table-scroll")
    .evaluate((element) => element.scrollTop);
  await opener.click();
  const drawer = page.getByRole("dialog", { name: "診斷詳情", exact: true });
  await expect(drawer.getByText("nexus-api-01", { exact: true })).toBeVisible();
  expect(await drawer.evaluate((element) => element.matches(":modal"))).toBe(
    false,
  );
  expect(
    await page
      .locator(".ui-table-scroll")
      .evaluate((element) => element.scrollTop),
  ).toBe(before);
  await row
    .locator("..")
    .getByRole("button", { name: /檢視 .* 診斷詳情/ })
    .nth(21)
    .click();
  await expect(
    drawer.getByText(fixture.events[21].logId, { exact: true }),
  ).toBeVisible();
  await drawer.getByRole("button", { name: "下一筆日誌", exact: true }).click();
  await expect(
    drawer.getByText(fixture.events[22].logId, { exact: true }),
  ).toBeVisible();
  await page.setViewportSize({ width: 375, height: 900 });
  await expect(drawer).toBeVisible();
  await expect(
    drawer.getByText(fixture.events[22].logId, { exact: true }),
  ).toBeVisible();
  expect(await drawer.evaluate((element) => element.matches(":modal"))).toBe(
    true,
  );
  await expectViewportContained(page);
  await settleEntrance(page);
  const bounds = await drawer.boundingBox();
  expect(bounds!.x).toBeCloseTo(0, 1);
  expect(bounds!.width).toBe(375);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await expect
    .poll(() => drawer.evaluate((element) => element.matches(":modal")))
    .toBe(false);
  await drawer
    .getByRole("button", { name: "關閉診斷詳情", exact: true })
    .click();
  await expect(drawer).not.toBeVisible();
  await expect(opener).toBeFocused();
});

test("詳情分頁支援鍵盤、複製與長內容，Escape 回到原列", async ({ page }) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  const opener = page.getByRole("button", { name: /檢視 .* 診斷詳情/ }).first();
  await opener.click();
  const drawer = page.getByRole("dialog", { name: "診斷詳情", exact: true });
  await expect(
    drawer.getByText("HttpRequestException", { exact: true }),
  ).toBeVisible();
  const overview = drawer.getByRole("tab", { name: "概覽", exact: true });
  await overview.focus();
  await page.keyboard.press("ArrowRight");
  await expect(drawer.getByRole("tab", { name: "受控屬性" })).toBeFocused();
  await expect(drawer.getByRole("tabpanel")).toContainText("REDACTED");
  await page.keyboard.press("ArrowRight");
  await expect(drawer.getByRole("tab", { name: "例外堆疊" })).toBeFocused();
  await expect(drawer.getByRole("tabpanel")).toContainText(
    "ProviderRunner.ExecuteAsync",
  );
  await drawer.getByRole("button", { name: "複製例外堆疊" }).click();
  expect(
    await page.evaluate(
      () => (window as unknown as { __copied: string }).__copied,
    ),
  ).toContain("ProviderRunner.ExecuteAsync");
  await drawer.locator(".ui-drawer-body").evaluate((element) => {
    element.scrollTop = 2000;
  });
  await expect(
    drawer.getByRole("button", { name: "關閉診斷詳情" }),
  ).toBeInViewport();
  await expect(drawer.getByRole("tab", { name: "例外堆疊" })).toBeInViewport();
  await page.keyboard.press("Escape");
  await expect(drawer).not.toBeVisible();
  await expect(opener).toBeFocused();
});

test("分頁與匯出沿用已查詢條件，重設可回到第一頁", async ({ page }) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  await page
    .getByLabel("查證代碼", { exact: true })
    .fill("NX-" + "C".repeat(32));
  await page.getByRole("button", { name: "下一頁", exact: true }).click();
  await expect(page.locator(".log-table tbody tr")).toHaveCount(15);
  expect(fixture.queries.at(-1)!.searchParams.get("issueCode")).toBeNull();
  expect(fixture.queries.at(-1)!.searchParams.get("cursor")).toBe("page-two");
  await page.getByRole("button", { name: "上一頁", exact: true }).click();
  await expect(page.locator(".log-table tbody tr")).toHaveCount(50);
  expect(fixture.queries.at(-1)!.searchParams.get("cursor")).toBeNull();
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "匯出 CSV", exact: true }).click();
  expect((await download).suggestedFilename()).toBe("ai-nexus-logs.csv");
  expect(fixture.exports.at(-1)!.searchParams.get("issueCode")).toBeNull();
  await page.getByRole("button", { name: "重設", exact: true }).click();
  await expect(page.getByLabel("查證代碼", { exact: true })).toHaveValue("");
  await expect(
    page.getByRole("button", { name: "上一頁", exact: true }),
  ).toBeDisabled();
});

test("關聯失敗不阻擋詳情，關閉後延遲回應不會重新開啟", async ({ page }) => {
  const fixture = new LogFixture();
  fixture.relatedFailure = true;
  await fixture.attach(page);
  await page
    .getByRole("button", { name: /檢視 .* 診斷詳情/ })
    .first()
    .click();
  const drawer = page.getByRole("dialog", { name: "診斷詳情", exact: true });
  await expect(drawer.getByText("nexus-api-01", { exact: true })).toBeVisible();
  await drawer.getByRole("tab", { name: "關聯流程", exact: true }).click();
  await expect(drawer.getByRole("alert")).toBeVisible();
  await expect(drawer).not.toContainText("fixture-private");
  await drawer.getByRole("tab", { name: "受控屬性", exact: true }).click();
  await expect(drawer.getByRole("tabpanel")).toContainText("REDACTED");
  await drawer
    .getByRole("button", { name: "關閉診斷詳情", exact: true })
    .click();
  fixture.detailDelay = 400;
  await page
    .getByRole("button", { name: /檢視 .* 診斷詳情/ })
    .nth(8)
    .click();
  await expect(drawer.getByRole("status")).toContainText("正在載入診斷詳情");
  await drawer
    .getByRole("button", { name: "關閉診斷詳情", exact: true })
    .click();
  await page.waitForTimeout(450);
  await expect(drawer).not.toBeVisible();
  await expect(page.locator(".log-table tbody tr.is-selected")).toHaveCount(0);
});

test("深色、窄螢幕、進階篩選與查詢權限", async ({ page }) => {
  const fixture = new LogFixture();
  fixture.api.preferences.theme = "dark";
  await fixture.attach(page, false);
  await expect(
    page.getByRole("button", { name: /檢視 .* 診斷詳情/ }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "匯出 CSV", exact: true }),
  ).toHaveCount(0);
  await page.emulateMedia({ colorScheme: "dark", reducedMotion: "reduce" });
  await page.setViewportSize({ width: 768, height: 1024 });
  await expectViewportContained(page);
  await page.setViewportSize({ width: 375, height: 900 });
  await expectViewportContained(page);
  await page.locator(".advanced-filters summary").click();
  await page.getByLabel("事件名稱", { exact: true }).fill("http.completed");
  await expect(page.locator(".log-filter-count")).toHaveText("1");
  await chooseSelect(page, "等級", "Warning");
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect(page.locator(".log-table tbody tr")).toHaveCount(
    fixture.events.filter((entry) => entry.level === "Warning").length,
  );
  await expectViewportContained(page);
  await expectCompactWorkspace(page);
});

test("日期輸入保留秒數，日曆支援鍵盤與 24 小時制", async ({ page }) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  await expect(page.getByText("篩選", { exact: true })).toBeVisible();
  await page
    .getByLabel("起始時間", { exact: true })
    .fill("2026/10/07 12:34:56");
  await page
    .getByLabel("結束時間", { exact: true })
    .fill("2026/10/08 12:34:57");
  await page.getByRole("button", { name: "查詢", exact: true }).click();
  await expect
    .poll(() => fixture.queries.at(-1)?.searchParams.get("from"))
    .toBe("2026-10-07T04:34:56.000Z");
  expect(fixture.queries.at(-1)?.searchParams.get("to")).toBe(
    "2026-10-08T04:34:57.000Z",
  );
  await page
    .getByRole("button", { name: "起始時間選擇日期", exact: true })
    .click();
  const calendar = page.getByRole("dialog", {
    name: "起始時間日期選擇",
    exact: true,
  });
  await expect(
    calendar.getByRole("button", { name: "2026/10/07", exact: true }),
  ).toBeFocused();
  await page.keyboard.press("ArrowRight");
  await expect(
    calendar.getByRole("button", { name: "2026/10/08", exact: true }),
  ).toBeFocused();
  await page.keyboard.press("Enter");
  await calendar.getByLabel("時", { exact: true }).fill("23");
  await calendar.getByLabel("分", { exact: true }).fill("59");
  await calendar.getByLabel("秒", { exact: true }).fill("58");
  await calendar.getByRole("button", { name: "套用", exact: true }).click();
  await expect(page.getByLabel("起始時間", { exact: true })).toHaveValue(
    "2026/10/08 23:59:58",
  );
  await expect(calendar).not.toBeVisible();
  await expect(page.getByLabel("起始時間", { exact: true })).toBeFocused();
  await page.getByRole("button", { name: "重設", exact: true }).click();
  await expect(page.getByLabel("起始時間", { exact: true })).toHaveValue(
    /^\d{4}\/\d{2}\/\d{2} \d{2}:\d{2}:\d{2}$/,
  );
});

test("整列可開啟明細，複製不會開啟，訊息與請求資訊可判讀", async ({ page }) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  const rows = page.locator(".log-table tbody tr");
  await expect(rows.nth(1).locator('[nxTableColumn="message"]')).toContainText(
    "HTTP request completed with 200 in 60 ms.",
  );
  await rows.nth(1).locator('[nxTableColumn="message"]').click();
  const drawer = page.getByRole("dialog", { name: "診斷詳情", exact: true });
  await expect(drawer).toContainText("203.0.113.8");
  await expect(drawer).toContainText("Mozilla/5.0 fixture-browser");
  await page.keyboard.press("Escape");
  await expect(rows.nth(1).locator("[data-row-action]")).toBeFocused();
  await page.keyboard.press("ArrowDown");
  await expect(rows.nth(2).locator("[data-row-action]")).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(drawer).toBeVisible();
  await page.keyboard.press("Escape");
  await rows.first().getByRole("button", { name: "複製問題查證代碼" }).click();
  await expect(drawer).not.toBeVisible();
});

test("欄位偏好可還原，排序與每頁筆數使用伺服器查詢快照", async ({ page }) => {
  const fixture = new LogFixture();
  await fixture.attach(page);
  const columns = page.getByRole("dialog", { name: "顯示欄位", exact: true });
  await page.getByRole("button", { name: "顯示欄位", exact: true }).click();
  await columns.getByLabel("HTTP 狀態", { exact: true }).check();
  await columns.getByLabel("耗時", { exact: true }).check();
  await columns.getByLabel("等級", { exact: true }).uncheck();
  await page.keyboard.press("Escape");
  await expect(
    page.locator('.log-table th[nxTableColumn="status"]'),
  ).toBeVisible();
  await expect(
    page.locator('.log-table th[nxTableColumn="level"]'),
  ).not.toBeVisible();
  await page.reload();
  await expect(page.locator(".log-table tbody tr")).toHaveCount(50);
  await expect(
    page.locator('.log-table th[nxTableColumn="level"]'),
  ).not.toBeVisible();
  await page
    .getByLabel("查證代碼", { exact: true })
    .fill("NX-" + "C".repeat(32));
  await chooseSelect(page, "每頁筆數", "每頁 25 筆");
  await expect(page.locator(".log-table tbody tr")).toHaveCount(25);
  expect(fixture.queries.at(-1)!.searchParams.get("issueCode")).toBeNull();
  expect(fixture.queries.at(-1)!.searchParams.get("take")).toBe("25");
  await page.getByRole("button", { name: /^時間：由新到舊/ }).click();
  await expect(
    page.locator('.log-table th[nxTableColumn="time"]'),
  ).toHaveAttribute("aria-sort", "ascending");
  await expect
    .poll(() => fixture.queries.at(-1)!.searchParams.get("sortDirection"))
    .toBe("asc");
  await expect(
    page.locator(".log-table tbody tr").first().locator("time"),
  ).toHaveAttribute("datetime", fixture.events.at(-1)!.at);
  await page.getByRole("button", { name: "顯示欄位", exact: true }).click();
  await columns.getByRole("button", { name: "還原預設欄位" }).click();
  await page.keyboard.press("Escape");
  await expect(
    page.locator('.log-table th[nxTableColumn="level"]'),
  ).toBeVisible();
  await expect(
    page.locator('.log-table th[nxTableColumn="status"]'),
  ).not.toBeVisible();
});
